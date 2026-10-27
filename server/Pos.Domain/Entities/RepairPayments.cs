using Pos.Domain.Enums;

namespace Pos.Domain.Entities;

/// <summary>
/// A payment attempt or completed payment against a repair balance. STK attempts are
/// finalized by the Daraja callback; manual payment methods are recorded as successful
/// rows directly so repairs without a linked customer still have a payment history.
/// </summary>
public class RepairPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RepairJobId { get; set; }
    public RepairJob? RepairJob { get; set; }

    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>Payment channel used when this repair payment succeeds.</summary>
    public string? PaymentMethod { get; set; }
    public string? BankName { get; set; }
    public string? ExternalReference { get; set; }

    /// <summary>Daraja CheckoutRequestID - the key the callback correlates on.</summary>
    public string? CheckoutRequestId { get; set; }

    /// <summary>M-Pesa receipt (e.g. QGH7XXXXXX), set on success.</summary>
    public string? MpesaReceiptNumber { get; set; }

    public string? PhoneNumber { get; set; }

    public Guid InitiatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}
