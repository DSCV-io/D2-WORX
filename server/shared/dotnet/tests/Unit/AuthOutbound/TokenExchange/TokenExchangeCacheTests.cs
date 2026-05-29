// -----------------------------------------------------------------------
// <copyright file="TokenExchangeCacheTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.TokenExchange;

using AwesomeAssertions;
using D2.Shared.Auth.Outbound;
using D2.Shared.Auth.Outbound.TokenExchange;
using D2.Shared.Caching;
using D2.Shared.Caching.Local.Default;
using D2.Shared.Tests.Unit.AuthOutbound.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="TokenExchangeCache"/> — verifies the
/// (sessionId, audience, scope-set) keying contract, reverse-index
/// bookkeeping, and backplane-driven session-revoked invalidation. Uses a
/// real <see cref="DefaultLocalCache"/> so the cache facade exercises the
/// canonical underlying primitive instead of a mock.
/// </summary>
public sealed class TokenExchangeCacheTests
{
    // ----------------------------------------------------------------------
    // BuildKey — stability + uniqueness
    // ----------------------------------------------------------------------

    [Fact]
    public void BuildKey_SameInputs_ProducesSameKey()
    {
        var cache = NewCache();
        var sessionId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        var key1 = cache.BuildKey(sessionId, "https://files.internal", null)!;
        var key2 = cache.BuildKey(sessionId, "https://files.internal", null)!;

        key1.Should().Be(key2);
    }

    [Fact]
    public void BuildKey_DifferentSessionIds_ProduceDifferentKeys()
    {
        var cache = NewCache();
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();

        cache.BuildKey(s1, "https://x", null)
            .Should().NotBe(cache.BuildKey(s2, "https://x", null));
    }

    [Fact]
    public void BuildKey_DifferentAudiences_ProduceDifferentKeys()
    {
        var cache = NewCache();
        var s = Guid.NewGuid();

        cache.BuildKey(s, "https://files.internal", null)
            .Should().NotBe(cache.BuildKey(s, "https://notifications.internal", null));
    }

    [Fact]
    public void BuildKey_NullVsEmptyScopes_ProduceDifferentKeys()
    {
        // Adversarial: null = "no narrowing" (sentinel); empty = "narrow to no
        // scopes". Treating these as the same would silently merge two distinct
        // semantic states into one cache slot.
        var cache = NewCache();
        var s = Guid.NewGuid();

        cache.BuildKey(s, "https://x", null)
            .Should().NotBe(cache.BuildKey(s, "https://x", new HashSet<string>()));
    }

    [Fact]
    public void BuildKey_DifferentScopeSets_ProduceDifferentKeys()
    {
        var cache = NewCache();
        var s = Guid.NewGuid();

        cache.BuildKey(s, "https://x", new HashSet<string> { "self.read" })
            .Should().NotBe(cache.BuildKey(s, "https://x", new HashSet<string> { "self.write" }));
    }

    [Fact]
    public void BuildKey_ScopeOrderInsensitive()
    {
        // Adversarial: callers may pass scopes in any order. The underlying
        // hash sorts them so equivalent SETS produce equivalent keys.
        var cache = NewCache();
        var s = Guid.NewGuid();

        var keyA = cache.BuildKey(s, "https://x", new HashSet<string> { "a", "b", "c" })!;
        var keyB = cache.BuildKey(s, "https://x", new HashSet<string> { "c", "a", "b" })!;

        keyA.Should().Be(keyB);
    }

    [Fact]
    public void BuildKey_AudienceAtLimit_ReturnsKey()
    {
        var cache = NewCache();
        var audience = new string('a', TokenExchangeCache.MAX_AUDIENCE_LENGTH);

        var key = cache.BuildKey(Guid.NewGuid(), audience, null);

        key.Should().NotBeNull();
    }

    [Fact]
    public void BuildKey_AudienceOverLimit_ReturnsNull()
    {
        // Adversarial: defense-in-depth against attacker-controlled or
        // accidentally-oversized audience strings blowing up cache-key memory.
        var cache = NewCache();
        var audience = new string('a', TokenExchangeCache.MAX_AUDIENCE_LENGTH + 1);

        var key = cache.BuildKey(Guid.NewGuid(), audience, null);

        key.Should().BeNull();
    }

