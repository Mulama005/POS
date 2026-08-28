namespace Pos.Domain.Entities;

public class ProductTierPrice
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public Guid PricingTierId { get; set; }
    public PricingTier Tier { get; set; } = null!;
    public decimal Price { get; set; }
}