using Pos.Domain.Enums;

namespace Pos.Domain.Entities;

/// <summary>
/// One row per status change. Two jobs: (1) an audit trail — "who moved this to Ready
/// and when" — useful the first time a customer disputes a timeline, and (2) the trigger
/// point for the WhatsApp status-change notifications in Step 33, which fire from the
/// same place a status update is recorded rather than being bolted on separately.
/// </summary>
public class RepairStatusHistory
{
    public Guid Id { get; set; }
    public Guid RepairJobId { get; set; }
    public RepairStatus FromStatus { get; set; }
    public RepairStatus ToStatus { get; set; }
    public Guid ChangedByUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
}
