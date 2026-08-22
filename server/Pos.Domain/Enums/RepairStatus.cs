namespace Pos.Domain.Enums;

/// <summary>
/// Defined stages a repair moves through, in order. Kept as a simple enum (not a
/// free-text field) so status transitions can be validated and queues can filter/sort
/// reliably — a technician's "in progress" view shouldn't depend on consistent spelling.
/// </summary>
public enum RepairStatus
{
    Received = 0,
    Diagnosing = 1,
    AwaitingParts = 2,
    InRepair = 3,
    Ready = 4,
    Collected = 5,
}