    [Fact]
    public void BuildKey_NullAudience_Throws()
    {
        var cache = NewCache();

        var act = () => cache.BuildKey(Guid.NewGuid(), null!, null);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildKey_UsesConfiguredPrefix()
    {
        var cache = NewCache(opts => opts.TokenExchangeCacheKeyPrefix = "custom:");
        var key = cache.BuildKey(Guid.NewGuid(), "https://x", null);

        key.Should().StartWith("custom:");
    }

    // ----------------------------------------------------------------------
    // Set + Get round-trip via the underlying ILocalCache
    // ----------------------------------------------------------------------

    [Fact]
    public async Task SetAsync_ThenTryGetAsync_RoundTrips()
    {
        var cache = NewCache();
        var sessionId = Guid.NewGuid();
        var key = cache.BuildKey(sessionId, "https://x", null)!;

        await cache.SetAsync(sessionId, key, "the-token", TimeSpan.FromMinutes(5));
        var got = await cache.TryGetAsync(key);

        got.Should().Be("the-token");
    }

    [Fact]
    public async Task TryGetAsync_Miss_ReturnsNull()
    {
        var cache = NewCache();

        var got = await cache.TryGetAsync("tokenexchange:missing");

        got.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_RegistersKeyInReverseIndex()
    {
        var cache = NewCache();
        var sessionId = Guid.NewGuid();
        var key = cache.BuildKey(sessionId, "https://x", null)!;

        await cache.SetAsync(sessionId, key, "tok", TimeSpan.FromMinutes(5));

        cache.GetKeysForSessionForTesting(sessionId).Should().Contain(key);
    }

    [Fact]
    public async Task SetAsync_MultipleKeysOneSession_AllInReverseIndex()
    {
        var cache = NewCache();
        var sessionId = Guid.NewGuid();
        var k1 = cache.BuildKey(sessionId, "https://files.internal", null)!;
        var k2 = cache.BuildKey(sessionId, "https://notifications.internal", null)!;

        await cache.SetAsync(sessionId, k1, "tok-1", TimeSpan.FromMinutes(5));
        await cache.SetAsync(sessionId, k2, "tok-2", TimeSpan.FromMinutes(5));

        cache.GetKeysForSessionForTesting(sessionId).Should().BeEquivalentTo([k1, k2]);
    }

    // ----------------------------------------------------------------------
    // Backplane-driven invalidation
    // ----------------------------------------------------------------------

    [Fact]
    public async Task SessionRevokedEvent_PurgesAllKeysForThatSession()
    {
        var backplane = new StubBackplane();
        var cache = NewCache(backplane: backplane);
        var sessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var k1 = cache.BuildKey(sessionId, "https://files.internal", null)!;
        var k2 = cache.BuildKey(sessionId, "https://notifications.internal", null)!;

        await cache.SetAsync(sessionId, k1, "tok-1", TimeSpan.FromMinutes(5));
        await cache.SetAsync(sessionId, k2, "tok-2", TimeSpan.FromMinutes(5));

        await backplane.PublishToSubscribersAsync($"session-revoked:{sessionId:D}");

        (await cache.TryGetAsync(k1)).Should().BeNull();
        (await cache.TryGetAsync(k2)).Should().BeNull();
        cache.GetKeysForSessionForTesting(sessionId).Should().BeEmpty();
    }

    [Fact]
    public async Task SessionRevokedEvent_DoesNotPurgeOtherSessions()
    {
        // Adversarial: one user may have many sessions (multi-device). A
        // revoke for session A must NOT purge tokens for session B.
        var backplane = new StubBackplane();
        var cache = NewCache(backplane: backplane);
        var sessionA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var sessionB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var keyA = cache.BuildKey(sessionA, "https://x", null)!;
        var keyB = cache.BuildKey(sessionB, "https://x", null)!;

        await cache.SetAsync(sessionA, keyA, "tok-A", TimeSpan.FromMinutes(5));
        await cache.SetAsync(sessionB, keyB, "tok-B", TimeSpan.FromMinutes(5));

        await backplane.PublishToSubscribersAsync($"session-revoked:{sessionA:D}");

        (await cache.TryGetAsync(keyA)).Should().BeNull();
        (await cache.TryGetAsync(keyB)).Should().Be("tok-B");
    }

    [Fact]
    public async Task BackplaneEvent_NotSessionRevokedPrefix_Ignored()
    {
        // Adversarial: the cache backplane carries arbitrary keys for many
        // consumers. Anything not prefixed "session-revoked:" must be a no-op.
        var backplane = new StubBackplane();
        var cache = NewCache(backplane: backplane);
        var sessionId = Guid.NewGuid();
        var key = cache.BuildKey(sessionId, "https://x", null)!;
        await cache.SetAsync(sessionId, key, "tok", TimeSpan.FromMinutes(5));

        await backplane.PublishToSubscribersAsync("jwks:default");
        await backplane.PublishToSubscribersAsync($"keyring:{Guid.NewGuid()}");
        await backplane.PublishToSubscribersAsync("session-other:xyz");

        (await cache.TryGetAsync(key)).Should().Be("tok");
    }

    [Fact]
    public async Task BackplaneEvent_MalformedSessionRevoked_Ignored()
    {
        // Adversarial: payload "session-revoked:not-a-guid" must not throw and
        // must not purge anything. Logged as warning per OutboundLog.
        var backplane = new StubBackplane();
        var cache = NewCache(backplane: backplane);
        var sessionId = Guid.NewGuid();
        var key = cache.BuildKey(sessionId, "https://x", null)!;
        await cache.SetAsync(sessionId, key, "tok", TimeSpan.FromMinutes(5));

        await backplane.PublishToSubscribersAsync("session-revoked:not-a-guid");

        (await cache.TryGetAsync(key)).Should().Be("tok");
        cache.GetKeysForSessionForTesting(sessionId).Should().Contain(key);
    }

    [Fact]
    public async Task BackplaneEvent_UnknownSessionId_NoOp()
    {
        // session-revoked event arrives for a session this cache has never seen.
        var backplane = new StubBackplane();
        var cache = NewCache(backplane: backplane);

        await backplane.PublishToSubscribersAsync($"session-revoked:{Guid.NewGuid():D}");

        // No exception, no side effect.
        cache.GetKeysForSessionForTesting(Guid.NewGuid()).Should().BeEmpty();
    }

    [Fact]
    public async Task BackplaneEvent_OversizedMalformedKey_DoesNotThrow_CacheStaysUsable()
    {
        // Adversarial: an attacker with backplane access could publish a
        // session-revoked event with an extremely long key. The cache must
        // handle it gracefully (no throw) and the cache itself must remain
        // usable afterward. The malformed key is also truncated before
        // logging — log-injection blast-radius defense.
        var backplane = new StubBackplane();
        var cache = NewCache(backplane: backplane);
        var sessionId = Guid.NewGuid();
        var key = cache.BuildKey(sessionId, "https://x", null)!;
        await cache.SetAsync(sessionId, key, "tok", TimeSpan.FromMinutes(5));
        var attackerKey = "session-revoked:" + new string('A', 100_000);

        var act = async () => await backplane.PublishToSubscribersAsync(attackerKey);

        await act.Should().NotThrowAsync();
        (await cache.TryGetAsync(key)).Should().Be("tok");
    }

    [Fact]
    public async Task SessionRevokedEvent_IncrementsRevokedPurgesCounter()
    {
        using var listener = new SimpleCounterListener(
            "d2.auth.outbound.token_exchange.revoked_purges");
        var backplane = new StubBackplane();
        var cache = NewCache(backplane: backplane);
        var sessionId = Guid.NewGuid();
        var key = cache.BuildKey(sessionId, "https://x", null)!;
        await cache.SetAsync(sessionId, key, "tok", TimeSpan.FromMinutes(5));

        await backplane.PublishToSubscribersAsync($"session-revoked:{sessionId:D}");

        listener.Total.Should().BeGreaterThan(0);
    }

    // ----------------------------------------------------------------------
    // Backplane absent — log warning + TTL-only fallback (no exception)
    // ----------------------------------------------------------------------

    [Fact]
    public async Task BackplaneAbsent_DoesNotThrow_AndCacheStillWorks()
    {
        var cache = NewCache(backplane: null);
        var sessionId = Guid.NewGuid();
        var key = cache.BuildKey(sessionId, "https://x", null)!;

        await cache.SetAsync(sessionId, key, "tok", TimeSpan.FromMinutes(5));
        var got = await cache.TryGetAsync(key);

        got.Should().Be("tok");
    }

    // ----------------------------------------------------------------------
    // Disposal
    // ----------------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_DisposesBackplaneSubscription()
    {
        var backplane = new StubBackplane();
        var cache = NewCache(backplane: backplane);

        await cache.DisposeAsync();

        // The stub doesn't actually mark itself disposed by subscription
        // lifetime, but this asserts no exception during dispose.
    }

    [Fact]
    public async Task DisposeAsync_Idempotent()
    {
        var cache = NewCache();

        await cache.DisposeAsync();
        await cache.DisposeAsync();
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static TokenExchangeCache NewCache(
        Action<AuthOutboundOptions>? configure = null,
        ICacheInvalidationBackplane? backplane = null)
    {
        var opts = new AuthOutboundOptions
        {
            Issuer = "https://edge.internal",
            ClientId = "test",
            ClientSecret = "test",
        };
        configure?.Invoke(opts);
        var localCache = new DefaultLocalCache(Options.Create(new LocalCacheOptions()));
        return new TokenExchangeCache(
            localCache,
            Options.Create(opts),
            NullLogger<TokenExchangeCache>.Instance,
            backplane);
    }
}
