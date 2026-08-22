using Pos.Domain.Enums;

namespace Pos.Api.Controllers;

public sealed record CreateRepairRequest(
    Guid CustomerId,
    string DeviceDescription,
    string ReportedFault,
    decimal? QuotedCost,
    Guid? AssignedTechnicianId);

public sealed record UpdateRepairStatusRequest(RepairStatus NewStatus, string? DiagnosisNotes);

public sealed record ConsumePartRequest(Guid ProductId, Guid? UnitId, int Quantity);

public sealed record AssignTechnicianRequest(Guid TechnicianId);

/// <summary>Deliberately minimal — the anonymous customer-facing view (Step 31) should
/// never leak internal notes, cost, or who's working on it.</summary>
public sealed record PublicRepairStatusResponse(string TicketNumber, string DeviceDescription, string Status, DateTimeOffset CreatedAt, DateTimeOffset? CollectedAt);
