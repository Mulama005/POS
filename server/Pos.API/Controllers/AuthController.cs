using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Auth;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private const string RefreshCookieName = "posRefreshToken";

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _authService.LoginAsync(request.Email, request.Password, ip);

        if (!result.Success)
        {
            // 401 regardless of the specific reason (wrong password vs. locked vs.
            // deactivated) keeps the response uniform; the message text carries the detail.
            return Unauthorized(new { message = result.ErrorMessage });
        }

        if (result.RequiresMfa)
        {
            // Step 10 will flesh this branch out. For now RequiresMfa is always false.
            return Ok(new { requiresMfa = true, userId = result.UserId });
        }

        SetRefreshTokenCookie(result.RefreshToken!);

        return Ok(new
        {
            accessToken = result.AccessToken,
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

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { message = "No refresh token present." });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _authService.RefreshAsync(refreshToken, ip);

        if (!result.Success)
        {
            Response.Cookies.Delete(RefreshCookieName);
            return Unauthorized(new { message = result.ErrorMessage });
        }

        SetRefreshTokenCookie(result.RefreshToken!);

        return Ok(new
        {
            accessToken = result.AccessToken,
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

    [HttpPost("logout")]
    [AllowAnonymous] // an expired/garbage access token shouldn't block clearing the cookie
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _authService.LogoutAsync(refreshToken);
        }
        Response.Cookies.Delete(RefreshCookieName);
        return Ok();
    }

    private void SetRefreshTokenCookie(string rawToken)
    {
        Response.Cookies.Append(RefreshCookieName, rawToken, new CookieOptions
        {
            HttpOnly = true,       // not readable by browser JS — the whole point, per Step 8's spec
            Secure = true,         // only sent over HTTPS
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            Path = "/api/auth",    // only sent back to auth endpoints, not every request
        });
    }
}