using Pos.Domain.Enums;
using Pos.Domain.Common;

namespace Pos.Domain.Entities;

public class InventoryAdjustment : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// Set for serialized products where the adjustment applies to a specific physical unit.
    public Guid? StockUnitId { get; set; }
    public StockUnit? StockUnit { get; set; }

    public InventoryAdjustmentType AdjustmentType { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;

    /// <summary>Set when this adjustment records a part consumed on a repair job (Step 30).</summary>
    public Guid? RepairId { get; set; }
    public Repair? Repair { get; set; }

    public Guid AdjustedByUserId { get; set; }
    public User AdjustedByUser { get; set; } = null!;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}