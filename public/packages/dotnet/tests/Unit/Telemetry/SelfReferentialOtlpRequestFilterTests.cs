// -----------------------------------------------------------------------
// <copyright file="SelfReferentialOtlpRequestFilterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry;

using AwesomeAssertions;
using DcsvIo.D2.Telemetry;
using Xunit;

/// <summary>
/// Pins the self-referential-OTLP-request filter predicate consumed by
/// <c>HttpClient</c>'s <c>FilterHttpRequestMessage</c> callback. Prevents
/// infinite-loop instrumentation: outbound calls whose URI starts with
/// any configured OTLP exporter endpoint are excluded from span emission
/// (otherwise <c>HttpClient → OTLP → HttpClient → OTLP → ...</c>).
/// </summary>
public sealed class SelfReferentialOtlpRequestFilterTests
{
    private const string _TRACES = "https://otlp.example.test:4318/v1/traces";
    private const string _LOGS = "https://otlp.example.test:4318/v1/logs";
    private const string _METRICS = "https://otlp.example.test:4318/v1/metrics";

    [Fact]
    public void Null_RequestUri_ReturnsFalse()
    {
        TelemetryServiceCollectionExtensions.IsSelfReferentialOtlpRequest(
            null, _TRACES, _LOGS, _METRICS).Should().BeFalse();
    }

    [Theory]
    [InlineData("https://otlp.example.test:4318/v1/traces")]
    [InlineData("https://otlp.example.test:4318/v1/traces/with/extra/path")]
    public void RequestUri_StartsWithTracesEndpoint_ReturnsTrue(string url)
    {
        TelemetryServiceCollectionExtensions.IsSelfReferentialOtlpRequest(
            new Uri(url), _TRACES, _LOGS, _METRICS).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://otlp.example.test:4318/v1/logs")]
    [InlineData("https://otlp.example.test:4318/v1/logs/append")]
    public void RequestUri_StartsWithLogsEndpoint_ReturnsTrue(string url)
    {
        TelemetryServiceCollectionExtensions.IsSelfReferentialOtlpRequest(
            new Uri(url), _TRACES, _LOGS, _METRICS).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://otlp.example.test:4318/v1/metrics")]
    [InlineData("https://otlp.example.test:4318/v1/metrics/post")]
    public void RequestUri_StartsWithMetricsEndpoint_ReturnsTrue(string url)
    {
        TelemetryServiceCollectionExtensions.IsSelfReferentialOtlpRequest(
            new Uri(url), _TRACES, _LOGS, _METRICS).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://api.example.com/users")]
    [InlineData("https://otlp.example.test:4318/v1/other-suffix")]
    [InlineData("https://different-host.example.test:4318/v1/traces")]
    public void RequestUri_DoesNotMatchAnyEndpoint_ReturnsFalse(string url)
    {
        TelemetryServiceCollectionExtensions.IsSelfReferentialOtlpRequest(
            new Uri(url), _TRACES, _LOGS, _METRICS).Should().BeFalse();
    }

    [Fact]
    public void AllEndpointsNull_ReturnsFalse()
    {
        TelemetryServiceCollectionExtensions.IsSelfReferentialOtlpRequest(
            new Uri("https://api.example.com/users"),
            tracesEndpoint: null,
            logsEndpoint: null,
            metricsEndpoint: null).Should().BeFalse();
    }

    [Fact]
    public void AllEndpointsEmpty_ReturnsFalse()
    {
        TelemetryServiceCollectionExtensions.IsSelfReferentialOtlpRequest(
            new Uri("https://api.example.com/users"),
            tracesEndpoint: string.Empty,
            logsEndpoint: string.Empty,
            metricsEndpoint: string.Empty).Should().BeFalse();
    }

    [Fact]
    public void AllEndpointsWhitespace_ReturnsFalse()
    {
        TelemetryServiceCollectionExtensions.IsSelfReferentialOtlpRequest(
            new Uri("https://api.example.com/users"),
            tracesEndpoint: "  ",
            logsEndpoint: "   ",
            metricsEndpoint: "    ").Should().BeFalse();
    }

    [Theory]
    [InlineData("HTTPS://OTLP.EXAMPLE.TEST:4318/V1/TRACES")]
    [InlineData("https://OTLP.EXAMPLE.TEST:4318/v1/TRACES")]
    public void Comparison_IsCaseInsensitive(string url)
    {
        TelemetryServiceCollectionExtensions.IsSelfReferentialOtlpRequest(
            new Uri(url), _TRACES, _LOGS, _METRICS).Should().BeTrue();
    }
}
