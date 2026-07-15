// -----------------------------------------------------------------------
// <copyright file="RealisticServiceSimulationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ServiceDefaults;

using System.Diagnostics;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Telemetry;
using DcsvIo.D2.Tests.Integration.ServiceDefaults.Infrastructure;
using DcsvIo.D2.Utilities.Extensions;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Realistic-service simulation E2E — drives the synthetic endpoint set
/// through the COMPOSED pipeline (Logging + Telemetry + AspNetCore +
/// ServiceDefaults aggregator) and asserts the cross-cutting observability
/// surfaces (span / log / metric) all capture coherently for a single
/// request flow. Exercises the fully integrated stack end-to-end so emergent
/// behaviors that only surface when all layers run together are pinned by tests.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class RealisticServiceSimulationTests
{
    [Fact]
    public async Task SyntheticService_GetEcho_ResponseBodyEchoes_QueryValue()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(
            new Uri("/echo?msg=hello-world", UriKind.Relative));

        response.IsSuccessStatusCode.Should().BeTrue();
        (await response.Content.ReadAsStringAsync()).Should().Be("hello-world");
    }

    [Fact]
    public async Task SyntheticService_GetEcho_FullPipelineExecutes_WithSpanAndSecurityHeaders()
    {
        // Full request → response with all observability surfaces
        // capturing: 200 OK + security headers + OTel server span.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync("https://localhost/echo?msg=ok");
        await CompositeTestHostBuilder.ForceFlushAsync(handle.Host);

        response.IsSuccessStatusCode.Should().BeTrue();
        response.Headers.Contains("X-Frame-Options").Should().BeTrue();

        var serverActivities = handle.Activities.Snapshot()
            .Where(a => a.Kind == ActivityKind.Server &&
                MatchesPath(a, "/echo"))
            .ToList();
        serverActivities.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SyntheticService_GetThrow_ProblemDetailsRendered_NoLeakedExceptionMessage()
    {
        // The composed pipeline does NOT auto-install UseExceptionHandler
        // (the aggregator stays minimal — services that want
        // ProblemDetails-on-throw wire UseExceptionHandler explicitly).
        // Wire it here for the realistic-simulation flavor.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraServices: services =>
                services.AddExceptionHandler<TestProblemDetailsExceptionHandler>(),
            extraConfigure: app => app.UseExceptionHandler());
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync("https://localhost/throw");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);
        response.Headers.Contains("X-Frame-Options").Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Synthetic /throw failure");
    }

    [Fact]
    public async Task SyntheticService_GetHealth_InfraPathSpanFiltered_AndShortCircuited()
    {
        // /health is an infrastructure path — Telemetry suppresses span
        // capture AND InfrastructureBypass short-circuits the pipeline.
        // Both layers verified end-to-end.
        var downstreamRan = false;
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraConfigure: app =>
            {
                global::Microsoft.AspNetCore.Builder.UseExtensions.Use(
                    app,
                    async (_, next) =>
                    {
                        downstreamRan = true;
                        await next();
                    });
            });
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/health", UriKind.Relative));
        await CompositeTestHostBuilder.ForceFlushAsync(handle.Host);

        downstreamRan.Should().BeFalse();
        var captured = handle.Activities.Snapshot()
            .Where(a => a.Kind == ActivityKind.Server &&
                MatchesPath(a, "/health"))
            .ToList();
        captured.Should().BeEmpty();
    }

    [Fact]
    public async Task SyntheticService_GetMetrics_PrometheusScrapeReturned()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(new Uri("/metrics", UriKind.Relative));

        response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SyntheticService_RequestPropagation_TraceIdFlowsFromHttpThroughActivity()
    {
        // Trace-id correlation under the composed pipeline. The OTel
        // server activity created by AspNetCore instrumentation carries
        // a non-empty TraceId derived from the W3C distributed tracing
        // context (or generated locally when no inbound traceparent
        // header is present).
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/echo?msg=trace-flow", UriKind.Relative));
        await CompositeTestHostBuilder.ForceFlushAsync(handle.Host);

        var serverActivities = handle.Activities.Snapshot()
            .Where(a => a.Kind == ActivityKind.Server &&
                MatchesPath(a, "/echo"))
            .ToList();
        serverActivities.Should().NotBeEmpty();
        serverActivities[0].TraceId.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SyntheticService_AggregatedSourceAndMeter_BothCapturedTogether()
    {
        // Pin both an aggregated-Activity emission AND an
        // aggregated-Meter emission round-trip cleanly through the
        // composed pipeline. Single-request-per-test pattern keeps
        // assertions deterministic.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/emit-activity", UriKind.Relative));
        await client.GetAsync(new Uri("/emit-counter", UriKind.Relative));
        await CompositeTestHostBuilder.ForceFlushAsync(handle.Host);

        handle.Activities.Snapshot(AuthTelemetry.ACTIVITY_SOURCE_NAME)
            .Should().NotBeEmpty();
        handle.Metrics.Snapshot(AuthTelemetry.METER_NAME)
            .Should().NotBeEmpty();
    }

    private static bool MatchesPath(Activity activity, string path)
    {
        var tag = activity.GetTagItem("url.path") as string
            ?? activity.GetTagItem("http.target") as string;
        if (tag.Falsey())
            return false;

        return tag!.StartsWith(path, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test-scope <see cref="global::Microsoft.AspNetCore.Diagnostics.IExceptionHandler"/>
    /// that triggers ASP.NET Core's
    /// <see cref="global::Microsoft.AspNetCore.Http.IProblemDetailsService"/>
    /// pipeline for unhandled exceptions from the composed test endpoints.
    /// </summary>
    private sealed class TestProblemDetailsExceptionHandler
        : global::Microsoft.AspNetCore.Diagnostics.IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            global::Microsoft.AspNetCore.Http.HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            httpContext.Response.StatusCode =
                (int)System.Net.HttpStatusCode.InternalServerError;
            var pdService = httpContext.RequestServices
                .GetRequiredService<global::Microsoft.AspNetCore.Http.IProblemDetailsService>();
            return await pdService.TryWriteAsync(
                new global::Microsoft.AspNetCore.Http.ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    ProblemDetails =
                    {
                        Status = (int)System.Net.HttpStatusCode.InternalServerError,
                        Title = "Internal Server Error",
                        Type =
                            "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
                    },
                });
        }
    }
}
