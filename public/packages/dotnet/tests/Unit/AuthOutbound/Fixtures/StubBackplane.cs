// -----------------------------------------------------------------------
// <copyright file="StubBackplane.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.Fixtures;

using System.Collections.Concurrent;
using D2.Shared.Caching;
using D2.Shared.Result;

/// <summary>
/// In-process <see cref="ICacheInvalidationBackplane"/> stub. Subscribers
/// receive every published key. Tests can also call
/// <see cref="PublishToSubscribersAsync"/> directly to drive subscriber
/// handlers without going through the publish path.
/// </summary>
internal sealed class StubBackplane : ICacheInvalidationBackplane
{
    private readonly ConcurrentBag<Func<string, CancellationToken, ValueTask>> r_handlers = [];
    private bool _disposed;

    /// <summary>Gets the count of every key the backplane has been asked to publish.</summary>
    public int PublishCount { get; private set; }

    /// <summary>
    /// Gets a value indicating whether <see cref="DisposeAsync"/> has been called.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <inheritdoc/>
    public IAsyncDisposable Subscribe(Func<string, CancellationToken, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        r_handlers.Add(handler);
        return new Subscription(() => { });
    }

    /// <inheritdoc/>
    public async ValueTask<D2Result> PublishInvalidationAsync(
        string key, CancellationToken ct = default)
    {
        await PublishToSubscribersAsync(key, ct);
        return D2Result.Ok();
    }

    /// <inheritdoc/>
    public async ValueTask<D2Result> PublishInvalidationManyAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        foreach (var key in keys)
            await PublishToSubscribersAsync(key, ct);

        return D2Result.Ok();
    }

    /// <summary>Drives every subscribed handler with <paramref name="key"/>.</summary>
    /// <param name="key">The invalidation key to deliver.</param>
    /// <param name="ct">Cancellation token forwarded to each handler.</param>
    public async Task PublishToSubscribersAsync(string key, CancellationToken ct = default)
    {
        PublishCount++;
        foreach (var handler in r_handlers)
            await handler(key, ct);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private sealed class Subscription : IAsyncDisposable
    {
        private readonly Action r_onDispose;

        public Subscription(Action onDispose) => r_onDispose = onDispose;

        public ValueTask DisposeAsync()
        {
            r_onDispose();
            return ValueTask.CompletedTask;
        }
    }
}
