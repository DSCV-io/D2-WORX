// -----------------------------------------------------------------------
// <copyright file="SecurityHeadersMiddlewareTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.Tests.Integration.AspNetCore.Infrastructure;
using global::Microsoft.AspNetCore.TestHost;
using Xunit;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task DefaultHeaders_PresentOnHttpsResponse()
    {
        using var host = await AspNetCoreTestHostBuilder.BuildAsync(
            extraConfigure: app => app.UseD2SecurityHeaders());

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/api/echo");

        response.Headers.Contains("X-Frame-Options").Should().BeTrue();
        response.Headers.GetValues("X-Frame-Options")
            .Should().ContainSingle().Which.Should().Be("DENY");
        response.Headers.GetValues("Referrer-Policy")
            .Should().ContainSingle().Which.Should().Be("strict-origin-when-cross-origin");
        response.Headers.GetValues("X-Permitted-Cross-Domain-Policies")
            .Should().ContainSingle().Which.Should().Be("none");
        response.Headers.GetValues("Cross-Origin-Resource-Policy")
            .Should().ContainSingle().Which.Should().Be("same-origin");
        response.Headers.GetValues("Cross-Origin-Opener-Policy")
            .Should().ContainSingle().Which.Should().Be("same-origin");

        // X-Content-Type-Options is a response header (sometimes sorted to
        // the content headers depending on the HttpClient handler — accept
        // either location).
        var hasOnResponse = response.Headers.Contains("X-Content-Type-Options");
        var hasOnContent = response.Content.Headers.Contains("X-Content-Type-Options");
        (hasOnResponse || hasOnContent).Should().BeTrue();
    }

    [Fact]
    public async Task Hsts_NotSetOnHttpResponse()
    {
        using var host = await AspNetCoreTestHostBuilder.BuildAsync(
            extraConfigure: app => app.UseD2SecurityHeaders());

        var client = host.GetTestClient();
        var response = await client.GetAsync("http://localhost/api/echo");

        response.Headers.Contains("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task Hsts_SetOnHttpsResponse_WithDefaultLiteral()
    {
        using var host = await AspNetCoreTestHostBuilder.BuildAsync(
            extraConfigure: app => app.UseD2SecurityHeaders());

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/api/echo");

        response.Headers.Contains("Strict-Transport-Security").Should().BeTrue();
        response.Headers.GetValues("Strict-Transport-Security")
            .Should().ContainSingle().Which.Should().Be(
                D2SecurityHeadersOptions.DEFAULT_STRICT_TRANSPORT_SECURITY);
    }

    [Fact]
    public async Task EmptyOverride_SuppressesHeader()
    {
        using var host = await AspNetCoreTestHostBuilder.BuildAsync(
            extraConfigure: app => app.UseD2SecurityHeaders(opts =>
            {
                opts.XFrameOptions = string.Empty;
            }));

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/api/echo");

        response.Headers.Contains("X-Frame-Options").Should().BeFalse();

        // Other defaults still applied.
        response.Headers.Contains("Referrer-Policy").Should().BeTrue();
    }

    [Fact]
    public async Task NonEmptyOverride_WritesOverrideLiteral()
    {
        using var host = await AspNetCoreTestHostBuilder.BuildAsync(
            extraConfigure: app => app.UseD2SecurityHeaders(opts =>
            {
                opts.XFrameOptions = "SAMEORIGIN";
            }));

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/api/echo");

        response.Headers.GetValues("X-Frame-Options")
            .Should().ContainSingle().Which.Should().Be("SAMEORIGIN");
    }
}
