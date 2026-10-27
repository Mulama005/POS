namespace Pos.Domain.Entities;

/// <summary>
/// A staff shift recorded by a manager or administrator. Each entry keeps
/// both the staff member and the user who recorded each clock event.
/// </summary>
public sealed class TechnicianTimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TechnicianId { get; set; }
    public DateTimeOffset ClockInAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ClockInRecordedByUserId { get; set; }
    public DateTimeOffset? ClockOutAt { get; set; }
    public Guid? ClockOutRecordedByUserId { get; set; }
}
