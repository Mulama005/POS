using Pos.Domain.Enums;
using Pos.Domain.Common;

namespace Pos.Domain.Entities;
/// A single physical, individually-tracked item (a specific phone, a specific laptop).

public class Unit : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string SerialNumber { get; set; } = string.Empty;

    ///Nullable — not every serialized product (e.g. a laptop) has an IMEI.
    public string? Imei { get; set; }

    public DateTime PurchaseDate { get; set; }
    public DateTime? WarrantyExpiry { get; set; }

    public UnitStatus Status { get; set; } = UnitStatus.InStock;

    /// Set when the unit is sold, so a return can be matched to the original sale.
    public Guid? SoldOnSaleItemId { get; set; }
    public SaleItem? SoldOnSaleItem { get; set; }
}
