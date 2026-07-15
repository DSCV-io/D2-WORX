// -----------------------------------------------------------------------
// <copyright file="DefaultLocalCache.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Local.Default;

using System.Collections.Concurrent;
using System.Threading;
using DcsvIo.D2.Caching;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
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
    /// <summary>Backing IMemoryCache for value storage.</summary>
    private readonly IMemoryCache r_cache;

    /// <summary>
    /// Dedicated monitor for the cache-state write lock. Locking on a
    /// dedicated private object (instead of on <see cref="r_cache"/>)
    /// avoids the CA2002-spirit anti-pattern of locking on a public-typed
    /// instance whose own internal locks could in principle deadlock with
    /// future BCL changes. Lock-free reads on <see cref="r_cache"/> are
    /// safe (CD-backed by <see cref="MemoryCache"/>'s implementation);
    /// writes synchronize against <see cref="r_expirations"/> via this lock.
    /// </summary>
    private readonly object r_writeLock = new();

    private readonly LocalCacheOptions r_options;

    /// <summary>
    /// The clock used for TTL accounting — matched to the IMemoryCache's
    /// own clock so both see the same "now" (critical for test determinism).
    /// </summary>
    private readonly TimeProvider r_clock;

    /// <summary>Locks indexed by prefixed key. Lifecycle is bounded by lock TTL.</summary>
    private readonly ConcurrentDictionary<string, LockEntry> r_locks = new(StringComparer.Ordinal);

    /// <summary>
    /// Tracks absolute expiration per key so <see cref="GetTtlAsync"/> can
    /// report remaining time AND <see cref="IncrementAsync"/> can preserve
    /// existing TTL across read-modify-write. Cheap parallel structure;
    /// pruned on remove and on eviction callbacks.
    /// </summary>
    /// <remarks>
    /// The eviction callback writes outside <see cref="r_writeLock"/> by
    /// design — see <see cref="EvictionCallback"/>. The narrow window where
    /// a concurrent capacity-eviction-then-re-Set could leave a stale
    /// <c>TryRemove</c> firing after the new TTL is recorded is accepted:
    /// <see cref="GetTtlAsync"/> may transiently report no-TTL for a TTL'd
    /// key in the immediate aftermath of a Capacity eviction. Reads of the
    /// cached value remain correct; the only observable degradation is the
    /// TTL report. Workloads needing strict-LRU-with-coherent-TTL semantics
    /// should compose a sibling impl rather than tighten the lock here
    /// (which would block reads on every eviction callback).
    /// </remarks>
    private readonly ConcurrentDictionary<string, DateTimeOffset> r_expirations
        = new(StringComparer.Ordinal);

    /// <summary>
    /// 0 = live; 1 = disposed. Set before structure teardown so concurrent
    /// ops fail closed rather than re-populating cleared maps.
    /// </summary>
    private int _disposed;

    /// <summary>Initializes a new <see cref="DefaultLocalCache"/>.</summary>
    /// <param name="options">Cache options (max entries, default TTL, key prefix).</param>
    /// <param name="clock">
    /// Optional time provider. Defaults to <see cref="TimeProvider.System"/>.
    /// Supplying a fake time provider makes TTL expiry deterministic in tests —
    /// both this class's TTL accounting and <see cref="IMemoryCache"/>'s
    /// internal expiry scanner advance together, so tests drive clock-based
    /// expiry without real-time sleeps.
    /// </param>
    [MustDisposeResource(false)]
    public DefaultLocalCache(IOptions<LocalCacheOptions> options, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        r_options = options.Value;
        r_clock = clock ?? TimeProvider.System;

        r_cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = r_options.MaxEntries,
            CompactionPercentage = 0.05,
            Clock = new TimeProviderSystemClockAdapter(r_clock),
        });
    }

    /// <inheritdoc />
    public ValueTask<D2Result<T?>> GetAsync<T>(string key, CancellationToken ct = default)
    {
        ThrowIfDisposed();

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
        ThrowIfDisposed();

        if (keys.Falsey())
            return new(InputFailures.Required<IReadOnlyDictionary<string, T?>>(nameof(keys)));

        foreach (var key in keys)
        {
            if (key.Falsey())
                return new(InputFailures.Required<IReadOnlyDictionary<string, T?>>(nameof(keys)));
        }

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
        ThrowIfDisposed();

        if (key.Falsey())
            return new(InputFailures.Required<bool>(nameof(key)));

        // ReSharper disable once InconsistentlySynchronizedField — see field
        // doc; reads are intentionally lock-free.
        return new(D2Result<bool>.Ok(r_cache.TryGetValue(Prefixed(key), out _)));
    }

    /// <inheritdoc />
    public ValueTask<D2Result<TimeSpan?>> GetTtlAsync(string key, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (key.Falsey())
            return new(InputFailures.Required<TimeSpan?>(nameof(key)));

        var prefixed = Prefixed(key);

        // IMemoryCache doesn't expose absolute expiration; we mirror it in
        // r_expirations on every Set. Lock the read pair against SetCore so
        // we can't observe a half-applied write.
        lock (r_writeLock)
        {
            if (!r_cache.TryGetValue(prefixed, out _))
                return new(D2Result<TimeSpan?>.NotFound());

            if (r_expirations.TryGetValue(prefixed, out var expiresAt))
            {
                var remaining = expiresAt - r_clock.GetUtcNow();
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
        ThrowIfDisposed();

        if (key.Falsey())
            return new(InputFailures.Required(nameof(key)));

        if (IsNonPositive(expiration))
            return new(InputFailures.Invalid(nameof(expiration)));

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
        ThrowIfDisposed();

        if (entries.Falsey())
            return new(InputFailures.Required(nameof(entries)));

        if (IsNonPositive(expiration))
            return new(InputFailures.Invalid(nameof(expiration)));

        foreach (var key in entries.Keys)
            if (key.Falsey()) return new(InputFailures.Required(nameof(entries)));

        foreach (var (key, value) in entries)
            SetCore(Prefixed(key), value, expiration);
        LocalCacheTelemetry.SR_Sets.Add(entries.Count);
        return new(D2Result.Ok());
    }

    /// <inheritdoc />
    public ValueTask<D2Result> RemoveAsync(string key, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (key.Falsey())
            return new(InputFailures.Required(nameof(key)));

        var prefixed = Prefixed(key);

        // Lock the cache+r_expirations pair against SetCore.
        lock (r_writeLock)
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
        ThrowIfDisposed();

        if (keys.Falsey())
            return new(InputFailures.Required(nameof(keys)));

        foreach (var key in keys)
            if (key.Falsey()) return new(InputFailures.Required(nameof(keys)));

        foreach (var key in keys)
        {
            var prefixed = Prefixed(key);
            lock (r_writeLock)
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
        ThrowIfDisposed();

        if (key.Falsey())
            return new(InputFailures.Required<bool>(nameof(key)));

        if (IsNonPositive(expiration))
            return new(InputFailures.Invalid<bool>(nameof(expiration)));

        var prefixed = Prefixed(key);

        // Atomicity via the same per-cache lock IncrementAsync uses.
        lock (r_writeLock)
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
        ThrowIfDisposed();

        if (key.Falsey())
            return new(InputFailures.Required<long>(nameof(key)));

        if (IsNonPositive(expiration))
            return new(InputFailures.Invalid<long>(nameof(expiration)));

        var prefixed = Prefixed(key);

        // Atomicity via a small per-key critical section.
        lock (r_writeLock)
        {
            if (r_cache.TryGetValue(prefixed, out var raw))
            {
                if (raw is not long current)
                    return new(D2Result<long>.Conflict([TK.Common.Errors.CONFLICT]));

                var next = current + amount;

                // Preserve existing TTL — Redis-parity. Read absolute
                // expiration from r_expirations under the same lock and
                // pass it through verbatim. Absent r_expirations entry =
                // existing entry has no TTL set; new entry stays no-TTL.
                // Ignores the per-call `expiration` arg on the increment
                // path (parity with Redis INCR-with-TTL: TTL set only on
                // first call, not subsequent increments).
                var existingAbsolute = r_expirations.TryGetValue(prefixed, out var absolute)
                    ? absolute
                    : (DateTimeOffset?)null;
                SetCoreAbsolute(prefixed, next, existingAbsolute);
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
        ThrowIfDisposed();

        if (key.Falsey())
            return new(InputFailures.Required<bool>(nameof(key)));

        if (lockId.Falsey())
            return new(InputFailures.Required<bool>(nameof(lockId)));

        if (expiration <= TimeSpan.Zero)
            return new(InputFailures.Invalid<bool>(nameof(expiration)));

        var prefixed = Prefixed(key);
        var now = r_clock.GetUtcNow();
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
        ThrowIfDisposed();

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
        // Flag first: concurrent public ops must see disposed and throw rather
        // than re-populate r_locks / r_expirations after Clear.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

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

    private static bool IsNonPositive(TimeSpan? expiration)
        => expiration is { } e && e <= TimeSpan.Zero;

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

    private string Prefixed(string key)
        => r_options.KeyPrefix.Falsey() ? key : r_options.KeyPrefix + key;

    private void SetCore<T>(string prefixedKey, T value, TimeSpan? expiration)
    {
        var ttl = expiration ?? r_options.DefaultExpiration;
        var expiresAt = ttl > TimeSpan.Zero ? r_clock.GetUtcNow() + ttl : (DateTimeOffset?)null;
        SetCoreAbsolute(prefixedKey, value, expiresAt);
    }

    private void SetCoreAbsolute<T>(
        string prefixedKey, T value, DateTimeOffset? absoluteExpiration)
    {
        // Lock the cache + r_expirations write pair so concurrent SetAsync /
        // SetManyAsync / SetNxAsync / IncrementAsync calls can't interleave
        // their cache writes and TTL writes.
        lock (r_writeLock)
        {
            using (var entry = r_cache.CreateEntry(prefixedKey))
            {
                entry.Value = value;

                // Every entry counts as 1 against MaxEntries — fixes the
                // IMemoryCache SizeLimit footgun.
                entry.Size = 1;
                if (absoluteExpiration is { } absolute)
                    entry.AbsoluteExpiration = absolute;
                entry.RegisterPostEvictionCallback(EvictionCallback, r_expirations);
            }

            if (absoluteExpiration is { } a)
                r_expirations[prefixedKey] = a;
            else
                r_expirations.TryRemove(prefixedKey, out _);
        }

        // Defensive sweep: if the entry got capacity-evicted during the write,
        // drop the r_expirations entry so it doesn't leak. Cache-side check is
        // cheap; intentionally outside the write lock (single read, no atomicity
        // requirement against subsequent writes).
        // ReSharper disable InconsistentlySynchronizedField
        if (!r_cache.TryGetValue(prefixedKey, out _))
            r_expirations.TryRemove(prefixedKey, out _);

        // ReSharper restore InconsistentlySynchronizedField
    }

    /// <summary>
    /// Adapts a <see cref="TimeProvider"/> to the <see cref="ISystemClock"/>
    /// interface consumed by <see cref="MemoryCacheOptions.Clock"/>. When a
    /// fake time provider is supplied, <see cref="IMemoryCache"/> TTL expiry
    /// advances with the fake clock rather than wall time — entry expiry
    /// is then fully deterministic.
    /// </summary>
    private sealed class TimeProviderSystemClockAdapter : ISystemClock
    {
        private readonly TimeProvider r_clock;

        internal TimeProviderSystemClockAdapter(TimeProvider clock)
        {
            r_clock = clock;
        }

        /// <inheritdoc/>
        public DateTimeOffset UtcNow => r_clock.GetUtcNow();
    }
}
