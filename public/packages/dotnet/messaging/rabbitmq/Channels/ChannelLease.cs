// -----------------------------------------------------------------------
// <copyright file="ChannelLease.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Channels;

using global::RabbitMQ.Client;
using JetBrains.Annotations;

/// <summary>
/// A scoped lease over a pooled <see cref="IChannel"/>. Always consume via
/// <c>await using</c> so the channel is returned (or discarded if faulted)
/// when the scope exits.
/// </summary>
[MustDisposeResource]
public sealed class ChannelLease : IAsyncDisposable
{
    private readonly Func<IChannel, ValueTask> r_returnToPool;
    private bool _disposed;

    /// <summary>Initializes a new lease.</summary>
    /// <param name="channel">The leased channel.</param>
    /// <param name="returnToPool">Callback the lease invokes on dispose.</param>
    internal ChannelLease(IChannel channel, Func<IChannel, ValueTask> returnToPool)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(returnToPool);
        Channel = channel;
        r_returnToPool = returnToPool;
    }

    /// <summary>Gets the leased channel. Do not store outside the lease scope.</summary>
    public IChannel Channel { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;
        await r_returnToPool(Channel);
    }
}
