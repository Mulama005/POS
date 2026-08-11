using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Authorization;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/_diagnostics")]
public class AuthDiagnosticsController : ControllerBase
{
    private readonly IAuthorizationService _authorizationService;

    public AuthDiagnosticsController(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    [HttpGet("manager-or-admin-only")]
    [Authorize(Roles = RoleGroups.ManagerOrAdmin)]
    public IActionResult ManagerOrAdminOnly() => Ok(new { message = "You're a Manager or Admin." });

    [HttpGet("admin-only")]
    [Authorize(Roles = RoleGroups.AdminOnly)]
    public IActionResult AdminOnly() => Ok(new { message = "You're an Admin." });

    [HttpGet("technician-only")]
    [Authorize(Roles = RoleGroups.TechnicianOnly)]
    public IActionResult TechnicianOnly() => Ok(new { message = "You're a Technician." });

    /// Mirrors what a real "close till" endpoint should do: check the caller is
    // authorized to close the till for the specified register, and return 403 if not.
    /// This is a temporary test harness for Step 9's authorization rules. Delete
    [HttpGet("registers/{registerId:guid}/close-till-check")]
    [Authorize]
    public async Task<IActionResult> CloseTillCheck(Guid registerId)
    {
        var result = await _authorizationService.AuthorizeAsync(User, registerId, PolicyNames.RegisterScoped);

        if (!result.Succeeded)
        {
            return Forbid();
        }

        return Ok(new { message = $"You are allowed to close the till for register {registerId}." });
    }
}