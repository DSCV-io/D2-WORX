// -----------------------------------------------------------------------
// <copyright file="RabbitMqConnectionOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Connection;

/// <summary>
/// Connection-level configuration for the RabbitMQ-backed messaging stack.
/// Bound via <c>services.Configure&lt;RabbitMqConnectionOptions&gt;(...)</c>
/// or set inside the <c>AddD2MessagingRabbitMq</c> composition root.
/// </summary>
public sealed class RabbitMqConnectionOptions
{
    /// <summary>
    /// Gets or sets the AMQP connection URI
    /// (<c>amqp://user:pass@host:port/vhost</c> or <c>amqps://...</c>).
    /// Required.
    /// </summary>
    public string ConnectionUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the descriptive client-provided name reported to the
    /// broker. Defaults to <c>DcsvIo.D2.Messaging</c>; overriding helps
    /// distinguish replicas in management UI.
    /// </summary>
    public string ClientProvidedName { get; set; } = "DcsvIo.D2.Messaging";

    /// <summary>
    /// Gets or sets the dispatch concurrency for async consumers (number of
    /// in-flight handler invocations the dispatcher allows per channel).
    /// Defaults to <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    /// <remarks>
    /// Values greater than 1 remove the per-channel ordering guarantee — the
    /// broker dispatches messages in order, but the .NET client may invoke
    /// the <c>ReceivedAsync</c> callback for delivery N+1 before delivery N
    /// has finished. Handlers MUST be thread-safe and idempotent under this
    /// default. Set to <c>1</c> if strict in-order processing per queue is
    /// required (e.g. event-sourced projections).
    /// </remarks>
    public ushort ConsumerDispatchConcurrency { get; set; }
        = (ushort)Math.Min(ushort.MaxValue, Environment.ProcessorCount);

    /// <summary>
    /// Gets or sets the initial backoff between reconnection attempts.
    /// Doubles on each failure up to <see cref="MaxReconnectDelay"/>.
    /// Defaults to 1 second.
    /// </summary>
    public TimeSpan InitialReconnectDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the cap on exponential backoff between reconnection
    /// attempts. The host retries forever — operators see persistent
    /// "broker unavailable" warnings rather than a crashed replica.
    /// Defaults to 60 seconds.
    /// </summary>
    public TimeSpan MaxReconnectDelay { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the interval at which the reconnect loop verifies the
    /// existing connection is still healthy. RabbitMQ.Client 7.x's
    /// automatic recovery handles in-flight failures, but this guard catches
    /// edge cases where the recovery handshake silently failed. Defaults
    /// to 30 seconds.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
}
