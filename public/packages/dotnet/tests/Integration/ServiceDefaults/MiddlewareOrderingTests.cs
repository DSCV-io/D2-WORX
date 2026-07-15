// -----------------------------------------------------------------------
// <copyright file="MiddlewareOrderingTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ServiceDefaults;

using AwesomeAssertions;
using D2.Shared.ServiceDefaults;
using D2.Shared.Tests.Integration.ServiceDefaults.Infrastructure;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Pipeline-ordering tests — verify the LOCKED middleware order
/// documented on
/// <c>WebApplicationServiceDefaultsExtensions.UseD2DefaultPipeline</c>:
/// <c>UseD2SecurityHeaders</c> → <c>UseD2RequestLogging</c> →
/// <c>UseD2Cors</c> → <c>UseRouting</c> → <c>UseD2InfrastructureBypass</c>
/// → (auth chain conditional). Marker middleware installed via
/// <see cref="MiddlewareOrderRecorder"/> captures the request-time
/// execution sequence.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class MiddlewareOrderingTests
{
    [Fact]
    public async Task MiddlewareOrder_AfterPipelineMarker_RunsForBusinessPath_ButNotForInfra()
    {
        // Marker middleware installed AFTER UseD2DefaultPipeline records
        // "after-default-pipeline" — must fire for /probe (no short-circuit
        // on business paths) and NOT fire for /health (InfrastructureBypass
        // short-circuits).
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraConfigure: app =>
            {
                app.Use(async (ctx, next) =>
                {
                    var recorder = ctx.RequestServices
                        .GetRequiredService<MiddlewareOrderRecorder>();
                    recorder.Record("after-default-pipeline");
                    await next();
                });
            });
        var client = handle.Host.GetTestClient();

        // Business endpoint — marker IS recorded
        await client.GetAsync(new Uri("/probe", UriKind.Relative));
        handle.OrderRecorder.Entries.Should()
            .Contain("after-default-pipeline");
        handle.OrderRecorder.Reset();

        // Infrastructure endpoint — marker is NOT recorded (bypass
        // short-circuits BEFORE the marker runs)
        await client.GetAsync(new Uri("/health", UriKind.Relative));
        handle.OrderRecorder.Entries.Should()
            .NotContain("after-default-pipeline");
    }

    [Fact]
    public async Task MiddlewareOrder_AuthOptOut_HostBuildsWithoutAuthRegistrations()
    {
        // SkipAuthAutoWiring=true → UseD2DefaultPipeline branches around
        // the auth chain entirely. Verify the host builds + serves
        // requests cleanly (no NRE / RNE for missing JwtValidator).
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts => opts.SkipAuthAutoWiring = true);
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(new Uri("/probe", UriKind.Relative));

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public void MiddlewareOrder_AuthOptIn_RegistrationSucceeds()
    {
        // SkipAuthAutoWiring=false + AuthConfigure populated → the
        // service-collection registration succeeds (auth middleware
        // pipeline branch is taken). Full host startup with auth wired
        // additionally requires ITieredCache, which services compose
        // alongside auth.
        var services = new global::Microsoft.Extensions.DependencyInjection
            .ServiceCollection();
        var configuration = new global::Microsoft.Extensions.Configuration
            .ConfigurationBuilder().Build();
        services.AddRouting();

        var act = () => services.AddD2ServiceDefaults(
            configuration,
            opts =>
            {
                opts.SkipAuthAutoWiring = false;
                opts.AuthConfigure = a =>
                {
                    a.Issuer = new Uri("https://edge.test");
                    a.Audience = "edge";
                };
            });

        act.Should().NotThrow();
    }

    [Fact]
    public async Task MiddlewareOrder_SecurityHeaders_RunsBeforeRouting_HeadersOnInfraResponse()
    {
        // SecurityHeaders runs FIRST so OWASP defaults ship even on
        // infrastructure-bypass short-circuit responses.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        response.Headers.Contains("X-Frame-Options").Should().BeTrue();
    }

    [Fact]
    public async Task MiddlewareOrder_RequestLogging_RunsBeforeEndpoint_LineEmittedOnHandlerThrow()
    {
        // Request-logging runs BEFORE endpoint stages so endpoint throws
        // still produce a request-completion line. Catch-all middleware
        // swallows the throw because TestServer would otherwise propagate
        // it to the HttpClient and break the assertion.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            extraConfigure: app => app.Use(async (ctx, next) =>
            {
                try
                {
                    await next();
                }
                catch
                {
                    ctx.Response.StatusCode = 500;
                }
            }));
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/throw", UriKind.Relative));

        var hasRequestEvent = handle.Sink.Events.Any(e =>
        {
            if (!e.Properties.TryGetValue("SourceContext", out var sc))
                return false;
            return sc.ToString().Contains(
                "Serilog.AspNetCore.RequestLoggingMiddleware",
                StringComparison.Ordinal);
        });
        hasRequestEvent.Should().BeTrue();
    }

    [Fact]
    public async Task MiddlewareOrder_InfrastructureBypass_RunsAfterRouting_RoutingMatchedFirst()
    {
        // The bypass needs the routing-resolved endpoint on the context
        // to invoke its RequestDelegate; if bypass ran BEFORE routing the
        // endpoint would be null and the path would fall through to next().
        var downstreamInvoked = false;
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraConfigure: app =>
            {
                app.Use(async (_, next) =>
                {
                    downstreamInvoked = true;
                    await next();
                });
            });
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        response.IsSuccessStatusCode.Should().BeTrue();
        downstreamInvoked.Should().BeFalse(
            "the routing-then-bypass order must short-circuit before the "
            + "downstream marker runs.");
    }

    [Fact]
    public async Task MiddlewareOrder_DefaultPipeline_NonInfraPath_PassesThroughEveryStage()
    {
        // Non-infra path runs through the full pipeline without any
        // locked-order middleware accidentally short-circuiting it.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(new Uri("/probe", UriKind.Relative));

        response.IsSuccessStatusCode.Should().BeTrue();
        (await response.Content.ReadAsStringAsync()).Should().Be("ok");
    }

    [Fact]
    public async Task MiddlewareOrder_OrderMarkerEndpoint_ExecutesAndAppendsToRecorder()
    {
        // Sanity-check the synthetic /order-marker-* endpoints reach the
        // recorder — pin the test infrastructure as a fixture.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/order-marker-A", UriKind.Relative));

        handle.OrderRecorder.Entries.Should().Contain("endpoint-A");
    }
}
