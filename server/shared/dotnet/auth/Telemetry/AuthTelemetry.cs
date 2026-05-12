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
    /// Counter — total inbound JWT validations. Tagged with <c>outcome</c>:
    /// <c>success</c>, <c>bearer_missing</c>, <c>bearer_malformed</c>,
    /// <c>signature_invalid</c>, <c>expired</c>, <c>not_yet_valid</c>,
    /// <c>issuer_mismatch</c>, <c>audience_mismatch</c>, <c>claim_missing</c>,
    /// <c>act_chain_malformed</c>, <c>kid_not_found</c>,
    /// <c>jwks_unavailable</c>.
    /// </summary>
    public static readonly Counter<long> JwtValidations =
        Meter.CreateCounter<long>(
            name: "d2.auth.jwt.validations",
            description: "Total inbound JWT validations.");

    /// <summary>
    /// Counter — total session liveness checks and revoke-event observations.
    /// Tagged with <c>outcome</c>: <c>alive</c>, <c>revoked</c>,
    /// <c>unavailable</c>, <c>invalid_input</c> (emitted by the liveness
    /// tracker on every <c>IsAliveAsync</c> call), and
    /// <c>backplane_revoked</c> (emitted by <c>SessionRevokedBackplaneSubscriber</c>
    /// for every cluster-wide revoke event matching the configured cache-key
    /// prefix — telemetry-only observation; the underlying liveness check
    /// remains correct via cache invalidation).
    /// </summary>
    public static readonly Counter<long> SessionLivenessChecks =
        Meter.CreateCounter<long>(
            name: "d2.auth.session.liveness.checks",
            description: "Total session liveness checks.");

    /// <summary>
    /// Counter — total JWKS fetches / refresh events. Tagged with
    /// <c>trigger</c>: <c>implicit</c> (passive read via GetKeysAsync —
    /// hits ConfigurationManager's internal cache or fetches if cold),
    /// <c>reactive</c> (forced via RefreshAsync after cooldown elapsed),
    /// <c>cooldown_skipped</c> (RefreshAsync within cooldown window —
    /// no upstream call), <c>backplane_rotation</c> (RefreshAsync invoked
    /// by JwksBackplaneSubscriber on a key-rotated event); and
    /// <c>outcome</c>: <c>success</c>, <c>failure</c>, <c>parse_error</c>
    /// (malformed-JSON discovery doc — distinguished from generic network
    /// failures so dashboards can spot upstream contract drift),
    /// <c>circuit_open</c> (breaker fast-failed without an upstream call
    /// during sustained outage), <c>received</c>.
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
    /// dashboards aggregate cleanly. Tagged with <c>d2_error_code</c> (one of
    /// the <c>AUTH_*</c> constants from
    /// <see cref="D2.Shared.Auth.Errors.AuthErrorCodes"/>:
    /// <c>AUTH_BEARER_MISSING</c>, <c>AUTH_BEARER_MALFORMED</c>,
    /// <c>AUTH_JWT_SIGNATURE_INVALID</c>, <c>AUTH_JWT_EXPIRED</c>,
    /// <c>AUTH_JWT_NOT_YET_VALID</c>, <c>AUTH_JWT_ISSUER_MISMATCH</c>,
    /// <c>AUTH_JWT_AUDIENCE_MISMATCH</c>, <c>AUTH_JWT_CLAIM_MISSING</c>,
    /// <c>AUTH_JWT_ACT_CHAIN_MALFORMED</c>,
    /// <c>AUTH_JWT_KID_NOT_FOUND</c>, <c>AUTH_JWKS_UNAVAILABLE</c>,
    /// <c>AUTH_SESSION_REVOKED</c>, <c>AUTH_SESSION_LIVENESS_UNAVAILABLE</c>,
    /// <c>AUTH_SCOPE_INSUFFICIENT</c>).
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
