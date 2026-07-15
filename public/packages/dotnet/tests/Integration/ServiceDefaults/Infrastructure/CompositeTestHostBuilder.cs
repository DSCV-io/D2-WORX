// -----------------------------------------------------------------------
// <copyright file="CompositeTestHostBuilder.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ServiceDefaults.Infrastructure;

using System.Collections.Generic;
using DcsvIo.D2.Logging.Destructuring;
using DcsvIo.D2.ServiceDefaults;
using DcsvIo.D2.Tests.Integration.Logging.Infrastructure;
using DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

/// <summary>
/// Wraps <see cref="ServiceDefaultsTestHostBuilder.BuildAsync"/> with the
/// per-test in-memory observability collectors used across the
/// synthetic-host integration suite — a per-test
/// <see cref="InMemorySink"/> for Serilog, the three OTel in-memory
/// exporters
/// (<see cref="InMemoryActivityExporter"/> /
/// <see cref="InMemoryMetricExporter"/> /
/// <see cref="InMemoryLogRecordExporter"/>), and the synthetic endpoint
/// mappers under <see cref="SyntheticEndpoints"/>. Centralizing the
/// wire-up here keeps individual test files focused on assertions, and
/// avoids 70+ duplicated wire-up blocks across the suite.
/// </summary>
internal static class CompositeTestHostBuilder
{
    /// <summary>
    /// Builds and starts an in-process AspNetCore test host wiring the full
    /// composed ServiceDefaults pipeline plus the in-memory observers and
    /// the synthetic endpoint set.
    /// </summary>
    /// <param name="configureOptions">
    /// Optional callback to mutate <see cref="D2ServiceDefaultsOptions"/>
    /// — same shape as <see cref="ServiceDefaultsTestHostBuilder.BuildAsync"/>.
    /// When null, defaults to <c>opts =&gt; opts.SkipAuthAutoWiring = true</c>.
    /// </param>
    /// <param name="extraServices">
    /// Optional services-collection mutator invoked AFTER
    /// <c>AddD2ServiceDefaults</c> (and AFTER the in-memory observer
    /// services are registered).
    /// </param>
    /// <param name="extraConfigure">
    /// Optional middleware-pipeline mutator invoked AFTER
    /// <c>UseD2DefaultPipeline</c> and BEFORE <c>UseEndpoints</c>.
    /// </param>
    /// <param name="extraEndpoints">
    /// Optional endpoint-registration mutator invoked alongside
    /// <c>MapD2DefaultEndpoints</c> + <c>MapSyntheticEndpoints</c>.
    /// </param>
    /// <param name="extraConfiguration">
    /// Optional in-memory configuration to layer beneath the host's
    /// configuration pipeline.
    /// </param>
    /// <param name="captureSerilog">
    /// When <c>true</c> (the default), pins <see cref="Log.Logger"/> to a
    /// per-test logger writing to a fresh <see cref="InMemorySink"/> so
    /// the Serilog request-completion middleware (which writes via the
    /// static facade) captures into the per-test sink. Tests that don't
    /// inspect the log output set this to <c>false</c> to avoid touching
    /// the static state.
    /// </param>
    /// <returns>
    /// A <see cref="CompositeTestHostHandle"/> exposing the host plus the
    /// captured observers.
    /// </returns>
    internal static async Task<CompositeTestHostHandle> BuildAsync(
        Action<D2ServiceDefaultsOptions>? configureOptions = null,
        Action<IServiceCollection>? extraServices = null,
        Action<IApplicationBuilder>? extraConfigure = null,
        Action<IEndpointRouteBuilder>? extraEndpoints = null,
        IDictionary<string, string?>? extraConfiguration = null,
        bool captureSerilog = true)
    {
        var sink = new InMemorySink();
        var activityExporter = new InMemoryActivityExporter();
        var metricExporter = new InMemoryMetricExporter();
        var logExporter = new InMemoryLogRecordExporter();
        var orderRecorder = new MiddlewareOrderRecorder();

        Serilog.ILogger? localLogger = null;
        if (captureSerilog)
        {
            // Mirror the per-host local-logger pin pattern from
            // LoggingTestHostBuilder so the Serilog request-logging
            // middleware (which writes via Log.Logger) captures into the
            // per-test sink. The [Collection("LogLoggerStaticState")]
            // attribute on calling test classes serializes against any
            // other test that touches the same static.
            localLogger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Destructure.With<RedactDataDestructuringPolicy>()
                .Enrich.FromLogContext()
                .WriteTo.Sink(sink, restrictedToMinimumLevel: LogEventLevel.Verbose)
                .CreateLogger();
            Log.Logger = localLogger;
        }

        var host = await ServiceDefaultsTestHostBuilder.BuildAsync(
            configureOptions: configureOptions,
            extraServices: services =>
            {
                services.AddSingleton(orderRecorder);

                // Re-pin Log.Logger AFTER AddD2Logging (which lives inside
                // AddD2ServiceDefaults) overwrites it with its own
                // Console-sink configuration. Without this re-pin the
                // request-completion events would not reach the per-test
                // sink.
                if (captureSerilog && localLogger is not null)
                    Log.Logger = localLogger;

                // Attach the per-test in-memory exporters AFTER the OTel
                // SDK is wired by AddD2Telemetry (inside
                // AddD2ServiceDefaults). Synchronous processors so tests
                // don't race the batch flush deadline.
                services.ConfigureOpenTelemetryTracerProvider(tracing =>
                    tracing.AddProcessor(
                        new SimpleActivityExportProcessor(activityExporter)));

                services.ConfigureOpenTelemetryMeterProvider(metrics =>
                    metrics.AddReader(new BaseExportingMetricReader(metricExporter)
                    {
                        TemporalityPreference =
                            MetricReaderTemporalityPreference.Delta,
                    }));

                services.Configure<OpenTelemetryLoggerOptions>(opts =>
                    opts.AddProcessor(
                        new SimpleLogRecordExportProcessor(logExporter)));

                extraServices?.Invoke(services);
            },
            extraConfigure: extraConfigure,
            extraEndpoints: endpoints =>
            {
                endpoints.MapSyntheticEndpoints();
                extraEndpoints?.Invoke(endpoints);
            },
            extraConfiguration: extraConfiguration);

        return new CompositeTestHostHandle(
            host,
            sink,
            activityExporter,
            metricExporter,
            logExporter,
            orderRecorder);
    }

    /// <summary>
    /// Forces the OTel providers to flush their pending exports so the
    /// in-memory captures are populated synchronously before the test
    /// inspects them. Mirrors
    /// <see cref="TelemetryTestHostBuilder.ForceFlushAsync"/>.
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
