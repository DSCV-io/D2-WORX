// -----------------------------------------------------------------------
// <copyright file="AuthTelemetry.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Telemetry;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Telemetry surface for the inbound auth runtime — JWT validation,
/// session liveness checking, JWKS fetching. Separate from the outbound
/// runtime's <c>OutboundTelemetry</c> source: different SLOs, different
/// dashboards, different alert thresholds (token-validate latency vs
/// token-acquire latency).
/// </summary>
public static class AuthTelemetry
{
    /// <summary>
    /// The OpenTelemetry <see cref="ActivitySource"/> name. Hosts add this
    /// to their <c>OpenTelemetryBuilder</c> via
    /// <c>.WithTracing(t => t.AddSource(AuthTelemetry.ACTIVITY_SOURCE_NAME))</c>.
    /// </summary>
    public const string ACTIVITY_SOURCE_NAME = "D2.Shared.Auth";

    /// <summary>
    /// The OpenTelemetry <see cref="Meter"/> name. Hosts add this via
    /// <c>.WithMetrics(m => m.AddMeter(AuthTelemetry.METER_NAME))</c>.
    /// </summary>
    public const string METER_NAME = "D2.Shared.Auth";

    /// <summary>The shared <see cref="ActivitySource"/> for this lib.</summary>
    public static readonly ActivitySource Activity = new(ACTIVITY_SOURCE_NAME);

    /// <summary>The shared <see cref="Meter"/> for this lib.</summary>
    public static readonly Meter Meter = new(METER_NAME);

    /// <summary>
    /// Counter — total inbound JWT validations. Tagged with <c>outcome</c>;
    /// closed-enum values emitted by codegen — see
    /// <see cref="AuthTelemetryTags.JwtValidations.Outcome"/>.
    /// </summary>
    public static readonly Counter<long> JwtValidations =
        Meter.CreateCounter<long>(
            name: "d2.auth.jwt.validations",
            description: "Total inbound JWT validations.");

    /// <summary>
    /// Counter — total session liveness checks and revoke-event observations.
    /// Tagged with <c>outcome</c>; closed-enum values emitted by codegen — see
    /// <see cref="AuthTelemetryTags.SessionLivenessChecks.Outcome"/>.
    /// (<c>backplane_revoked</c> is emitted by <c>SessionRevokedBackplaneSubscriber</c>
    /// for every cluster-wide revoke event matching the configured cache-key
    /// prefix — telemetry-only observation; the underlying liveness check
    /// remains correct via cache invalidation.)
    /// </summary>
    public static readonly Counter<long> SessionLivenessChecks =
        Meter.CreateCounter<long>(
            name: "d2.auth.session.liveness.checks",
            description: "Total session liveness checks.");

    /// <summary>
    /// Counter — total JWKS fetches / refresh events. Tagged with
    /// <c>trigger</c> (<see cref="AuthTelemetryTags.JwksFetches.Trigger"/>:
    /// <c>implicit</c> = passive read via GetKeysAsync; <c>reactive</c> =
    /// forced via RefreshAsync after cooldown elapsed; <c>cooldown_skipped</c>
    /// = RefreshAsync within cooldown window — no upstream call;
    /// <c>backplane_rotation</c> = RefreshAsync invoked by
    /// JwksBackplaneSubscriber on a key-rotated event) and <c>outcome</c>
    /// (<see cref="AuthTelemetryTags.JwksFetches.Outcome"/>: <c>success</c>,
    /// <c>failure</c>, <c>parse_error</c> — malformed-JSON discovery doc, so
    /// dashboards can spot upstream contract drift, <c>circuit_open</c> —
    /// breaker fast-failed without an upstream call during sustained outage,
    /// <c>received</c>).
    /// </summary>
    public static readonly Counter<long> JwksFetches =
        Meter.CreateCounter<long>(
            name: "d2.auth.jwks.fetches",
            description: "Total JWKS fetches from the upstream OIDC issuer.");

    /// <summary>
    /// Counter — auth-failure responses emitted by the transport-binding
    /// libraries: RFC 7807 ProblemDetails from the HTTP middleware
    /// (<c>D2.Shared.Auth.Http</c>) AND <see cref="System.Exception"/>-
    /// shaped <c>RpcException(Status, Trailers)</c> from the gRPC interceptor
    /// (<c>D2.Shared.Auth.Grpc</c>). Single sink across both transports so
    /// dashboards aggregate cleanly. Tagged with <c>d2_error_code</c>
    /// (<see cref="AuthTelemetryTags.ProblemEmitted.TAG_D2_ERROR_CODE"/>);
    /// values are the <c>AUTH_*</c> constants from
    /// <see cref="D2.Shared.Auth.Errors.AuthErrorCodes"/> (cross-spec
    /// resolved by codegen — see
    /// <c>contracts/telemetry/telemetry.spec.json</c>).
    /// </summary>
    public static readonly Counter<long> ProblemEmitted =
        Meter.CreateCounter<long>(
            name: "d2.auth.problem.emitted",
            description:
                "Total auth-failure responses emitted by the transport-binding libraries "
                + "(HTTP ProblemDetails + gRPC RpcException trailers).");

    /// <summary>
    /// Histogram — wall-clock duration of the full JWT validation pipeline
    /// (signature verify + standard claim checks + claim → context mapping +
    /// session liveness check) in milliseconds. Excludes downstream handler
    /// time.
    /// </summary>
    public static readonly Histogram<double> JwtValidationDurationMs =
        Meter.CreateHistogram<double>(
            name: "d2.auth.jwt.validation.duration",
            unit: "ms",
            description: "Wall-clock duration of the JWT validation pipeline.");

    /// <summary>
    /// Histogram — wall-clock duration of a session liveness lookup
    /// (cache check + on-miss backplane reconciliation) in milliseconds.
    /// </summary>
    public static readonly Histogram<double> SessionLivenessLookupDurationMs =
        Meter.CreateHistogram<double>(
            name: "d2.auth.session.liveness.lookup.duration",
            unit: "ms",
            description: "Wall-clock duration of a session liveness lookup.");

    /// <summary>
    /// Histogram — wall-clock duration of a JWKS fetch from the upstream
    /// OIDC issuer (HTTP round-trip + JSON parse) in milliseconds.
    /// </summary>
    public static readonly Histogram<double> JwksFetchDurationMs =
        Meter.CreateHistogram<double>(
            name: "d2.auth.jwks.fetch.duration",
            unit: "ms",
            description: "Wall-clock duration of a JWKS fetch from the upstream issuer.");
}
