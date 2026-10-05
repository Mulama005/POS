namespace Pos.Application.Features.Products;

/// <summary>
/// One CSV row exactly as typed. Everything is a string on purpose: parsing happens in
/// the importer so a bad cell produces a readable per-row message instead of a CSV
/// library exception that fails the whole file.
/// Headers are matched case-insensitively and ignore spaces/underscores
/// ("Sale Price", "sale_price" and "SalePrice" are the same column).
/// </summary>
public class ProductImportRow
{
    /// <summary>Leave blank to auto-generate (category prefix + running number).</summary>
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public string? Name { get; set; }
    public string? CategoryName { get; set; }
    public string? CostPrice { get; set; }
    public string? SalePrice { get; set; }
    public string? TaxClass { get; set; }
    public string? ReorderThreshold { get; set; }
    public string? WarrantyMonths { get; set; }
    /// <summary>Starting stock for NON-serialized categories only.</summary>
    public string? OpeningQuantity { get; set; }
    /// <summary>yes/no - only used when this row's category doesn't exist yet and is created.</summary>
    public string? SerialTracked { get; set; }
}

public class ProductImportRowResult
{
    public int RowNumber { get; set; }
    public ProductImportRow Row { get; set; } = new();
    /// <summary>Create | Skip | Error</summary>
    public string Action { get; set; } = "Create";
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? ResolvedSku { get; set; }
    public bool WillCreateCategory { get; set; }
}