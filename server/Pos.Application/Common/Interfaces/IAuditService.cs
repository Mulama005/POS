namespace Pos.Application.Common.Interfaces;

public interface IAuditService
{
    Task LogAsync(
        Guid userId,
        string actionType,
        string entityName,
        Guid entityId,
        string? details = null,
        string? ipAddress = null);
}