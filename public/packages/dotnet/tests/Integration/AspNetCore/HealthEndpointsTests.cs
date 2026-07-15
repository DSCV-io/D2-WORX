// -----------------------------------------------------------------------
// <copyright file="HealthEndpointsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore;

using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.Tests.Integration.AspNetCore.Infrastructure;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.DependencyInjection;
using global::Microsoft.Extensions.Diagnostics.HealthChecks;
using global::Microsoft.Extensions.Hosting;
using Xunit;

public sealed class HealthEndpointsTests
{
    [Fact]
    public async Task HealthEndpoint_OnlySelfCheck_ReturnsHealthy()
    {
        using var host = await BuildHostAsync(extraChecks: null);

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task AliveEndpoint_OnlySelfCheck_ReturnsHealthy()
    {
        using var host = await BuildHostAsync(extraChecks: null);

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/alive");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task HealthEndpoint_CustomUnhealthyCheck_Returns503()
    {
        using var host = await BuildHostAsync(extraChecks: builder =>
        {
            builder.AddCheck("custom-unhealthy", () =>
                HealthCheckResult.Unhealthy("oops"));
        });

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/health");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task AliveEndpoint_CustomUnhealthyCheckNotTaggedLive_Still200()
    {
        using var host = await BuildHostAsync(extraChecks: builder =>
        {
            builder.AddCheck("custom-unhealthy", () =>
                HealthCheckResult.Unhealthy("oops"));   // no live tag
        });

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/alive");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AliveEndpoint_CustomCheckTaggedLive_Participates()
    {
        using var host = await BuildHostAsync(extraChecks: builder =>
        {
            builder.AddCheck(
                "custom-live-unhealthy",
                () => HealthCheckResult.Unhealthy("oops"),
                tags: [D2AspNetCoreConstants.LIVE_HEALTH_TAG]);
        });

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/alive");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private static async Task<IHost> BuildHostAsync(Action<IHealthChecksBuilder>? extraChecks)
    {
        return await AspNetCoreTestHostBuilder.BuildAsync(
            extraServices: services =>
            {
                services.AddD2HealthChecks();
                if (extraChecks is not null)
                {
                    var builder = services.AddHealthChecks();
                    extraChecks(builder);
                }
            },
            extraEndpoints: endpoints => endpoints.MapD2HealthEndpoints());
    }
}
