using Pos.Domain.Enums;

namespace Pos.Application.Features.Products;

public class CreateProductRequest
{
    // New products receive an SKU from the database sequence when this is blank.
    // Keep the request nullable so ASP.NET's implicit required validation does not
    // reject the intentionally empty SKU sent by the inventory form.
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public TaxClass TaxClass { get; set; } = TaxClass.Standard;
    public int ReorderThreshold { get; set; } = 5;
    public int WarrantyMonths { get; set; } = 12;
}
