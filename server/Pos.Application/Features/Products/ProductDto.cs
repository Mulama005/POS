using Pos.Domain.Enums;

namespace Pos.Application.Features.Products;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public TaxClass TaxClass { get; set; }
    public string? ImageUrl { get; set; }
    public int ReorderThreshold { get; set; }
    public int WarrantyMonths { get; set; }
    public bool IsActive { get; set; }
    public int StockCount { get; set; } // computed from StockUnits
}