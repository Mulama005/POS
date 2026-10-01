using Pos.Domain.Enums;

namespace Pos.Api.Controllers;

public sealed record CreateRepairRequest(
    Guid CustomerId,
    string DeviceDescription,
    string ReportedFault,
    decimal? QuotedCost,
    Guid? AssignedTechnicianId);

public sealed record UpdateRepairStatusRequest(RepairStatus NewStatus, string? DiagnosisNotes);

/// <summary>
/// One-time action — finalizes what the customer owes for this repair. Deliberately
/// only settable while FinalCost is still null: this fires a CreditSale transaction
/// against the customer's ledger, and CreditTransaction amounts are always positive
/// (see its own doc comment), so there's no clean way to represent a later correction
/// as a second call here. A genuine price correction after finalizing is rare enough
/// (and consequential enough) to be a deliberate admin action against the ledger
/// directly, not a self-service re-call of this endpoint.
/// </summary>
public sealed record SetFinalCostRequest(decimal FinalCost);

/// <summary>Records money actually collected against an already-finalized repair cost.
/// Mirrors CustomersController's RecordPaymentRequest shape deliberately — same
/// concept, same shape, different entry point.</summary>
public sealed record RecordRepairPaymentRequest(decimal Amount, string PaymentMethod, string? Notes);

public sealed record ConsumePartRequest(Guid ProductId, Guid? UnitId, int Quantity);

public sealed record AssignTechnicianRequest(Guid TechnicianId);

/// <summary>Deliberately minimal — the anonymous customer-facing view (Step 31) should
/// never leak internal notes, cost, or who's working on it.</summary>
public sealed record PublicRepairStatusResponse(string TicketNumber, string DeviceDescription, string Status, DateTimeOffset CreatedAt, DateTimeOffset? CollectedAt);