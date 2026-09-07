using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly PosDbContext _context;

    public AuditService(PosDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(
        Guid userId,
        string actionType,
        string entityName,
        Guid entityId,
        string? details = null,
        string? ipAddress = null)
    {
        var entry = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ActionType = actionType,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(entry);
        await _context.SaveChangesAsync();
    }
}