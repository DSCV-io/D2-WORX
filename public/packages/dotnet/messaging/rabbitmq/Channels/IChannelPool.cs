// -----------------------------------------------------------------------
// <copyright file="IChannelPool.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Channels;

using global::RabbitMQ.Client;
using JetBrains.Annotations;

/// <summary>
/// Bounded pool of <see cref="IChannel"/> instances reserved for the
/// publisher path. Acquire one via <see cref="AcquireAsync"/>, dispose the
/// returned lease to release back to the pool.
/// </summary>
internal interface IChannelPool : IAsyncDisposable
{
    /// <summary>
    /// Acquires a channel from the pool. Blocks (asynchronously) until one
    /// is available or <paramref name="ct"/> is canceled / the configured
    /// acquire timeout elapses.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A lease that owns the channel for its scope. Disposing the lease
    /// returns the channel to the pool (or discards it if it has faulted).
    /// </returns>
    [MustDisposeResource]
    ValueTask<ChannelLease> AcquireAsync(CancellationToken ct = default);
}
