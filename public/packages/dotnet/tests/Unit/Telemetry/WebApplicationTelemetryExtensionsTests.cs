// -----------------------------------------------------------------------
// <copyright file="WebApplicationTelemetryExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry;

using AwesomeAssertions;
using DcsvIo.D2.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// Lightweight unit coverage for <c>MapD2PrometheusEndpoint</c> — argument
/// guards + chaining + OTEL_SDK_DISABLED short-circuit + route-mapping
/// smoke. End-to-end runtime coverage (request emission, IP filter
/// behavior, Prometheus scrape body) lives in
/// <c>Integration.Telemetry.PrometheusEndpointIpRestrictionTests</c>.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class WebApplicationTelemetryExtensionsTests
{
    [Fact]
    public void MapD2PrometheusEndpoint_NullEndpoints_Throws()
    {
        IEndpointRouteBuilder? endpoints = null;

        var act = () => endpoints!.MapD2PrometheusEndpoint();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task MapD2PrometheusEndpoint_ReturnsSameEndpointsForChaining()
    {
        using var host = await BuildTestHostAsync(
            mapEndpoints: endpoints =>
            {
                var ret = endpoints.MapD2PrometheusEndpoint();

                ret.Should().BeSameAs(endpoints);
            });
    }

    [Fact]
    public async Task MapD2PrometheusEndpoint_OtelSdkDisabled_ShortCircuits_NoRouteMapped()
    {
        using var host = await BuildOtelDisabledHostAsync();

        var client = host.GetTestClient();

        var response = await client.GetAsync(new Uri("/metrics", UriKind.Relative));

        // No /metrics route mapped → 404.
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MapD2PrometheusEndpoint_NoOtelSdkDisabled_RouteMapped()
    {
        var prior = Environment.GetEnvironmentVariable(
            D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR);
        try
        {
            Environment.SetEnvironmentVariable(
                D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, null);

            using var host = await BuildTestHostAsync(
                mapEndpoints: endpoints => endpoints.MapD2PrometheusEndpoint());

            var client = host.GetTestClient();
            var response = await client.GetAsync(new Uri("/metrics", UriKind.Relative));

            // Loopback request from TestServer is allowed by the IP filter
            // → 200 (with the Prometheus exposition body) when configured;
            // the route is at least mapped (anything other than 404
            // confirms the route is present).
            response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.NotFound);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, prior);
        }
    }

    private static async Task<IHost> BuildTestHostAsync(
        Action<IEndpointRouteBuilder> mapEndpoints)
    {
        var prior = Environment.GetEnvironmentVariable(
            D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR);
        Environment.SetEnvironmentVariable(
            D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, null);

        try
        {
            var builder = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddRouting();
                            services.AddD2Telemetry(
                                new ConfigurationBuilder().Build(),
                                opts => opts.ServiceName = "telemetry-tests");
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseEndpoints(mapEndpoints);
                        });
                });

            return await builder.StartAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, prior);
        }
    }

    private static async Task<IHost> BuildOtelDisabledHostAsync()
    {
        var prior = Environment.GetEnvironmentVariable(
            D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR);
        Environment.SetEnvironmentVariable(
            D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, "true");

        try
        {
            var builder = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddRouting();
                            services.AddD2Telemetry(
                                new ConfigurationBuilder().Build(),
                                opts => opts.ServiceName = "telemetry-tests");
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseEndpoints(endpoints =>
                                endpoints.MapD2PrometheusEndpoint());
                        });
                });

            return await builder.StartAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, prior);
        }
    }
}
