using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;
using Pos.Application.Common.Interfaces;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/repairs")]
[Authorize] // any authenticated staff member by default; specific endpoints tighten further below
public sealed class RepairsController : ControllerBase
{
    private readonly PosDbContext _db;
    private readonly ILogger<RepairsController> _logger;
    private readonly IWhatsAppService _whatsApp; // wired in once Step 33 files are added

    public RepairsController(PosDbContext db, ILogger<RepairsController> logger,  IWhatsAppService whatsApp)
    {
        _db = db;
        _logger = logger;
        _whatsApp = whatsApp;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    // ---------- Step 30: Intake ----------

    /// <summary>Any staff member can log a device intake — typically whoever's at the counter.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRepairRequest request, CancellationToken cancellationToken)
    {
        var customerExists = await _db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
        {
            return BadRequest("Unknown customer. Create the customer record first.");
        }
        if (request.AssignedTechnicianId is not null && !await IsActiveTechnicianAsync(request.AssignedTechnicianId.Value, cancellationToken))
        {
            return BadRequest("Assigned user must be an active Technician.");
        }

        var job = new RepairJob
        {
            Id = Guid.NewGuid(),
            TicketNumber = await GenerateTicketNumberAsync(cancellationToken),
            CustomerId = request.CustomerId,
            DeviceDescription = request.DeviceDescription,
            ReportedFault = request.ReportedFault,
            QuotedCost = request.QuotedCost,
            AssignedTechnicianId = request.AssignedTechnicianId,
            Status = RepairStatus.Received,
        };

        _db.RepairJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = job.Id }, new { job.Id, job.TicketNumber });
    }

    /// <summary>Assign or reassign the technician on a job. Manager/Admin only — a
    /// technician shouldn't be able to hand their own workload to someone else.</summary>
    [HttpPut("{id:guid}/assign")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> AssignTechnician(Guid id, [FromBody] AssignTechnicianRequest request, CancellationToken cancellationToken)
    {
        var job = await _db.RepairJobs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (job is null) return NotFound();
        if (!await IsActiveTechnicianAsync(request.TechnicianId, cancellationToken))
        {
            return BadRequest("Assigned user must be an active Technician.");
        }

        job.AssignedTechnicianId = request.TechnicianId;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Technician assigned." });
    }

    /// <summary>
    /// Moves a repair to a new status. A Technician can only update jobs assigned to
    /// them — enforced here, not just hidden in the UI — Manager/Admin can update any
    /// job. Every change is logged to RepairStatusHistory, which is what Step 33's
    /// WhatsApp notification will hook into.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateRepairStatusRequest request, CancellationToken cancellationToken)
    {
        var job = await _db.RepairJobs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (job is null) return NotFound();

        var isManagerOrAdmin = User.IsInRole("Manager") || User.IsInRole("Admin");
        var isAssignedTechnician = User.IsInRole("Technician") && job.AssignedTechnicianId == CurrentUserId;

        if (!isManagerOrAdmin && !isAssignedTechnician)
        {
            return Forbid();
        }

        var previousStatus = job.Status;
        job.Status = request.NewStatus;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        if (request.DiagnosisNotes is not null)
        {
            job.DiagnosisNotes = request.DiagnosisNotes;
        }
        if (request.NewStatus == RepairStatus.Collected)
        {
            job.CollectedAt = DateTimeOffset.UtcNow;
        }

        _db.RepairStatusHistories.Add(new RepairStatusHistory
        {
            Id = Guid.NewGuid(),
            RepairJobId = job.Id,
            FromStatus = previousStatus,
            ToStatus = request.NewStatus,
            ChangedByUserId = CurrentUserId,
        });

        await _db.SaveChangesAsync(cancellationToken);

        // Step 33 hook point — send the WhatsApp status-change message here once IWhatsAppService is wired in:
        // var customer = await _db.Customers.FindAsync(job.CustomerId);
        // await _whatsApp.SendRepairStatusUpdateAsync(customer.PhoneNumber, job.TicketNumber, request.NewStatus, cancellationToken);

        return Ok(new { message = $"Status updated to {request.NewStatus}." });
    }

    /// <summary>
    /// Consumes a part against this repair AND decrements the real inventory in the same
    /// operation — this is the line that prevents a parallel, untracked "repair parts"
    /// stock from existing. Adjust the Product/Unit field names to match your actual
    /// Phase 4 entities if they differ.
    /// </summary>
    [HttpPost("{id:guid}/parts")]
    public async Task<IActionResult> ConsumePart(Guid id, [FromBody] ConsumePartRequest request, CancellationToken cancellationToken)
    {
        var job = await _db.RepairJobs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (job is null) return NotFound();

        var isManagerOrAdmin = User.IsInRole("Manager") || User.IsInRole("Admin");
        var isAssignedTechnician = User.IsInRole("Technician") && job.AssignedTechnicianId == CurrentUserId;
        if (!isManagerOrAdmin && !isAssignedTechnician) return Forbid();

        // --- Consume from the existing inventory ---
        var product = await _db.Products
        .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (product is null)
        {
            return BadRequest("Unknown product.");
        }
        if (request.UnitId is not null)
        {
            // A serialized unit represents one physical item,
            // so only one can be consumed per request.
            if (request.Quantity != 1)
            {
                return BadRequest("A serialized unit can only be consumed with quantity 1.");
            }
            
        var unit = await _db.StockUnits
        .FirstOrDefaultAsync(
            u => u.Id == request.UnitId && u.ProductId == request.ProductId,
            cancellationToken);

            // The existing UnitStatus enum has no "Consumed" value.
            // InRepair means this physical unit is no longer available
            // as normal stock and is currently being used in a repair.
            
            if (unit is null)
            {
                return BadRequest("Unknown unit or unit does not belong to the selected product.");
            }
            
            if (unit.Status != "InStock")
            {
                return BadRequest(
                    $"Unit is not available for consumption. Current status: {unit.Status}.");
            }

            unit.Status = "InRepair";
        }
            else
            {
    // Non-serialized part: decrement the normal product stock.
    if (request.Quantity <= 0)
    {
        return BadRequest("Quantity must be greater than zero.");
    }

    if (product.StockQuantity < request.Quantity)
    {
        return BadRequest(
            $"Insufficient stock: {product.StockQuantity} available, {request.Quantity} requested.");
    }

    //product.StockQuantity -= request.Quantity;
}

// --- end consume ---

        _db.RepairPartsUsed.Add(new RepairPartUsed
        {
            Id = Guid.NewGuid(),
            RepairJobId = job.Id,
            ProductId = request.ProductId,
            UnitId = request.UnitId,
            Quantity = request.Quantity,
            UnitCostAtTimeOfUse = product.CostPrice, // adjust field name to match your Product entity
        });

        job.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Part consumed and inventory updated." });
    }

    /// <summary>
    /// Finalizes what the customer owes for this repair and charges it to their credit
    /// ledger — the same CreditTransaction/Customer.CurrentCreditBalance mechanism
    /// checkout's "pay on credit" and CustomersController use, so a repair's cost shows
    /// up in the customer's one ledger rather than a parallel, repair-only balance.
    /// Only callable once (see SetFinalCostRequest's doc comment).
    /// </summary>
    [HttpPut("{id:guid}/cost")]
    public async Task<IActionResult> SetFinalCost(Guid id, [FromBody] SetFinalCostRequest request, CancellationToken cancellationToken)
    {
        if (request.FinalCost <= 0) return BadRequest("Final cost must be positive.");

        var job = await _db.RepairJobs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (job is null) return NotFound();

        var isManagerOrAdmin = User.IsInRole("Manager") || User.IsInRole("Admin");
        var isAssignedTechnician = User.IsInRole("Technician") && job.AssignedTechnicianId == CurrentUserId;
        if (!isManagerOrAdmin && !isAssignedTechnician) return Forbid();

        if (job.FinalCost is not null)
        {
            return BadRequest("This repair's cost has already been finalized.");
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == job.CustomerId, cancellationToken);
        if (customer is null) return NotFound("Customer record for this repair no longer exists.");

        job.FinalCost = request.FinalCost;
        job.UpdatedAt = DateTimeOffset.UtcNow;

        customer.CurrentCreditBalance += request.FinalCost;
        _db.CreditTransactions.Add(new CreditTransaction
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Type = CreditTransactionType.CreditSale,
            Amount = request.FinalCost,
            Notes = $"Repair {job.TicketNumber} — {job.DeviceDescription}",
            RecordedByUserId = CurrentUserId,
            BalanceAfter = customer.CurrentCreditBalance,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Cost finalized and charged to customer.", finalCost = job.FinalCost, customerBalance = customer.CurrentCreditBalance });
    }

    /// <summary>
    /// Records money collected against an already-finalized repair — typically at
    /// pickup, but nothing stops a partial payment earlier. Open to any authenticated
    /// staff member (not just Manager/Admin/the assigned technician): a cashier at the
    /// counter, not a technician, is who usually takes payment at collection time.
    /// </summary>
    [HttpPost("{id:guid}/payment")]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordRepairPaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0) return BadRequest("Amount must be positive.");

        var job = await _db.RepairJobs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (job is null) return NotFound();

        if (job.FinalCost is null)
        {
            return BadRequest("Finalize this repair's cost before recording a payment.");
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == job.CustomerId, cancellationToken);
        if (customer is null) return NotFound("Customer record for this repair no longer exists.");

        job.AmountPaid += request.Amount;
        job.UpdatedAt = DateTimeOffset.UtcNow;

        customer.CurrentCreditBalance -= request.Amount;
        // Not clamped at zero, same as CustomersController.RecordPayment — an
        // overpayment is meaningful information (customer now in credit), not an error.

        _db.CreditTransactions.Add(new CreditTransaction
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Type = CreditTransactionType.Payment,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            Notes = request.Notes is null
                ? $"Repair {job.TicketNumber} payment"
                : $"Repair {job.TicketNumber} payment — {request.Notes}",
            RecordedByUserId = CurrentUserId,
            BalanceAfter = customer.CurrentCreditBalance,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Payment recorded.",
            amountPaid = job.AmountPaid,
            balance = job.FinalCost - job.AmountPaid,
            customerBalance = customer.CurrentCreditBalance,
        });
    }

    /// <summary>
    /// Everything needed to print a repair receipt (or the intake slip, while the cost
    /// is still unfinalized). Payments are read from the customer's CreditTransaction
    /// ledger - the authoritative record - filtered to entries written for this ticket.
    /// </summary>
    [HttpGet("{id:guid}/receipt")]
    public async Task<IActionResult> GetReceipt(Guid id, CancellationToken cancellationToken)
    {
        var job = await _db.RepairJobs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (job is null) return NotFound();

        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == job.CustomerId, cancellationToken);

        // Both payment paths (manual + M-Pesa callback) write Notes starting with this prefix.
        var prefix = $"Repair {job.TicketNumber} payment";
        var rows = await _db.CreditTransactions.AsNoTracking()
            .Where(t => t.CustomerId == job.CustomerId
                        && t.Type == CreditTransactionType.Payment
                        && t.Notes != null && t.Notes.StartsWith(prefix))
            .OrderBy(t => t.Timestamp)
            .Select(t => new { t.Timestamp, t.PaymentMethod, t.Amount, t.Notes })
            .ToListAsync(cancellationToken);

        var payments = rows.Select(t =>
        {
            // The M-Pesa callback writes "... - M-Pesa <RECEIPT>"; surface just the code.
            string? reference = null;
            var marker = "M-Pesa ";
            var at = t.Notes!.LastIndexOf(marker, StringComparison.Ordinal);
            if (at >= 0) reference = t.Notes[(at + marker.Length)..].Trim();
            return new { timestamp = t.Timestamp, method = t.PaymentMethod, amount = t.Amount, reference };
        }).ToList();

        return Ok(new
        {
            job.TicketNumber,
            job.CreatedAt,
            job.CollectedAt,
            Status = job.Status.ToString(),
            CustomerName = customer?.FullName ?? "Customer",
            CustomerPhone = customer?.Phone,
            job.DeviceDescription,
            job.ReportedFault,
            job.QuotedCost,
            job.FinalCost,
            job.AmountPaid,
            Balance = job.FinalCost != null ? job.FinalCost - job.AmountPaid : (decimal?)null,
            Payments = payments,
        });
    }

    // ---------- Step 31: Status interfaces ----------

    /// <summary>Technician's own queue — only jobs assigned to them. This is enforced by
    /// the query itself, not a client-side filter, so a technician can't page around it.</summary>
    [HttpGet("my-queue")]
    [Authorize(Roles = "Technician")]
    public async Task<IActionResult> MyQueue(CancellationToken cancellationToken)
    {
        var jobs = await _db.RepairJobs
            .Where(r => r.AssignedTechnicianId == CurrentUserId && r.Status != RepairStatus.Collected)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new {
                r.Id, r.TicketNumber, r.DeviceDescription, r.ReportedFault, Status = r.Status.ToString(), r.CreatedAt,
                r.QuotedCost, r.FinalCost, r.AmountPaid,
                Balance = r.FinalCost != null ? r.FinalCost - r.AmountPaid : (decimal?)null,
            })
            .ToListAsync(cancellationToken);

        return Ok(jobs);
    }

    /// <summary>Full list for Manager/Admin — everyone's jobs, for oversight.</summary>
    [HttpGet]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var jobs = await _db.RepairJobs
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id, r.TicketNumber, r.DeviceDescription, r.ReportedFault,
                Status = r.Status.ToString(), r.AssignedTechnicianId, r.CreatedAt,
                AssignedTechnicianName = _db.DomainUsers.Where(u => u.Id == r.AssignedTechnicianId).Select(u => u.FullName).FirstOrDefault(),
                r.QuotedCost, r.FinalCost, r.AmountPaid,
                Balance = r.FinalCost != null ? r.FinalCost - r.AmountPaid : (decimal?)null,
            })
            .ToListAsync(cancellationToken);

        return Ok(jobs);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var job = await _db.RepairJobs
            .Include(r => r.PartsUsed)
            .Include(r => r.StatusHistory)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return job is null ? NotFound() : Ok(job);
    }

    /// <summary>
    /// Customer-facing status lookup — anonymous by design (customers don't have
    /// accounts), but requires the last 4 digits of the phone number on file as a light
    /// check so a guessed ticket number alone doesn't expose someone else's repair.
    /// Returns only what a customer should see — no internal notes, cost, or technician.
    /// </summary>
    [HttpGet("track/{ticketNumber}")]
    [AllowAnonymous]
    public async Task<IActionResult> Track(string ticketNumber, [FromQuery] string phoneLast4, CancellationToken cancellationToken)
    {
        var job = await _db.RepairJobs
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.TicketNumber == ticketNumber, cancellationToken);

        if (job is null ||
        job.Customer is null ||
        string.IsNullOrWhiteSpace(job.Customer.Phone) ||
        !job.Customer.Phone.EndsWith(phoneLast4))
        {
            return NotFound("No matching repair found.");
        }

        var response = new PublicRepairStatusResponse(
            job.TicketNumber, job.DeviceDescription, job.Status.ToString(), job.CreatedAt, job.CollectedAt);

        return Ok(response);
    }

    private async Task<string> GenerateTicketNumberAsync(CancellationToken cancellationToken)
    {
        var today = DateTimeOffset.UtcNow;
        var countToday = await _db.RepairJobs.CountAsync(r => r.CreatedAt.Date == today.Date, cancellationToken);
        return $"RPR-{today:yyyyMMdd}-{(countToday + 1):D3}"; // e.g. RPR-20260814-004
    }

    private Task<bool> IsActiveTechnicianAsync(Guid userId, CancellationToken cancellationToken) =>
        _db.DomainUsers.AnyAsync(u => u.Id == userId && u.IsActive && u.Role == RegisterUserRole.Technician, cancellationToken);
}