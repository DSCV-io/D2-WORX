// -----------------------------------------------------------------------
// <copyright file="PrometheusEndpointIpRestrictionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry;

using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Xunit;

/// <summary>
/// Pins the IP-allow-list contract on the Prometheus scraping endpoint:
/// loopback → 200; RFC 1918 → 200; public IPv4 / IPv6 → 403.
/// Spoofs <c>HttpContext.Connection.RemoteIpAddress</c> via custom
/// middleware before the endpoint filter runs.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class PrometheusEndpointIpRestrictionTests
{
    [Fact]
    public async Task LoopbackRequest_ReturnsSuccess()
    {
        // TestServer leaves Connection.RemoteIpAddress null by default.
        // Spoof loopback explicitly so the IP filter sees a known-allowed
        // remote — equivalent to running against a real loopback socket
        // outside the in-process test harness.
        await using var handle = await TelemetryTestHostBuilder.BuildAsync(
            extraConfigure: app => UseRemoteIpSpoof(app, "127.0.0.1"));
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(new Uri("/metrics", UriKind.Relative));

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NullRemoteIp_ReturnsForbidden()
    {
        // TestServer's default null RemoteIpAddress mirrors the
        // "no connection IP available" production scenario the filter
        // must reject (fail-closed).
        await using var handle = await TelemetryTestHostBuilder.BuildAsync();
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(new Uri("/metrics", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.5.5")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    public async Task Rfc1918Request_ReturnsSuccess(string ip)
    {
        await using var handle = await TelemetryTestHostBuilder.BuildAsync(
            extraConfigure: app => UseRemoteIpSpoof(app, ip));
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(new Uri("/metrics", UriKind.Relative));

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("172.32.0.0")]
    [InlineData("192.169.0.0")]
    [InlineData("2001:4860:4860::8888")]
    public async Task PublicIpRequest_ReturnsForbidden(string ip)
    {
        await using var handle = await TelemetryTestHostBuilder.BuildAsync(
            extraConfigure: app => UseRemoteIpSpoof(app, ip));
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(new Uri("/metrics", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static void UseRemoteIpSpoof(IApplicationBuilder app, string ip)
    {
        app.Use(async (ctx, next) =>
        {
            ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            await next();
        });
    }
}
