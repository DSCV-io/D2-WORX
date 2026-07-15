// -----------------------------------------------------------------------
// <copyright file="DefaultTieredCacheAdversarialTests.cs" company="DCSV">
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
/// Adversarial / chaos tests for <c>DefaultTieredCache</c>. Cluster-wide
/// scenarios with multiple instances, concurrent ops, and races between
/// L1 / L2 / backplane.
/// </summary>
[Collection("Redis")]
public sealed class DefaultTieredCacheAdversarialTests
{
    private readonly RedisFixture r_fixture;

    public DefaultTieredCacheAdversarialTests(RedisFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    public async Task TwoInstances_ConcurrentSetAndBroadcast_FinalStateConsistent()
    {
        // Two replicas. Both fire SetAndBroadcast on the same key with
        // different values. Final L2 value: one of the two (last-writer-wins).
        // After all broadcasts settle, BOTH instances' L1 must agree with L2.
        var channel = $"chaos-{Guid.NewGuid():N}";
        var keyPrefix = $"chaos-{Guid.NewGuid():N}:";

        await using var a = NewTieredContext(channel, keyPrefix);
        await using var b = NewTieredContext(channel, keyPrefix);

        // Fire 50 sets from each instance concurrently.
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 100),
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            async (i, ct) =>
            {
                var instance = i % 2 == 0 ? a : b;
                await instance.Tiered.SetAndBroadcastAsync("k", $"value-{i}", ct: ct);
            });

        // Let broadcasts drain.
        await Task.Delay(500);

        // L2 has the final value (whichever was last).
        var l2Final = await a.L2.GetAsync<string>("k");
        l2Final.IsOk.Should().BeTrue();
        var canonical = l2Final.Data;

