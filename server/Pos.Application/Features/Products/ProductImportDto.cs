namespace Pos.Application.Features.Products;

public class ProductImportDto
{
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public string? TaxClass { get; set; }
    public int ReorderThreshold { get; set; }
    public int WarrantyMonths { get; set; }
}