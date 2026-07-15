// -----------------------------------------------------------------------
// <copyright file="D2TelemetryOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry;

using DcsvIo.D2.AspNetCore;

/// <summary>
/// Configuration for
/// <see cref="TelemetryServiceCollectionExtensions.AddD2Telemetry"/>. Use
/// the parameterless ctor for all defaults; the <c>with</c>-expression and
/// the standard object-initializer pattern both work for selective
/// overrides. All fields are init-only — bind via the
/// <c>configure</c> callback on <c>AddD2Telemetry</c> for runtime mutation
/// of nullable fields (the callback receives the live options instance
/// after env-derived defaults are applied).
/// </summary>
/// <remarks>
/// <para>
/// Validation runs at the first
/// <c>IOptions&lt;D2TelemetryOptions&gt;.Value</c> resolution
/// (typically host-startup composition) via
/// <c>ValidateOnStart()</c> — fail-fast on invalid config.
/// </para>
/// <para>
/// <see cref="ServiceName"/>,
/// <see cref="OtlpTracesEndpoint"/>,
/// <see cref="OtlpMetricsEndpoint"/>,
/// and <see cref="OtlpLogsEndpoint"/> default to values resolved by
/// <see cref="TelemetryServiceCollectionExtensions.AddD2Telemetry"/> from
/// the canonical OTel env vars + <c>IHostEnvironment.ApplicationName</c>
/// fallback for service name. When all three OTLP endpoints are absent,
/// telemetry stays in-process — the Prometheus scraping endpoint still
/// works for metrics; spans still emit to in-process listeners; logs still
/// flow through MEL providers.
/// </para>
/// </remarks>
public sealed record D2TelemetryOptions
{
    /// <summary>
    /// Default infrastructure-path prefixes excluded from AspNetCore
    /// instrumentation auto-span emission. Re-exports
    /// <see cref="D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS"/> so
    /// Telemetry's filter, Logging's request-logging middleware, and
    /// AspNetCore's bypass middleware stay aligned on the same path set
    /// without per-lib literal duplication.
    /// </summary>
    internal static readonly IReadOnlyList<string> SR_DefaultExcludedPaths =
        D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS;

    /// <summary>
    /// Gets or sets the service name emitted on every span / metric / log
    /// record via the <c>service.name</c> OTel resource attribute.
    /// Settable so the
    /// <c>AddD2Telemetry(Action&lt;D2TelemetryOptions&gt;)</c> configure
    /// lambda can populate it after the options instance is constructed by
    /// the DI container. When null at composition time,
    /// <see cref="TelemetryServiceCollectionExtensions.AddD2Telemetry"/>
    /// fills it from the
    /// <see cref="D2TelemetryConstants.OTEL_SERVICE_NAME_CONFIG_KEY"/>
    /// config value, then from <c>IHostEnvironment.ApplicationName</c>.
    /// Validated non-empty / non-whitespace at startup.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Gets or sets the OTLP traces endpoint URI. When falsey
    /// (null / empty / whitespace), the traces OTLP exporter is NOT
    /// registered — spans still emit to any in-process listeners (test
    /// harnesses, custom processors). Defaults to the
    /// <see cref="D2TelemetryConstants.OTLP_TRACES_ENDPOINT_CONFIG_KEY"/>
    /// config value. URI-shape validation runs at startup when truthy.
    /// </summary>
    public string? OtlpTracesEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the OTLP metrics endpoint URI. When falsey, the
    /// metrics OTLP exporter is NOT registered — Prometheus scraping
    /// remains active when
    /// <see cref="EnablePrometheusExporter"/> is <c>true</c>. Defaults to
    /// the
    /// <see cref="D2TelemetryConstants.OTLP_METRICS_ENDPOINT_CONFIG_KEY"/>
    /// config value. URI-shape validation runs at startup when truthy.
    /// </summary>
    public string? OtlpMetricsEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the OTLP logs endpoint URI. When falsey, the logs OTLP
    /// exporter is NOT registered — logs still flow through other MEL
    /// providers (e.g. console sink wired by <c>DcsvIo.D2.Logging</c>).
    /// Defaults to the
    /// <see cref="D2TelemetryConstants.OTLP_LOGS_ENDPOINT_CONFIG_KEY"/>
    /// config value. URI-shape validation runs at startup when truthy.
    /// </summary>
    public string? OtlpLogsEndpoint { get; set; }

    /// <summary>
    /// Gets the path prefixes excluded from AspNetCore instrumentation
    /// auto-span emission. Defaults to <c>/health</c>, <c>/alive</c>,
    /// <c>/metrics</c>, <c>/.well-known</c>. Validated non-empty
    /// (collection) and per-entry non-empty / non-whitespace at startup.
    /// </summary>
    public IReadOnlyList<string> InstrumentationExcludedPaths { get; init; } =
        SR_DefaultExcludedPaths;

    /// <summary>
    /// Gets the names of additional <c>ActivitySource</c>s registered with
    /// the tracer provider on top of the standard aggregation set
    /// (Handler, Auth, Auth.Outbound, Messaging.RabbitMq). Use for
    /// service-specific spans (e.g. <c>"DcsvIo.D2.Private.Edge"</c>). Validated per-entry
    /// non-empty / non-whitespace at startup when populated.
    /// </summary>
    public IReadOnlyList<string> AdditionalActivitySources { get; init; } = [];

    /// <summary>
    /// Gets the names of additional <c>Meter</c>s registered with the
    /// meter provider on top of the standard aggregation set. Same shape
    /// as <see cref="AdditionalActivitySources"/>.
    /// </summary>
    public IReadOnlyList<string> AdditionalMeters { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether AspNetCore inbound HTTP
    /// instrumentation is registered. Default <c>true</c>.
    /// </summary>
    public bool EnableAspNetCoreInstrumentation { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether HttpClient outbound instrumentation
    /// is registered. Default <c>true</c>. The instrumentation includes a
    /// self-referential filter that suppresses spans for outbound calls to
    /// the configured OTLP exporter endpoints (prevents infinite-loop
    /// instrumentation).
    /// </summary>
    public bool EnableHttpClientInstrumentation { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether GrpcNetClient outbound
    /// instrumentation is registered. Default <c>true</c>.
    /// </summary>
    public bool EnableGrpcNetClientInstrumentation { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether Process metrics
    /// (CPU, memory, fd count) are registered. Default <c>true</c>.
    /// </summary>
    public bool EnableProcessInstrumentation { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether .NET Runtime metrics
    /// (GC, threadpool, JIT) are registered. Default <c>true</c>.
    /// </summary>
    public bool EnableRuntimeInstrumentation { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the in-process Prometheus exporter
    /// is registered. Default <c>true</c>. When <c>false</c>, the
    /// <c>/metrics</c> route mapped by
    /// <see cref="WebApplicationTelemetryExtensions.MapD2PrometheusEndpoint"/>
    /// returns 404 (no scrape data); operators using OTLP-only metrics
    /// push set this to <c>false</c>.
    /// </summary>
    public bool EnablePrometheusExporter { get; init; } = true;
}
