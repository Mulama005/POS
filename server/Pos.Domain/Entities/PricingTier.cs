namespace Pos.Domain.Entities;

public class PricingTier
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty; // Retail, Trade, Wholesale
    public decimal DiscountPercentage { get; set; }
    public bool IsDefault { get; set; }
    public ICollection<ProductTierPrice> ProductTierPrices { get; set; } = new List<ProductTierPrice>();
}