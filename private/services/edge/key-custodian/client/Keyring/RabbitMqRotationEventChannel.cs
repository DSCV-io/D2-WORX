// -----------------------------------------------------------------------
// <copyright file="RabbitMqRotationEventChannel.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;

using System.Collections.Concurrent;
using DcsvIo.D2.Utilities.Diagnostics;
using Microsoft.Extensions.Logging;

/// <summary>
/// Thread-safe registry of <c>domain → rotation callbacks</c>. The dispatch source is
/// the <see cref="KeyringRefreshSubscriber"/> (fed by the <c>d2.security.key-rotated</c>
/// fanout); on each rotation event this channel invokes the callbacks registered for the
/// event's domain. Registered as a singleton; a keyring holder subscribes its domain and
/// disposes the returned handle to unsubscribe.
/// </summary>
public sealed class RabbitMqRotationEventChannel : IRotationEventChannel
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Subscription, byte>>
        r_byDomain = new(StringComparer.Ordinal);

    private readonly ILogger<RabbitMqRotationEventChannel> r_logger;

    /// <summary>Initializes a new <see cref="RabbitMqRotationEventChannel"/>.</summary>
    /// <param name="logger">Logger for isolated callback failures.</param>
    public RabbitMqRotationEventChannel(ILogger<RabbitMqRotationEventChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        r_logger = logger;
    }

    /// <inheritdoc />
    public IAsyncDisposable Subscribe(string domain, Func<CancellationToken, Task> onRotation)
    {
        domain.ThrowIfFalsey();
        ArgumentNullException.ThrowIfNull(onRotation);

        var set = r_byDomain.GetOrAdd(domain, static _ => new());
        var subscription = new Subscription(this, domain, onRotation);
        set[subscription] = 0;

        return subscription;
    }

    /// <summary>
    /// Dispatches a rotation notification to every callback registered for
    /// <paramref name="domain"/>. Each callback is isolated — a throwing callback is
    /// logged and does not stop siblings. A non-matching domain is a no-op.
    /// </summary>
    /// <param name="domain">The rotated domain, from the <c>KeyRotatedEvent</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    internal async Task DispatchAsync(string domain, CancellationToken ct)
    {
        if (domain.Falsey() || !r_byDomain.TryGetValue(domain, out var set))
            return;

        foreach (var subscription in set.Keys)
        {
            try
            {
                await subscription.Invoke(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                KeyringLog.RotationCallbackFailed(
                    r_logger, domain, SanitizedExceptionRender.TypeName(ex));
            }
        }
    }

    private void Unsubscribe(string domain, Subscription subscription)
    {
        if (r_byDomain.TryGetValue(domain, out var set))
            set.TryRemove(subscription, out _);
    }

    private sealed class Subscription(
        RabbitMqRotationEventChannel owner,
        string domain,
        Func<CancellationToken, Task> onRotation) : IAsyncDisposable
    {
        private int _disposed;

        public Task Invoke(CancellationToken ct) => onRotation(ct);

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.Unsubscribe(domain, this);

            return ValueTask.CompletedTask;
        }
    }
}
