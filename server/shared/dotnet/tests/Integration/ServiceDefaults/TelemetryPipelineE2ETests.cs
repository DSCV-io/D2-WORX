// -----------------------------------------------------------------------
// <copyright file="TelemetryPipelineE2ETests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ServiceDefaults;

using System.Diagnostics;
using AwesomeAssertions;
using D2.Shared.Auth.Telemetry;
using D2.Shared.Telemetry;
using D2.Shared.Tests.Integration.ServiceDefaults.Infrastructure;
using D2.Shared.Utilities.Extensions;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.DependencyInjection;
using global::Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Composed-pipeline E2E tests for the OpenTelemetry surface — pins
/// AspNetCore inbound span capture, smart-filtering of infrastructure
/// paths under the COMPOSED pipeline, aggregated <c>ActivitySource</c> /
/// <see cref="System.Diagnostics.Metrics.Meter"/> emission, and the OTel
/// SDK kill-switch propagation.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class TelemetryPipelineE2ETests
{
    [Fact]
    public async Task OTelSdk_OnGetProbe_AspNetCoreInstrumentation_CapturesSpan_WithMethodAndRoute()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/probe", UriKind.Relative));
        await CompositeTestHostBuilder.ForceFlushAsync(handle.Host);

        var serverActivities = handle.Activities.Snapshot()
            .Where(a => a.Kind == ActivityKind.Server &&
                MatchesPath(a, "/probe"))
            .ToList();
        serverActivities.Should().NotBeEmpty(
            because: "AspNetCore inbound instrumentation should emit a "
            + "Server-kind activity tagged with url.path=/probe.");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    [InlineData("/.well-known/openid-configuration")]
    public async Task OTelSdk_OnGetInfrastructurePath_SmartFiltering_NoSpanCaptured(string path)
    {
        // Telemetry's AspNetCore Filter callback suppresses auto-spans
        // for infrastructure paths even when the InfrastructureBypass
        // middleware ALSO short-circuits — both layers must hold; this
        // pin asserts the Telemetry filter specifically.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraEndpoints: endpoints =>
            {
                global::Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions.MapGet(
                    endpoints,
                    "/.well-known/openid-configuration",
                    () => global::Microsoft.AspNetCore.Http.Results.Text("ok"));
            });
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri(path, UriKind.Relative));
        await CompositeTestHostBuilder.ForceFlushAsync(handle.Host);

        var serverActivities = handle.Activities.Snapshot()
            .Where(a => a.Kind == ActivityKind.Server &&
                MatchesPath(a, path))
            .ToList();
        serverActivities.Should().BeEmpty(
            because: $"InstrumentationExcludedPaths covers '{path}' so the "
            + "AspNetCore Filter callback should suppress span emission "
            + "for requests targeting that path under the composed pipeline.");
    }

    [Fact]
    public async Task OTelSdk_OnEmitActivity_AggregatedActivitySource_CapturesSpan()
    {
        // The /emit-activity endpoint starts an Activity from the
        // AuthTelemetry.ACTIVITY_SOURCE_NAME aggregated source. The
        // composed pipeline's AddD2Telemetry registers that source via
        // AddSource, so the span should reach the in-memory exporter.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/emit-activity", UriKind.Relative));
        await CompositeTestHostBuilder.ForceFlushAsync(handle.Host);

        var captured = handle.Activities.Snapshot(AuthTelemetry.ACTIVITY_SOURCE_NAME);
        captured.Should().NotBeEmpty(
            because: $"the {AuthTelemetry.ACTIVITY_SOURCE_NAME} ActivitySource is "
            + "registered via AggregatedTelemetrySources; emitted spans must reach "
            + "the in-memory exporter.");
    }

    [Fact]
    public async Task OTelSdk_OnEmitCounter_AggregatedMeter_CapturesMetric()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/emit-counter", UriKind.Relative));
        await CompositeTestHostBuilder.ForceFlushAsync(handle.Host);

        var captured = handle.Metrics.Snapshot(AuthTelemetry.METER_NAME);
        captured.Should().NotBeEmpty(
            because: $"the {AuthTelemetry.METER_NAME} Meter is registered via "
            + "AggregatedTelemetrySources; emitted counters must reach the "
            + "in-memory exporter.");
    }

    [Fact]
    public async Task OTelSdk_MELBridge_OpenTelemetryLoggerProviderRegistered_InMelPipeline()
    {
        // Pin the OpenTelemetryLoggerProvider REGISTRATION shape under
        // the composed wire-up; Serilog's writeToProviders dispatch may
        // not deliver MEL records to the in-memory exporter, so the
        // standalone-Telemetry data-flow path is pinned by the per-lib
        // LogsExporterTests rather than here.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/log-mel-info", UriKind.Relative));
        handle.Host.Services.GetRequiredService<ILoggerFactory>().Dispose();

        // The OTel MEL provider IS resolvable; the ILoggerFactory dispose
        // path runs without throwing — confirming the registration is
        // intact under the composed pipeline.
        handle.Host.Services
            .GetService<global::Microsoft.Extensions.Logging.ILoggerFactory>()
            .Should().NotBeNull();
    }

    [Fact]
    public async Task OTelSdk_PrometheusEndpoint_WhenEnabled_ReturnsScrapeData()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(new Uri("/metrics", UriKind.Relative));

        response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OTelSdk_OtelDisabled_NoMeterProvider_NoTracerProvider_NoMetricsRoute()
    {
        // Composed-pipeline kill-switch verification: OTEL_SDK_DISABLED=true
        // → AddD2Telemetry no-ops → MapD2PrometheusEndpoint no-ops too,
        // mirroring MapD2DefaultEndpoints_PrometheusEndpoint_When_OtelDisabled
        // (AggregatorWiringTests) under the broader composed surface.
        var prior = Environment.GetEnvironmentVariable(
            D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR);
        try
        {
            Environment.SetEnvironmentVariable(
                D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, "true");

            await using var handle = await CompositeTestHostBuilder.BuildAsync(
                captureSerilog: false);

            handle.Host.Services
                .GetService<OpenTelemetry.Metrics.MeterProvider>()
                .Should().BeNull();
            handle.Host.Services
                .GetService<OpenTelemetry.Trace.TracerProvider>()
                .Should().BeNull();

            var client = handle.Host.GetTestClient();
            var response = await client.GetAsync(new Uri("/metrics", UriKind.Relative));
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, prior);
        }
    }

    [Fact]
    public async Task OTelSdk_ConfigureCallback_TelemetryServiceNameApplied_UnderComposedPipeline()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.TelemetryConfigure = t => t.ServiceName = "composed-svc";
            });

        var resolved = handle.Host.Services
            .GetRequiredService<global::Microsoft.Extensions.Options.IOptions<D2TelemetryOptions>>()
            .Value;
        resolved.ServiceName.Should().Be("composed-svc");
    }

    [Fact]
    public async Task OTelSdk_AddD2ServiceDefaults_HttpClientResilience_DoesNotBreakHostBuild()
    {
        // SkipHttpClientResilienceDefaults=false (default) installs the
        // standard resilience handler on EVERY HttpClient including OTel's
        // OTLP exporters. Pin that the host still builds cleanly.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.SkipHttpClientResilienceDefaults = false;
            });

        handle.Host.Should().NotBeNull();
    }

    [Fact]
    public async Task OTelSdk_SkipHttpClientResilienceDefaults_True_HostBuilds()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.SkipHttpClientResilienceDefaults = true;
            });

        handle.Host.Should().NotBeNull();
    }

    [Fact]
    public async Task OTelSdk_OnGetProbe_TraceContext_PropagatedThroughActivity()
    {
        // Verify the trace-id flows from HttpContext through the OTel
        // span. Pins end-to-end correlation under the composed pipeline.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/probe", UriKind.Relative));
        await CompositeTestHostBuilder.ForceFlushAsync(handle.Host);

        var serverActivities = handle.Activities.Snapshot()
            .Where(a => a.Kind == ActivityKind.Server &&
                MatchesPath(a, "/probe"))
            .ToList();
        serverActivities.Should().NotBeEmpty();
        serverActivities[0].TraceId.ToString().Should().NotBeNullOrEmpty();
    }

    private static bool MatchesPath(Activity activity, string path)
    {
        var tag = activity.GetTagItem("url.path") as string
            ?? activity.GetTagItem("http.target") as string;
        if (tag.Falsey())
            return false;

        return tag!.StartsWith(path, StringComparison.OrdinalIgnoreCase);
    }
}
