using Pos.Domain.Common;

namespace Pos.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
// Self‑referencing parent
    public Guid? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    
    // Children
    public ICollection<Category> ChildCategories { get; set; } = new List<Category>();
    
    // Products in this category
    public ICollection<Product> Products { get; set; } = new List<Product>();
	public bool RequiresSerialTracking { get; set; } = false;
	public int DefaultWarrantyMonths { get; set; } = 12;
}