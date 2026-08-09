using Pos.Domain.Common;

namespace Pos.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string ActionType { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }

    
    public string? Details { get; set; }

    public string? IpAddress { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
