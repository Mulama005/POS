using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

/// <summary>
/// Receives asynchronous payment confirmations from external gateways. Deliberately
/// anonymous — Safaricom (and later Pesapal) can't attach our JWT bearer tokens to
/// their callback requests — so this treats its input as untrusted and only ever
/// updates a Payment row that already exists, correlated by an opaque id
/// (CheckoutRequestID) we generated and handed to the gateway ourselves at
/// initiation. It can never create a new Sale or Payment, and it's idempotent:
/// Safaricom does resend callbacks, and re-processing an already-resolved payment
/// would be a bug, not a feature.
/// </summary>
[ApiController]
[Route("api/payment-callbacks")]
[AllowAnonymous]
public sealed class PaymentCallbacksController : ControllerBase
{
    private readonly PosDbContext _db;
    private readonly ILogger<PaymentCallbacksController> _logger;

    public PaymentCallbacksController(PosDbContext db, ILogger<PaymentCallbacksController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("mpesa")]
    public async Task<IActionResult> MpesaCallback([FromBody] DarajaCallbackEnvelope envelope, CancellationToken cancellationToken)
    {
        var callback = envelope?.Body?.StkCallback;
        if (callback is null || string.IsNullOrWhiteSpace(callback.CheckoutRequestId))
        {
            _logger.LogWarning("Received an M-Pesa callback with no CheckoutRequestID.");
            // Always 200 — Safaricom retries on non-2xx, and retrying a malformed
            // payload won't fix itself.
            return Ok();
        }

        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.ExternalReference == callback.CheckoutRequestId, cancellationToken);

        if (payment is null)
        {
            // Not a retail sale payment - it may be an STK Push for a repair balance.
            if (await TryResolveRepairPaymentAsync(callback, cancellationToken)) return Ok();

            _logger.LogWarning("M-Pesa callback for unknown CheckoutRequestID {CheckoutRequestId}.", callback.CheckoutRequestId);
            return Ok();
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            // Already resolved — this is Safaricom's retry, not a new event.
            return Ok();
        }

        if (callback.ResultCode == 0)
        {
            var receiptNumber = callback.CallbackMetadata?.Item?
                .FirstOrDefault(i => i.Name == "MpesaReceiptNumber")?.Value?.ToString();

            payment.Status = PaymentStatus.Success;
            payment.ExternalReference = receiptNumber ?? callback.CheckoutRequestId;
            payment.ProcessedAt = DateTime.UtcNow;
        }
        else
        {
            // 1037 = Safaricom's own timeout code (customer never responded to the
            // prompt); everything else (1032 = customer cancelled, and other codes)
            // maps to a generic Failed — the UI's retry/fallback path is the same
            // either way, but TimedOut is worth distinguishing in reports.
            payment.Status = callback.ResultCode == 1037 ? PaymentStatus.TimedOut : PaymentStatus.Failed;
            payment.ProcessedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "M-Pesa callback resolved payment {PaymentId} as {Status} ({ResultDesc})",
            payment.Id, payment.Status, callback.ResultDesc);

        return Ok();
    }

    /// <summary>
    /// Resolves a callback against a RepairPayment attempt. Returns false if no such
    /// attempt exists. On success the repair's AmountPaid and the customer ledger are
    /// updated in the same transaction that claims the attempt (Pending -> Success via a
    /// conditional UPDATE), so a duplicate or concurrent Safaricom retry can never apply
    /// the money twice.
    /// </summary>
    private async Task<bool> TryResolveRepairPaymentAsync(DarajaStkCallback callback, CancellationToken ct)
    {
        var attempt = await _db.RepairPayments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.CheckoutRequestId == callback.CheckoutRequestId, ct);
        if (attempt is null) return false;
        if (attempt.Status != PaymentStatus.Pending) return true; // Safaricom retry

        var succeeded = callback.ResultCode == 0;
        var newStatus = succeeded
            ? PaymentStatus.Success
            : callback.ResultCode == 1037 ? PaymentStatus.TimedOut : PaymentStatus.Failed;
        var receipt = succeeded
            ? callback.CallbackMetadata?.Item?.FirstOrDefault(i => i.Name == "MpesaReceiptNumber")?.Value?.ToString()
            : null;
        var now = DateTimeOffset.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var claimed = await _db.RepairPayments
            .Where(p => p.Id == attempt.Id && p.Status == PaymentStatus.Pending)
            .ExecuteUpdateAsync(u => u
                .SetProperty(p => p.Status, newStatus)
                .SetProperty(p => p.MpesaReceiptNumber, receipt)
                .SetProperty(p => p.ProcessedAt, now), ct);
        if (claimed == 0) return true; // another request already resolved it

        if (succeeded)
        {
            var job = await _db.RepairJobs.FirstOrDefaultAsync(r => r.Id == attempt.RepairJobId, ct);
            var customer = job is null ? null : await _db.Customers.FirstOrDefaultAsync(c => c.Id == job.CustomerId, ct);
            if (job is null || customer is null)
            {
                // Money has been taken but we can't attribute it - leave the row Success
                // with its receipt so it is findable, and shout.
                _logger.LogError(
                    "M-Pesa repair payment {PaymentId} (receipt {Receipt}) succeeded but repair/customer is missing.",
                    attempt.Id, receipt);
                await tx.CommitAsync(ct);
                return true;
            }

            job.AmountPaid += attempt.Amount;
            job.UpdatedAt = now;
            customer.CurrentCreditBalance -= attempt.Amount;

            _db.CreditTransactions.Add(new CreditTransaction
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                Type = CreditTransactionType.Payment,
                Amount = attempt.Amount,
                PaymentMethod = "M-Pesa",
                Notes = $"Repair {job.TicketNumber} payment - M-Pesa {receipt ?? callback.CheckoutRequestId}",
                RecordedByUserId = attempt.InitiatedByUserId,
                BalanceAfter = customer.CurrentCreditBalance,
            });
            await _db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        _logger.LogInformation(
            "M-Pesa callback resolved repair payment {PaymentId} as {Status} ({ResultDesc})",
            attempt.Id, newStatus, callback.ResultDesc);
        return true;
    }
}