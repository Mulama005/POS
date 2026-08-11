namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Handles the short-lived, single-use token issued between "password correct" and
/// "MFA code correct" during login. Deliberately NOT a JWT and NOT the same token
/// service used for real access/refresh tokens — this token proves nothing except
/// "this user's password was just verified," and expires quickly.
/// </summary>
public interface IMfaChallengeStore
{
    /// <summary>Creates a new challenge for a user and returns the opaque token to send them.</summary>
    string CreateChallenge(Guid userId, TimeSpan? expiry = null);

    /// <summary>
    /// Validates and immediately invalidates a challenge token (single use). Returns false
    /// if the token is unknown, expired, or already used.
    /// </summary>
    bool TryConsumeChallenge(string token, out Guid userId);
}
