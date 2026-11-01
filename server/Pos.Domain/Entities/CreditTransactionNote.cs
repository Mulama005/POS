namespace Pos.Domain.Entities;

/// <summary>A product or memo attached to one customer credit sale.</summary>
public sealed class CreditTransactionNote
{
    public Guid Id { get; set; }
    public Guid CreditTransactionId { get; set; }
    public CreditTransaction CreditTransaction { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
