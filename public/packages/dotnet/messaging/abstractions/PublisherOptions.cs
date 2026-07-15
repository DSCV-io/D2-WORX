// -----------------------------------------------------------------------
// <copyright file="PublisherOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

/// <summary>
/// Per-publish toggles passed to <see cref="IMessageBus.PublishAsync"/>.
/// All fields nullable; null means "use the lib's default".
/// </summary>
/// <remarks>
/// Connection-level settings (channel pool size, default confirm timeout,
/// AMQP URI, etc.) live on the transport-specific options class in the
/// impl lib (<c>RabbitMqMessagingOptions</c>), not here.
/// </remarks>
public sealed class PublisherOptions
{
    /// <summary>
    /// Gets or sets whether the publisher waits for the broker's
    /// publisher-confirm before returning success. <c>null</c> uses the
    /// transport-level default (typically true). Setting <c>false</c>
    /// fires-and-forgets — caller accepts that lost-on-broker-crash is
    /// possible.
    /// </summary>
    public bool? WaitForConfirm { get; set; }

    /// <summary>
    /// Gets or sets an override for the confirm wait timeout. <c>null</c>
    /// uses the transport-level default (typically 5 seconds).
    /// </summary>
    public TimeSpan? ConfirmTimeout { get; set; }

    /// <summary>
    /// Gets or sets an override for the AMQP routing key. <c>null</c>
    /// derives the routing key from convention (empty for fanout, lowercased
    /// dot-separated message-name for topic).
    /// </summary>
    public string? RoutingKey { get; set; }

    /// <summary>
    /// Gets or sets an override for the destination exchange. <c>null</c>
    /// derives from the convention <c>d2.{producer}.{purpose}</c>.
    /// </summary>
    public string? Exchange { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of publish attempts (including the
    /// initial call) on transient failure. <c>null</c> uses the
    /// transport-level default (typically 5). Set to <c>1</c> for true
    /// fire-and-forget semantics — no retry on transient failure, caller
    /// gets <c>ServiceUnavailable</c> immediately.
    /// </summary>
    /// <remarks>
    /// "Transient" means: broker unreachable, channel dropped mid-publish,
    /// confirm timeout, AMQP-level connection interrupt. Schema / encryption
    /// / argument-validation failures are NOT retried (they'll keep failing).
    /// </remarks>
    public int? MaxAttempts { get; set; }
}
