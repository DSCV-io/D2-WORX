// -----------------------------------------------------------------------
// <copyright file="D2TelemetryConstants.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry;

using DcsvIo.D2.AspNetCore;

/// <summary>
/// Public constants exposed by <see cref="DcsvIo.D2.Telemetry"/> — config-key
/// strings, env-var names, and infrastructure endpoint paths consumers may
/// want to reference by symbol rather than by literal.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OTEL_SERVICE_NAME_CONFIG_KEY"/> carries the same value as
/// <c>DcsvIo.D2.Logging.D2LoggingConstants.OTEL_SERVICE_NAME_CONFIG_KEY</c>
/// — both reference the same OpenTelemetry-canonical env var. Re-declared
/// here rather than depending on <c>DcsvIo.D2.Logging</c> for the constant
/// because Telemetry is intentionally independent of Logging (consumers may
/// wire one without the other).
/// </para>
/// <para>
/// Infrastructure-path constants
/// (<see cref="HEALTH_ENDPOINT_PATH"/>, <see cref="ALIVE_ENDPOINT_PATH"/>,
/// <see cref="PROMETHEUS_ENDPOINT_PATH"/>,
/// <see cref="WELL_KNOWN_ENDPOINT_PATH"/>) re-export the canonical literals
/// from <see cref="D2AspNetCoreConstants"/>. Telemetry now ProjectReferences
/// AspNetCore for the shared <c>InfrastructurePathMatcher</c>; the constant
/// re-export keeps any consumer that imports
/// <c>DcsvIo.D2.Telemetry.D2TelemetryConstants.HEALTH_ENDPOINT_PATH</c>
/// working without code change while removing per-lib literal duplication.
/// </para>
/// </remarks>
public static class D2TelemetryConstants
{
    /// <summary>
    /// Configuration key used by
    /// <see cref="TelemetryServiceCollectionExtensions.AddD2Telemetry"/> to
    /// source the service name when
    /// <see cref="D2TelemetryOptions.ServiceName"/> is left null. Matches
    /// the OpenTelemetry-canonical convention so log + trace + metric
    /// service names stay aligned across the three pipelines.
    /// </summary>
    public const string OTEL_SERVICE_NAME_CONFIG_KEY = "OTEL_SERVICE_NAME";

    /// <summary>
    /// Configuration key for the OTLP traces endpoint. Falsey value
    /// (null / empty / whitespace) suppresses traces OTLP exporter
    /// registration; spans still emit to in-process listeners.
    /// </summary>
    public const string OTLP_TRACES_ENDPOINT_CONFIG_KEY =
        "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT";

    /// <summary>
    /// Configuration key for the OTLP metrics endpoint. Falsey value
    /// suppresses metrics OTLP exporter registration; in-process metrics
    /// still flow to the Prometheus scraping endpoint when enabled.
    /// </summary>
    public const string OTLP_METRICS_ENDPOINT_CONFIG_KEY =
        "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT";

    /// <summary>
    /// Configuration key for the OTLP logs endpoint. Falsey value
    /// suppresses logs OTLP exporter registration; logs still flow through
    /// MEL providers (e.g. console sink wired by
    /// <c>DcsvIo.D2.Logging</c>).
    /// </summary>
    public const string OTLP_LOGS_ENDPOINT_CONFIG_KEY =
        "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT";

    /// <summary>
    /// Environment variable consulted at registration time —
    /// case-insensitive equality with the literal <c>"true"</c> short-
    /// circuits the entire OTel setup
    /// (<see cref="TelemetryServiceCollectionExtensions.AddD2Telemetry"/>
    /// becomes a no-op,
    /// <see cref="WebApplicationTelemetryExtensions.MapD2PrometheusEndpoint"/>
    /// maps no route). The OpenTelemetry SDK observes the same env var
    /// internally; honoring it here ensures the lib's surface stays
    /// consistent with that contract for E2E test scenarios.
    /// </summary>
    public const string OTEL_SDK_DISABLED_ENV_VAR = "OTEL_SDK_DISABLED";

    /// <summary>
    /// The route path the Prometheus scraping endpoint is mapped at
    /// (<see cref="WebApplicationTelemetryExtensions.MapD2PrometheusEndpoint"/>).
    /// Re-exports
    /// <see cref="D2AspNetCoreConstants.METRICS_ENDPOINT_PATH"/>.
    /// </summary>
    public const string PROMETHEUS_ENDPOINT_PATH =
        D2AspNetCoreConstants.METRICS_ENDPOINT_PATH;

    /// <summary>
    /// Health-probe infrastructure endpoint — by convention excluded from
    /// AspNetCore instrumentation auto-spans + downgraded to verbose by
    /// <c>DcsvIo.D2.Logging</c>'s request-logging middleware. Re-exports
    /// <see cref="D2AspNetCoreConstants.HEALTH_ENDPOINT_PATH"/>.
    /// </summary>
    public const string HEALTH_ENDPOINT_PATH =
        D2AspNetCoreConstants.HEALTH_ENDPOINT_PATH;

    /// <summary>
    /// Liveness-probe infrastructure endpoint — same treatment as
    /// <see cref="HEALTH_ENDPOINT_PATH"/>. Re-exports
    /// <see cref="D2AspNetCoreConstants.ALIVE_ENDPOINT_PATH"/>.
    /// </summary>
    public const string ALIVE_ENDPOINT_PATH =
        D2AspNetCoreConstants.ALIVE_ENDPOINT_PATH;

    /// <summary>
    /// Well-known-doc prefix — covers OIDC discovery (
    /// <c>/.well-known/openid-configuration</c>), JWKS (
    /// <c>/.well-known/jwks.json</c>), security.txt, etc. All treated as
    /// infrastructure and excluded from auto-spans. Re-exports
    /// <see cref="D2AspNetCoreConstants.WELL_KNOWN_ENDPOINT_PATH"/>.
    /// </summary>
    public const string WELL_KNOWN_ENDPOINT_PATH =
        D2AspNetCoreConstants.WELL_KNOWN_ENDPOINT_PATH;
}
