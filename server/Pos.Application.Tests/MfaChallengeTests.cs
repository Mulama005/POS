using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pos.Application.Auth;
using Pos.Application.Common.Interfaces;
using Xunit;

namespace Pos.Application.Tests;

public class MfaChallengeTests
{
    [Fact]
    public void VerifyMfaAsync_ShouldNotConsumeChallengeTwice()
    {
        var store = new TestChallengeStore();
        var authService = new TestAuthService(store);

        var challenge = store.CreateChallenge(Guid.NewGuid());
        var result = authService.VerifyMfaAsync(challenge, "123456", "127.0.0.1").GetAwaiter().GetResult();

        Assert.True(result.Success);
        Assert.True(store.ConsumeCalls == 1);
    }

    private sealed class TestChallengeStore : IMfaChallengeStore
    {
        public int ConsumeCalls { get; private set; }
        private readonly Dictionary<string, Guid> _challenges = new();

        public string CreateChallenge(Guid userId, TimeSpan? expiry = null)
        {
            var token = Guid.NewGuid().ToString();
            _challenges[token] = userId;
            return token;
        }

        public bool TryConsumeChallenge(string token, out Guid userId)
        {
            ConsumeCalls++;
            return _challenges.Remove(token, out userId);
        }
    }

    private sealed class TestAuthService : IAuthService
    {
        private readonly IMfaChallengeStore _challengeStore;

        public TestAuthService(IMfaChallengeStore challengeStore)
        {
            _challengeStore = challengeStore;
        }

        public Task<AuthResult> LoginAsync(string email, string password, string ipAddress)
            => Task.FromResult(AuthResult.Fail("not used"));

        public Task<AuthResult> RefreshAsync(string refreshToken, string ipAddress)
            => Task.FromResult(AuthResult.Fail("not used"));

        public Task LogoutAsync(string refreshToken)
            => Task.CompletedTask;

        public Task<AuthResult> VerifyMfaAsync(string challengeToken, string code, string ipAddress)
        {
            if (!_challengeStore.TryConsumeChallenge(challengeToken, out _))
            {
                return Task.FromResult(AuthResult.Fail("challenge expired"));
            }

            return Task.FromResult(new AuthResult { Success = true });
        }
    }
}