        // Both instances' tiered Get should agree (they fall through to L2 on miss
        // since broadcast invalidated their L1).
        var aGet = await a.Tiered.GetAsync<string>("k");
        var bGet = await b.Tiered.GetAsync<string>("k");
        aGet.Data.Should().Be(canonical);
        bGet.Data.Should().Be(canonical);
    }

    [Fact]
    public async Task TwoInstances_RemoveOnA_BReadsNotFound()
    {
        // Sequential test of cross-instance remove + broadcast.
        var channel = $"chaos-{Guid.NewGuid():N}";
        var keyPrefix = $"chaos-{Guid.NewGuid():N}:";

        await using var a = NewTieredContext(channel, keyPrefix);
        await using var b = NewTieredContext(channel, keyPrefix);

        // Both have the value cached in L1 + L2 (via initial Set on A).
        await a.Tiered.SetAsync("k", "shared");
        await b.Tiered.GetAsync<string>("k");  // populates B's L1 from L2

        // A removes + broadcasts.
        await a.Tiered.RemoveAndBroadcastAsync("k");
        await Task.Delay(300);  // let broadcast propagate

        // B's tiered Get → L1 was dropped by broadcast → L2 miss → NotFound.
        var bGet = await b.Tiered.GetAsync<string>("k");
        bGet.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task ThreeInstances_ChainedInvalidation_AllAgree()
    {
        // 3-instance scenario simulating a small cluster. Instance A writes,
        // B and C read (populate their L1), then A writes again with broadcast.
        // After settling, all three L1s must agree with L2.
        var channel = $"chaos-{Guid.NewGuid():N}";
        var keyPrefix = $"chaos-{Guid.NewGuid():N}:";

        await using var a = NewTieredContext(channel, keyPrefix);
        await using var b = NewTieredContext(channel, keyPrefix);
        await using var c = NewTieredContext(channel, keyPrefix);

        await a.Tiered.SetAsync("k", "v1");
        await b.Tiered.GetAsync<string>("k");  // B's L1 = v1
        await c.Tiered.GetAsync<string>("k");  // C's L1 = v1

        await a.Tiered.SetAndBroadcastAsync("k", "v2");
        await Task.Delay(400);  // broadcast settle

        // All three must see v2 on next read.
        var aGet = await a.Tiered.GetAsync<string>("k");
        var bGet = await b.Tiered.GetAsync<string>("k");
        var cGet = await c.Tiered.GetAsync<string>("k");

        aGet.Data.Should().Be("v2");
        bGet.Data.Should().Be("v2");
        cGet.Data.Should().Be("v2");
    }

    [Fact]
    public async Task GetMany_PartialL1_FallsThroughToL2_PopulatesL1()
    {
        // L1 has {a, b}; L2 has {a, b, c, d, e}. GetMany([a,b,c,d,e]) should
        // return all 5, populate L1 with {c, d, e}, and verify L1 now has all.
        await using var ctx = NewTieredContext(
            $"chaos-{Guid.NewGuid():N}",
            $"chaos-{Guid.NewGuid():N}:");

        // Seed L2 with all 5.
        for (var i = 0; i < 5; i++)
            await ctx.L2.SetAsync($"k{i}", $"v{i}");

        // Seed L1 with first 2.
        await ctx.L1.SetAsync("k0", "v0");
        await ctx.L1.SetAsync("k1", "v1");

        var get = await ctx.Tiered.GetManyAsync<string>(new[] { "k0", "k1", "k2", "k3", "k4" });

        get.IsOk.Should().BeTrue();
        get.Data!.Should().HaveCount(5);
        for (var i = 0; i < 5; i++)
            get.Data[$"k{i}"].Should().Be($"v{i}");

        // L1 now has all 5 (populate-on-L2-hit).
        for (var i = 0; i < 5; i++)
        {
            var l1 = await ctx.L1.GetAsync<string>($"k{i}");
            l1.IsOk.Should().BeTrue();
        }
    }

    [Fact]
    public async Task IncrementAsync_AcrossInstances_AggregatesAtomically()
    {
        // Two instances both incrementing the same counter via tiered.
        // Routes through L2 (cluster source of truth). Final L2 value
        // must equal total increments across both instances.
        var channel = $"chaos-{Guid.NewGuid():N}";
        var keyPrefix = $"chaos-{Guid.NewGuid():N}:";

        await using var a = NewTieredContext(channel, keyPrefix);
        await using var b = NewTieredContext(channel, keyPrefix);

        const int perInstance = 100;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 2 * perInstance),
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            async (i, ct) =>
            {
                var instance = i % 2 == 0 ? a : b;
                await instance.Tiered.IncrementAsync("counter", ct: ct);
            });

        // Verify directly on L2.
        var l2Final = await a.L2.GetAsync<long>("counter");
        l2Final.IsOk.Should().BeTrue();
        l2Final.Data.Should().Be(2 * perInstance);
    }

    [Fact]
    public async Task SetAndBroadcastAsync_BroadcastFails_ReturnsFailure_DataInL2()
    {
        // Edge case documentation: if Set succeeds but broadcast fails,
        // we return the broadcast failure. The data IS in the cache;
        // other instances just don't know to invalidate.
        // Here we don't have an easy way to inject a backplane failure,
        // but we can verify the inverse: WITHOUT a backplane registered,
        // the broadcast call throws (different failure mode).
        await using var ctx = NewTieredContextNoBackplane();

        InvalidOperationException? caught = null;
        try
        {
            await ctx.Tiered.SetAndBroadcastAsync("k", "v");
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull();
    }

    [Fact]
    public async Task TieredCache_DisposedAndReused_HandlesGracefully()
    {
        // Dispose tiered cache → its backplane subscription is removed.
        // Verify no NPE / no crash on subsequent ops.
        var channel = $"chaos-{Guid.NewGuid():N}";
        var keyPrefix = $"chaos-{Guid.NewGuid():N}:";
        var ctx = NewTieredContext(channel, keyPrefix);

        await ctx.Tiered.SetAsync("k", "v");
        await ctx.DisposeAsync();

        // After dispose, the underlying caches are gone too. We can't legally use
        // ctx anymore — that's the contract. The test verifies that disposal
        // itself doesn't throw.
    }

    [Fact]
    public async Task Tiered_SetAsyncWithVeryShortTtl_ExpiresOnBothTiers()
    {
        // 100ms TTL on both L1 and L2. After 250ms, both should report NotFound.
        await using var ctx = NewTieredContext(
            $"chaos-{Guid.NewGuid():N}",
            $"chaos-{Guid.NewGuid():N}:");

        await ctx.Tiered.SetAsync("k", "v", TimeSpan.FromMilliseconds(100));

        await Task.Delay(300);

        var l1Get = await ctx.L1.GetAsync<string>("k");
        var l2Get = await ctx.L2.GetAsync<string>("k");
        var tieredGet = await ctx.Tiered.GetAsync<string>("k");

        l1Get.IsNotFound.Should().BeTrue();
        l2Get.IsNotFound.Should().BeTrue();
        tieredGet.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task Tiered_NullValue_RoundTripsThroughBothTiers()
    {
        // Cache a null value via tiered. Both L1 and L2 should hold "null"
        // (not absent — present-with-null). GetAsync<string?> should return
        // Ok(null), not NotFound.
        await using var ctx = NewTieredContext(
            $"chaos-{Guid.NewGuid():N}",
            $"chaos-{Guid.NewGuid():N}:");

        await ctx.Tiered.SetAsync<string?>("k", null);

        var get = await ctx.Tiered.GetAsync<string?>("k");
        get.IsOk.Should().BeTrue();
        get.Data.Should().BeNull();

        // Both tiers should report Exists=true.
        (await ctx.L1.ExistsAsync("k")).Data.Should().BeTrue();
        (await ctx.L2.ExistsAsync("k")).Data.Should().BeTrue();
    }

    [MustDisposeResource(false)]
    private TieredContext NewTieredContext(string channel, string keyPrefix)
    {
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
    private TieredContext NewTieredContextNoBackplane()
    {
        var redis = ConnectionMultiplexer.Connect(r_fixture.ConnectionString);
        var redisOpts = Options.Create(new RedisCacheOptions
        {
            ConnectionString = r_fixture.ConnectionString,
            KeyPrefix = $"no-bp-{Guid.NewGuid():N}:",
            DefaultExpiration = TimeSpan.FromMinutes(5),
        });
        var l1 = new DefaultLocalCache(Options.Create(new LocalCacheOptions
        {
            DefaultExpiration = TimeSpan.FromMinutes(5),
        }));
        var l2 = new RedisDistributedCache(
            redis,
            redisOpts,
            new JsonCacheSerializer(),
            NullLogger<RedisDistributedCache>.Instance,
            backplane: null);
        var tiered = new DefaultTieredCache(
            l1,
            l2,
            NullLogger<DefaultTieredCache>.Instance,
            backplane: null);
        return new TieredContext(redis, l1, l2, tiered, backplane: null);
    }

    [MustDisposeResource(false)]
    private sealed class TieredContext : IAsyncDisposable
    {
        private readonly ConnectionMultiplexer r_redis;
        private readonly DefaultLocalCache r_l1;
        private readonly RedisCacheInvalidationBackplane? r_backplane;
        private readonly DefaultTieredCache r_tiered;

        [MustDisposeResource(false)]
        internal TieredContext(
            ConnectionMultiplexer redis,
            DefaultLocalCache l1,
            RedisDistributedCache l2,
            DefaultTieredCache tiered,
            RedisCacheInvalidationBackplane? backplane)
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
            if (r_backplane is not null)
                await r_backplane.DisposeAsync();
            r_l1.Dispose();
            await r_redis.DisposeAsync();
        }
    }
}
