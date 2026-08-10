namespace Pos.Application.Auth;

public interface ITokenService
{
    ///Issues a short-lived signed JWT carrying identity + role claims.
    string GenerateAccessToken(Guid userId, string email, string fullName, string role, Guid? assignedRegisterId);

    
    (string RawToken, string TokenHash) GenerateRefreshToken();

    string HashToken(string rawToken);
}