using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Roles = "Manager,Admin")]
public class AuditController : ControllerBase
{
    private readonly PosDbContext _context;

    public AuditController(PosDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] string? userId = null,
        [FromQuery] string? actionType = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(a => a.UserId.ToString() == userId);

        if (!string.IsNullOrEmpty(actionType))
            query = query.Where(a => a.ActionType.Contains(actionType));

        if (fromDate.HasValue)
            query = query.Where(a => a.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(a => a.Timestamp <= toDate.Value);
        
        var orderedQuery = query.OrderByDescending(a => a.Timestamp);

        var total = await orderedQuery.CountAsync();

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.Timestamp,
                UserName = a.User.FullName,
                a.ActionType,
                a.EntityName,
                a.EntityId,
                a.Details,
                a.IpAddress
            })
            .ToListAsync();

        return Ok(new
        {
            items,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }
}