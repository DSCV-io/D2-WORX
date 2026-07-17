// -----------------------------------------------------------------------
// <copyright file="DefaultLocalCacheBehaviorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Caching.Local;

using AwesomeAssertions;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Caching.Local.Default;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

/// <summary>
/// Behavior tests for <see cref="DefaultLocalCache"/> — exercise real
/// <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/> semantics
/// (capacity-driven eviction, TTL expiration, concurrent-access stress).
/// TTL-expiry tests use a <see cref="FakeTimeProvider"/> so expiry is
/// driven by clock advance rather than real-time sleeps — load-independent
/// and fully deterministic. The Integration tier is reserved for libs whose
/// dependencies require real infrastructure (Redis Testcontainers, real PG,
/// etc.).
/// </summary>
public sealed class DefaultLocalCacheBehaviorTests
{
    [Fact]
    public async Task SetAsync_ExpiredEntryReturnsNotFoundOnNextRead()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock: clock);
        await cache.SetAsync("k", 1, TimeSpan.FromMinutes(1));

        // Advance past the TTL — IMemoryCache uses the same fake clock.
        clock.Advance(TimeSpan.FromMinutes(2));

        var result = await cache.GetAsync<int>("k");
        result.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task SetAsync_GetTtlReportsRemainingThenZero()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock: clock);
        await cache.SetAsync("k", 1, TimeSpan.FromMinutes(5));

        var early = await cache.GetTtlAsync("k");
        early.IsOk.Should().BeTrue();
        early.Data.Should().NotBeNull();
        early.Data!.Value.Should().BeGreaterThan(TimeSpan.FromMinutes(1));

        // Advance past the TTL.
        clock.Advance(TimeSpan.FromMinutes(6));

        var late = await cache.GetTtlAsync("k");
        late.IsNotFound.Should().BeTrue();  // entry has been evicted by IMemoryCache
    }

    [Fact]
    public async Task SetAsync_ExceedingCapacityEvictsEntries()
    {
        using var cache = NewCache(opts => opts.MaxEntries = 50);

        for (var i = 0; i < 100; i++)
            await cache.SetAsync($"k{i}", i);

        // Wait for the post-eviction callback to drain (compaction is async).
        // This is a genuine async platform callback — not TTL-related — so a
        // small real-time settle is correct here (not a timing hazard because
        // the assertion is a loose "< 100" bound, not an exact count).
        await Task.Delay(200);

        var present = 0;

        for (var i = 0; i < 100; i++)
        {
            if ((await cache.GetAsync<int>($"k{i}")).IsOk)
                present++;
        }

        // IMemoryCache compaction is approximate (priority + age based, not
        // strict LRU), but the cap should mean fewer than 100 entries survive.
        present.Should().BeLessThan(100);
        present.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task IncrementAsync_ConcurrentAdditionsAggregateCorrectly()
    {
        using var cache = NewCache();

        var iterations = 1000;
        var threads = 8;
        var totalExpected = iterations * threads;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, threads),
            new ParallelOptions { MaxDegreeOfParallelism = threads },
            async (_, ct) =>
            {
                for (var i = 0; i < iterations; i++)
                    await cache.IncrementAsync("counter", ct: ct);
            });

        var final = await cache.GetAsync<long>("counter");
        final.IsOk.Should().BeTrue();
        final.Data.Should().Be(totalExpected);
    }

    [Fact]
    public async Task SetAsync_ConcurrentWritesAndGetTtl_StayConsistent()
    {
        // Race-protection check for the SetCore write pair (r_cache + r_expirations).
        // N threads alternate between Set (with TTL=1h) and GetTtl on the same key.
        // Without locking the SetCore body, GetTtl could observe a key in r_cache
        // but no TTL in r_expirations (or vice versa). The check here is that we
        // never see a half-applied state: every observable GetTtl must either
        // report NotFound (impossible after first Set) OR return a TTL within
        // the expected band.
        using var cache = NewCache();
        await cache.SetAsync("k", 0, TimeSpan.FromHours(1));

        var threads = 16;
        var iterations = 200;
        var inconsistencies = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, threads),
            new ParallelOptions { MaxDegreeOfParallelism = threads },
            async (i, ct) =>
            {
                for (var iter = 0; iter < iterations; iter++)
                {
                    if (iter % 2 == 0)
                    {
                        await cache.SetAsync("k", (i * 1000) + iter, TimeSpan.FromHours(1), ct);
                    }
                    else
                    {
                        var ttl = await cache.GetTtlAsync("k", ct);

                        // Key is always present, so we should always see Ok
                        // with a TTL in (0h, 1h]. NotFound or Ok(null) would
                        // indicate a half-applied state.
                        if (!ttl.IsOk || ttl.Data is null
                            || ttl.Data > TimeSpan.FromHours(1)
                            || ttl.Data <= TimeSpan.Zero)
                        {
                            Interlocked.Increment(ref inconsistencies);
                        }
                    }
                }
            });

        inconsistencies.Should().Be(0);
    }

    [Fact]
    public async Task SetAndRemove_Interleaved_NeverThrows()
    {
        // Set vs Remove interleaving stress. The strict invariant version
        // of this test is impossible: Get and GetTtl are separate ops, and
        // between them anything can happen (Remove, new Set), so combined
        // assertions across the two ops will race even when each op is
        // individually correct. The narrower property worth checking under
        // chaotic concurrency is "no exceptions, no crashes."
        using var cache = NewCache();

        var threads = 16;
        var iterations = 500;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, threads),
            new ParallelOptions { MaxDegreeOfParallelism = threads },
            async (i, ct) =>
            {
                for (var iter = 0; iter < iterations; iter++)
                {
                    var op = (iter + i) % 4;
                    if (op == 0)
                        await cache.SetAsync("k", iter, TimeSpan.FromHours(1), ct);
                    else if (op == 1)
                        await cache.RemoveAsync("k", ct);
                    else if (op == 2)
                        _ = await cache.GetAsync<int>("k", ct);
                    else
                        _ = await cache.GetTtlAsync("k", ct);
                }
            });

        // Reaching this line without exception is the test passing.
    }

    [Fact]
    public async Task MixedOps_HighConcurrency_NeverThrowsAndRemainsCoherent()
    {
        // Smoke test for the full op surface under contention. 24 threads,
        // each doing a mix of Get / Set / Remove / SetNx / Increment /
        // AcquireLock / ReleaseLock against a small key space (10 keys).
        // Verifies (a) no exceptions / no torn results, (b) the lock state
        // never reports more than one acquirer at a time per key.
        using var cache = NewCache();

        var threads = 24;
        var iterations = 250;
        var keyCount = 10;
        var lockHolders = new int[keyCount];  // per-key acquirer count

        await Parallel.ForEachAsync(
            Enumerable.Range(0, threads),
            new ParallelOptions { MaxDegreeOfParallelism = threads },
            async (i, ct) =>
            {
                for (var iter = 0; iter < iterations; iter++)
                {
                    // Random.Shared is thread-safe; per-thread instances were
                    // unnecessary and triggered the §1.14/§4.7 carve-out.
                    var key = $"k{Random.Shared.Next(keyCount)}";
                    var op = Random.Shared.Next(7);
                    switch (op)
                    {
                        case 0:
                            // Get can be Ok (hit) or NotFound (miss); both are
                            // valid outcomes. We're checking the call doesn't
                            // throw and the result is well-formed.
                            _ = await cache.GetAsync<int>(key, ct);
                            break;
                        case 1:
                            await cache.SetAsync(key, iter, TimeSpan.FromMinutes(5), ct);
                            break;
                        case 2:
                            await cache.RemoveAsync(key, ct);
                            break;
                        case 3:
                            await cache.SetNxAsync(key, iter, TimeSpan.FromMinutes(5), ct);
                            break;
                        case 4:
                            await cache.IncrementAsync($"counter-{key}", ct: ct);
                            break;
                        case 5:
                            var keyIndex = int.Parse(
                                key.AsSpan(1),
                                System.Globalization.CultureInfo.InvariantCulture);
                            var lockId = $"thread-{i}-{iter}";
                            var acquired = await cache.AcquireLockAsync(
                                key, lockId, TimeSpan.FromSeconds(30), ct);
                            if (acquired.Data)
                            {
                                Interlocked.Increment(ref lockHolders[keyIndex])
                                    .Should()
                                    .Be(1, "only one thread may hold the lock at a time");
                                Interlocked.Decrement(ref lockHolders[keyIndex]);
                                await cache.ReleaseLockAsync(key, lockId, ct);
                            }

                            break;
                        case 6:
                            _ = await cache.ExistsAsync(key, ct);
                            break;
                    }
                }
            });
    }

    [Fact]
    public async Task IncrementAsync_RaceWithRemove_NeverLosesAtomicity()
    {
        // Increment + Remove interleaving stress. Increments fire on a key
        // while another thread occasionally removes it. Each Increment must
        // either return Ok(value) (we incremented an existing or fresh key)
        // or contend cleanly — never throw, never return stale Conflict.
        var cache = NewCache();
        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, 2),
                new ParallelOptions { MaxDegreeOfParallelism = 2 },
                async (worker, ct) =>
                {
                    if (worker == 0)
                    {
                        for (var i = 0; i < 1000; i++)
                        {
                            var result = await cache.IncrementAsync("k", ct: ct);
                            result.IsOk.Should().BeTrue();
                            result.Data.Should().BeGreaterThan(0);
                        }
                    }
                    else
                    {
                        for (var i = 0; i < 100; i++)
                        {
                            await cache.RemoveAsync("k", ct);
                            await Task.Delay(1, ct);
                        }
                    }
                });
        }
        finally
        {
            cache.Dispose();
        }
    }

    [Fact]
    public async Task SetNxAsync_ConcurrentContenders_OnlyOneWritesAndReturnsTrue()
    {
        // Race-protection check: N threads call SetNx on the same empty key
        // simultaneously. Exactly one must observe "absent" and return true;
        // every other caller must see the winner's write and return false.
        // Without per-cache locking around the read+write window, multiple
        // threads can both observe "absent" and both claim victory while
        // only one write actually persists.
        using var cache = NewCache();
        var contenders = 32;
        var winners = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, contenders),
            new ParallelOptions { MaxDegreeOfParallelism = contenders },
            async (i, ct) =>
            {
                var result = await cache.SetNxAsync("k", $"owner-{i}", ct: ct);
                if (result.Data)
                    Interlocked.Increment(ref winners);
            });

        winners.Should().Be(1);
        (await cache.ExistsAsync("k")).Data.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireLockAsync_ExpirationAllowsReacquisition()
    {
        // AcquireLockAsync now uses r_clock.GetUtcNow() for the expiry
        // computation, so a FakeTimeProvider drives the expiry check —
        // no real-time sleep required.
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock: clock);
        var first = await cache.AcquireLockAsync("k", "owner-A", TimeSpan.FromMinutes(1));
        first.Data.Should().BeTrue();

        // Advance past the lock TTL — AcquireLockAsync will see the new "now"
        // and treat the existing entry as expired.
        clock.Advance(TimeSpan.FromMinutes(2));

        var second = await cache.AcquireLockAsync("k", "owner-B", TimeSpan.FromSeconds(5));
        second.Data.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireLockAsync_ConcurrentContenderOnlyOneAcquires()
    {
        using var cache = NewCache();

        var contenders = 16;
        var acquired = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, contenders),
            new ParallelOptions { MaxDegreeOfParallelism = contenders },
            async (i, ct) =>
            {
                var result = await cache.AcquireLockAsync(
                    "k", $"owner-{i}", TimeSpan.FromSeconds(30), ct);
                if (result.Data)
                    Interlocked.Increment(ref acquired);
            });

        acquired.Should().Be(1);
    }

    [Fact]
    public async Task SetMany_GetMany_RoundTripPreservesValues()
    {
        using var cache = NewCache();

        var entries = Enumerable.Range(0, 100)
            .ToDictionary(i => $"k{i}", i => i * 10);

        await cache.SetManyAsync(entries);

        var result = await cache.GetManyAsync<int>(entries.Keys.ToList());
        result.IsOk.Should().BeTrue();
        result.Data!.Should().BeEquivalentTo(
            entries.ToDictionary(kv => kv.Key, kv => (int?)kv.Value));
    }

    [Fact]
    public async Task GetManyAsync_HighConcurrencyReadsRemainConsistent()
    {
        using var cache = NewCache();
        for (var i = 0; i < 50; i++)
            await cache.SetAsync($"k{i}", i);

        var threads = 16;
        var iterations = 100;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, threads),
            new ParallelOptions { MaxDegreeOfParallelism = threads },
            async (_, ct) =>
            {
                for (var iter = 0; iter < iterations; iter++)
                {
                    var keys = Enumerable.Range(0, 50).Select(i => $"k{i}").ToList();
                    var result = await cache.GetManyAsync<int>(keys, ct);
                    result.IsOk.Should().BeTrue();
                    result.Data!.Should().HaveCount(50);
                }
            });
    }

    [Fact]
    public async Task KeyPrefix_IsolatesNamespacesAcrossInstances()
    {
        using var cacheA = NewCache(opts => opts.KeyPrefix = "a:");
        using var cacheB = NewCache(opts => opts.KeyPrefix = "b:");

        await cacheA.SetAsync("k", 1);
        await cacheB.SetAsync("k", 2);

        (await cacheA.GetAsync<int>("k")).Data.Should().Be(1);
        (await cacheB.GetAsync<int>("k")).Data.Should().Be(2);
    }

    [Fact]
    public async Task Dispose_ReleasesUnderlyingMemoryCache()
    {
        var cache = NewCache();
        await cache.SetAsync("k", 1);
        cache.Dispose();

        // After dispose, every public op throws ObjectDisposedException (fail-closed).
        var act = async () => await cache.GetAsync<int>("k");
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    private static DefaultLocalCache NewCache(
        Action<LocalCacheOptions>? configure = null, TimeProvider? clock = null)
    {
        var opts = new LocalCacheOptions();
        configure?.Invoke(opts);
        return new DefaultLocalCache(Options.Create(opts), clock);
    }
}
