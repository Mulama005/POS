using Pos.Domain.Enums;
using Pos.Domain.Common;

namespace Pos.Domain.Entities;

public class Product : BaseEntity
{
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public string TaxClass { get; set; } = "standard"; // e.g., standard, reduced, zero
    public string? ImageUrl { get; set; } // Supabase Storage URL
    public int ReorderThreshold { get; set; } = 5;
    public int WarrantyMonths { get; set; } = 12;
    public bool IsActive { get; set; } = true;
    public ICollection<StockUnit> StockUnits { get; set; } = new List<StockUnit>();
    public ICollection<ProductTierPrice> TierPrices { get; set; } = new List<ProductTierPrice>();
}
