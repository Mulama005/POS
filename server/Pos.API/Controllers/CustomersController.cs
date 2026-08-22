using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
public sealed class CustomersController : ControllerBase
{
    private readonly PosDbContext _db;

    public CustomersController(PosDbContext db)
    {
        _db = db;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Phone = request.PhoneNumber,
            Email = request.Email,
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool onlyWithBalance, CancellationToken cancellationToken)
    {
        var query = _db.Customers.AsQueryable();
        if (onlyWithBalance)
        {
            query = query.Where(c => c.CurrentCreditBalance > 0);
        }

        var customers = await query
            .OrderBy(c => c.FullName)
            .Select(c => new { c.Id, c.FullName, c.Phone, c.CurrentCreditBalance })
            .ToListAsync(cancellationToken);

        return Ok(customers);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    /// <summary>Full chronological ledger for one customer — the "queryable,
    /// disputable-with-evidence" record the Step 32 brief asks for.</summary>
    [HttpGet("{id:guid}/ledger")]
    public async Task<IActionResult> GetLedger(Guid id, CancellationToken cancellationToken)
    {
        var customerExists = await _db.Customers.AnyAsync(c => c.Id == id, cancellationToken);
        if (!customerExists) return NotFound();

        var transactions = await _db.CreditTransactions
            .Where(t => t.CustomerId == id)
            .OrderBy(t => t.Timestamp)
            .Select(t => new
            {
                t.Id,
                Type = t.Type.ToString(),
                t.Amount,
                t.PaymentMethod,
                t.Notes,
                t.BalanceAfter,
                t.Timestamp,
            })
            .ToListAsync(cancellationToken);

        return Ok(transactions);
    }

    /// <summary>
    /// Records a credit sale — increases the balance owed. This would typically be
    /// called from within the checkout flow (Phase 5) when "pay on credit" is selected,
    /// not as a manually-triggered separate action, but exposed here as its own endpoint
    /// so it works standalone until that integration exists.
    /// </summary>
    [HttpPost("{id:guid}/credit-sale")]
    public async Task<IActionResult> RecordCreditSale(Guid id, [FromBody] RecordCreditSaleRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0) return BadRequest("Amount must be positive.");

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null) return NotFound();

        // Single SaveChangesAsync call = one implicit transaction, so the balance and the
        // ledger row land together or not at all. Worth adding an EF concurrency token on
        // Customer if two cashiers might record transactions for the same customer at the
        // exact same moment — not included here to keep this focused, flagged for later.
        customer.CurrentCreditBalance += request.Amount;

        _db.CreditTransactions.Add(new CreditTransaction
        {
            Id = Guid.NewGuid(),
            CustomerId = id,
            Type = CreditTransactionType.CreditSale,
            Amount = request.Amount,
            RelatedSaleId = request.RelatedSaleId,
            Notes = request.Notes,
            RecordedByUserId = CurrentUserId,
            BalanceAfter = customer.CurrentCreditBalance,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Credit sale recorded.", newBalance = customer.CurrentCreditBalance });
    }

    /// <summary>Records a partial or full payment against the balance — timestamped and
    /// method-tagged, per the Step 32 requirement.</summary>
    [HttpPost("{id:guid}/payment")]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0) return BadRequest("Amount must be positive.");

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null) return NotFound();

        customer.CurrentCreditBalance -= request.Amount;
        // Deliberately NOT clamping at zero — an overpayment resulting in a negative
        // balance (customer is in credit) is meaningful information, not an error.

        _db.CreditTransactions.Add(new CreditTransaction
        {
            Id = Guid.NewGuid(),
            CustomerId = id,
            Type = CreditTransactionType.Payment,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            Notes = request.Notes,
            RecordedByUserId = CurrentUserId,
            BalanceAfter = customer.CurrentCreditBalance,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Payment recorded.", newBalance = customer.CurrentCreditBalance });
    }
}
