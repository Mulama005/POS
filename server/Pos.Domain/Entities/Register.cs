using Pos.Domain.Common;

namespace Pos.Domain.Entities;

public class Register : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Set when a cashier opens the till for a shift; null when closed.</summary>
    public bool IsTillOpen { get; set; }
    public decimal? ExpectedCashAtOpen { get; set; }

    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
