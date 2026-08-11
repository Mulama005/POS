using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Auth;

/// <summary>
/// IMemoryCache-backed challenge store. Fine as long as the API runs as a single instance
/// (true today). If you later scale to multiple instances behind a load balancer, swap this
/// for a Redis- or database-backed implementation — a challenge created on instance A
/// wouldn't be found on instance B otherwise. IMfaChallengeStore is the seam that makes that
/// swap a one-file change.
/// </summary>
public sealed class MemoryCacheMfaChallengeStore : IMfaChallengeStore
{
    private const string KeyPrefix = "mfa_challenge_";
    private readonly IMemoryCache _cache;

    public MemoryCacheMfaChallengeStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string CreateChallenge(Guid userId, TimeSpan? expiry = null)
    {
        var token = GenerateToken();
        _cache.Set(KeyPrefix + token, userId, expiry ?? TimeSpan.FromMinutes(5));
        return token;
    }

    public bool TryConsumeChallenge(string token, out Guid userId)
    {
        var key = KeyPrefix + token;
        if (_cache.TryGetValue(key, out object? value) && value is Guid id)
        {
            _cache.Remove(key); // single use
            userId = id;
            return true;
        }

        userId = default;
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
