// -----------------------------------------------------------------------
// <copyright file="TelemetryTestHostBuilder.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;

using DcsvIo.D2.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/// <summary>
/// Static helper that builds a fully wired AspNetCore test host suitable
/// for driving the OpenTelemetry SDK end-to-end through
/// <c>Microsoft.AspNetCore.TestHost</c>. Each call gets its own
/// <see cref="InMemoryActivityExporter"/> /
/// <see cref="InMemoryMetricExporter"/> /
/// <see cref="InMemoryLogRecordExporter"/> so xUnit-parallel test classes
/// don't see each other's emissions; the host's local OpenTelemetry
/// pipeline writes to those exporters via the
/// <c>SimpleActivityExportProcessor</c> /
/// <c>BaseExportingMetricReader</c> / <c>SimpleLogRecordExportProcessor</c>
/// shapes (synchronous-flush so tests don't race).
/// </summary>
internal static class TelemetryTestHostBuilder
{
    /// <summary>
    /// Builds and starts an in-process AspNetCore host with
    /// <c>AddD2Telemetry</c> wired plus the three in-memory exporters
    /// attached. Returns the started host and the three exporters.
    /// </summary>
    /// <param name="configureOptions">
    /// Optional callback to mutate the lib's <see cref="D2TelemetryOptions"/>
    /// (e.g. set <c>OtlpTracesEndpoint</c> for the self-referential
    /// HttpClient filter test).
    /// </param>
    /// <param name="extraServices">
    /// Optional services-collection mutator invoked AFTER
    /// <c>AddD2Telemetry</c>.
    /// </param>
    /// <param name="extraConfigure">
    /// Optional middleware-pipeline mutator invoked AFTER
    /// <c>UseRouting</c> and BEFORE <c>UseEndpoints</c>.
    /// </param>
    /// <param name="extraEndpoints">
    /// Optional endpoint-registration mutator invoked alongside the
    /// default endpoints.
    /// </param>
    /// <returns>
    /// Tuple of the started host and three in-memory exporters.
    /// </returns>
    internal static async Task<TelemetryTestHostHandle> BuildAsync(
        Action<D2TelemetryOptions>? configureOptions = null,
        Action<IServiceCollection>? extraServices = null,
        Action<IApplicationBuilder>? extraConfigure = null,
        Action<IEndpointRouteBuilder>? extraEndpoints = null)
    {
        var activityExporter = new InMemoryActivityExporter();
        var metricExporter = new InMemoryMetricExporter();
        var logExporter = new InMemoryLogRecordExporter();

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();

                        services.AddD2Telemetry(
                            new ConfigurationBuilder().Build(),
                            opts =>
                            {
                                opts.ServiceName = "telemetry-tests";
                                configureOptions?.Invoke(opts);
                            });

                        // Attach the per-test in-memory exporters AFTER
                        // AddD2Telemetry so they ride alongside any OTLP
                        // exporters the lib registered via env-derived
                        // defaults. Synchronous processors so tests don't
                        // race the batch flush deadline.
                        services.ConfigureOpenTelemetryTracerProvider(tracing =>
                            tracing.AddProcessor(
                                new SimpleActivityExportProcessor(activityExporter)));

                        services.ConfigureOpenTelemetryMeterProvider(metrics =>
                            metrics.AddReader(new BaseExportingMetricReader(metricExporter)
                            {
                                TemporalityPreference = MetricReaderTemporalityPreference.Delta,
                            }));

                        services.Configure<OpenTelemetryLoggerOptions>(opts =>
                            opts.AddProcessor(
                                new SimpleLogRecordExportProcessor(logExporter)));

                        extraServices?.Invoke(services);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        extraConfigure?.Invoke(app);

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/api/echo", () => Results.Text("ok"));
                            endpoints.MapGet("/health", () => Results.Text("ok"));
                            endpoints.MapGet("/alive", () => Results.Text("ok"));
                            endpoints.MapGet(
                                "/.well-known/openid-configuration",
                                () => Results.Text("ok"));

                            endpoints.MapD2PrometheusEndpoint();

                            extraEndpoints?.Invoke(endpoints);
                        });
                    });
            });

        var host = await hostBuilder.StartAsync();
        return new TelemetryTestHostHandle(
            host,
            activityExporter,
            metricExporter,
            logExporter);
    }

    /// <summary>
    /// Forces all three providers to flush their pending exports so the
    /// in-memory captures are populated synchronously by the time the
    /// test inspects them.
    /// </summary>
    /// <param name="host">The host to flush.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when all flushes settle.</returns>
    internal static Task ForceFlushAsync(IHost host, CancellationToken ct = default)
    {
        var sp = host.Services;
        sp.GetService<TracerProvider>()?.ForceFlush(5000);
        sp.GetService<MeterProvider>()?.ForceFlush(5000);
        sp.GetService<LoggerFactory>()?.Dispose();
        return Task.CompletedTask;
    }
}
