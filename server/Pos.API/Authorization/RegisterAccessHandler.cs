using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Pos.Api.Authorization;


public class RegisterAccessHandler : AuthorizationHandler<RegisterAccessRequirement, Guid>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RegisterAccessRequirement requirement,
        Guid targetRegisterId)
    {
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value;

        if (role == "Technician")
        {
            return Task.CompletedTask; // explicit no-op: never succeeds for this role
        }

        if (role is "Manager" or "Admin")
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Remaining case: Cashier. Must be assigned to exactly this register.
        var registerClaim = context.User.FindFirst("register_id")?.Value;
        if (Guid.TryParse(registerClaim, out var assignedRegisterId) && assignedRegisterId == targetRegisterId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}