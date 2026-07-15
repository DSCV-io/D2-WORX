// -----------------------------------------------------------------------
// <copyright file="RequestLoggingMiddlewareTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Logging;

using AwesomeAssertions;
using DcsvIo.D2.Tests.Integration.Logging.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Serilog.Events;
using Xunit;

[Collection("LogLoggerStaticState")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestHost lifetime is bounded by individual tests.")]
public sealed class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task ApiEcho_EmitsInformationLevelRequestCompletionEvent()
    {
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync();
        using var hostScope = host;
        var client = host.GetTestClient();

        var response = await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        response.IsSuccessStatusCode.Should().BeTrue();
        var requestEvent = FindRequestLoggingEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Level.Should().Be(LogEventLevel.Information);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    [InlineData("/metrics")]
    [InlineData("/.well-known/openid-configuration")]
    public async Task InfrastructurePath_DoesNotEmitInformationLevelRequestEvent(string path)
    {
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync();
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri(path, UriKind.Relative));

        var requestEvent = FindRequestLoggingEvent(sink);

        // Either the Serilog middleware emitted at Verbose (matched but
        // wrong-level for our suppression contract) OR didn't emit at all
        // because the level filter dropped it before write — both shapes
        // satisfy the suppression contract. Pin: NO Information-level event.
        if (requestEvent is not null)
            requestEvent.Level.Should().NotBe(LogEventLevel.Information);
    }

    [Fact]
    public async Task ApiEcho_LogEventCarriesStaticEnrichmentFields()
    {
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync();
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestLoggingEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Keys.Should().Contain("RequestScheme");
        requestEvent.Properties.Keys.Should().Contain("UserAgent");
        requestEvent.Properties.Keys.Should().Contain("TraceId");
        requestEvent.Properties.Keys.Should().Contain("RequestHost");
    }

    [Fact]
    public async Task ApiEcho_LogEventNeverContainsRemoteIpRelatedKeys()
    {
        // Negative regression — pins the design decision that we do NOT log
        // any form of the connection-remote IP. Internal services see the
        // upstream Edge IP (footgun); Edge sees PII (a PII-handling path).
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync();
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var rendered = string.Join("\n", sink.Events.Select(sink.Render));
        rendered.Should().NotContain("\"RemoteIp\"");
        rendered.Should().NotContain("\"ClientIp\"");
        rendered.Should().NotContain("\"RemoteIpAddress\"");
    }

    [Fact]
    public async Task ApiEcho_RequestLogEventCarriesStatusCodeAndElapsedMs()
    {
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync();
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestLoggingEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Keys.Should().Contain("StatusCode");
        requestEvent.Properties.Keys.Should().Contain("Elapsed");
    }

    [Fact]
    public async Task ApiEcho_PathAndMethodPropertiesPopulatedFromRequest()
    {
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync();
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestLoggingEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties["RequestPath"].ToString().Should().Contain("/api/echo");
        requestEvent.Properties["RequestMethod"].ToString().Should().Contain("GET");
    }

    /// <summary>
    /// Selects the Serilog request-logging middleware's request-completion
    /// event from the sink — distinct from the MEL HostingApplicationDiagnostics
    /// event that AspNetCore emits in parallel (which uses different property
    /// names like <c>Method</c> vs <c>RequestMethod</c>).
    /// </summary>
    private static LogEvent? FindRequestLoggingEvent(InMemorySink sink) =>
        sink.Events.FirstOrDefault(e =>
            e.Properties.TryGetValue("SourceContext", out var sc)
            && sc.ToString().Contains("Serilog.AspNetCore.RequestLoggingMiddleware"));
}
