using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

/// <summary>Small, role-scoped directory used by repair assignment controls.</summary>
[ApiController]
[Route("api/staff")]
[Authorize(Roles = "Manager,Admin")]
public sealed class StaffDirectoryController : ControllerBase
{
    private readonly PosDbContext _db;

    public StaffDirectoryController(PosDbContext db) => _db = db;

    [HttpGet("technicians")]
    public async Task<IActionResult> Technicians(CancellationToken cancellationToken)
    {
        var technicians = await _db.DomainUsers
            .Where(u => u.IsActive && u.Role == RegisterUserRole.Technician)
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                OpenRepairCount = _db.RepairJobs.Count(r => r.AssignedTechnicianId == u.Id && r.Status != RepairStatus.Collected),
            })
            .ToListAsync(cancellationToken);

        return Ok(technicians);
    }
}
