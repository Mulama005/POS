using Pos.Domain.Enums;
using Pos.Domain.Common;

namespace Pos.Domain.Entities;


public class CreditLedger : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    /// <summary>Set when this entry originated from a credit sale at checkout; null for a standalone payment.</summary>
    public Guid? SaleId { get; set; }
    public Sale? Sale { get; set; }

    public CreditLedgerEntryType EntryType { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Running balance after this entry is applied — denormalized for fast history display.</summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>For EntryType = Payment: how the payment was made (cash, M-Pesa, etc.).</summary>
    public PaymentMethod? PaymentMethod { get; set; }

    public Guid RecordedByUserId { get; set; }
    public User RecordedByUser { get; set; } = null!;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}