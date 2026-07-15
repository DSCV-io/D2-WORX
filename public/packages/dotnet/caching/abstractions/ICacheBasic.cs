// -----------------------------------------------------------------------
// <copyright file="ICacheBasic.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching;

using DcsvIo.D2.Result;

/// <summary>
/// Core read + write surface common to every cache flavor (local,
/// distributed, tiered). Every method returns <see cref="D2Result{TData}"/>.
/// Cache misses surface as <c>NotFound</c> failures; partial bulk hits as
/// <c>SomeFound</c> failures (both carry their specific error codes —
/// callers discriminate via <c>IsNotFound</c> / <c>IsSomeFound</c>).
/// Generic infrastructure failures (couldn't reach the backing store,
/// serializer threw, etc.) surface as <c>ServiceUnavailable</c> /
/// <c>UnhandledException</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Behavior per implementation:</b>
/// </para>
/// <list type="bullet">
/// <item><see cref="ILocalCache"/>: in-process reads/writes against
/// <c>IMemoryCache</c>. Sub-microsecond per op.</item>
/// <item><see cref="IDistributedCache"/>: every read and write hits the
/// remote store. No L1 buffer. Predictable freshness, network-bound latency.</item>
/// <item><see cref="ITieredCache"/>: reads check L1 first / fall through
/// to L2 / populate L1. Writes go L2-first — L1 only writes if L2 succeeded
/// — so partial-write states are impossible and the result is binary
/// (success or the L2 failure bubbled up).</item>
/// </list>
/// </remarks>
public interface ICacheBasic
{
    /// <summary>
    /// Reads a single value by key.
    /// </summary>
    /// <typeparam name="T">Stored value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(value)</c> on hit, <c>NotFound</c> on miss, failure on
    /// backing-store error.
    /// </returns>
    ValueTask<D2Result<T?>> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Reads many keys in one round-trip.
    /// </summary>
    /// <typeparam name="T">Stored value type.</typeparam>
    /// <param name="keys">Keys to fetch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(dict)</c> when every key hit, <c>SomeFound(partialDict)</c>
    /// when some hit and some missed, <c>NotFound</c> when none hit, or a
    /// failure on backing-store error.
    /// </returns>
    ValueTask<D2Result<IReadOnlyDictionary<string, T?>>> GetManyAsync<T>(
        IReadOnlyCollection<string> keys, CancellationToken ct = default);

    /// <summary>
    /// Returns whether a key is currently present.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>Ok(true|false)</c>; failure on backing-store error.</returns>
    ValueTask<D2Result<bool>> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Returns the remaining TTL for a key.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>NotFound</c> if the key is absent. <c>Ok(null)</c> if the key
    /// exists with no expiration set. <c>Ok(span)</c> with the remaining
    /// time when expiration is set. Failure on backing-store error.
    /// </returns>
    ValueTask<D2Result<TimeSpan?>> GetTtlAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Writes (or overwrites) a value.
    /// </summary>
    /// <typeparam name="T">Stored value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to store.</param>
    /// <param name="expiration">Optional TTL; <c>null</c> means use the cache's default.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>Ok</c> on success; failure on backing-store error.</returns>
    ValueTask<D2Result> SetAsync<T>(
        string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);

    /// <summary>
    /// Writes many entries in one round-trip.
    /// </summary>
    /// <typeparam name="T">Stored value type.</typeparam>
    /// <param name="entries">Key/value pairs to store.</param>
    /// <param name="expiration">Optional TTL applied to every entry.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>Ok</c> on success; failure on backing-store error.</returns>
    ValueTask<D2Result> SetManyAsync<T>(
        IReadOnlyDictionary<string, T> entries,
        TimeSpan? expiration = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a key. Idempotent — succeeds whether the key existed or not.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>Ok</c>; failure on backing-store error.</returns>
    ValueTask<D2Result> RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Removes many keys. Idempotent.
    /// </summary>
    /// <param name="keys">Keys to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>Ok</c>; failure on backing-store error.</returns>
    ValueTask<D2Result> RemoveManyAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct = default);
}
