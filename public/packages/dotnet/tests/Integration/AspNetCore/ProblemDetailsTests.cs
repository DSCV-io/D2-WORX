// -----------------------------------------------------------------------
// <copyright file="ProblemDetailsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore;

using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.Headers.Http;
using DcsvIo.D2.ProblemDetails;
using DcsvIo.D2.Tests.Integration.AspNetCore.Infrastructure;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.AspNetCore.Diagnostics;
using global::Microsoft.AspNetCore.Http;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class ProblemDetailsTests
{
    [Fact]
    public async Task UnhandledException_ResponseBodyMatchesRfc7807()
    {
        using var host = await BuildHostAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/throw");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetInt32().Should().Be(500);

        doc.RootElement.TryGetProperty(D2ProblemDetailsKeys.EXTENSION_TRACE_ID, out var traceId)
            .Should().BeTrue();
        traceId.GetString().Should().NotBeNullOrEmpty();

        doc.RootElement.TryGetProperty(
            D2ProblemDetailsKeys.EXTENSION_CORRELATION_ID, out var correlationId)
            .Should().BeTrue();
        correlationId.GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RequestWithCorrelationId_BodyAndResponseHeaderEchoSameValue()
    {
        using var host = await BuildHostAsync();

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/throw");
        request.Headers.Add(HttpHeaders.CORRELATION_ID, "my-correlation-id");

        var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty(D2ProblemDetailsKeys.EXTENSION_CORRELATION_ID).GetString()
            .Should().Be("my-correlation-id");

        response.Headers.GetValues(HttpHeaders.CORRELATION_ID)
            .Should().ContainSingle().Which.Should().Be("my-correlation-id");
    }

    [Fact]
    public async Task RequestWithoutCorrelationId_GeneratesGuidAndEchoesIt()
    {
        using var host = await BuildHostAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/throw");

        response.Headers.Contains(HttpHeaders.CORRELATION_ID)
            .Should().BeTrue();
        var generated = response.Headers
            .GetValues(HttpHeaders.CORRELATION_ID)
            .Single();

        // 32-char hex GUID (.ToString("N") format)
        generated.Should().HaveLength(32);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty(D2ProblemDetailsKeys.EXTENSION_CORRELATION_ID).GetString()
            .Should().Be(generated);
    }

    [Fact]
    public async Task RequestWithOverlongCorrelationId_TreatedAsAbsentAndGuidGenerated()
    {
        using var host = await BuildHostAsync();

        var client = host.GetTestClient();
        var oversized = new string('x', D2AspNetCoreConstants.MAX_CORRELATION_ID_LENGTH + 1);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/throw");
        request.Headers.Add(HttpHeaders.CORRELATION_ID, oversized);

        var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var resolved = doc.RootElement
            .GetProperty(D2ProblemDetailsKeys.EXTENSION_CORRELATION_ID).GetString();
        resolved.Should().HaveLength(32);   // generated GUID, not the oversized value
        resolved.Should().NotBe(oversized);
    }

    [Fact]
    public async Task ResponseBody_DoesNotContainExceptionMessage()
    {
        using var host = await BuildHostAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/throw");

        var body = await response.Content.ReadAsStringAsync();

        // Plan §3.7 / Plan PII discipline regression: response body must
        // not include the exception message text.
        body.Should().NotContain("Synthetic test failure (do not log).");
    }

    [Fact]
    public async Task InstanceField_PopulatedWithMethodAndPath()
    {
        using var host = await BuildHostAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/throw");

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("instance", out var instance).Should().BeTrue();
        instance.GetString().Should().Be("GET /throw");
    }

    private static async Task<global::Microsoft.Extensions.Hosting.IHost> BuildHostAsync()
    {
        return await AspNetCoreTestHostBuilder.BuildAsync(
            extraServices: services =>
            {
                services.AddD2ProblemDetails();
                services.AddExceptionHandler<ThrowingExceptionHandler>();
            },
            extraConfigure: app => app.UseExceptionHandler(),
            extraEndpoints: endpoints =>
            {
                endpoints.MapGet("/throw", ThrowAlways);
            });
    }

    private static IResult ThrowAlways() =>
        throw new InvalidOperationException("Synthetic test failure (do not log).");

    /// <summary>
    /// Exception handler that triggers ASP.NET Core's IProblemDetailsService
    /// pipeline (which the D² customizer plugs into) for all exceptions.
    /// </summary>
    private sealed class ThrowingExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var problemDetailsService = httpContext.RequestServices
                .GetRequiredService<IProblemDetailsService>();
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails =
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal Server Error",
                    Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
                },
            });
        }
    }
}
