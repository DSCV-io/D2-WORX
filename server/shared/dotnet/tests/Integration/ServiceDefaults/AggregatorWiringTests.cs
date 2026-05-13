// -----------------------------------------------------------------------
// <copyright file="AggregatorWiringTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ServiceDefaults;

using System.Net;
using AwesomeAssertions;
using D2.Shared.Auth.Validation;
using D2.Shared.Telemetry;
using D2.Shared.Tests.Integration.ServiceDefaults.Infrastructure;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// End-to-end smoke tests for the D² ServiceDefaults aggregator —
/// verifies the composed pipeline builds, requests flow through, the
/// default endpoints are reachable, security headers ship on every
/// response, and the OTel kill-switch propagates symmetrically through the
/// aggregator's <c>MapD2DefaultEndpoints</c>.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class AggregatorWiringTests
{
    [Fact]
    public async Task AddD2ServiceDefaults_HostBuilds_NoExceptions()
    {
        using var host = await ServiceDefaultsTestHostBuilder.BuildAsync();

        host.Should().NotBeNull();
    }

    [Fact]
    public async Task UseD2DefaultPipeline_RequestThroughEndpoint_ReturnsOk()
    {
        using var host = await ServiceDefaultsTestHostBuilder.BuildAsync();
        var client = host.GetTestClient();

        var response = await client.GetAsync(new Uri("/probe", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("ok");
    }

    [Fact]
    public async Task MapD2DefaultEndpoints_HealthEndpoint_ReturnsHealthy()
    {
        using var host = await ServiceDefaultsTestHostBuilder.BuildAsync();
        var client = host.GetTestClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task MapD2DefaultEndpoints_AliveEndpoint_ReturnsHealthy()
    {
        using var host = await ServiceDefaultsTestHostBuilder.BuildAsync();
        var client = host.GetTestClient();

        var response = await client.GetAsync(new Uri("/alive", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task MapD2DefaultEndpoints_PrometheusEndpoint_When_OtelDisabled_NotMapped()
    {
        var prior = Environment.GetEnvironmentVariable(
            D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR);
        try
        {
            Environment.SetEnvironmentVariable(
                D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, "true");

            using var host = await ServiceDefaultsTestHostBuilder.BuildAsync();
            var client = host.GetTestClient();

            var response = await client.GetAsync(
                new Uri("/metrics", UriKind.Relative));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, prior);
        }
    }

    [Fact]
    public async Task UseD2DefaultPipeline_SecurityHeaders_PresentOnHealthResponse()
    {
        // SecurityHeaders runs FIRST in the LOCKED middleware order so the
        // OWASP defaults ship even on infrastructure-bypass short-circuit
        // responses (and on the regular health-endpoint response).
        using var host = await ServiceDefaultsTestHostBuilder.BuildAsync();
        var client = host.GetTestClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.GetValues("X-Content-Type-Options")
            .Should().Contain("nosniff");
    }

    [Fact]
    public async Task UseD2DefaultPipeline_InfrastructureBypass_ShortCircuitsDownstreamMiddleware()
    {
        // Install a downstream middleware that THROWS if invoked. Health
        // endpoint should bypass it via UseD2InfrastructureBypass's default
        // short-circuit mode (path matches /health → invokes the routed
        // endpoint directly).
        var downstreamInvoked = false;

        using var host = await ServiceDefaultsTestHostBuilder.BuildAsync(
            extraConfigure: app =>
            {
                app.Use(async (_, next) =>
                {
                    downstreamInvoked = true;
                    await next();
                });
            });
        var client = host.GetTestClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        downstreamInvoked.Should().BeFalse(
            "the infrastructure-bypass middleware should short-circuit BEFORE "
            + "any downstream middleware is invoked for /health");
    }

    [Fact]
    public async Task AddD2ServiceDefaults_AuthSkipped_HostBuilds_AndJwtValidatorNotRegistered()
    {
        using var host = await ServiceDefaultsTestHostBuilder.BuildAsync(
            configureOptions: opts => opts.SkipAuthAutoWiring = true);

        host.Services.GetService<JwtValidator>().Should().BeNull();
    }
}
