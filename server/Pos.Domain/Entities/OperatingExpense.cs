using Pos.Domain.Common;

namespace Pos.Domain.Entities;

/// <summary>A recurring monthly operating cost used by the financial summary.</summary>
public class OperatingExpense : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Occupancy";
    public decimal MonthlyAmount { get; set; }
    public bool IsActive { get; set; } = true;
}
