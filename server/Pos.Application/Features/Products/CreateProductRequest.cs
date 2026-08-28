namespace Pos.Application.Features.Products;

public class CreateProductRequest
{
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public string TaxClass { get; set; } = "standard";
    public int ReorderThreshold { get; set; } = 5;
    public int WarrantyMonths { get; set; } = 12;
}