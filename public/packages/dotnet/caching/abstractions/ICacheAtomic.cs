// -----------------------------------------------------------------------
// <copyright file="ICacheAtomic.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching;

using DcsvIo.D2.Result;

/// <summary>
/// Atomic primitives — set-if-absent, increment, lock acquire/release.
/// Implemented by every cache flavor (<see cref="ILocalCache"/>,
/// <see cref="IDistributedCache"/>, <see cref="ITieredCache"/>); the
/// atomicity scope depends on the implementing cache.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope per implementation:</b>
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="ILocalCache"/>: per-process. Atomicity guaranteed within
/// this instance only.
/// </item>
/// <item>
/// <see cref="IDistributedCache"/>: cluster-wide. Atomicity enforced by
/// the remote store (e.g. Redis SETNX / INCR).
/// </item>
/// <item>
/// <see cref="ITieredCache"/>: cluster-wide. Routes through L2 (the
/// source of truth); L1 is invalidated or refreshed as a side effect.
/// </item>
/// </list>
/// <para>
/// Callers pick the scope by injecting the appropriate marker interface.
/// The same atomic op called via <c>ILocalCache</c> coordinates within one
/// process; via <c>IDistributedCache</c> or <c>ITieredCache</c> it
/// coordinates across the cluster.
/// </para>
/// </remarks>
public interface ICacheAtomic
{
    /// <summary>
    /// Sets a value only if the key is not already present (atomic).
    /// </summary>
    /// <typeparam name="T">Stored value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to store.</param>
    /// <param name="expiration">Optional TTL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(true)</c> if the value was written. <c>Ok(false)</c> if the key
    /// already existed (no write). Failure on backing-store error.
    /// </returns>
    ValueTask<D2Result<bool>> SetNxAsync<T>(
        string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);

    /// <summary>
    /// Atomically increments a numeric counter and returns the new value.
    /// Creates the key if absent (initial value = 0 + amount). The optional
    /// expiration is applied only when the key is created — existing keys
    /// keep their TTL.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="amount">Amount to add (may be negative).</param>
    /// <param name="expiration">TTL for newly-created keys.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(newValue)</c> on success. <c>Conflict</c> if the existing key
    /// holds a non-numeric value (Redis WRONGTYPE parity). Failure on
    /// backing-store error.
    /// </returns>
    ValueTask<D2Result<long>> IncrementAsync(
        string key, long amount = 1, TimeSpan? expiration = null, CancellationToken ct = default);

    /// <summary>
    /// Attempts to acquire a lock on the given key. The caller-supplied
    /// <paramref name="lockId"/> identifies the holder and is required for
    /// release. Locks expire automatically after <paramref name="expiration"/>
    /// to prevent indefinite hold by a crashed process.
    /// </summary>
    /// <param name="key">Lock key.</param>
    /// <param name="lockId">Caller-supplied identifier; needed for release.</param>
    /// <param name="expiration">Lock TTL — auto-released after this elapses.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(true)</c> on acquisition. <c>Ok(false)</c> if the lock is held
    /// by someone else. Failure on backing-store error.
    /// </returns>
    ValueTask<D2Result<bool>> AcquireLockAsync(
        string key, string lockId, TimeSpan expiration, CancellationToken ct = default);

    /// <summary>
    /// Releases a lock previously acquired with <see cref="AcquireLockAsync"/>.
    /// Releasing a lock you don't hold (or one that's already expired) is
    /// a no-op rather than an error — release is idempotent.
    /// </summary>
    /// <param name="key">Lock key.</param>
    /// <param name="lockId">The same identifier used at acquire time.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>Ok</c>; failure on backing-store error.</returns>
    ValueTask<D2Result> ReleaseLockAsync(
        string key, string lockId, CancellationToken ct = default);
}
