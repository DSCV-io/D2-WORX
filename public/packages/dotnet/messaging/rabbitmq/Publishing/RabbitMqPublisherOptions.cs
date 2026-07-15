// -----------------------------------------------------------------------
// <copyright file="RabbitMqPublisherOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Publishing;

/// <summary>
/// Transport-level publisher defaults. Per-call <see cref="PublisherOptions"/>
/// can override individual knobs at <c>PublishAsync</c>-call time; null fields
/// on the per-call options fall back to these defaults.
/// </summary>
public sealed class RabbitMqPublisherOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether publishes wait for the
    /// broker's publisher-confirm before returning. Default true —
    /// durability beats the per-publish round-trip cost in our context.
    /// </summary>
    public bool WaitForConfirm { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout the publisher waits for a confirm. Default
    /// 5 seconds. On timeout, the publisher returns
    /// <c>D2Result.ServiceUnavailable</c>.
    /// </summary>
    public TimeSpan ConfirmTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum publish attempts (including the initial
    /// call) on transient failure. Default 5. Per-call
    /// <see cref="PublisherOptions.MaxAttempts"/> overrides; set to 1
    /// there for fire-and-forget semantics on a specific call.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the base delay before the first retry. Default 200ms;
    /// doubles on each retry, capped at <see cref="MaxRetryDelay"/>.
    /// </summary>
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets the cap on exponential backoff between publish retries.
    /// Default 5 seconds — keeps the worst-case publish latency bounded
    /// even under sustained broker outage.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}
