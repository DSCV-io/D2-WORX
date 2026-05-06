// -----------------------------------------------------------------------
// <copyright file="DefaultLocalCache.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Caching.Local.Default;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using D2.Shared.Caching;
using D2.Shared.I18n;
using D2.Shared.Result;
using D2.Shared.Utilities.Extensions;
using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

/// <summary>
/// In-process implementation of <see cref="ILocalCache"/>. Values and
/// counters live in <see cref="IMemoryCache"/>; locks live in a dedicated
/// <see cref="ConcurrentDictionary{TKey, TValue}"/>; expirations are
/// mirrored in a parallel dictionary so <see cref="GetTtlAsync"/> can
/// report remaining time. All ops use direct dispatch — no
/// <c>BaseHandler</c> wrapping — because the work itself is tens of
/// nanoseconds and a handler pipeline would be 100× overhead.
/// </summary>
/// <remarks>
/// <para>
/// Atomic primitives have process-local scope. <see cref="IncrementAsync"/>
/// guarantees atomicity within this process via a <c>lock</c> on the
/// IMemoryCache instance for the read-modify-write window; cluster-wide
/// coordination requires an <see cref="IDistributedCache"/>.
/// </para>
/// <para>
/// <see cref="IncrementAsync"/> matches Redis WRONGTYPE semantics — the
/// counter shares the value cache namespace; if a key holds a
/// non-<see cref="long"/> value, increment returns <c>Conflict</c>.
/// </para>
/// <para>
/// Input validation (null/empty key, null collections, etc.) returns
/// <see cref="D2Result.ValidationFailed"/> with an <see cref="InputError"/>
/// for the offending parameter — never throws. Constructor still throws
/// because that's a DI / startup failure, not a per-call data flow.
/// </para>
/// </remarks>
[MustDisposeResource(false)]
public sealed class DefaultLocalCache : ILocalCache, IDisposable
{
    /// <summary>
    /// Backing IMemoryCache for value storage. Doubles as the monitor for
    /// the cache-state lock taken by every write op (SetCore + Remove
    /// variants) and by GetTtlAsync's read pair.
    /// </summary>
    [SuppressMessage(
        "ReSharper",
        "InconsistentlySynchronizedField",
        Justification = "Lock-free reads on IMemoryCache are safe (CD-backed); "
            + "writes synchronize against r_expirations.")]
    private readonly IMemoryCache r_cache;

    private readonly LocalCacheOptions r_options;

    /// <summary>Locks indexed by prefixed key. Lifecycle is bounded by lock TTL.</summary>
    private readonly ConcurrentDictionary<string, LockEntry> r_locks = new(StringComparer.Ordinal);

    /// <summary>
    /// Tracks absolute expiration per key so <see cref="GetTtlAsync"/> can
    /// report remaining time. Cheap parallel structure; pruned on remove
    /// and on eviction callbacks.
    /// </summary>
    [SuppressMessage(
        "ReSharper",
        "InconsistentlySynchronizedField",
        Justification = "Eviction callback writes intentionally outside the lock; "
            + "filtered to non-caller-driven reasons so it can't race concurrent SetCore.")]
    private readonly ConcurrentDictionary<string, DateTimeOffset> r_expirations
        = new(StringComparer.Ordinal);

