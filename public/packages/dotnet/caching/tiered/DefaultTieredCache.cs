// -----------------------------------------------------------------------
// <copyright file="DefaultTieredCache.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Tiered;

using DcsvIo.D2.Result;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

/// <summary>
/// Composes one <see cref="ILocalCache"/> (L1) and one
/// <see cref="IDistributedCache"/> (L2) into a tiered cache. Reads check
/// L1 first / fall through to L2 / populate L1 on L2 hit. Writes go
/// L2-first — L1 only writes if L2 succeeded — so partial-write states
/// are impossible. Atomic primitives route through L2 (the cluster source
/// of truth) and invalidate L1 as a side effect. Optional
/// <see cref="ICacheInvalidationBackplane"/> wires up cluster-wide L1
/// invalidation: this instance subscribes at construction and drops L1
/// entries on every received invalidation (including its own, per the
/// universal "everyone acts" rule).
/// </summary>
[MustDisposeResource(false)]
public sealed class DefaultTieredCache : ITieredCache, IAsyncDisposable
{
    private readonly ILocalCache r_l1;
    private readonly IDistributedCache r_l2;
    private readonly ILogger<DefaultTieredCache> r_logger;
    private readonly ICacheInvalidationBackplane? r_backplane;
    private readonly IAsyncDisposable? r_subscription;
    private bool _disposed;

    /// <summary>Initializes a new <see cref="DefaultTieredCache"/>.</summary>
    /// <param name="l1">Local (in-process) cache instance.</param>
    /// <param name="l2">Distributed (cluster) cache instance.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="backplane">Optional invalidation backplane; if registered, this instance
    /// subscribes for cluster-wide L1 invalidation.</param>
    [MustDisposeResource(false)]
    public DefaultTieredCache(
        ILocalCache l1,
        IDistributedCache l2,
        ILogger<DefaultTieredCache> logger,
        ICacheInvalidationBackplane? backplane = null)
    {
        ArgumentNullException.ThrowIfNull(l1);
        ArgumentNullException.ThrowIfNull(l2);
        ArgumentNullException.ThrowIfNull(logger);
        r_l1 = l1;
        r_l2 = l2;
        r_logger = logger;
        r_backplane = backplane;

        if (backplane is not null)
        {
            // Capture into locals for the lambda closure to avoid an
            // implicit `this` capture (subscription outlives the immediate
            // ctor frame).
            var capturedLogger = logger;
            var capturedL1 = l1;
            r_subscription = backplane.Subscribe(async (key, ct) =>
            {
                var result = await capturedL1.RemoveAsync(key, ct);
                if (!result.IsOk)
                {
                    TieredCacheLog.L1InvalidationFailed(
                        capturedLogger, key, result.ErrorCode ?? "unknown");
                }
            });
        }
    }

    // ----- ICacheBasic -----

    /// <inheritdoc />
    public async ValueTask<D2Result<T?>> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var l1 = await r_l1.GetAsync<T>(key, ct);
        if (l1.IsOk) return l1;

        var l2 = await r_l2.GetAsync<T>(key, ct);
        if (!l2.IsOk) return l2;  // NotFound or backing-store failure; pass through.

