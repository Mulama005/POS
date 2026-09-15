using Pos.Domain.Common;
using Pos.Domain.Enums;

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
    public string? ImageUrl { get; set; }
    public int ReorderThreshold { get; set; } = 5;
    public int WarrantyMonths { get; set; } = 12;
    public bool IsActive { get; set; } = true;

    public ICollection<StockUnit> StockUnits { get; set; } = new List<StockUnit>();
    public ICollection<ProductTierPrice> TierPrices { get; set; } = new List<ProductTierPrice>();
    public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    public ICollection<InventoryAdjustment> InventoryAdjustments { get; set; } = new List<InventoryAdjustment>();

    // ---------- eTIMS / KRA classification ----------

    /// <summary>
    /// KRA item classification code assigned through the eTIMS classification picker.
    /// This is intentionally not a database FK because the KRA classification catalogue
    /// can be re-synchronised independently of live products.
    /// </summary>
    public string? EtimsItemClassificationCode { get; set; }

    /// <summary>
    /// Snapshot of the classification TaxTyCd at the time classification was assigned.
    /// May be null because KRA classification records can legitimately have no TaxTyCd.
    /// </summary>
    public string? EtimsTaxTypeCode { get; set; }

    public DateTime? EtimsClassifiedAt { get; set; }

    public Guid? EtimsClassifiedByUserId { get; set; }

    // ---------- eTIMS / KRA item registration ----------

    /// <summary>
    /// The unique KRA eTIMS item code returned/registered for this product.
    /// Example format: KE2NTU0000001.
    /// </summary>
    public string? EtimsItemCode { get; set; }

    /// <summary>
    /// KRA product type code used when registering the item.
    /// 2 = Finished Product for normal stocked POS products.
    /// </summary>
    public string? EtimsItemTypeCode { get; set; }

    /// <summary>
    /// Country of origin code used for eTIMS item registration.
    /// Defaults to KE for products registered as Kenyan-origin products.
    /// </summary>
    public string? EtimsOriginCountryCode { get; set; }

    /// <summary>
    /// KRA packaging unit code used when registering the item.
    /// </summary>
    public string? EtimsPackagingUnitCode { get; set; }

    /// <summary>
    /// KRA quantity unit code used when registering the item.
    /// </summary>
    public string? EtimsQuantityUnitCode { get; set; }

    /// <summary>
    /// Time at which KRA accepted the item registration.
    /// </summary>
    public DateTime? EtimsRegisteredAt { get; set; }

    /// <summary>
    /// POS user who registered the item with eTIMS.
    /// </summary>
    public Guid? EtimsRegisteredByUserId { get; set; }

    /// <summary>
    /// On-hand quantity for bulk products.
    /// Serialized products derive stock from StockUnits.
    /// </summary>
    public int BulkQuantityOnHand { get; set; } = 0;

    public int StockQuantity =>
        BulkQuantityOnHand +
        (StockUnits?.Count(u => u.Status == "InStock") ?? 0);
}