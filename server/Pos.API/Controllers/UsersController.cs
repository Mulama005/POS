using System.Text;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Identity;
using Pos.Infrastructure.Persistence;
using System.Security.Claims;

namespace Pos.Api.Controllers;

/// <summary>
/// Admin-only. Adapt DbContext/entity names below to your actual project if they differ —
/// written to match the shapes seen elsewhere in this codebase (PosDbContext, DomainUsers,
/// ApplicationUser, RegisterUserRole).
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public sealed class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PosDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _config;
    private readonly ILogger<UsersController> _logger;
    private readonly IAuditService _auditService;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        PosDbContext db,
        IEmailSender emailSender,
        IConfiguration config,
        IAuditService auditService,
        ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _db = db;
        _emailSender = emailSender;
        _config = config;
        _logger = logger;
        _auditService = auditService;
    }

    /// <summary>List every user for the admin panel table.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var users = await _db.DomainUsers
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                Role = u.Role.ToString(),
                u.IsActive,
                u.MfaEnabled,
            })
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    /// <summary>
    /// Creates the account in an unusable state (no password) and returns an invite link
    /// built from a password-reset token — reusing Identity's own token mechanism rather
    /// than inventing a separate one. No email provider is wired up yet (see
    /// ConsoleEmailSender), so the link is logged and also returned in the response for now
    /// — swap in a real provider later without changing this endpoint's contract.
    /// </summary>
    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteUserRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<RegisterUserRole>(request.Role, ignoreCase: true, out var role))
        {
            return BadRequest($"Unknown role '{request.Role}'.");
        }

        if (await _userManager.FindByEmailAsync(request.Email) is not null)
        {
            return Conflict("A user with this email already exists.");
        }

        var appUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = false,
        };

        // No password set — CreateAsync without a password leaves the account unable to
        // log in until accept-invite sets a real one via the reset token below.
        var createResult = await _userManager.CreateAsync(appUser);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
            return BadRequest(errors);
        }

        await _userManager.AddToRoleAsync(appUser, role.ToString());

        _db.DomainUsers.Add(new User
        {
            Id = appUser.Id,
            FullName = request.FullName,
            Email = request.Email,
            Role = role,
            IsActive = true,
        });
        await _db.SaveChangesAsync(cancellationToken);

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(appUser);
        var encodedToken = HttpUtility.UrlEncode(resetToken);

        var frontendBaseUrl = _config["Frontend:BaseUrl"] ?? "http://localhost:5173";
        var inviteLink = $"{frontendBaseUrl}/accept-invite?userId={appUser.Id}&token={encodedToken}";

        await _emailSender.SendAsync(
            request.Email,
            "You've been invited to the POS system",
            $"<p>Hi {request.FullName},</p><p>You've been invited as {role}. " +
            $"<a href=\"{inviteLink}\">Click here to set your password</a> and get started.</p>" +
            $"<p>This link expires in 24 hours.</p>",
            cancellationToken);

        // Returned only because no real email provider exists yet — remove this once one does,
        // so invite links only ever reach the intended inbox, never an API response body.
        return Ok(new { message = "Invite created.", inviteLink });
    }

    
    /// The new user lands here from the emailed link and sets their real password.
    /// Anonymous by design — they don't have an account to authenticate with yet; the
    /// token itself (from the reset-password mechanism) is what proves they're legitimate.
    
    [HttpPost("accept-invite")]
    [AllowAnonymous]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteRequest request)
    {
        var appUser = await _userManager.FindByIdAsync(request.UserId);
        if (appUser is null)
        {
            return BadRequest("Invalid invite link.");
        }

        var result = await _userManager.ResetPasswordAsync(appUser, request.Token, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            return BadRequest(errors);
        }

        appUser.EmailConfirmed = true;
        await _userManager.UpdateAsync(appUser);
        
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();
        var currentUserId = Guid.Parse(userId);
        
        await _auditService.LogAsync(
            userId: currentUserId,
            actionType: "USER_CREATED",
            entityName: "User",
            entityId: appUser.Id,
            details: $"Created user {appUser.Email} with role"
        );

        return Ok(new { message = "Account activated. You can now log in." });
    }

    /// <summary>Change a user's role. Blocks removing the last active Admin.</summary>
    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<RegisterUserRole>(request.Role, ignoreCase: true, out var newRole))
        {
            return BadRequest($"Unknown role '{request.Role}'.");
        }

        var domainUser = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (domainUser is null)
        {
            return NotFound();
        }

        if (domainUser.Role == RegisterUserRole.Admin && newRole != RegisterUserRole.Admin)
        {
            var activeAdminCount = await _db.DomainUsers
                .CountAsync(u => u.Role == RegisterUserRole.Admin && u.IsActive, cancellationToken);
            if (activeAdminCount <= 1)
            {
                return BadRequest("Cannot change role — this is the last active Admin.");
            }
        }

        var appUser = await _userManager.FindByIdAsync(id.ToString());
        if (appUser is null)
        {
            return NotFound();
        }

        var currentRoles = await _userManager.GetRolesAsync(appUser);
        await _userManager.RemoveFromRolesAsync(appUser, currentRoles);
        await _userManager.AddToRoleAsync(appUser, newRole.ToString());

        domainUser.Role = newRole;
        await _db.SaveChangesAsync(cancellationToken);
        
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();
        var currentUserId = Guid.Parse(userId);
        
        await _auditService.LogAsync(
            userId: currentUserId,
            actionType: "USER_ROLE_CHANGED",
            entityName: "User",
            entityId: domainUser.Id,
            details: $"Changed role from {currentRoles} to {domainUser.Role}"
        );

        return Ok(new { message = $"Role updated to {newRole}." });
    }

    /// <summary>
    /// Deactivates the account AND immediately kills every active session — sets
    /// IsActive=false, stamps SessionsRevokedAt (checked on every request going forward,
    /// see Program.cs), and revokes stored refresh tokens so a new access token can't be
    /// minted either. Blocks deactivating the last active Admin.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var domainUser = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (domainUser is null)
        {
            return NotFound();
        }

        if (domainUser.Role == RegisterUserRole.Admin)
        {
            var activeAdminCount = await _db.DomainUsers
                .CountAsync(u => u.Role == RegisterUserRole.Admin && u.IsActive, cancellationToken);
            if (activeAdminCount <= 1)
            {
                return BadRequest("Cannot deactivate — this is the last active Admin.");
            }
        }

        domainUser.IsActive = false;
        domainUser.SessionsRevokedAt = DateTimeOffset.UtcNow;

        // Adjust to your actual RefreshToken entity's field names if these differ —
        // the point is: every stored refresh token for this user stops being usable.
        var activeRefreshTokens = await _db.RefreshTokens
        .Where(rt => rt.UserId == id && rt.RevokedAt == null)
        .ToListAsync(cancellationToken);
        
        foreach (var token in activeRefreshTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();
        var currentUserId = Guid.Parse(userId);
        
        await _auditService.LogAsync(
            userId: currentUserId,
            actionType: "USER_DEACTIVATED",
            entityName: "User",
            entityId: domainUser.Id,
            details: $"User {domainUser.Email} status set to Deactivated"
        );

        _logger.LogInformation("User {UserId} deactivated — all sessions revoked as of {RevokedAt}", id, domainUser.SessionsRevokedAt);

        return Ok(new { message = "User deactivated. All active sessions have been terminated." });
    }

    /// <summary>Reactivates a previously deactivated account (not strictly required by Step 13, included since it's the natural undo).</summary>
    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        var domainUser = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (domainUser is null)
        {
            return NotFound();
        }

        domainUser.IsActive = true;
        // Deliberately NOT clearing SessionsRevokedAt — they simply log in fresh and get a
        // new token issued after this point, which passes the check naturally.
        await _db.SaveChangesAsync(cancellationToken);
        
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();
        var currentUserId = Guid.Parse(userId);
        
        await _auditService.LogAsync(
            userId: currentUserId,
            actionType: "USER_REACTIVATED",
            entityName: "User",
            entityId: domainUser.Id,
            details: $"User {domainUser.Email} status set to Activated"
        );

        return Ok(new { message = "User reactivated." });
    }
}
