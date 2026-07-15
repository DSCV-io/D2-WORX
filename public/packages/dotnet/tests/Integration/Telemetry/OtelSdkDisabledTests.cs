// -----------------------------------------------------------------------
// <copyright file="OtelSdkDisabledTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry;

using AwesomeAssertions;
using DcsvIo.D2.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

/// <summary>
/// End-to-end pin that the
/// <see cref="D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR"/>
/// short-circuits both <c>AddD2Telemetry</c> AND
/// <c>MapD2PrometheusEndpoint</c> at host build time. Symmetric kill
/// switch — neither side leaves a partial registration behind.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class OtelSdkDisabledTests
{
    [Fact]
    public async Task OtelSdkDisabled_NoMeterProvider_NoTracerProvider_NoMetricsRoute()
    {
        var prior = Environment.GetEnvironmentVariable(
            D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR);
        try
        {
            Environment.SetEnvironmentVariable(
                D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, "true");

            using var host = await BuildHostAsync();

            host.Services.GetService<MeterProvider>().Should().BeNull();
            host.Services.GetService<TracerProvider>().Should().BeNull();

            var client = host.GetTestClient();
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
    public async Task OtelSdkDisabledFalse_MeterProviderRegistered_MetricsRouteMapped()
    {
        var prior = Environment.GetEnvironmentVariable(
            D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR);
        try
        {
            Environment.SetEnvironmentVariable(
                D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, null);

            using var host = await BuildHostAsync();

            host.Services.GetService<MeterProvider>().Should().NotBeNull();

            var client = host.GetTestClient();
            var response = await client.GetAsync(new Uri("/metrics", UriKind.Relative));
            response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.NotFound);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, prior);
        }
    }

    private static async Task<IHost> BuildHostAsync()
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
                            opts => opts.ServiceName = "otel-disabled-tests");
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
}