    /// <summary>Initializes a new <see cref="DefaultLocalCache"/>.</summary>
    /// <param name="options">Cache options (max entries, default TTL, key prefix).</param>
    [MustDisposeResource(false)]
    public DefaultLocalCache(IOptions<LocalCacheOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        r_options = options.Value;

        r_cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = r_options.MaxEntries,
            CompactionPercentage = 0.05,
        });
    }

    /// <inheritdoc />
    public ValueTask<D2Result<T?>> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (key.Falsey())
            return new(InputFailures.Required<T?>(nameof(key)));

        var prefixed = Prefixed(key);

        // ReSharper disable once InconsistentlySynchronizedField — see field
        // doc; reads are intentionally lock-free.
        if (r_cache.TryGetValue(prefixed, out var raw))
        {
            LocalCacheTelemetry.SR_Hits.Add(1);
            return new(D2Result<T?>.Ok((T?)raw));
        }

        LocalCacheTelemetry.SR_Misses.Add(1);
        return new(D2Result<T?>.NotFound());
    }

    /// <inheritdoc />
    public ValueTask<D2Result<IReadOnlyDictionary<string, T?>>> GetManyAsync<T>(
        IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        if (keys.Falsey())
            return new(InputFailures.Required<IReadOnlyDictionary<string, T?>>(nameof(keys)));

        var hits = new Dictionary<string, T?>(keys.Count, StringComparer.Ordinal);
        var hitCount = 0;
        foreach (var key in keys)
        {
            var prefixed = Prefixed(key);

            // ReSharper disable once InconsistentlySynchronizedField — see field
            // doc; reads are intentionally lock-free.
            if (r_cache.TryGetValue(prefixed, out var raw))
            {
                hits[key] = (T?)raw;
                hitCount++;
            }
        }

        LocalCacheTelemetry.SR_Hits.Add(hitCount);
        LocalCacheTelemetry.SR_Misses.Add(keys.Count - hitCount);

        if (hitCount == 0)
            return new(D2Result<IReadOnlyDictionary<string, T?>>.NotFound());

        if (hitCount == keys.Count)
            return new(D2Result<IReadOnlyDictionary<string, T?>>.Ok(hits));

        return new(D2Result<IReadOnlyDictionary<string, T?>>.SomeFound(hits));
    }

    /// <inheritdoc />
    public ValueTask<D2Result<bool>> ExistsAsync(string key, CancellationToken ct = default)
    {
        if (key.Falsey())
            return new(InputFailures.Required<bool>(nameof(key)));

        // ReSharper disable once InconsistentlySynchronizedField — see field
        // doc; reads are intentionally lock-free.
        return new(D2Result<bool>.Ok(r_cache.TryGetValue(Prefixed(key), out _)));
    }

    /// <inheritdoc />
    public ValueTask<D2Result<TimeSpan?>> GetTtlAsync(string key, CancellationToken ct = default)
    {
        if (key.Falsey())
            return new(InputFailures.Required<TimeSpan?>(nameof(key)));

        var prefixed = Prefixed(key);

        // IMemoryCache doesn't expose absolute expiration; we mirror it in
        // r_expirations on every Set. Lock the read pair against SetCore so
        // we can't observe a half-applied write.
        lock (r_cache)
        {
            if (!r_cache.TryGetValue(prefixed, out _))
                return new(D2Result<TimeSpan?>.NotFound());

            if (r_expirations.TryGetValue(prefixed, out var expiresAt))
            {
                var remaining = expiresAt - DateTimeOffset.UtcNow;
                return new(D2Result<TimeSpan?>.Ok(remaining > TimeSpan.Zero ? remaining : null));
            }

            // Key present but no expiration was tracked → no TTL set.
            return new(D2Result<TimeSpan?>.Ok());
        }
    }

    /// <inheritdoc />
    public ValueTask<D2Result> SetAsync<T>(
        string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        if (key.Falsey())
            return new(InputFailures.Required(nameof(key)));

        SetCore(Prefixed(key), value, expiration);
        LocalCacheTelemetry.SR_Sets.Add(1);
        return new(D2Result.Ok());
    }

    /// <inheritdoc />
    public ValueTask<D2Result> SetManyAsync<T>(
        IReadOnlyDictionary<string, T> entries,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        if (entries.Falsey())
            return new(InputFailures.Required(nameof(entries)));

        foreach (var (key, value) in entries)
            SetCore(Prefixed(key), value, expiration);
        LocalCacheTelemetry.SR_Sets.Add(entries.Count);
        return new(D2Result.Ok());
    }

    /// <inheritdoc />
    public ValueTask<D2Result> RemoveAsync(string key, CancellationToken ct = default)
    {
        if (key.Falsey())
            return new(InputFailures.Required(nameof(key)));

        var prefixed = Prefixed(key);

        // Lock the cache+r_expirations pair against SetCore.
        lock (r_cache)
        {
            r_cache.Remove(prefixed);
            r_expirations.TryRemove(prefixed, out _);
        }

        LocalCacheTelemetry.SR_Removes.Add(1);
        return new(D2Result.Ok());
    }

    /// <inheritdoc />
    public ValueTask<D2Result> RemoveManyAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        if (keys.Falsey())
            return new(InputFailures.Required(nameof(keys)));

        foreach (var key in keys)
        {
            var prefixed = Prefixed(key);
            lock (r_cache)
            {
                r_cache.Remove(prefixed);
                r_expirations.TryRemove(prefixed, out _);
            }
        }

        LocalCacheTelemetry.SR_Removes.Add(keys.Count);
        return new(D2Result.Ok());
    }

    /// <inheritdoc />
    public ValueTask<D2Result<bool>> SetNxAsync<T>(
        string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        if (key.Falsey())
            return new(InputFailures.Required<bool>(nameof(key)));

        var prefixed = Prefixed(key);

        // Atomicity via the same per-cache lock IncrementAsync uses.
        lock (r_cache)
        {
            // false = key already existed; no write
            if (r_cache.TryGetValue(prefixed, out _))
                return new(D2Result<bool>.Ok());

            SetCore(prefixed, value, expiration);
            LocalCacheTelemetry.SR_Sets.Add(1);
            return new(D2Result<bool>.Ok(true));
        }
    }

    /// <inheritdoc />
    public ValueTask<D2Result<long>> IncrementAsync(
        string key, long amount = 1, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        if (key.Falsey())
            return new(InputFailures.Required<long>(nameof(key)));

        var prefixed = Prefixed(key);

        // Atomicity via a small per-key critical section.
        lock (r_cache)
        {
            if (r_cache.TryGetValue(prefixed, out var raw))
            {
                if (raw is not long current)
                    return new(D2Result<long>.Conflict([TK.Common.Errors.CONFLICT]));

                var next = current + amount;
                SetCore(prefixed, next, expiration: null);  // preserve existing TTL
                return new(D2Result<long>.Ok(next));
            }

            SetCore(prefixed, amount, expiration);
            LocalCacheTelemetry.SR_Sets.Add(1);
            return new(D2Result<long>.Ok(amount));
        }
    }

    /// <inheritdoc />
    public ValueTask<D2Result<bool>> AcquireLockAsync(
        string key, string lockId, TimeSpan expiration, CancellationToken ct = default)
    {
        if (key.Falsey())
            return new(InputFailures.Required<bool>(nameof(key)));
        if (lockId.Falsey())
            return new(InputFailures.Required<bool>(nameof(lockId)));

        var prefixed = Prefixed(key);
        var now = DateTimeOffset.UtcNow;
        var newEntry = new LockEntry(lockId, now + expiration);

        var stored = r_locks.AddOrUpdate(
            prefixed,
            _ => newEntry,
            (_, existing) => existing.ExpiresAt <= now ? newEntry : existing);

        var acquired = ReferenceEquals(stored, newEntry);
        return new(D2Result<bool>.Ok(acquired));
    }

    /// <inheritdoc />
    public ValueTask<D2Result> ReleaseLockAsync(
        string key, string lockId, CancellationToken ct = default)
    {
        if (key.Falsey())
            return new(InputFailures.Required(nameof(key)));
        if (lockId.Falsey())
            return new(InputFailures.Required(nameof(lockId)));

        var prefixed = Prefixed(key);

        if (r_locks.TryGetValue(prefixed, out var existing) && existing.LockId == lockId)
            r_locks.TryRemove(KeyValuePair.Create(prefixed, existing));

        return new(D2Result.Ok());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        r_cache.Dispose();
        r_locks.Clear();
        r_expirations.Clear();
    }

    private static void EvictionCallback(
        object key,
        object? value,
        EvictionReason reason,
        object? state)
    {
        if (reason is EvictionReason.Capacity or EvictionReason.Expired)
            LocalCacheTelemetry.SR_Evictions.Add(1);

        // Only clean r_expirations when the entry actually left the cache
        // for an external reason. Replaced and Removed are handled by the
        // initiating call (SetCore writes the new TTL; RemoveAsync does its
        // own r_expirations cleanup).
        if (reason is not (EvictionReason.Capacity
            or EvictionReason.Expired
            or EvictionReason.TokenExpired))
            return;

        if (state is ConcurrentDictionary<string, DateTimeOffset> expirations
            && key is string keyString)
            expirations.TryRemove(keyString, out _);
    }

    private string Prefixed(string key)
        => r_options.KeyPrefix.Falsey() ? key : r_options.KeyPrefix + key;

    private void SetCore<T>(string prefixedKey, T value, TimeSpan? expiration)
    {
        var ttl = expiration ?? r_options.DefaultExpiration;
        var expiresAt = ttl > TimeSpan.Zero ? DateTimeOffset.UtcNow + ttl : (DateTimeOffset?)null;

        // Lock the cache + r_expirations write pair so concurrent SetAsync /
        // SetManyAsync / SetNxAsync calls can't interleave their cache writes
        // and TTL writes.
        lock (r_cache)
        {
            using (var entry = r_cache.CreateEntry(prefixedKey))
            {
                entry.Value = value;

                // Every entry counts as 1 against MaxEntries — fixes the
                // IMemoryCache SizeLimit footgun.
                entry.Size = 1;
                if (expiresAt is not null)
                    entry.AbsoluteExpiration = (DateTimeOffset)expiresAt;
                entry.RegisterPostEvictionCallback(EvictionCallback, r_expirations);
            }

            if (expiresAt is not null)
                r_expirations[prefixedKey] = (DateTimeOffset)expiresAt;
            else
                r_expirations.TryRemove(prefixedKey, out _);
        }

        // Defensive sweep: if the entry got capacity-evicted during SetCore,
        // drop the r_expirations entry so it doesn't leak. Cache-side check
        // is cheap.
        // ReSharper disable InconsistentlySynchronizedField — defensive sweep
        // intentionally outside the write lock.
        if (!r_cache.TryGetValue(prefixedKey, out _))
            r_expirations.TryRemove(prefixedKey, out _);

        // ReSharper restore InconsistentlySynchronizedField
    }
}
