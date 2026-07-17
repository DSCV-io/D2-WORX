// -----------------------------------------------------------------------
// <copyright file="TelemetrySpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.Telemetry.Tags.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the telemetry spec loader's JSON-shape validation.
/// </summary>
public sealed class TelemetrySpecLoaderTests
{
    private const string _PATH = "telemetry.spec.json";

    [Fact]
    public void Load_ValidSpec_ReturnsPopulatedSpec()
    {
        var json = """
        {
          "meters": [
            {
              "meter": "M.A",
              "consumingAssembly": "Asm.A",
              "instruments": [
                {
                  "name": "m.a.counter",
                  "kind": "counter",
                  "description": "A counter",
                  "tags": [
                    { "name": "outcome", "values": ["ok", "err"] }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var result = TelemetrySpecLoader.Load(_PATH, json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Meters.Should().HaveCount(1);
        var meter = result.Spec.Meters[0];
        meter.Meter.Should().Be("M.A");
        meter.ConsumingAssembly.Should().Be("Asm.A");
        meter.Instruments.Should().HaveCount(1);
        meter.Instruments[0].Name.Should().Be("m.a.counter");
        meter.Instruments[0].Kind.Should().Be("counter");
        meter.Instruments[0].Tags.Should().HaveCount(1);
        meter.Instruments[0].Tags[0].Values.Should().BeEquivalentTo(new[] { "ok", "err" });
    }

    [Fact]
    public void Load_MalformedJson_ReturnsMalformedSpecDiagnostic()
    {
        var result = TelemetrySpecLoader.Load(_PATH, "{not valid");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_TagWithBothValuesAndValuesFromSpec_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "meters": [
            {
              "meter": "M",
              "consumingAssembly": "A",
              "instruments": [
                {
                  "name": "x",
                  "kind": "counter",
                  "description": "x",
                  "tags": [
                    { "name": "t", "values": ["a"], "valuesFromSpec": "auth-error-codes" }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var result = TelemetrySpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_TagWithNeitherValuesNorValuesFromSpec_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "meters": [
            {
              "meter": "M",
              "consumingAssembly": "A",
              "instruments": [
                {
                  "name": "x",
                  "kind": "counter",
                  "description": "x",
                  "tags": [ { "name": "t" } ]
                }
              ]
            }
          ]
        }
        """;

        var result = TelemetrySpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_ValuesFromSpecOnly_LoadsSuccessfully()
    {
        var json = """
        {
          "meters": [
            {
              "meter": "M",
              "consumingAssembly": "A",
              "instruments": [
                {
                  "name": "x",
                  "kind": "counter",
                  "description": "x",
                  "tags": [ { "name": "t", "valuesFromSpec": "auth-error-codes" } ]
                }
              ]
            }
          ]
        }
        """;

        var result = TelemetrySpecLoader.Load(_PATH, json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Meters[0].Instruments[0].Tags[0].ValuesFromSpec
            .Should().Be("auth-error-codes");
    }
}
