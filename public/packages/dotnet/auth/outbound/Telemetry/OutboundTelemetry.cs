// -----------------------------------------------------------------------
// <copyright file="OutboundTelemetry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Outbound.Telemetry;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Telemetry surface for the outbound auth runtime — RFC-8693 token exchange.
/// Separate from the inbound <c>DcsvIo.D2.Auth</c> source (which lives in
/// <c>AuthTelemetry</c> in the inbound runtime lib): different SLOs, different
/// operational dashboards, different alert thresholds.
/// </summary>
public static class OutboundTelemetry
{
    /// <summary>
    /// The OpenTelemetry <see cref="ActivitySource"/> name. Hosts add this
    /// to their <c>OpenTelemetryBuilder</c> via
    /// <c>.WithTracing(t => t.AddSource(OutboundTelemetry.ACTIVITY_SOURCE_NAME))</c>.
    /// </summary>
    public const string ACTIVITY_SOURCE_NAME = "DcsvIo.D2.Auth.Outbound";

    /// <summary>
    /// The OpenTelemetry <see cref="Meter"/> name. Hosts add this via
    /// <c>.WithMetrics(m => m.AddMeter(OutboundTelemetry.METER_NAME))</c>.
    /// </summary>
    public const string METER_NAME = "DcsvIo.D2.Auth.Outbound";

    /// <summary>The shared <see cref="ActivitySource"/> for this lib.</summary>
    public static readonly ActivitySource SR_Activity = new(ACTIVITY_SOURCE_NAME);

    /// <summary>The shared <see cref="Meter"/> for this lib.</summary>
    public static readonly Meter SR_Meter = new(METER_NAME);

    /// <summary>
    /// Counter — total token-exchange requests. Tagged with <c>outcome</c>;
    /// closed-enum values emitted by codegen — see
    /// <see cref="OutboundTelemetryTags.TokenExchangeRequests.Outcome"/>.
    /// </summary>
    public static readonly Counter<long> SR_TokenExchangeRequests =
        SR_Meter.CreateCounter<long>(
            name: "d2.auth.outbound.token_exchange.requests",
            unit: "{request}",
            description: "Total token-exchange requests.");

    /// <summary>
    /// Counter — token-exchange cache entries purged by session-revoked
    /// backplane events. Useful for verifying cluster-wide invalidation
    /// propagation. Untagged; one increment per purged cache key.
    /// </summary>
    public static readonly Counter<long> SR_TokenExchangeRevokedPurges =
        SR_Meter.CreateCounter<long>(
            name: "d2.auth.outbound.token_exchange.revoked_purges",
            unit: "{purge}",
            description: "Total token-exchange cache entries purged on session-revoked.");

    /// <summary>
    /// Counter — workload leaf reissue failures. Untagged; one increment per failed
    /// <see cref="WorkloadCertificate.WorkloadLeafClient"/> reissue attempt.
    /// The cached leaf's not-after is captured on the accompanying
    /// <c>WorkloadLeafReissueFailed</c> log event as the <c>CachedLeafNotAfter</c>
    /// structured field (ISO-8601 UTC, or <c>none</c> when no cached leaf exists).
    /// </summary>
    public static readonly Counter<long> SR_LeafReissueFailures =
        SR_Meter.CreateCounter<long>(
            name: "d2.auth.outbound.workload_leaf.reissue_failures",
            unit: "{failure}",
            description: "Total workload leaf reissue failures.");
}
