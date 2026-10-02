using Pos.Domain.Enums;

namespace Pos.Domain.Entities;

/// <summary>
/// One M-Pesa STK Push attempt against a repair's balance. Repair money otherwise lives
/// only in CreditTransaction rows (Payment requires a SaleId), so this row tracks the
/// asynchronous attempt itself. Nothing touches RepairJob.AmountPaid or the customer
/// ledger until the Daraja callback confirms Success - then it is applied exactly once.
/// </summary>
public class RepairPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RepairJobId { get; set; }
    public RepairJob? RepairJob { get; set; }

    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>Daraja CheckoutRequestID - the key the callback correlates on.</summary>
    public string? CheckoutRequestId { get; set; }

    /// <summary>M-Pesa receipt (e.g. QGH7XXXXXX), set on success.</summary>
    public string? MpesaReceiptNumber { get; set; }

    public string? PhoneNumber { get; set; }

    public Guid InitiatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}