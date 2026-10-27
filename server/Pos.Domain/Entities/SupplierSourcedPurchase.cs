using Pos.Domain.Common;

namespace Pos.Domain.Entities;

/// <summary>A recorded purchase sourced from an external supplier and received into stock.</summary>
public sealed class SupplierSourcedPurchase : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string SupplierName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? Reference { get; set; }
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

    public Guid RecordedByUserId { get; set; }
    public User RecordedByUser { get; set; } = null!;
}
