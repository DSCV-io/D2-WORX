// -----------------------------------------------------------------------
// <copyright file="AspNetCoreInstrumentationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry;

using AwesomeAssertions;
using DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.TestHost;
using Xunit;

/// <summary>
/// Pins the AspNetCore-instrumentation contract: business endpoints emit
/// inbound HTTP spans; infrastructure endpoints (
/// <c>/health</c>, <c>/alive</c>, <c>/metrics</c>,
/// <c>/.well-known/*</c>) are smart-filtered and emit ZERO spans.
/// Negative-regression tests force the implementation to walk the
/// suppression contract on every CI run.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class AspNetCoreInstrumentationTests
{
    [Fact]
    public async Task ApiEcho_Request_EmitsInboundSpanForRequestedPath()
    {
        await using var handle = await TelemetryTestHostBuilder.BuildAsync();
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(new Uri("/api/echo", UriKind.Relative));
        response.IsSuccessStatusCode.Should().BeTrue();

        await TelemetryTestHostBuilder.ForceFlushAsync(handle.Host);

        // Filter on the specific request path to insulate from any
        // cross-test span leakage through the OTel SDK's process-wide
        // listener (multiple integration tests sharing the same listener
        // collection sometimes see each other's emissions).
        var serverActivities = handle.Activities.Snapshot()
            .Where(a => a.Kind == System.Diagnostics.ActivityKind.Server &&
                MatchesPath(a, "/api/echo"))
            .ToList();

        serverActivities.Should().NotBeEmpty(
            because: "AspNetCore inbound instrumentation should emit a "
            + "Server-kind activity tagged with url.path=/api/echo.");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    [InlineData("/.well-known/openid-configuration")]
    public async Task InfrastructurePath_Request_EmitsZeroInboundSpansForThatPath(string path)
    {
        await using var handle = await TelemetryTestHostBuilder.BuildAsync();
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri(path, UriKind.Relative));

        await TelemetryTestHostBuilder.ForceFlushAsync(handle.Host);

        var serverActivities = handle.Activities.Snapshot()
            .Where(a => a.Kind == System.Diagnostics.ActivityKind.Server &&
                MatchesPath(a, path))
            .ToList();

        serverActivities.Should().BeEmpty(
            because: $"InstrumentationExcludedPaths covers '{path}' so the "
            + "AspNetCore Filter callback should suppress span emission "
            + "for requests targeting that path.");
    }

    private static bool MatchesPath(System.Diagnostics.Activity activity, string path)
    {
        var tag = activity.GetTagItem("url.path") as string
            ?? activity.GetTagItem("http.target") as string;
        if (tag.Falsey())
            return false;

        return tag!.StartsWith(path, StringComparison.OrdinalIgnoreCase);
    }
}
