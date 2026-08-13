namespace Pos.Application.Auth;

public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public string? AccessToken { get; set; }

    public string? RefreshToken { get; set; }

    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid? AssignedRegisterId { get; set; }

    public bool RequiresMfa { get; set; }
    public string? ChallengeToken { get; set; }
    public bool MfaEnabled { get; set; }

    public static AuthResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}