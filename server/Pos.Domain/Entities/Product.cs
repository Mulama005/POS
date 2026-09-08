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
    public TaxClass TaxClass { get; set; } = TaxClass.Standard;
    public string? ImageUrl { get; set; } // Supabase Storage URL
    public int ReorderThreshold { get; set; } = 5;
    public int WarrantyMonths { get; set; } = 12;
    public bool IsActive { get; set; } = true;
    public ICollection<StockUnit> StockUnits { get; set; } = new List<StockUnit>();
    public ICollection<ProductTierPrice> TierPrices { get; set; } = new List<ProductTierPrice>();
    public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    public ICollection<InventoryAdjustment> InventoryAdjustments { get; set; } = new List<InventoryAdjustment>();

    /// <summary>
    /// On-hand quantity for products whose Category.RequiresSerialTracking is false (bulk
    /// items — cables, chargers — where individual units aren't worth tracking one row
    /// each). Written by StockController's bulk-receive endpoint and decremented directly
    /// on sale. Stays 0 for serialized products; their quantity lives entirely in
    /// StockUnits instead. Whether a product is bulk or serialized is decided by its
    /// Category — the two tracking modes are never mixed for the same product.
    /// </summary>
    public int BulkQuantityOnHand { get; set; } = 0;

    
    public int StockQuantity => BulkQuantityOnHand + (StockUnits?.Count(u => u.Status == "InStock") ?? 0);
}