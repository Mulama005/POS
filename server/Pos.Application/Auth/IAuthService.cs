namespace Pos.Application.Auth;


public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password, string ipAddress);

    ///Validates the refresh token cookie value, rotates it, and issues a new access token.
    Task<AuthResult> RefreshAsync(string refreshToken, string ipAddress);

    ///Revokes the given refresh token so it can no longer be used, even if not expired.
    Task LogoutAsync(string refreshToken);
}