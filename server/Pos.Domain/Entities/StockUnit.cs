using Pos.Domain.Enums;
using Pos.Domain.Common;

namespace Pos.Domain.Entities;

public class StockUnit : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string SerialNumber { get; set; } = string.Empty;
    public string? Imei { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? SaleDate { get; set; }
    public decimal? SalePrice { get; set; }
    public string Status { get; set; } = "InStock"; // InStock, Sold, Returned, etc.
}