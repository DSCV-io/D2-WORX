// -----------------------------------------------------------------------
// <copyright file="OtelSdkDisabledGateTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry.Internal;

using AwesomeAssertions;
using DcsvIo.D2.Telemetry;
using DcsvIo.D2.Telemetry.Internal;
using Xunit;

/// <summary>
/// Process-wide env-var mutation — every test wraps the mutation in a
/// try/finally that restores the prior value to avoid cross-test
/// contamination. Pinned into the <c>LogLoggerStaticState</c> collection
/// alongside the integration tests that touch the same env var.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class OtelSdkDisabledGateTests
{
    [Fact]
    public void IsDisabled_EnvUnset_ReturnsFalse()
    {
        WithEnvVar(null, () =>
        {
            OtelSdkDisabledGate.IsDisabled().Should().BeFalse();
        });
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("tRuE")]
    public void IsDisabled_EnvTrueCaseInsensitive_ReturnsTrue(string raw)
    {
        WithEnvVar(raw, () =>
        {
            OtelSdkDisabledGate.IsDisabled().Should().BeTrue();
        });
    }

    [Theory]
    [InlineData("false")]
    [InlineData("FALSE")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("disabled")]
    [InlineData("enabled")]
    [InlineData(" true")]
    [InlineData("true ")]
    public void IsDisabled_EnvAnyOtherValue_ReturnsFalse(string raw)
    {
        WithEnvVar(raw, () =>
        {
            OtelSdkDisabledGate.IsDisabled().Should().BeFalse();
        });
    }

    [Fact]
    public void IsDisabled_EnvEmptyString_ReturnsFalse()
    {
        WithEnvVar(string.Empty, () =>
        {
            OtelSdkDisabledGate.IsDisabled().Should().BeFalse();
        });
    }

    private static void WithEnvVar(string? value, Action body)
    {
        var name = D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR;
        var prior = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, value);
            body();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, prior);
        }
    }
}
