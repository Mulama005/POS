using Pos.Domain.Enums;
using Pos.Domain.Common;

namespace Pos.Domain.Entities;

public class Sale : BaseEntity
{
    public Guid RegisterId { get; set; }
    public Register Register { get; set; } = null!;

    public Guid CashierId { get; set; }
    public User Cashier { get; set; } = null!;

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public DateTime SaleDate { get; set; } = DateTime.UtcNow;

    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal Total { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Held;

    public Guid? DiscountApprovedByUserId { get; set; }

    public User? DiscountApprovedByUser { get; set; }
    public string? EtimsInvoiceNumber { get; set; }
    public string? EtimsControlNumber { get; set; }
    public string? EtimsQrCodeData { get; set; }

    /// <summary>
    /// True once this sale (created while offline) has been successfully replayed
    /// against the backend, including eTIMS invoicing (Step 35). Sales created online are
    /// synced = true immediately.
    /// </summary>
    public bool IsSynced { get; set; } = true;

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
