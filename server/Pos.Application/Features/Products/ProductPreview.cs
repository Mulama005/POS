namespace Pos.Application.Features.Products;

public class ProductPreview
{
    public ProductImportDto Row { get; set; } = new();
    public bool IsDuplicate { get; set; }
    public string Action { get; set; } = "Create";
}
