// -----------------------------------------------------------------------
// <copyright file="RedisDistributedCacheTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Caching.Distributed;

using AwesomeAssertions;
using JetBrains.Annotations;
using Xunit;

/// <summary>
/// End-to-end tests for <c>RedisDistributedCache</c> against a real
/// Redis container. Covers the full op surface (basic / atomic / set /
/// broadcast). Each test uses its own key prefix to stay isolated from
/// neighbors in the shared fixture.
/// </summary>
[Collection("Redis")]
public sealed class RedisDistributedCacheTests
{
    private readonly RedisFixture r_fixture;

    public RedisDistributedCacheTests(RedisFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    public async Task GetSet_RoundTrips()
    {
        await using var ctx = NewCache();
        var setResult = await ctx.Cache.SetAsync("k", "hello world");
        setResult.IsOk.Should().BeTrue();

        var getResult = await ctx.Cache.GetAsync<string>("k");
        getResult.IsOk.Should().BeTrue();
        getResult.Data.Should().Be("hello world");
    }

    [Fact]
    public async Task GetAsync_MissReturnsNotFound()
    {
        await using var ctx = NewCache();
        var result = await ctx.Cache.GetAsync<string>("absent");
        result.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task GetManyAsync_PartialHit_ReturnsSomeFound()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("a", 1);
        await ctx.Cache.SetAsync("b", 2);

        var result = await ctx.Cache.GetManyAsync<int>(["a", "b", "missing"]);
        result.IsSomeFound.Should().BeTrue();
        result.Data!.Should().ContainKey("a");
        result.Data!.Should().ContainKey("b");
        result.Data!.Should().NotContainKey("missing");
    }

    [Fact]
    public async Task SetNxAsync_Concurrent_OnlyOneWinsAcrossCluster()
    {
        // Cluster-wide atomicity: 32 concurrent SetNx attempts on the same key,
        // exactly one wins (returns true), all others observe the winner's value.
        await using var ctx = NewCache();
        var winners = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 32),
            new ParallelOptions { MaxDegreeOfParallelism = 32 },
            async (i, ct) =>
            {
                var result = await ctx.Cache.SetNxAsync("k", $"owner-{i}", ct: ct);
                if (result.Data) Interlocked.Increment(ref winners);
            });

        winners.Should().Be(1);
    }

    [Fact]
    public async Task IncrementAsync_Concurrent_AggregatesAtomically()
    {
        // Cluster-wide atomic increment: 8 threads × 200 increments → 1600 total.
        await using var ctx = NewCache();
        const int threads = 8;
        const int iterations = 200;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, threads),
            new ParallelOptions { MaxDegreeOfParallelism = threads },
            async (_, ct) =>
            {
                for (var i = 0; i < iterations; i++)
                    await ctx.Cache.IncrementAsync("counter", ct: ct);
            });

        var final = await ctx.Cache.GetAsync<long>("counter");
        final.IsOk.Should().BeTrue();
        final.Data.Should().Be(threads * iterations);
    }

    [Fact]
    public async Task IncrementAsync_OnNonNumeric_ReturnsConflict()
    {
        // Redis WRONGTYPE → Conflict result.
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("k", "not-a-number");
        var result = await ctx.Cache.IncrementAsync("k");
        result.IsConflict.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireReleaseLock_RoundTrip()
    {
        await using var ctx = NewCache();
        var acquire = await ctx.Cache.AcquireLockAsync(
            "lock-k", "owner-A", TimeSpan.FromSeconds(30));
        acquire.Data.Should().BeTrue();

        // Same owner re-acquiring sees the existing lock — SET NX fails.
        var reacquire = await ctx.Cache.AcquireLockAsync(
            "lock-k", "owner-A", TimeSpan.FromSeconds(30));
        reacquire.Data.Should().BeFalse();

        var release = await ctx.Cache.ReleaseLockAsync("lock-k", "owner-A");
        release.IsOk.Should().BeTrue();

        var reacquireAfterRelease = await ctx.Cache.AcquireLockAsync(
            "lock-k", "owner-B", TimeSpan.FromSeconds(30));
        reacquireAfterRelease.Data.Should().BeTrue();
    }

    [Fact]
    public async Task ReleaseLockAsync_WrongOwner_DoesNotRelease()
    {
        // Compare-and-delete Lua should refuse to release if lockId mismatches.
        await using var ctx = NewCache();
        await ctx.Cache.AcquireLockAsync("lock-k", "owner-A", TimeSpan.FromSeconds(30));
        await ctx.Cache.ReleaseLockAsync("lock-k", "owner-B");  // wrong owner — no-op

        var stillHeld = await ctx.Cache.AcquireLockAsync(
            "lock-k", "owner-C", TimeSpan.FromSeconds(30));
        stillHeld.Data.Should().BeFalse();  // A still holds
    }

    [Fact]
    public async Task SetAddAsync_Concurrent_BuildsCorrectCardinality()
    {
        // 100 concurrent SetAdds with 30 distinct members + repeats.
        // Final cardinality must equal exactly the number of distinct values.
        await using var ctx = NewCache();
        const int distinctMembers = 30;
        const int totalAdds = 100;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, totalAdds),
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            async (i, ct) =>
            {
                var member = $"member-{i % distinctMembers}";
                await ctx.Cache.SetAddAsync("set-k", member, ct: ct);
            });

        var card = await ctx.Cache.SetCardinalityAsync("set-k");
        card.IsOk.Should().BeTrue();
        card.Data.Should().Be(distinctMembers);
    }

    [Fact]
    public async Task SetContainsAsync_AddedThenContains()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAddAsync("set-k", "alpha");

        var has = await ctx.Cache.SetContainsAsync("set-k", "alpha");
        has.Data.Should().BeTrue();

        var missing = await ctx.Cache.SetContainsAsync("set-k", "beta");
        missing.Data.Should().BeFalse();
    }

    [Fact]
    public async Task SetRemoveAsync_RemovesPreviouslyAdded()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAddAsync("set-k", "alpha");
        var removed = await ctx.Cache.SetRemoveAsync("set-k", "alpha");
        removed.Data.Should().BeTrue();

        var has = await ctx.Cache.SetContainsAsync("set-k", "alpha");
        has.Data.Should().BeFalse();
    }

    [Fact]
    public async Task GetTtlAsync_HonorsExpiration()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("k", "v", TimeSpan.FromMinutes(5));
        var ttl = await ctx.Cache.GetTtlAsync("k");
        ttl.IsOk.Should().BeTrue();
        ttl.Data.Should().NotBeNull();
        ttl.Data!.Value.Should().BeGreaterThan(TimeSpan.FromMinutes(4));
        ttl.Data!.Value.Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task GetTtlAsync_AbsentKey_ReturnsNotFound()
    {
        await using var ctx = NewCache();
        var ttl = await ctx.Cache.GetTtlAsync("absent");
        ttl.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAsync_RemovesValue()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("k", "v");
        await ctx.Cache.RemoveAsync("k");
        var get = await ctx.Cache.GetAsync<string>("k");
        get.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task SetAndBroadcast_WithoutBackplane_Throws()
    {
        await using var ctx = NewCache();  // no backplane
        InvalidOperationException? caught = null;
        try
        {
            await ctx.Cache.SetAndBroadcastAsync("k", "v");
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull();
    }

    [MustDisposeResource(false)]
    private CacheContext NewCache()
    {
        // Fresh key prefix per test for isolation against other tests in the same fixture.
        var prefix = $"test-{Guid.NewGuid():N}:";
        return new CacheContext(r_fixture.ConnectionString, prefix);
    }
}
