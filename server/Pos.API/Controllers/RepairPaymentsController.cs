using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

public sealed record InitiateRepairMpesaRequest(decimal Amount, string PhoneNumber);

/// <summary>
/// M-Pesa STK Push for repair balances. Initiating only records a Pending attempt and
/// sends the prompt; the repair's AmountPaid and the customer ledger are updated by
/// PaymentCallbacksController when Safaricom confirms - never by the client.
/// Open to any authenticated staff, same as RepairsController.RecordPayment.
/// </summary>
[ApiController]
[Route("api/repairs/{repairId:guid}/payments/mpesa")]
[Authorize]
public sealed class RepairPaymentsController : ControllerBase
{
    private readonly PosDbContext _db;
    private readonly IDarajaService _daraja;

    public RepairPaymentsController(PosDbContext db, IDarajaService daraja)
    {
        _db = db;
        _daraja = daraja;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate(Guid repairId, [FromBody] InitiateRepairMpesaRequest request, CancellationToken ct)
    {
        if (request.Amount < 1) return BadRequest(new { message = "Amount must be at least KES 1." });

        var job = await _db.RepairJobs.FirstOrDefaultAsync(r => r.Id == repairId, ct);
        if (job is null) return NotFound(new { message = "Repair not found." });
        if (job.FinalCost is null)
            return BadRequest(new { message = "Finalize this repair's cost before taking a payment." });

        var balance = job.FinalCost.Value - job.AmountPaid;
        if (balance <= 0) return BadRequest(new { message = "This repair is already paid in full." });

        // Daraja only accepts whole shillings; round here so what we record equals what is charged.
        var amount = Math.Round(request.Amount, 0, MidpointRounding.AwayFromZero);
        if (amount > balance)
            return BadRequest(new { message = $"Amount exceeds the outstanding balance (KES {balance:N2})." });

        var attempt = new RepairPayment
        {
            RepairJobId = job.Id,
            Amount = amount,
            PhoneNumber = request.PhoneNumber,
            InitiatedByUserId = CurrentUserId,
        };

        var result = await _daraja.InitiateStkPushAsync(
            request.PhoneNumber, amount, job.TicketNumber, "Repair", ct);

        if (!result.Success || result.CheckoutRequestId is null)
            return BadRequest(new { message = result.ErrorMessage ?? "Could not start the M-Pesa payment. Try again." });

        attempt.CheckoutRequestId = result.CheckoutRequestId;
        _db.RepairPayments.Add(attempt);
        await _db.SaveChangesAsync(ct);

        return Ok(new { paymentId = attempt.Id, checkoutRequestId = attempt.CheckoutRequestId });
    }

    [HttpGet("{paymentId:guid}")]
    public async Task<IActionResult> GetStatus(Guid repairId, Guid paymentId, CancellationToken ct)
    {
        var attempt = await _db.RepairPayments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.RepairJobId == repairId, ct);
        if (attempt is null) return NotFound(new { message = "Payment not found." });

        return Ok(new
        {
            paymentId = attempt.Id,
            method = "Mpesa",
            status = attempt.Status.ToString(),
            externalReference = attempt.MpesaReceiptNumber ?? attempt.CheckoutRequestId,
            processedAt = attempt.ProcessedAt,
        });
    }
}