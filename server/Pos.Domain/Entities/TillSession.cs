using Pos.Domain.Enums;
using Pos.Domain.Common;

namespace Pos.Domain.Entities;

/// <summary>
/// One open→close shift on a register. This is the source of truth for whether a
/// register's till is currently open — there is at most one TillSession with
/// Status == Open per register at any time (enforced in TillController, not at the DB
/// level, since "at most one" per FK value isn't expressible as a simple constraint).
///
/// Replaces the old Register.IsTillOpen/ExpectedCashAtOpen fields from Step 24, which
/// could only represent "open or not" with no history of who opened/closed it, when, or
/// what the reconciliation looked like — exactly the gap flagged when Step 24 was built.
/// </summary>
public class TillSession : BaseEntity
{
    public Guid RegisterId { get; set; }
    public Register Register { get; set; } = null!;

    public Guid OpenedByUserId { get; set; }
    public User OpenedByUser { get; set; } = null!;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Cash physically counted into the till drawer at the start of the shift.</summary>
    public decimal OpeningFloat { get; set; }

    public Guid? ClosedByUserId { get; set; }
    public User? ClosedByUser { get; set; }
    public DateTime? ClosedAt { get; set; }

    /// <summary>OpeningFloat + all successful Cash payments recorded on sales made during
    /// this session. Computed and stored at close time — not a live/computed column —
    /// so the reconciliation record is a fixed snapshot even if something about the
    /// underlying sales data were ever to change later.</summary>
    public decimal? ExpectedCashAtClose { get; set; }

    /// <summary>What was physically counted in the drawer at close.</summary>
    public decimal? CountedCashAtClose { get; set; }

    /// <summary>CountedCashAtClose - ExpectedCashAtClose. Positive = over, negative = short.</summary>
    public decimal? VarianceAtClose { get; set; }

    public TillSessionStatus Status { get; set; } = TillSessionStatus.Open;

    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}