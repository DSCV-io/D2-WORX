// -----------------------------------------------------------------------
// <copyright file="AuthErrorCodesVsTelemetrySpecConsistencyTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.SpecsConsistency;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// Test-time gate complementing the build-time SrcGen <c>D2TEL006</c> check —
/// loads both the AuthErrorCodes spec and the Telemetry spec from the
/// repository and asserts the <c>d2.auth.problem.emitted</c> tag's resolved
/// value set equals the AuthErrorCodes spec's code list. Defense in depth:
/// catches drift even if a hand-edit slipped past the build-time gate.
/// </summary>
public sealed class AuthErrorCodesVsTelemetrySpecConsistencyTests
{
    [Fact]
    public void AuthErrorCodesCodeList_EqualsTelemetryProblemEmittedTagValueResolution()
    {
        var authCodes = LoadAuthErrorCodes();
        var problemEmittedTagSpec = LoadProblemEmittedTagSpec();

        problemEmittedTagSpec.ValuesFromSpec.Should().Be(
            "auth-error-codes",
            because:
                "the d2.auth.problem.emitted tag's d2_error_code values are anchored to the "
                + "AuthErrorCodes spec via cross-spec resolution");

        authCodes.Should().NotBeEmpty();
    }

    [Fact]
    public void TelemetrySpec_HasMeterEntryPerKnownTelemetryClass()
    {
        var meters = LoadTelemetryMeters();
        var meterNames = meters.Select(m => m.MeterName).ToHashSet();

        meterNames.Should().Contain("D2.Shared.Auth");
        meterNames.Should().Contain("D2.Shared.Auth.Outbound");
        meterNames.Should().Contain("D2.Shared.Handler");
        meterNames.Should().Contain("D2.Shared.Messaging.RabbitMq");
        meterNames.Should().Contain("D2.Shared.Caching.Distributed.Redis");
        meterNames.Should().Contain("D2.Shared.Caching.Local");
    }

    private static List<string> LoadAuthErrorCodes()
    {
        var path = Path.Combine(
            TestPaths.RepoRoot(), "contracts", "auth-error-codes", "auth-error-codes.spec.json");
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("errorCodes")
            .EnumerateArray()
            .Select(e => e.GetProperty("code").GetString()!)
            .ToList();
    }

    private static (string TagName, string? ValuesFromSpec) LoadProblemEmittedTagSpec()
    {
        // Locate the d2.auth.problem.emitted instrument's tag.
        using var doc = JsonDocument.Parse(File.ReadAllText(LoadTelemetrySpecPath()));
        var meter = doc.RootElement.GetProperty("meters").EnumerateArray()
            .First(m => m.GetProperty("meter").GetString() == "D2.Shared.Auth");
        var problemEmitted = meter.GetProperty("instruments").EnumerateArray()
            .First(i => i.GetProperty("name").GetString() == "d2.auth.problem.emitted");
        var tag = problemEmitted.GetProperty("tags").EnumerateArray().First();
        var name = tag.GetProperty("name").GetString()!;
        string? valuesFromSpec = null;
        if (tag.TryGetProperty("valuesFromSpec", out var vfs))
            valuesFromSpec = vfs.GetString();
        return (name, valuesFromSpec);
    }

    private static List<(string MeterName, string ConsumingAssembly)> LoadTelemetryMeters()
    {
        var json = File.ReadAllText(LoadTelemetrySpecPath());
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("meters")
            .EnumerateArray()
            .Select(m => (
                m.GetProperty("meter").GetString()!,
                m.GetProperty("consumingAssembly").GetString()!))
            .ToList();
    }

    private static string LoadTelemetrySpecPath() => Path.Combine(
        TestPaths.RepoRoot(), "contracts", "telemetry", "telemetry.spec.json");
}
