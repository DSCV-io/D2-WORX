// -----------------------------------------------------------------------
// <copyright file="MessagingTelemetry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Telemetry;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Static OTel <see cref="ActivitySource"/> + <see cref="Meter"/> used
/// across the messaging-rabbitmq lib. Static (not instance) per OTel
/// guidance — generic-static-field issues + listener attachment cost
/// dominate when these are per-handler instances.
/// </summary>
/// <remarks>
/// Class is <c>public</c> so the source / meter name constant
/// (<see cref="SOURCE_NAME"/>) is reachable cross-assembly — consumed by
/// <c>DcsvIo.D2.Telemetry</c>'s aggregation registration so messaging
/// publish / consume spans + metrics reach the OTLP / Prometheus exporters
/// without per-host opt-in. The activity source / meter / counter / histogram
/// fields remain <c>internal</c> — only the lib's own publish + consume
/// hot-path code emits to them.
/// </remarks>
public static class MessagingTelemetry
{
    /// <summary>OTel source / meter name for the messaging-rabbitmq lib.</summary>
    public const string SOURCE_NAME = "DcsvIo.D2.Messaging.RabbitMq";

    /// <summary>Activity source for publish + consume spans.</summary>
    public static readonly ActivitySource SR_ActivitySource = new(SOURCE_NAME);

    /// <summary>Meter that owns the publish counters / histogram.</summary>
    public static readonly Meter SR_Meter = new(SOURCE_NAME);

    /// <summary>Total publish attempts (including retries).</summary>
    public static readonly Counter<long> SR_PublishesCounter = SR_Meter.CreateCounter<long>(
        "d2.messaging.rabbitmq.publishes",
        unit: "{publish}",
        description: "Total publish attempts (including retries).");

    /// <summary>Terminal publish failures (after retries exhausted).</summary>
    public static readonly Counter<long> SR_PublishFailuresCounter = SR_Meter.CreateCounter<long>(
        "d2.messaging.rabbitmq.publish_failures",
        unit: "{publish}",
        description: "Terminal publish failures (after retries exhausted).");

    /// <summary>Wall-clock duration of a publish operation, end-to-end.</summary>
    public static readonly Histogram<double> SR_PublishDurationHistogram =
        SR_Meter.CreateHistogram<double>(
            "d2.messaging.rabbitmq.publish_duration",
            unit: "ms",
            description:
                "Wall-clock duration of a publish operation, end-to-end (including retries).");

    /// <summary>Publish retry attempts (transient failure → backoff → re-attempt).</summary>
    public static readonly Counter<long> SR_PublishRetriesCounter = SR_Meter.CreateCounter<long>(
        "d2.messaging.rabbitmq.publish_retries",
        unit: "{retry}",
        description: "Publish retry attempts (transient failure → backoff → re-attempt).");

    /// <summary>DLQ republish failures — consumer fell back from
    /// republish-with-failure-header to plain BasicNack, so the DLQ
    /// message lacks the <c>x-d2-failure-reason</c> diagnostic header.</summary>
    public static readonly Counter<long> SR_DlqRepublishFailuresCounter =
        SR_Meter.CreateCounter<long>(
            "d2.messaging.rabbitmq.dlq_republish_failures",
            unit: "{republish}",
            description:
                "Consumer-side republish-to-DLX failures (failure-header lost; "
                + "fell back to BasicNack-no-requeue).");

    /// <summary>BasicAck failures after a successful handler run — narrowed
    /// catch around the ack call. Spikes here indicate broker / channel
    /// instability between handler completion and ack; the broker will
    /// redeliver on reconnect (idempotency mark prevents duplicate work).</summary>
    public static readonly Counter<long> SR_AckFailuresCounter =
        SR_Meter.CreateCounter<long>(
            "d2.messaging.rabbitmq.ack_failures",
            unit: "{ack}",
            description:
                "Consumer-side BasicAck failures (handler succeeded but ack "
                + "could not be sent — broker will redeliver).");
}
