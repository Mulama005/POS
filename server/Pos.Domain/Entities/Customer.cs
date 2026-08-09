using Pos.Domain.Enums;
using Pos.Domain.Common;

namespace Pos.Domain.Entities;

public class Customer : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public CustomerType Type { get; set; } = CustomerType.Retail;

    public string? PricingTier { get; set; }

    public decimal CreditLimit { get; set; }

    
    public decimal CurrentCreditBalance { get; set; }

    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<CreditLedger> CreditLedgerEntries { get; set; } = new List<CreditLedger>();
    public ICollection<Repair> Repairs { get; set; } = new List<Repair>();
}
