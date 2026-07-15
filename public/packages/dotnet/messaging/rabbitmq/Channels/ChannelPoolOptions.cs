// -----------------------------------------------------------------------
// <copyright file="ChannelPoolOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Channels;

/// <summary>
/// Configuration for the bounded shared channel pool used by the publisher
/// path. Subscriber-side channels are dedicated per subscriber (one channel
/// each, owned by the consumer host) and not pulled from this pool.
/// </summary>
/// <remarks>
/// AMQP channels are not thread-safe; one channel per concurrent publisher
/// is required. Confirm-tracking state is per-channel too — sharing a single
/// channel across many publishers means slow confirms back up the entire
/// queue.
/// </remarks>
public sealed class ChannelPoolOptions
{
    /// <summary>
    /// Gets or sets the maximum channels held by the publisher pool.
    /// Default 4 — handles ~1000-5000 publish/sec on a typical service.
    /// Tune up for high-throughput publishers (audit collector, telemetry
    /// sink) where confirms back up under sustained load.
    /// </summary>
    public int PublishPoolSize { get; set; } = 4;

    /// <summary>
    /// Gets or sets a value indicating whether channels created by the pool
    /// have publisher confirms enabled. Default true — durability beats the
    /// per-publish round-trip cost in our context.
    /// </summary>
    public bool PublisherConfirmsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout the pool waits for a free channel when all
    /// pool slots are leased. Default 30 seconds — well above any normal
    /// confirm latency, but short enough that a stuck pool surfaces as a
    /// loud failure rather than indefinite hang.
    /// </summary>
    public TimeSpan AcquireTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the idle TTL for pooled channels. On
    /// <c>BoundedChannelPool.AcquireAsync</c>, channels that have been
    /// idle (not leased) longer than this are disposed and replaced with
    /// a fresh one before being handed out. Default 5 minutes — bounded
    /// connection state on the broker side (heartbeat / publisher-confirm
    /// state machines age out cleanly without a slow leak under
    /// low-traffic services).
    /// </summary>
    public TimeSpan IdleTtl { get; set; } = TimeSpan.FromMinutes(5);
}
