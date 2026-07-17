// -----------------------------------------------------------------------
// <copyright file="DefaultTieredCacheRedisTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Caching.Tiered;

using AwesomeAssertions;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Caching.Distributed.Redis;
using DcsvIo.D2.Caching.Local.Default;
using DcsvIo.D2.Caching.Tiered;
using DcsvIo.D2.Tests.Integration.Caching.Distributed;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

/// <summary>
/// End-to-end tests for <see cref="DefaultTieredCache"/> against real
/// Redis + real local IMemoryCache. Verifies the cross-instance flows
/// the abstraction promises: cluster invalidation, L2-first writes, L1
/// populate on L2 hit.
/// </summary>
[Collection("Redis")]
public sealed class DefaultTieredCacheRedisTests
{
    private readonly RedisFixture r_fixture;

    public DefaultTieredCacheRedisTests(RedisFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    public async Task Get_L1Miss_PopulatesFromL2()
    {
        // Write to L2 directly (bypassing tiered) → tiered Get should
        // miss L1, hit L2, populate L1, return.
        await using var ctx = NewTieredContext();
        await ctx.L2.SetAsync("k", "from-L2");

        var get1 = await ctx.Tiered.GetAsync<string>("k");
        get1.IsOk.Should().BeTrue();
        get1.Data.Should().Be("from-L2");

        // L1 should now have it (no extra L2 round-trip on next call).
        var l1Direct = await ctx.L1.GetAsync<string>("k");
        l1Direct.IsOk.Should().BeTrue();
        l1Direct.Data.Should().Be("from-L2");
    }

    [Fact]
    public async Task Set_WritesToBoth()
    {
        await using var ctx = NewTieredContext();
        await ctx.Tiered.SetAsync("k", "v");

        var l1 = await ctx.L1.GetAsync<string>("k");
        var l2 = await ctx.L2.GetAsync<string>("k");
        l1.Data.Should().Be("v");
        l2.Data.Should().Be("v");
    }

    [Fact]
    public async Task Remove_RemovesFromBoth()
    {
        await using var ctx = NewTieredContext();
        await ctx.Tiered.SetAsync("k", "v");
        await ctx.Tiered.RemoveAsync("k");

        (await ctx.L1.ExistsAsync("k")).Data.Should().BeFalse();
        (await ctx.L2.ExistsAsync("k")).Data.Should().BeFalse();
    }

    [Fact]
    public async Task SetAndBroadcast_OtherInstanceL1Drops()
    {
        // Two tiered instances sharing one Redis backplane = two "replicas".
        // Instance A writes K with broadcast → instance B's L1 must drop
        // its (potentially stale) copy of K so its next read re-fetches
        // the fresh value from L2.
        var channel = $"test-channel-{Guid.NewGuid():N}";
        var keyPrefix = $"test-{Guid.NewGuid():N}:";

        await using var instanceA = NewTieredContext(channel, keyPrefix);
        await using var instanceB = NewTieredContext(channel, keyPrefix);

        // Pre-load instance B's L1 with stale value via direct L1 access.
        await instanceB.L1.SetAsync("k", "stale-on-B");

        // Instance A writes the new value with broadcast.
        var setResult = await instanceA.Tiered.SetAndBroadcastAsync("k", "fresh-from-A");
        setResult.IsOk.Should().BeTrue();

        // Wait for broadcast to propagate.
        await Task.Delay(300);

        // Instance B's L1 should be empty for K (dropped by backplane handler).
        var bL1 = await instanceB.L1.GetAsync<string>("k");
        bL1.IsNotFound.Should().BeTrue();

        // Instance B's tiered Get should now fetch the fresh value from L2.
        var bTiered = await instanceB.Tiered.GetAsync<string>("k");
        bTiered.Data.Should().Be("fresh-from-A");
    }

    [Fact]
    public async Task SetAndBroadcast_OwnInstanceL1AlsoDrops()
    {
        // Universal "everyone acts" rule: the publishing instance also
        // receives its own message and drops its L1 entry. Next read
        // re-populates from L2 (one extra round-trip — bounded cost).
        var channel = $"test-channel-{Guid.NewGuid():N}";
        var keyPrefix = $"test-{Guid.NewGuid():N}:";

        await using var instance = NewTieredContext(channel, keyPrefix);

        await instance.Tiered.SetAndBroadcastAsync("k", "v");
        await Task.Delay(300);  // wait for self-receive

        // L1 should be empty (self-receive dropped it).
        var l1 = await instance.L1.GetAsync<string>("k");
        l1.IsNotFound.Should().BeTrue();

        // Tiered Get re-populates from L2 (which still has the value).
        var tiered = await instance.Tiered.GetAsync<string>("k");
        tiered.Data.Should().Be("v");

        // L1 now populated again from L2.
        var l1AfterRead = await instance.L1.GetAsync<string>("k");
        l1AfterRead.Data.Should().Be("v");
    }

    [Fact]
    public async Task IncrementAsync_RoutesThroughL2_InvalidatesL1()
    {
        await using var ctx = NewTieredContext();

        // Pre-load L1 with a stale counter value.
        await ctx.L1.SetAsync("counter", 100L);

        // Increment via tiered → routes to L2 (which doesn't have the key
        // yet, starts at 0+1=1) and invalidates L1.
        var inc = await ctx.Tiered.IncrementAsync("counter");
        inc.IsOk.Should().BeTrue();
        inc.Data.Should().Be(1);  // L2 was empty; counter starts at 0 + 1

        // L1 should have been invalidated.
        var l1 = await ctx.L1.GetAsync<long>("counter");
        l1.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAndBroadcast_OtherInstancesDropL1()
    {
        var channel = $"test-channel-{Guid.NewGuid():N}";
        var keyPrefix = $"test-{Guid.NewGuid():N}:";

        await using var instanceA = NewTieredContext(channel, keyPrefix);
        await using var instanceB = NewTieredContext(channel, keyPrefix);

        // Both instances write the value (independent L1 entries).
        await instanceA.Tiered.SetAsync("k", "shared");
        await instanceB.L1.SetAsync("k", "shared-on-B");

        // A removes + broadcasts.
        await instanceA.Tiered.RemoveAndBroadcastAsync("k");
        await Task.Delay(300);

        // B's L1 should be cleared.
        var bL1 = await instanceB.L1.GetAsync<string>("k");
        bL1.IsNotFound.Should().BeTrue();

        // B's tiered Get → L1 miss → L2 miss (A removed from L2) → NotFound.
        var bTiered = await instanceB.Tiered.GetAsync<string>("k");
        bTiered.IsNotFound.Should().BeTrue();
    }

    [MustDisposeResource(false)]
    private TieredContext NewTieredContext(string? channel = null, string? keyPrefix = null)
    {
        channel ??= $"test-channel-{Guid.NewGuid():N}";
        keyPrefix ??= $"test-{Guid.NewGuid():N}:";

        var redis = ConnectionMultiplexer.Connect(r_fixture.ConnectionString);
        var redisOpts = Options.Create(new RedisCacheOptions
        {
            ConnectionString = r_fixture.ConnectionString,
            KeyPrefix = keyPrefix,
            InvalidationChannel = channel,
            DefaultExpiration = TimeSpan.FromMinutes(5),
        });

        var backplane = new RedisCacheInvalidationBackplane(
            redis,
            redisOpts,
            NullLogger<RedisCacheInvalidationBackplane>.Instance);

        var l1 = new DefaultLocalCache(Options.Create(new LocalCacheOptions
        {
            DefaultExpiration = TimeSpan.FromMinutes(5),
        }));

        var l2 = new RedisDistributedCache(
            redis,
            redisOpts,
            new JsonCacheSerializer(),
            NullLogger<RedisDistributedCache>.Instance,
            backplane);

        var tiered = new DefaultTieredCache(
            l1,
            l2,
            NullLogger<DefaultTieredCache>.Instance,
            backplane);

        return new TieredContext(redis, l1, l2, tiered, backplane);
    }

    [MustDisposeResource(false)]
    private sealed class TieredContext : IAsyncDisposable
    {
        private readonly ConnectionMultiplexer r_redis;
        private readonly DefaultLocalCache r_l1;
        private readonly RedisCacheInvalidationBackplane r_backplane;
        private readonly DefaultTieredCache r_tiered;

        [MustDisposeResource(false)]
        internal TieredContext(
            ConnectionMultiplexer redis,
            DefaultLocalCache l1,
            RedisDistributedCache l2,
            DefaultTieredCache tiered,
            RedisCacheInvalidationBackplane backplane)
        {
            r_redis = redis;
            r_l1 = l1;
            L2 = l2;
            r_tiered = tiered;
            r_backplane = backplane;
        }

        internal ILocalCache L1 => r_l1;

        internal RedisDistributedCache L2 { get; }

        internal ITieredCache Tiered => r_tiered;

        public async ValueTask DisposeAsync()
        {
            await r_tiered.DisposeAsync();
            await r_backplane.DisposeAsync();
            r_l1.Dispose();
            await r_redis.DisposeAsync();
        }
    }
}
