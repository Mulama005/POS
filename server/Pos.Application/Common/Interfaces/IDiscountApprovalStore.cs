namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Handles the short-lived, single-use token issued after a Manager/Admin re-enters their
/// password to approve a discount above the configured threshold (Step 24). Same shape as
/// IMfaChallengeStore and deliberately for the same reason: this token proves nothing except
/// "a Manager/Admin's password was just verified for this specific approval," and it expires
/// quickly. It is NOT a JWT and NOT reusable as a bearer token.
/// </summary>
public interface IDiscountApprovalStore
{
    /// <summary>Creates a new approval token for the approving user and returns it.</summary>
    string CreateApproval(Guid approverId, TimeSpan? expiry = null);

    /// <summary>
    /// Validates and immediately invalidates an approval token (single use). Returns false if
    /// the token is unknown, expired, or already used.
    /// </summary>
    bool TryConsumeApproval(string token, out Guid approverId);
}