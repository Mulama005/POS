namespace Pos.Domain.Entities;

/// <summary>
/// This is the entity that makes Step 30's core requirement real: a part consumed in a
/// repair comes OUT of the same inventory retail sales draw from — there is no separate,
/// untracked "repair parts shelf" the system doesn't know about.
///
/// Field names below (ProductId, UnitId) assume your Phase 4 Product/Unit schema uses
/// those names — adjust to match whatever you actually called them. UnitId is nullable
/// because not every part is individually serialized (a screw, a tube of adhesive) —
/// those decrement a plain quantity on Product instead. A serialized part (a replacement
/// screen with its own IMEI-like unit record) references the specific UnitId consumed.
/// </summary>
public class RepairPartUsed
{
    public Guid Id { get; set; }
    public Guid RepairJobId { get; set; }

    public Guid ProductId { get; set; } // FK into your existing Products table (Phase 4)
    public Guid? UnitId { get; set; }   // FK into your existing Units table, if this part is serialized

    public int Quantity { get; set; } = 1;
    public decimal UnitCostAtTimeOfUse { get; set; } // snapshot — don't let a later cost change rewrite repair history

    public DateTimeOffset ConsumedAt { get; set; } = DateTimeOffset.UtcNow;
}
