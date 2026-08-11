using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Auth;
using Pos.Application.Common.Interfaces;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

// Adapt the ICurrentUserService / user-lookup calls below to whatever your Step 8/9 auth
// already exposes (e.g. a ClaimsPrincipal extension, an ICurrentUserService, etc.) — shown
// here as a plausible shape, not a drop-in-unchanged file.

[ApiController]
[Route("api/auth/mfa")]
public sealed class MfaController : ControllerBase
{
    private readonly PosDbContext _db;
    private readonly IMfaService _mfaService;
    private readonly IMfaChallengeStore _challengeStore;
    private readonly IAuthService _authService;

    public MfaController(PosDbContext db, IMfaService mfaService, IMfaChallengeStore challengeStore, IAuthService authService)
    {
        _db = db;
        _mfaService = mfaService;
        _challengeStore = challengeStore;
        _authService = authService;
    }

    /// <summary>
    /// Step 1 of enabling MFA: generates a new secret, returns the otpauth:// URI for the
    /// frontend to render as a QR code. Does NOT enable MFA yet — the secret is stored but
    /// MfaEnabled stays false until /enable confirms the user can actually generate codes.
    /// Manager/Admin only — MFA is optional infrastructure for those roles, not something
    /// a Cashier account should be able to touch.
    /// </summary>
    [HttpPost("setup")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Setup(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "User is not authenticated." });
        }

        var user = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);
        if (user is null)
        {
            return NotFound(new { error = "User account was not found." });
        }

        var rawSecret = _mfaService.GenerateSecret();
        user.MfaSecret = _mfaService.EncryptSecret(rawSecret);
        user.MfaEnabled = false;
        await _db.SaveChangesAsync(cancellationToken);

        var uri = _mfaService.GenerateOtpAuthUri(rawSecret, user.Email);

        return Ok(new { otpAuthUri = uri });
        // Frontend renders `otpAuthUri` as a QR code (e.g. with the `qrcode.react` npm package)
        // for the user to scan with Google Authenticator / Authy / 1Password / etc.
    }

    /// <summary>
    /// Step 2 of enabling MFA: user submits the code their authenticator app just generated,
    /// proving the setup actually works. Only then does MfaEnabled flip to true.
    /// </summary>
    [HttpPost("enable")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Enable([FromBody] EnableMfaRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "User is not authenticated." });
        }

        var user = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);
        if (user is null)
        {
            return NotFound(new { error = "User account was not found." });
        }

        if (string.IsNullOrWhiteSpace(user.MfaSecret))
        {
            return BadRequest("Call /setup first.");
        }

        var rawSecret = _mfaService.DecryptSecret(user.MfaSecret);
        if (!_mfaService.ValidateCode(rawSecret, request.Code))
        {
            return BadRequest("Incorrect code — check your authenticator app and try again.");
        }

        user.MfaEnabled = true;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { mfaEnabled = true });
    }

    /// <summary>
    /// Part two of login (see LOGIN-FLOW-CHANGES.md) — called after the password step
    /// returned mfaRequired: true. Consuming the challenge token here means it can't be
    /// replayed even if intercepted.
    /// </summary>
    [HttpPost("verify")]
    [AllowAnonymous] // caller isn't authenticated yet — the challenge token is what proves the password step already passed
    public async Task<IActionResult> Verify([FromBody] VerifyMfaRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.VerifyMfaAsync(request.ChallengeToken, request.Code, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        if (!result.Success)
        {
            return Unauthorized(new { message = result.ErrorMessage });
        }

        return Ok(new
        {
            mfaRequired = false,
            accessToken = result.AccessToken,
            refreshToken = result.RefreshToken,
            user = new
            {
                id = result.UserId,
                fullName = result.FullName,
                email = result.Email,
                role = result.Role,
                assignedRegisterId = result.AssignedRegisterId,
            }
        });
    }

    /// <summary>
    /// Lets a user turn MFA back off (e.g. lost their phone and re-enrolling). Requires an
    /// already-authenticated session — you may also want to require re-entering the
    /// password here, since this is a security-lowering action.
    /// </summary>
    [HttpPost("disable")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Disable(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "User is not authenticated." });
        }

        var user = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);
        if (user is null)
        {
            return NotFound(new { error = "User account was not found." });
        }

        user.MfaEnabled = false;
        user.MfaSecret = null;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { mfaEnabled = false });
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }
}

public sealed record EnableMfaRequest(string Code);
public sealed record VerifyMfaRequest(string ChallengeToken, string Code);
