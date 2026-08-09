using Pos.Domain.Common;

namespace Pos.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }

    
    public int DefaultWarrantyMonths { get; set; } = 0;

    
    public bool RequiresSerialTracking { get; set; }

    public ICollection<Category> ChildCategories { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
