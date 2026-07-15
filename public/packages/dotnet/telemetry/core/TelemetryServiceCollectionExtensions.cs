// -----------------------------------------------------------------------
// <copyright file="TelemetryServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry;

using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.Telemetry.Internal;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

/// <summary>
/// DI registration entry point for <see cref="DcsvIo.D2.Telemetry"/> —
/// wires the OpenTelemetry SDK (traces / metrics / logs) plus the OTLP
/// exporters (when their canonical env vars are set), the in-process
/// Prometheus exporter (when enabled), and the standard
/// AspNetCore + HttpClient + GrpcNetClient + Process + Runtime
/// instrumentations into the host's pipeline. Aggregates every shipped
/// shared lib's <see cref="System.Diagnostics.ActivitySource"/> and
/// <see cref="System.Diagnostics.Metrics.Meter"/> registrations through
/// <see cref="Internal.AggregatedTelemetrySources"/>.
/// </summary>
public static class TelemetryServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the D² OpenTelemetry pipeline (traces + metrics +
        /// logs), the OTLP exporters when their canonical env vars are
        /// truthy, the in-process Prometheus exporter when
        /// <see cref="D2TelemetryOptions.EnablePrometheusExporter"/> is
        /// <c>true</c>, and the standard auto-instrumentations.
        /// Idempotent at the IServiceCollection level — calling twice
        /// doesn't throw, but the second call's options stack via the
        /// standard <c>IOptions</c> pipeline.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <see cref="D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR"/>
        /// is set to <c>"true"</c> (case-insensitive), this method
        /// short-circuits — no OpenTelemetry providers / exporters /
        /// instrumentations are registered, and consumers MUST NOT rely on
        /// resolving <c>MeterProvider</c> / <c>TracerProvider</c> from DI
        /// after the call returns. The
        /// <see cref="WebApplicationTelemetryExtensions.MapD2PrometheusEndpoint"/>
        /// extension applies the same gate symmetrically.
        /// </para>
        /// <para>
        /// Reads <see cref="D2TelemetryOptions.ServiceName"/> default from
        /// the
        /// <see cref="D2TelemetryConstants.OTEL_SERVICE_NAME_CONFIG_KEY"/>
        /// config value, then from
        /// <see cref="IHostEnvironment.ApplicationName"/>. Reads each OTLP
        /// endpoint default from its corresponding canonical env var.
        /// Configure-callback values override the defaults.
        /// </para>
        /// <para>
        /// Validates options at the first
        /// <c>IOptions&lt;D2TelemetryOptions&gt;.Value</c> resolution via
        /// <c>ValidateOnStart()</c> — invalid configuration fails the
        /// host build, never propagates as a runtime telemetry-emission
        /// failure.
        /// </para>
        /// </remarks>
        /// <param name="configuration">
        /// The host's <see cref="IConfiguration"/>, used to source the
        /// canonical OTel env-var defaults.
        /// </param>
        /// <param name="configure">
        /// Optional configuration delegate applied AFTER the env-derived
        /// defaults so callers can override any field.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> for fluent chaining.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or
        /// <paramref name="configuration"/> is null.
        /// </exception>
        public IServiceCollection AddD2Telemetry(
            IConfiguration configuration,
            Action<D2TelemetryOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            // Honor the OTel canonical kill-switch BEFORE any registration.
            // Returning the unmutated services collection keeps the
            // contract: callers can chain freely; resolving providers the
            // lib didn't register surfaces as a normal DI miss instead of
            // an unexpected NullReferenceException at host startup.
            if (OtelSdkDisabledGate.IsDisabled())
                return services;

            services.AddOptions<D2TelemetryOptions>()
                .Configure<IServiceProvider>((opts, sp) =>
                {
                    var env = sp.GetService<IHostEnvironment>();

                    if (opts.ServiceName.Falsey())
                    {
                        var fromConfig = configuration[
                            D2TelemetryConstants.OTEL_SERVICE_NAME_CONFIG_KEY]
                            ?.ToNullIfEmpty();
                        opts.ServiceName = fromConfig.Truthy()
                            ? fromConfig
                            : env?.ApplicationName;
                    }

                    if (opts.OtlpTracesEndpoint.Falsey())
                    {
                        opts.OtlpTracesEndpoint = configuration[
                            D2TelemetryConstants.OTLP_TRACES_ENDPOINT_CONFIG_KEY]
                            ?.ToNullIfEmpty();
                    }

                    if (opts.OtlpMetricsEndpoint.Falsey())
                    {
                        opts.OtlpMetricsEndpoint = configuration[
                            D2TelemetryConstants.OTLP_METRICS_ENDPOINT_CONFIG_KEY]
                            ?.ToNullIfEmpty();
                    }

                    if (opts.OtlpLogsEndpoint.Falsey())
                    {
                        opts.OtlpLogsEndpoint = configuration[
                            D2TelemetryConstants.OTLP_LOGS_ENDPOINT_CONFIG_KEY]
                            ?.ToNullIfEmpty();
                    }

                    configure?.Invoke(opts);
                })
                .Validate(
                    o => o.ServiceName.Truthy(),
                    "D2TelemetryOptions.ServiceName must be set (via "
                    + "OTEL_SERVICE_NAME, IHostEnvironment.ApplicationName, "
                    + "or the configure callback).")
                .Validate(
                    o => o.InstrumentationExcludedPaths.Count > 0,
                    "D2TelemetryOptions.InstrumentationExcludedPaths must "
                    + "contain at least one entry.")
                .Validate(
                    o => o.InstrumentationExcludedPaths.All(p => p.Truthy()),
                    "D2TelemetryOptions.InstrumentationExcludedPaths entries "
                    + "must not be empty / whitespace.")
                .Validate(
                    o => o.AdditionalActivitySources.All(s => s.Truthy()),
                    "D2TelemetryOptions.AdditionalActivitySources entries "
                    + "must not be empty / whitespace.")
                .Validate(
                    o => o.AdditionalMeters.All(m => m.Truthy()),
                    "D2TelemetryOptions.AdditionalMeters entries must not "
                    + "be empty / whitespace.")
                .Validate(
                    o => IsValidUriOrNull(o.OtlpTracesEndpoint),
                    "D2TelemetryOptions.OtlpTracesEndpoint must be a "
                    + "well-formed absolute URI when set.")
                .Validate(
                    o => IsValidUriOrNull(o.OtlpMetricsEndpoint),
                    "D2TelemetryOptions.OtlpMetricsEndpoint must be a "
                    + "well-formed absolute URI when set.")
                .Validate(
                    o => IsValidUriOrNull(o.OtlpLogsEndpoint),
                    "D2TelemetryOptions.OtlpLogsEndpoint must be a "
                    + "well-formed absolute URI when set.")
                .ValidateOnStart();

            // Resolve the eager defaults that the OTel SDK builders need
            // at registration time (before IOptions resolution). The
            // pattern mirrors DcsvIo.D2.Logging — walk ServiceDescriptors
            // for the host environment without materializing a temp
            // service provider.
            var serviceName = ResolveServiceName(services, configuration, configure);
            var tracesEndpoint = ResolveEndpoint(
                configuration,
                D2TelemetryConstants.OTLP_TRACES_ENDPOINT_CONFIG_KEY,
                opts => opts.OtlpTracesEndpoint,
                configure);
            var metricsEndpoint = ResolveEndpoint(
                configuration,
                D2TelemetryConstants.OTLP_METRICS_ENDPOINT_CONFIG_KEY,
                opts => opts.OtlpMetricsEndpoint,
                configure);
            var logsEndpoint = ResolveEndpoint(
                configuration,
                D2TelemetryConstants.OTLP_LOGS_ENDPOINT_CONFIG_KEY,
                opts => opts.OtlpLogsEndpoint,
                configure);
            var resolvedOptions = ResolveOptionsSnapshot(configure);

            // Logs go through the MEL pipeline. AddLogging is idempotent
            // (TryAdd internally) so calling it here is safe whether the
            // host already wired it or not. AddOpenTelemetry on
            // ILoggingBuilder registers an OpenTelemetryLoggerProvider
            // which Serilog's writeToProviders: true (set by
            // DcsvIo.D2.Logging) routes through automatically.
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddOpenTelemetry(logging =>
                {
                    logging.IncludeFormattedMessage = true;
                    logging.IncludeScopes = true;

                    if (logsEndpoint.Truthy())
                    {
                        logging.AddOtlpExporter(otlp =>
                        {
                            otlp.Endpoint = new Uri(logsEndpoint!);
                            otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
                        });
                    }
                });
            });

            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(serviceName ?? "unknown");

            services.AddOpenTelemetry()
                .ConfigureResource(rb => rb.AddService(serviceName ?? "unknown"))
                .WithTracing(tracing =>
                {
                    foreach (var sourceName in AggregatedTelemetrySources.SR_ActivitySourceNames)
                        tracing.AddSource(sourceName);

                    foreach (var extra in resolvedOptions.AdditionalActivitySources)
                        tracing.AddSource(extra);

                    tracing.SetResourceBuilder(resourceBuilder);

                    if (resolvedOptions.EnableAspNetCoreInstrumentation)
                    {
                        tracing.AddAspNetCoreInstrumentation(opts =>
                        {
                            opts.Filter = ctx => !InfrastructurePathMatcher.IsInfrastructurePath(
                                ctx.Request.Path,
                                resolvedOptions.InstrumentationExcludedPaths);
                            opts.RecordException = true;
                        });
                    }

                    if (resolvedOptions.EnableHttpClientInstrumentation)
                    {
                        tracing.AddHttpClientInstrumentation(opts =>
                        {
                            opts.RecordException = true;
                            opts.FilterHttpRequestMessage = msg =>
                                !IsSelfReferentialOtlpRequest(
                                    msg.RequestUri,
                                    tracesEndpoint,
                                    logsEndpoint,
                                    metricsEndpoint);
                            opts.EnrichWithHttpRequestMessage = (activity, msg) =>
                            {
                                // Strip query strings from any URI tags the SDK
                                // may set on this activity. v1 baseline applied
                                // a similar discipline to keep PII (potential
                                // query-string user input) out of span tags
                                // regardless of upstream SDK changes to default
                                // capture behavior.
                                StripUrlFullQueryString(activity, msg.RequestUri);
                            };
                        });
                    }

                    if (resolvedOptions.EnableGrpcNetClientInstrumentation)
                        tracing.AddGrpcClientInstrumentation();

                    if (tracesEndpoint.Truthy())
                    {
                        tracing.AddOtlpExporter(otlp =>
                        {
                            otlp.Endpoint = new Uri(tracesEndpoint!);
                            otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
                        });
                    }
                })
                .WithMetrics(metrics =>
                {
                    foreach (var meterName in AggregatedTelemetrySources.SR_MeterNames)
                        metrics.AddMeter(meterName);

                    foreach (var extra in resolvedOptions.AdditionalMeters)
                        metrics.AddMeter(extra);

                    metrics.SetResourceBuilder(resourceBuilder);

                    if (resolvedOptions.EnableAspNetCoreInstrumentation)
                        metrics.AddAspNetCoreInstrumentation();

                    if (resolvedOptions.EnableHttpClientInstrumentation)
                        metrics.AddHttpClientInstrumentation();

                    if (resolvedOptions.EnableProcessInstrumentation)
                        metrics.AddProcessInstrumentation();

                    if (resolvedOptions.EnableRuntimeInstrumentation)
                        metrics.AddRuntimeInstrumentation();

                    if (resolvedOptions.EnablePrometheusExporter)
                        metrics.AddPrometheusExporter();

                    if (metricsEndpoint.Truthy())
                    {
                        metrics.AddOtlpExporter(otlp =>
                        {
                            otlp.Endpoint = new Uri(metricsEndpoint!);
                            otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
                        });
                    }
                });

            return services;
        }
    }

    /// <summary>
    /// Predicate used by the HttpClient instrumentation
    /// <c>FilterHttpRequestMessage</c> callback to suppress span emission
    /// for outbound calls whose URI starts with any of the configured
    /// OTLP exporter endpoints — prevents infinite-loop instrumentation
    /// (<c>HttpClient → OTLP → HttpClient → OTLP → ...</c>). Internal
    /// for direct unit-test coverage; production code never calls it
    /// directly outside the wired callback.
    /// </summary>
    /// <param name="requestUri">The outbound request URI.</param>
    /// <param name="tracesEndpoint">The configured OTLP traces endpoint.</param>
    /// <param name="logsEndpoint">The configured OTLP logs endpoint.</param>
    /// <param name="metricsEndpoint">The configured OTLP metrics endpoint.</param>
    /// <returns>
    /// <c>true</c> when the URI is a self-referential OTLP call;
    /// otherwise <c>false</c>.
    /// </returns>
    internal static bool IsSelfReferentialOtlpRequest(
        Uri? requestUri,
        string? tracesEndpoint,
        string? logsEndpoint,
        string? metricsEndpoint)
    {
        if (requestUri is null)
            return false;

        var absolute = requestUri.AbsoluteUri;

        if (StartsWith(absolute, tracesEndpoint))
            return true;

        if (StartsWith(absolute, logsEndpoint))
            return true;

        if (StartsWith(absolute, metricsEndpoint))
            return true;

        return false;

        static bool StartsWith(string target, string? prefix)
        {
            if (prefix.Falsey())
                return false;

            return target.StartsWith(prefix!, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool IsValidUriOrNull(string? value)
    {
        if (value.Falsey())
            return true;

        return Uri.TryCreate(value, UriKind.Absolute, out _);
    }

    private static string? ResolveServiceName(
        IServiceCollection services,
        IConfiguration configuration,
        Action<D2TelemetryOptions>? configure)
    {
        var probe = new D2TelemetryOptions();
        configure?.Invoke(probe);

        if (probe.ServiceName.Truthy())
            return probe.ServiceName;

        var fromConfig = configuration[
            D2TelemetryConstants.OTEL_SERVICE_NAME_CONFIG_KEY]?.ToNullIfEmpty();
        if (fromConfig.Truthy())
            return fromConfig;

        return TryGetHostEnvironment(services)?.ApplicationName;
    }

    private static string? ResolveEndpoint(
        IConfiguration configuration,
        string configKey,
        Func<D2TelemetryOptions, string?> selector,
        Action<D2TelemetryOptions>? configure)
    {
        var probe = new D2TelemetryOptions();
        configure?.Invoke(probe);

        var fromCallback = selector(probe);
        if (fromCallback.Truthy())
            return fromCallback;

        return configuration[configKey]?.ToNullIfEmpty();
    }

    private static D2TelemetryOptions ResolveOptionsSnapshot(
        Action<D2TelemetryOptions>? configure)
    {
        var snapshot = new D2TelemetryOptions();
        configure?.Invoke(snapshot);
        return snapshot;
    }

    private static IHostEnvironment? TryGetHostEnvironment(IServiceCollection services)
    {
        // Walk ServiceDescriptors directly per the same rationale as
        // LoggingServiceCollectionExtensions — at AddD2Telemetry time the
        // ServiceProvider has not been built yet and BuildServiceProvider
        // inside an extension method is the canonical "container leak"
        // anti-pattern.
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(IHostEnvironment)
                && descriptor.ImplementationInstance is IHostEnvironment env)
            {
                return env;
            }
        }

        return null;
    }

    private static void StripUrlFullQueryString(
        System.Diagnostics.Activity? activity,
        Uri? requestUri)
    {
        if (activity is null || requestUri is null)
            return;

        // Recompose URL without query/fragment so any downstream tag
        // emission that reads from the activity's request enricher reflects
        // path-only data. The SDK's default tags already strip the query
        // string in 1.15.x, but the explicit override locks the contract
        // so a future SDK regression doesn't silently leak query-string
        // PII (matches DcsvIo.D2.Logging's defense-in-depth posture for
        // RemoteIp on the request-completion log line).
        var sanitized = $"{requestUri.Scheme}://{requestUri.Host}"
            + (requestUri.IsDefaultPort ? string.Empty : $":{requestUri.Port}")
            + requestUri.AbsolutePath;
        activity.SetTag("url.full", sanitized);
    }
}