        // Populate L1. We don't have L2's remaining TTL atomically here;
        // future optimization: GetWithTtl Lua script for one round-trip.
        // For now: populate L1 with the local default TTL (caller's L1
        // options govern). L2's actual TTL governs cluster freshness.
        await r_l1.SetAsync(key, l2.Data, ct: ct);
        return l2;
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<IReadOnlyDictionary<string, T?>>> GetManyAsync<T>(
        IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        var l1 = await r_l1.GetManyAsync<T>(keys, ct);

        // Identify the keys L1 missed.
        var l1Hits = l1.IsOk || l1.IsSomeFound
            ? l1.Data ?? new Dictionary<string, T?>()
            : new Dictionary<string, T?>();
        if (l1.IsOk) return l1;  // L1 hit them all; done.

        var missing = keys.Where(k => !l1Hits.ContainsKey(k)).ToArray();
        if (missing.Length == 0) return l1;  // shouldn't happen, but defensive.

        var l2 = await r_l2.GetManyAsync<T>(missing, ct);

        // Distinguish L2 NotFound (legitimate "nothing more to find") from
        // L2 failure (couldn't even ask). For failure, propagate it — the
        // caller has the right to know L2 was unreachable, even if L1
        // happened to have part of what they wanted.
        if (l2.IsNotFound)
        {
            return l1Hits.Count == 0
                ? D2Result<IReadOnlyDictionary<string, T?>>.NotFound()
                : D2Result<IReadOnlyDictionary<string, T?>>.SomeFound(l1Hits);
        }

        if (!l2.IsOk && !l2.IsSomeFound)
        {
            // L2 returned a failure (ServiceUnavailable, etc.). Propagate it.
            return l2;
        }

        // Merge L2 hits into the result + populate L1 with them.
        var l2Hits = l2.Data ?? new Dictionary<string, T?>();
        var merged = new Dictionary<string, T?>(l1Hits, StringComparer.Ordinal);
        var populate = new Dictionary<string, T?>(l2Hits.Count, StringComparer.Ordinal);
        foreach (var (k, v) in l2Hits)
        {
            merged[k] = v;
            populate[k] = v;
        }

        if (populate.Count > 0)
            await r_l1.SetManyAsync(populate, ct: ct);

        if (merged.Count == keys.Count)
            return D2Result<IReadOnlyDictionary<string, T?>>.Ok(merged);
        return D2Result<IReadOnlyDictionary<string, T?>>.SomeFound(merged);
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<bool>> ExistsAsync(string key, CancellationToken ct = default)
    {
        var l1 = await r_l1.ExistsAsync(key, ct);
        if (l1.IsOk && l1.Data) return l1;
        return await r_l2.ExistsAsync(key, ct);
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<TimeSpan?>> GetTtlAsync(
        string key, CancellationToken ct = default)
    {
        // L2 is the source of truth for TTL — local TTLs are an L1
        // implementation detail, not what the cluster sees.
        return await r_l2.GetTtlAsync(key, ct);
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> SetAsync<T>(
        string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        // L2 first — if L2 fails, don't touch L1 (L2-first ordering means
        // partial-write states are impossible).
        var l2 = await r_l2.SetAsync(key, value, expiration, ct);
        if (!l2.IsOk) return l2;

        // L1 is the optional layer (§18 graceful degradation): L2 has the
        // canonical write; an L1 failure here just means this instance's
        // L1 misses the warm-up. Log + return L2 success — never fail a
        // write the cluster successfully accepted just because the local
        // L1 sneeze.
        var l1 = await r_l1.SetAsync(key, value, expiration, ct);
        if (!l1.IsOk)
        {
            TieredCacheLog.L1WriteFailedAfterL2Success(
                r_logger, "SetAsync", key, l1.ErrorCode ?? "unknown");
        }

        return l2;
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> SetManyAsync<T>(
        IReadOnlyDictionary<string, T> entries,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        var l2 = await r_l2.SetManyAsync(entries, expiration, ct);
        if (!l2.IsOk) return l2;
        var l1 = await r_l1.SetManyAsync(entries, expiration, ct);
        if (!l1.IsOk)
        {
            TieredCacheLog.L1WriteFailedAfterL2Success(
                r_logger, "SetManyAsync", $"{entries.Count} entries", l1.ErrorCode ?? "unknown");
        }

        return l2;
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> RemoveAsync(string key, CancellationToken ct = default)
    {
        var l2 = await r_l2.RemoveAsync(key, ct);
        if (!l2.IsOk) return l2;
        var l1 = await r_l1.RemoveAsync(key, ct);
        if (!l1.IsOk)
        {
            TieredCacheLog.L1WriteFailedAfterL2Success(
                r_logger, "RemoveAsync", key, l1.ErrorCode ?? "unknown");
        }

        return l2;
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> RemoveManyAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        var l2 = await r_l2.RemoveManyAsync(keys, ct);
        if (!l2.IsOk) return l2;
        var l1 = await r_l1.RemoveManyAsync(keys, ct);
        if (!l1.IsOk)
        {
            TieredCacheLog.L1WriteFailedAfterL2Success(
                r_logger, "RemoveManyAsync", $"{keys.Count} keys", l1.ErrorCode ?? "unknown");
        }

        return l2;
    }

    // ----- ICacheAtomic — route through L2 (cluster source of truth) -----

    /// <inheritdoc />
    public async ValueTask<D2Result<bool>> SetNxAsync<T>(
        string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        var l2 = await r_l2.SetNxAsync(key, value, expiration, ct);
        if (!l2.IsOk) return l2;
        if (l2.Data)
        {
            // L2 took the write — populate L1 with the same value.
            await r_l1.SetAsync(key, value, expiration, ct);
        }
        else
        {
            // L2 already had it — drop our L1 in case it was stale; next
            // read will re-populate from L2's canonical value.
            await r_l1.RemoveAsync(key, ct);
        }

        return l2;
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<long>> IncrementAsync(
        string key, long amount = 1, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        var l2 = await r_l2.IncrementAsync(key, amount, expiration, ct);
        if (!l2.IsOk) return l2;

        // Counter values held in L1 would diverge from L2 immediately as
        // other instances increment. Safer: invalidate L1 so the next
        // read on this instance fetches the canonical value from L2.
        await r_l1.RemoveAsync(key, ct);
        return l2;
    }

    /// <inheritdoc />
    // Pure delegation; L1 not involved in lock state.
    public ValueTask<D2Result<bool>> AcquireLockAsync(
        string key, string lockId, TimeSpan expiration, CancellationToken ct = default)
        => r_l2.AcquireLockAsync(key, lockId, expiration, ct);

    /// <inheritdoc />
    // Pure delegation.
    public ValueTask<D2Result> ReleaseLockAsync(
        string key, string lockId, CancellationToken ct = default)
        => r_l2.ReleaseLockAsync(key, lockId, ct);

    // ----- ICacheBroadcast — broadcast variants delegate to L2 (which owns the backplane) -----

    /// <inheritdoc />
    public async ValueTask<D2Result> SetAndBroadcastAsync<T>(
        string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        var setResult = await SetAsync(key, value, expiration, ct);
        if (!setResult.IsOk) return setResult;
        return await PublishAsync(key, ct);
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> SetManyAndBroadcastAsync<T>(
        IReadOnlyDictionary<string, T> entries,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        var setResult = await SetManyAsync(entries, expiration, ct);
        if (!setResult.IsOk) return setResult;
        return await PublishManyAsync(entries.Keys.ToArray(), ct);
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> RemoveAndBroadcastAsync(
        string key, CancellationToken ct = default)
    {
        var removeResult = await RemoveAsync(key, ct);
        if (!removeResult.IsOk) return removeResult;
        return await PublishAsync(key, ct);
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> RemoveManyAndBroadcastAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        var removeResult = await RemoveManyAsync(keys, ct);
        if (!removeResult.IsOk) return removeResult;
        return await PublishManyAsync(keys, ct);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (r_subscription is not null)
            await r_subscription.DisposeAsync();
    }

    private async ValueTask<D2Result> PublishAsync(string key, CancellationToken ct)
    {
        if (r_backplane is null)
        {
            throw new InvalidOperationException(
                "ICacheInvalidationBackplane is not registered. Use SetAsync / "
                + "RemoveAsync (no broadcast), or register the backplane via "
                + "AddD2RedisCacheInvalidationBackplane.");
        }

        return await r_backplane.PublishInvalidationAsync(key, ct);
    }

    private async ValueTask<D2Result> PublishManyAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        if (r_backplane is null)
        {
            throw new InvalidOperationException(
                "ICacheInvalidationBackplane is not registered. Use SetManyAsync "
                + "/ RemoveManyAsync (no broadcast), or register the backplane "
                + "via AddD2RedisCacheInvalidationBackplane.");
        }

        return await r_backplane.PublishInvalidationManyAsync(keys, ct);
    }
}
