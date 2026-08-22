using Pos.Domain.Enums;

namespace Pos.Domain.Entities;

/// <summary>
/// Append-only — rows are never edited or deleted, only added. This is what makes the
/// ledger "queryable, disputable-with-evidence" per the Step 32 brief: every entry has a
/// timestamp and, for payments, a method, so "who owes what and since when" has a real
/// paper trail behind it instead of just a mutable balance number.
/// </summary>
public class CreditTransaction
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }

    public CreditTransactionType Type { get; set; }
    public decimal Amount { get; set; } // always positive; Type determines the sign's meaning

    public string? PaymentMethod { get; set; } // "Cash", "M-Pesa", etc. — only set for Type=Payment
    public string? Notes { get; set; }
    public Guid? RelatedSaleId { get; set; } // links back to the retail sale, once Phase 5's Sale entity exists

    public Guid RecordedByUserId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    // Snapshot of the running balance immediately AFTER this transaction — makes the
    // ledger readable chronologically without recomputing a running sum every time it's
    // displayed, while CreditTransaction rows remain the append-only source of truth.
    public decimal BalanceAfter { get; set; }
}
