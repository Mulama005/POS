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

    // ---------- eTIMS / KRA classification (Step 26) ----------
    // Deliberately scoped to *classification* only, not the full KRA item-master record
    // (itemCd, itemTyCd, pkgUnitCd, qtyUnitCd, etc.) — that's the separate, still-not-
    // started "wire /items/saveItems" slice of Step 26. This is just what the
    // search-and-assign picker writes: which KRA taxonomy leaf this product maps to.

    /// <summary>KRA's classification code (matches EtimsItemClass.ItemClsCd) — assigned
    /// via the classification picker, not entered freely. Deliberately NOT a DB foreign
    /// key to EtimsItemClasses: that table gets replaced wholesale on every re-sync
    /// (upserted, but KRA could in principle retire a code), and a hard FK would let a
    /// future sync's data shift silently orphan or block updates on live products. The
    /// assignment endpoint validates against EtimsItemClasses at write time instead.</summary>
    public string? EtimsItemClassificationCode { get; set; }

    /// <summary>Snapshot of the classification's TaxTyCd at the moment it was assigned —
    /// not a live join. Since Product.TaxClass already drives today's VAT-inclusive
    /// pricing math in checkout, this is kept separate rather than merged into that enum;
    /// reconciling the two is a deliberate later decision, not an accident of this
    /// schema.</summary>
    public string? EtimsTaxTypeCode { get; set; }

    public DateTime? EtimsClassifiedAt { get; set; }

    /// <summary>Who assigned the classification — kept directly on Product (in addition
    /// to the audit log entry the assignment endpoint also writes) so a product's own
    /// detail view can show "classified by X on Y" without joining audit history.</summary>
    public Guid? EtimsClassifiedByUserId { get; set; }

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