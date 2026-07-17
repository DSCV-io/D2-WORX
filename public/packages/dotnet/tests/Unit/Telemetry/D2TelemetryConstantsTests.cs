// -----------------------------------------------------------------------
// <copyright file="D2TelemetryConstantsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry;

using AwesomeAssertions;
using DcsvIo.D2.Telemetry;
using Xunit;

/// <summary>
/// Per-VALUE pinning for every public constant — the value IS the wire /
/// route / env-var contract operators consume from outside the codebase
/// (Loki / Tempo / Prometheus / Grafana dashboards / docker compose env
/// files). A rename of the symbol without an explicit value swap surfaces
/// as a compile break; an in-place value change without symbol rename
/// surfaces as a test failure here.
/// </summary>
public sealed class D2TelemetryConstantsTests
{
    [Fact]
    public void OtelServiceNameConfigKey_Value_IsExpected()
    {
        D2TelemetryConstants.OTEL_SERVICE_NAME_CONFIG_KEY
            .Should().Be("OTEL_SERVICE_NAME");
    }

    [Fact]
    public void OtlpTracesEndpointConfigKey_Value_IsExpected()
    {
        D2TelemetryConstants.OTLP_TRACES_ENDPOINT_CONFIG_KEY
            .Should().Be("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT");
    }

    [Fact]
    public void OtlpMetricsEndpointConfigKey_Value_IsExpected()
    {
        D2TelemetryConstants.OTLP_METRICS_ENDPOINT_CONFIG_KEY
            .Should().Be("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT");
    }

    [Fact]
    public void OtlpLogsEndpointConfigKey_Value_IsExpected()
    {
        D2TelemetryConstants.OTLP_LOGS_ENDPOINT_CONFIG_KEY
            .Should().Be("OTEL_EXPORTER_OTLP_LOGS_ENDPOINT");
    }

    [Fact]
    public void OtelSdkDisabledEnvVar_Value_IsExpected()
    {
        D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR
            .Should().Be("OTEL_SDK_DISABLED");
    }

    [Fact]
    public void PrometheusEndpointPath_Value_IsExpected()
    {
        D2TelemetryConstants.PROMETHEUS_ENDPOINT_PATH
            .Should().Be("/metrics");
    }

    [Fact]
    public void HealthEndpointPath_Value_IsExpected()
    {
        D2TelemetryConstants.HEALTH_ENDPOINT_PATH.Should().Be("/health");
    }

    [Fact]
    public void AliveEndpointPath_Value_IsExpected()
    {
        D2TelemetryConstants.ALIVE_ENDPOINT_PATH.Should().Be("/alive");
    }

    [Fact]
    public void WellKnownEndpointPath_Value_IsExpected()
    {
        D2TelemetryConstants.WELL_KNOWN_ENDPOINT_PATH.Should().Be("/.well-known");
    }
}
