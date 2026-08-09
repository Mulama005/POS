using Pos.Domain.Enums;
using Pos.Domain.Common;

namespace Pos.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public string? ExternalReference { get; set; }

    /// <summary>Phone number the STK Push was sent to, when Method = Mpesa.</summary>
    public string? MpesaPhoneNumber { get; set; }

    public DateTime? ProcessedAt { get; set; }
}
