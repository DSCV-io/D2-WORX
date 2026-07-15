// -----------------------------------------------------------------------
// <copyright file="ICacheBroadcast.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching;

using DcsvIo.D2.Result;

/// <summary>
/// Write surface that publishes invalidation messages via a registered
/// <see cref="ICacheInvalidationBackplane"/> after the underlying write
/// completes. Subscribers (typically tiered caches in other instances)
/// drop their L1 copy of the affected key on receipt.
/// </summary>
/// <remarks>
/// Implemented by both <see cref="IDistributedCache"/> (so callers
/// writing directly to L2 can still bust other instances' L1) and
/// <see cref="ITieredCache"/> (which writes both tiers and broadcasts
/// in one call).
/// </remarks>
public interface ICacheBroadcast
{
    /// <summary>
    /// Writes a value AND publishes an invalidation message via the
    /// registered <see cref="ICacheInvalidationBackplane"/>.
    /// </summary>
    /// <typeparam name="T">Stored value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to store.</param>
    /// <param name="expiration">Optional TTL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>Ok</c> on success; failure on backing-store error.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no <see cref="ICacheInvalidationBackplane"/> was
    /// registered with the cache. Use the plain <c>SetAsync</c> if you
    /// don't intend to broadcast.
    /// </exception>
    ValueTask<D2Result> SetAndBroadcastAsync<T>(
        string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);

    /// <summary>
    /// Bulk-write counterpart of <see cref="SetAndBroadcastAsync{T}"/>.
    /// </summary>
    /// <typeparam name="T">Stored value type.</typeparam>
    /// <param name="entries">Key/value pairs to store.</param>
    /// <param name="expiration">Optional TTL applied to every entry.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>Ok</c> on success; failure on backing-store error.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no <see cref="ICacheInvalidationBackplane"/> was registered.
    /// </exception>
    ValueTask<D2Result> SetManyAndBroadcastAsync<T>(
        IReadOnlyDictionary<string, T> entries,
        TimeSpan? expiration = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a key AND publishes an invalidation message so other
    /// instances drop their L1 copies.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>Ok</c>; failure on backing-store error.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no <see cref="ICacheInvalidationBackplane"/> was registered.
    /// </exception>
    ValueTask<D2Result> RemoveAndBroadcastAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Bulk-remove + broadcast invalidation per key.
    /// </summary>
    /// <param name="keys">Keys to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>Ok</c>; failure on backing-store error.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no <see cref="ICacheInvalidationBackplane"/> was registered.
    /// </exception>
    ValueTask<D2Result> RemoveManyAndBroadcastAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct = default);
}
