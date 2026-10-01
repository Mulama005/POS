using Pos.Domain.Enums;

namespace Pos.Domain.Entities;

public class RepairJob
{
    public Guid Id { get; set; }

    // Ticket number shown to the customer/printed on the intake slip — short and
    // human-readable, distinct from the internal Guid. Also doubles as the lookup key
    // for the anonymous customer-facing status view (Step 31), combined with a phone
    // check, so a ticket number alone isn't enough to see someone else's repair.
    public string TicketNumber { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string DeviceDescription { get; set; } = string.Empty; // e.g. "Samsung A14, cracked screen"
    public string ReportedFault { get; set; } = string.Empty;
    public string? DiagnosisNotes { get; set; }

    public RepairStatus Status { get; set; } = RepairStatus.Received;

    // Nullable — a repair can sit unassigned briefly right after intake.
    public Guid? AssignedTechnicianId { get; set; }

    public decimal? QuotedCost { get; set; }
    public decimal? FinalCost { get; set; }

    // Amount actually collected from the customer against FinalCost, via
    // /api/repairs/{id}/payment. Kept here (rather than derived by summing
    // CreditTransactions every read) so the repair list can show a balance
    // without an extra join — the CreditTransaction rows remain the
    // authoritative, auditable source of truth for the money itself.
    public decimal AmountPaid { get; set; } = 0m;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? CollectedAt { get; set; }

    public ICollection<RepairPartUsed> PartsUsed { get; set; } = new List<RepairPartUsed>();
    public ICollection<RepairStatusHistory> StatusHistory { get; set; } = new List<RepairStatusHistory>();
}