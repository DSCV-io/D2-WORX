// -----------------------------------------------------------------------
// <copyright file="ICacheInvalidationBackplane.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching;

using DcsvIo.D2.Result;

/// <summary>
/// Pub/sub backplane for cross-instance cache invalidation. Publishers fire
/// when a caller uses one of the <c>*AndBroadcast*</c> variants on
/// <see cref="ITieredCache"/> or <see cref="IDistributedCache"/>, or when
/// any code calls <see cref="PublishInvalidationAsync"/> directly.
/// Subscribers (typically tiered caches in other instances) receive the
/// key and act on it — usually by dropping the named entry from their L1.
/// </summary>
/// <remarks>
/// <para>
/// <b>Universal "everyone acts" rule:</b> every subscriber receives every
/// message, INCLUDING messages this instance itself published. There is no
/// sender-ID filter inside the implementation. The cost of the publisher
/// receiving its own message is bounded — for tiered cache after a Set,
/// it forces an L2 re-read on next access; for every other case, it's a
/// no-op or the desired behavior (e.g. session revocation invalidating
/// the originating instance's L1 entry too).
/// </para>
/// <para>
/// <b>Subscribe semantics:</b> <see cref="Subscribe"/> returns an
/// <see cref="IAsyncDisposable"/> tied to the subscriber's lifetime —
/// dispose to unsubscribe. Each call returns its own subscription;
/// multiple subscribers are independent and isolated (one handler
/// throwing does not break delivery to others).
/// </para>
/// <para>
/// <b>Delivery semantics:</b> at-most-once. Redis pub/sub does not
/// persist or redeliver messages. If a subscriber crashes mid-handler,
/// the message is lost — acceptable for cache invalidation since the
/// next read on that instance will hit the canonical L2 anyway.
/// </para>
/// <para>
/// <b>Provider-agnostic abstraction</b> — Redis pub/sub is the default
/// implementation, but the same interface can wrap Postgres LISTEN/NOTIFY,
/// an in-process channel for tests, etc.
/// </para>
/// </remarks>
public interface ICacheInvalidationBackplane : IAsyncDisposable
{
    /// <summary>
    /// Subscribes to receive invalidation messages. The handler is invoked
    /// once per received key (including keys this instance published).
    /// Implementations isolate errors per handler — one handler throwing
    /// does not affect delivery to other handlers.
    /// </summary>
    /// <param name="handler">
    /// Async callback invoked for every received invalidation key.
    /// Receives the key and a cancellation token tied to subscription
    /// lifetime (signaled when the returned subscription is disposed).
    /// </param>
    /// <returns>
    /// Disposable subscription. Dispose to unsubscribe; typically held as
    /// a field on the subscriber and disposed in the subscriber's own
    /// disposal so the subscription lifetime matches the subscriber.
    /// </returns>
    IAsyncDisposable Subscribe(Func<string, CancellationToken, ValueTask> handler);

    /// <summary>
    /// Publishes an invalidation message for a single key. Every subscriber
    /// (across every connected instance, including this one) receives the
    /// key.
    /// </summary>
    /// <param name="key">The key to invalidate cluster-wide.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>Ok</c>; failure on backplane error.</returns>
    ValueTask<D2Result> PublishInvalidationAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Bulk-publish counterpart of <see cref="PublishInvalidationAsync"/>.
    /// Implementations may pipeline the publishes for fewer round-trips.
    /// </summary>
    /// <param name="keys">Keys to invalidate cluster-wide.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>Ok</c>; failure on backplane error.</returns>
    ValueTask<D2Result> PublishInvalidationManyAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct = default);
}
