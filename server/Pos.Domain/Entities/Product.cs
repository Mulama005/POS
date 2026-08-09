using Pos.Domain.Enums;
using Pos.Domain.Common;

namespace Pos.Domain.Entities;

public class Product : BaseEntity
{
    public string Sku { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public TaxClass TaxClass { get; set; } = TaxClass.Standard;

    
    public int ReorderThreshold { get; set; }

    
    public int StockQuantity { get; set; }
    public int? WarrantyMonthsOverride { get; set; }

    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Unit> Units { get; set; } = new List<Unit>();
    public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    public ICollection<InventoryAdjustment> InventoryAdjustments { get; set; } = new List<InventoryAdjustment>();
}
