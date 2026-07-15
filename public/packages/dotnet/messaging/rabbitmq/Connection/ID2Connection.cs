// -----------------------------------------------------------------------
// <copyright file="ID2Connection.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Connection;

using global::RabbitMQ.Client;
using JetBrains.Annotations;

/// <summary>
/// Internal wrapper over <see cref="IConnection"/>. Hides
/// connection-lifecycle concerns from the rest of the lib (publisher /
/// consumer / channel pool consume this instead of the raw
/// <see cref="IConnection"/>) and lets us mock the surface in unit tests.
/// </summary>
/// <remarks>
/// Connection management is graceful-degradation by design: <see cref="StartReconnectLoop"/>
/// kicks off a background task that retries forever with exponential backoff.
/// Publishers / consumers consult <see cref="IsOpen"/> (or await
/// <see cref="ReadyTask"/>) and return <c>ServiceUnavailable</c> when the
/// broker is unreachable rather than throwing the host down.
/// </remarks>
internal interface ID2Connection : IAsyncDisposable
{
    /// <summary>Gets a value indicating whether the underlying connection
    /// is currently open.</summary>
    bool IsOpen { get; }

    /// <summary>Gets a task that completes the FIRST time the connection
    /// successfully opens. Consumers await this before registering
    /// channels; subsequent reconnects are handled automatically by
    /// RabbitMQ.Client's topology-recovery and don't re-trigger this.</summary>
    Task ReadyTask { get; }

    /// <summary>
    /// Kicks off the background reconnect loop. Returns immediately —
    /// connection establishment happens asynchronously. Idempotent — calling
    /// twice is a no-op.
    /// </summary>
    void StartReconnectLoop();

    /// <summary>
    /// Creates a new AMQP channel against the underlying connection.
    /// </summary>
    /// <param name="options">
    /// Channel options (publisher confirms enabled, etc.). Pass null for
    /// defaults.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An open <see cref="IChannel"/>; caller owns disposal.</returns>
    /// <exception cref="BrokerUnavailableException">
    /// Connection is not currently open. Caller (publisher / channel pool)
    /// catches and converts to <c>D2Result.ServiceUnavailable</c>.
    /// </exception>
    [MustDisposeResource]
    ValueTask<IChannel> CreateChannelAsync(
        CreateChannelOptions? options = null, CancellationToken ct = default);
}
