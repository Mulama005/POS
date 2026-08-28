namespace Pos.Api.Controllers;

/// <summary>
/// Shape returned by both /api/products/search and /api/products/lookup — this is the
/// contract Step 18's full product module should keep stable when it extends this
/// controller, since the checkout screen (Step 24) is built against it.
/// </summary>
public class ProductDtos
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public string TaxClass { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }
}