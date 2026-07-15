// -----------------------------------------------------------------------
// <copyright file="AggregatedTelemetrySources.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Internal;

using DcsvIo.D2.Auth.Outbound.Telemetry;
using DcsvIo.D2.Auth.Telemetry;
using DcsvIo.D2.Caching.Distributed.Redis;
using DcsvIo.D2.Caching.Local.Default;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging.RabbitMq.Telemetry;

/// <summary>
/// Single source of truth for the
/// <see cref="System.Diagnostics.ActivitySource"/> /
/// <see cref="System.Diagnostics.Metrics.Meter"/> name aggregation that
/// <see cref="TelemetryServiceCollectionExtensions.AddD2Telemetry"/>
/// registers with the tracer / meter providers. Every entry is sourced
/// from the owning lib's published <c>const string</c> via direct
/// compile-time-bound symbol reference — a rename of any const surfaces
/// as a build break across this aggregation layer.
/// </summary>
/// <remarks>
/// <para>
/// Spec-pinning unit tests in <c>AggregatedTelemetrySourcesTests</c>
/// assert the LITERAL wire values
/// (<c>"DcsvIo.D2.Handler"</c>, <c>"DcsvIo.D2.Auth"</c>, etc.) so a const
/// symbol rename to a different VALUE doesn't silently change the wire
/// format consumed by Tempo / Loki / Prometheus dashboards. Both layers
/// of safety apply: const-symbol references catch renames at compile
/// time; literal-pinning tests catch value drift at test time.
/// </para>
/// <para>
/// All shipped shared-lib telemetry classes that publish their names through
/// public const strings are aggregated here:
/// <list type="bullet">
///  <item><c>HandlerTelemetry.SourceName</c></item>
///  <item><c>AuthTelemetry.ACTIVITY_SOURCE_NAME</c> /
///   <c>METER_NAME</c></item>
///  <item><c>OutboundTelemetry.ACTIVITY_SOURCE_NAME</c> /
///   <c>METER_NAME</c></item>
///  <item><c>MessagingTelemetry.SOURCE_NAME</c></item>
///  <item><c>RedisCacheTelemetry.METER_NAME</c></item>
///  <item><c>LocalCacheTelemetry.METER_NAME</c></item>
/// </list>
/// </para>
/// </remarks>
internal static class AggregatedTelemetrySources
{
    /// <summary>
    /// The aggregated set of <see cref="System.Diagnostics.ActivitySource"/>
    /// names registered with the tracer provider so spans from those libs
    /// flow to the OTLP traces exporter.
    /// </summary>
    internal static readonly IReadOnlyList<string> SR_ActivitySourceNames =
    [
        HandlerTelemetry.SourceName,
        AuthTelemetry.ACTIVITY_SOURCE_NAME,
        OutboundTelemetry.ACTIVITY_SOURCE_NAME,
        MessagingTelemetry.SOURCE_NAME,
    ];

    /// <summary>
    /// The aggregated set of <see cref="System.Diagnostics.Metrics.Meter"/>
    /// names registered with the meter provider so metrics from those libs
    /// flow to the OTLP / Prometheus metrics exporters.
    /// </summary>
    /// <remarks>
    /// Cache-lib meters
    /// (<see cref="RedisCacheTelemetry.METER_NAME"/> +
    /// <see cref="LocalCacheTelemetry.METER_NAME"/>) appear in the meter
    /// list but NOT in the tracer-source list — the cache libs publish
    /// counters only; no spans.
    /// </remarks>
    internal static readonly IReadOnlyList<string> SR_MeterNames =
    [
        HandlerTelemetry.SourceName,
        AuthTelemetry.METER_NAME,
        OutboundTelemetry.METER_NAME,
        MessagingTelemetry.SOURCE_NAME,
        RedisCacheTelemetry.METER_NAME,
        LocalCacheTelemetry.METER_NAME,
    ];
}
