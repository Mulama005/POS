using Pos.Domain.Enums;
using Pos.Domain.Common;

namespace Pos.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    public RegisterUserRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public string? MfaSecret { get; set; }
    public bool MfaEnabled { get; set; } = false;

    ///Required for the register-scoped authorization policy in Step 9 (e.g. closing a till).
    public Guid? AssignedRegisterId { get; set; }
    public Register? AssignedRegister { get; set; }


    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<Repair> AssignedRepairs { get; set; } = new List<Repair>();
    public ICollection<AuditLog> AuditLogEntries { get; set; } = new List<AuditLog>();
    public DateTimeOffset? SessionsRevokedAt { get; set; }
}
