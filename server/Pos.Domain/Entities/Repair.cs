using Pos.Domain.Enums;
using Pos.Domain.Common;

namespace Pos.Domain.Entities;

public class Repair : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public string DeviceDescription { get; set; } = string.Empty;
    public string ReportedFault { get; set; } = string.Empty;

    public Guid? AssignedTechnicianId { get; set; }
    public User? AssignedTechnician { get; set; }

    public RepairStatus Status { get; set; } = RepairStatus.Received;

    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedDate { get; set; }

    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }

    public string? TechnicianNotes { get; set; }

    public ICollection<InventoryAdjustment> PartsConsumed { get; set; } = new List<InventoryAdjustment>();
}
