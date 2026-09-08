using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
}