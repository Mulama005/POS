using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pos.Application.Auth;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Enums;
using Pos.Infrastructure.Identity;
using Pos.Infrastructure.Persistence;
using DomainUser = Pos.Domain.Entities.User;

namespace Pos.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly PosDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _config;
    private readonly IMfaChallengeStore _challengeStore;
    private readonly IMfaService _mfaService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        PosDbContext db,
        ITokenService tokenService,
        IConfiguration config,
        IMfaChallengeStore challengeStore,
        IMfaService mfaService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _tokenService = tokenService;
        _config = config;
        _challengeStore = challengeStore;
        _mfaService = mfaService;
    }

    public async Task<AuthResult> LoginAsync(string email, string password, string ipAddress)
    {
        // Same generic message whether the email doesn't exist or the password is
        // wrong — distinguishing the two lets an attacker enumerate valid emails.
        const string genericError = "Invalid email or password.";

        var appUser = await _userManager.FindByEmailAsync(email);
        if (appUser is null)
        {
            return AuthResult.Fail(genericError);
        }

        // CheckPasswordSignInAsync (rather than a bare password comparison) gives us
        // Identity's built-in lockout-after-N-failed-attempts behavior for free.
        var passwordCheck = await _signInManager.CheckPasswordSignInAsync(appUser, password, lockoutOnFailure: true);
        if (!passwordCheck.Succeeded)
        {
            return passwordCheck.IsLockedOut
                ? AuthResult.Fail("Account locked due to repeated failed attempts. Try again later or contact an admin.")
                : AuthResult.Fail(genericError);
        }

        var domainUser = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == appUser.Id);
        if (domainUser is null)
        {
            // Identity row exists but the business-side User row doesn't — a data
            // integrity problem (see the shared-primary-key note on Domain.User),
            // not a credentials problem. Fail closed rather than logging in a
            // "ghost" user with no role or register assignment.
            return AuthResult.Fail("Account setup is incomplete. Contact an admin.");
        }

        // Provide a clearer reason for Cashier accounts missing a register assignment —
        // they cannot operate without being tied to a Register (Step 9 requirement).
        if (domainUser.Role == RegisterUserRole.Cashier && domainUser.AssignedRegisterId is null)
        {
            return AuthResult.Fail("Cashier account is missing a register assignment. Ask a manager/admin to assign a register.");
        }

        if (!domainUser.IsActive)
        {
            return AuthResult.Fail("This account has been deactivated.");
        }

        if ((domainUser.Role == RegisterUserRole.Manager || domainUser.Role == RegisterUserRole.Admin) && domainUser.MfaEnabled)
        {
            var challengeToken = _challengeStore.CreateChallenge(domainUser.Id);
            return new AuthResult
            {
                Success = true,
                RequiresMfa = true,
                ChallengeToken = challengeToken,
                UserId = domainUser.Id,
                FullName = domainUser.FullName,
                Email = appUser.Email ?? string.Empty,
                Role = domainUser.Role.ToString(),
                AssignedRegisterId = domainUser.AssignedRegisterId,
            };
        }

        return await IssueTokensAsync(appUser, domainUser, ipAddress);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, string ipAddress)
    {
        const string genericError = "Session expired. Please log in again.";

        var tokenHash = _tokenService.HashToken(refreshToken);
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (stored is null)
        {
            return AuthResult.Fail(genericError);
        }

        if (!stored.IsActive)
        {
            // Reuse of an already-revoked token is a signal the token may have been
            // stolen and the legitimate rotation already happened (or vice versa).
            // Revoke the whole chain rather than just failing quietly.
            if (stored.RevokedAt != null)
            {
                await RevokeDescendantsAsync(stored, ipAddress);
            }
            return AuthResult.Fail(genericError);
        }

        var domainUser = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == stored.UserId);
        if (domainUser is null || !domainUser.IsActive)
        {
            return AuthResult.Fail(genericError);
        }

        // Rotate: revoke the presented token, issue a brand new one, link them.
        var (rawNewToken, newHash) = _tokenService.GenerateRefreshToken();
        stored.RevokedAt = DateTime.UtcNow;
        stored.RevokedByIp = ipAddress;
        stored.ReplacedByTokenHash = newHash;

        var refreshDays = int.TryParse(_config["Jwt:RefreshTokenDays"], out var d) ? d : 7;
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = stored.UserId,
            TokenHash = newHash,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
            CreatedByIp = ipAddress,
        });

        await _db.SaveChangesAsync();

        var accessToken = _tokenService.GenerateAccessToken(
            domainUser.Id, stored.User.Email ?? string.Empty, domainUser.FullName,
            domainUser.Role.ToString(), domainUser.AssignedRegisterId);

        return new AuthResult
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = rawNewToken,
            UserId = domainUser.Id,
            FullName = domainUser.FullName,
            Email = stored.User.Email ?? string.Empty,
            Role = domainUser.Role.ToString(),
            AssignedRegisterId = domainUser.AssignedRegisterId,
        };
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var tokenHash = _tokenService.HashToken(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<AuthResult> VerifyMfaAsync(string challengeToken, string code, string ipAddress)
    {
        if (!_challengeStore.TryConsumeChallenge(challengeToken, out var userId))
        {
            return AuthResult.Fail("Challenge expired or already used.");
        }

        var domainUser = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == userId);
        if (domainUser is null || !domainUser.IsActive)
        {
            return AuthResult.Fail("Account setup is incomplete or inactive.");
        }

        if (string.IsNullOrWhiteSpace(domainUser.MfaSecret))
        {
            return AuthResult.Fail("MFA is not configured for this account.");
        }

        var rawSecret = _mfaService.DecryptSecret(domainUser.MfaSecret);
        if (!_mfaService.ValidateCode(rawSecret, code))
        {
            return AuthResult.Fail("Incorrect MFA code.");
        }

        var appUser = await _userManager.FindByIdAsync(userId.ToString());
        if (appUser is null)
        {
            return AuthResult.Fail("Account not found.");
        }

        return await IssueTokensAsync(appUser, domainUser, ipAddress);
    }

    private async Task<AuthResult> IssueTokensAsync(ApplicationUser appUser, DomainUser domainUser, string ipAddress)
    {
        var accessToken = _tokenService.GenerateAccessToken(
            domainUser.Id, appUser.Email ?? string.Empty, domainUser.FullName,
            domainUser.Role.ToString(), domainUser.AssignedRegisterId);

        var (rawRefreshToken, refreshHash) = _tokenService.GenerateRefreshToken();
        var refreshDays = int.TryParse(_config["Jwt:RefreshTokenDays"], out var d) ? d : 7;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = appUser.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
            CreatedByIp = ipAddress,
        });
        await _db.SaveChangesAsync();

        return new AuthResult
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            UserId = domainUser.Id,
            FullName = domainUser.FullName,
            Email = appUser.Email ?? string.Empty,
            Role = domainUser.Role.ToString(),
            AssignedRegisterId = domainUser.AssignedRegisterId,
        };
    }

    private async Task RevokeDescendantsAsync(RefreshToken token, string ipAddress)
    {
        var current = token;
        while (current.ReplacedByTokenHash is not null)
        {
            var next = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == current.ReplacedByTokenHash);
            if (next is null || next.RevokedAt is not null) break;

            next.RevokedAt = DateTime.UtcNow;
            next.RevokedByIp = ipAddress;
            current = next;
        }
        await _db.SaveChangesAsync();
    }
}