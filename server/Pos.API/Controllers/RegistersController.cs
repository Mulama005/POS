using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Api.Authorization;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

/// <summary>
/// Read-only register list — the minimum Step 24 (checkout) needs to know which registers
/// exist and whether each one's till is open. Full register management (create/edit/deactivate
/// registers) is expected to extend this controller with write endpoints, not replace it.
/// Till open/close itself lives in TillController (Step 25) — see TillSession for why
/// "is the till open" is derived from session history rather than a plain boolean column.
/// </summary>
[ApiController]
[Route("api/registers")]
[Authorize(Roles = RoleGroups.RegisterCapableRoles)]
public sealed class RegistersController : ControllerBase
{
    private readonly PosDbContext _db;

    public RegistersController(PosDbContext db)
    {
        _db = db;
    }

    /// <summary>All active registers with their current till status — used to populate the
    /// checkout screen's register indicator (Cashier, locked to their assigned register) or
    /// picker (Manager/Admin, who can operate any register).</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var registers = await _db.Registers
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new RegisterSummaryDto(
                r.Id,
                r.Name,
                r.Location,
                r.TillSessions.Any(t => t.Status == TillSessionStatus.Open)))
            .ToListAsync(cancellationToken);

        return Ok(registers);
    }
}

public sealed record RegisterSummaryDto(Guid Id, string Name, string? Location, bool IsTillOpen);