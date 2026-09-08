using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Api.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

public sealed record InitiateMpesaRequest(string PhoneNumber);

/// <summary>
/// Step 27: triggers and tracks the async M-Pesa STK Push flow for a Payment that
/// SalesController.Complete already created as Pending. Split out from
/// SalesController deliberately — completing a sale is synchronous and instantaneous;
/// confirming an M-Pesa payment on it is asynchronous, can take up to ~60s, and can
/// fail, time out, or be retried, which is a genuinely different shape of endpoint.
/// </summary>
[ApiController]
[Route("api/sales/{saleId:guid}/payments/{paymentId:guid}")]
[Authorize(Roles = RoleGroups.RegisterCapableRoles)]
public sealed class SalePaymentsController : ControllerBase
{
    private readonly PosDbContext _db;
    private readonly IDarajaService _daraja;
    private readonly IAuthorizationService _authorizationService;

    public SalePaymentsController(PosDbContext db, IDarajaService daraja, IAuthorizationService authorizationService)
    {
        _db = db;
        _daraja = daraja;
        _authorizationService = authorizationService;
    }

    /// <summary>Triggers (or re-triggers, on retry) the STK Push prompt for this payment line.</summary>
    [HttpPost("mpesa/initiate")]
    public async Task<IActionResult> InitiateMpesa(
        Guid saleId, Guid paymentId, [FromBody] InitiateMpesaRequest request, CancellationToken cancellationToken)
    {
        var lookup = await LoadAuthorizedPaymentAsync(saleId, paymentId, cancellationToken);
        if (lookup.Error is not null) return lookup.Error;
        var (sale, payment) = (lookup.Sale!, lookup.Payment!);

        if (payment.Method != PaymentMethod.Mpesa)
        {
            return BadRequest(new { message = "This payment line isn't an M-Pesa payment." });
        }
        if (payment.Status == PaymentStatus.Success)
        {
            return BadRequest(new { message = "This payment has already been completed." });
        }

        var result = await _daraja.InitiateStkPushAsync(
            request.PhoneNumber, payment.Amount, sale.Id.ToString("N")[..12], "POS Sale", cancellationToken);

        if (!result.Success || result.CheckoutRequestId is null)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "Could not start the M-Pesa payment. Try again." });
        }

        // Explicit reset on Status/ProcessedAt below — this covers the retry path,
        // where a prior attempt already left this payment as Failed or TimedOut.
        payment.ExternalReference = result.CheckoutRequestId;
        payment.MpesaPhoneNumber = request.PhoneNumber;
        payment.Status = PaymentStatus.Pending;
        payment.ProcessedAt = null;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { checkoutRequestId = result.CheckoutRequestId });
    }

    /// <summary>Polled by the checkout screen while waiting for the customer to respond
    /// to the STK Push prompt on their phone.</summary>
    [HttpGet]
    public async Task<IActionResult> GetStatus(Guid saleId, Guid paymentId, CancellationToken cancellationToken)
    {
        var lookup = await LoadAuthorizedPaymentAsync(saleId, paymentId, cancellationToken);
        if (lookup.Error is not null) return lookup.Error;
        var payment = lookup.Payment!;

        return Ok(new
        {
            paymentId = payment.Id,
            method = payment.Method.ToString(),
            status = payment.Status.ToString(),
            externalReference = payment.ExternalReference,
            processedAt = payment.ProcessedAt,
        });
    }

    private readonly record struct PaymentLookupResult(Sale? Sale, Payment? Payment, IActionResult? Error);

    private async Task<PaymentLookupResult> LoadAuthorizedPaymentAsync(Guid saleId, Guid paymentId, CancellationToken cancellationToken)
    {
        var sale = await _db.Sales.Include(s => s.Payments).FirstOrDefaultAsync(s => s.Id == saleId, cancellationToken);
        if (sale is null)
        {
            return new PaymentLookupResult(null, null, NotFound(new { message = "Sale not found." }));
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, sale.RegisterId, PolicyNames.RegisterScoped);
        if (!authResult.Succeeded)
        {
            return new PaymentLookupResult(null, null, Forbid());
        }

        var payment = sale.Payments.FirstOrDefault(p => p.Id == paymentId);
        if (payment is null)
        {
            return new PaymentLookupResult(null, null, NotFound(new { message = "Payment not found on this sale." }));
        }

        return new PaymentLookupResult(sale, payment, null);
    }
}