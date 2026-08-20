using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Auth;

/// <summary>
/// IMemoryCache-backed approval store — same single-instance caveat as
/// MemoryCacheMfaChallengeStore. Swap for a Redis/database-backed implementation if the API
/// ever scales to multiple instances; IDiscountApprovalStore is the seam that makes that a
/// one-file change.
/// </summary>
public sealed class MemoryCacheDiscountApprovalStore : IDiscountApprovalStore
{
    private const string KeyPrefix = "discount_approval_";
    private readonly IMemoryCache _cache;

    public MemoryCacheDiscountApprovalStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string CreateApproval(Guid approverId, TimeSpan? expiry = null)
    {
        var token = GenerateToken();
        // Short expiry — this is meant to be redeemed by the same checkout flow within
        // seconds of the approver typing their password, not carried around.
        _cache.Set(KeyPrefix + token, approverId, expiry ?? TimeSpan.FromMinutes(3));
        return token;
    }

    public bool TryConsumeApproval(string token, out Guid approverId)
    {
        var key = KeyPrefix + token;
        if (_cache.TryGetValue(key, out object? value) && value is Guid id)
        {
            _cache.Remove(key); // single use
            approverId = id;
            return true;
        }

        approverId = default;
        return false;
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}