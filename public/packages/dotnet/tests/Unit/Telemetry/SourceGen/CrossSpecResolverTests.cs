// -----------------------------------------------------------------------
// <copyright file="CrossSpecResolverTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias TelemetryTagsSourceGen;

namespace DcsvIo.D2.Tests.Unit.Telemetry.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.Telemetry.Tags.SourceGen;
using Xunit;
using SpecFile = TelemetryTagsSourceGen::DcsvIo.D2.SourceGen.SpecFile;

/// <summary>
/// Tests for <see cref="CrossSpecResolver"/> — the build-time cross-spec
/// resolver that surfaces <c>D2TEL006</c> when the AuthErrorCodes spec
/// referenced via <c>valuesFromSpec=auth-error-codes</c> is missing or
/// malformed.
/// </summary>
public sealed class CrossSpecResolverTests
{
    [Fact]
    public void Resolve_ValidAuthErrorCodesSpec_ReturnsCodeList()
    {
        var siblingJson = """
        {
          "errorCodes": [
            { "code": "AUTH_A", "httpStatus": 401, "category": "validation_failure",
              "userMessageKey": "TK", "factoryName": "A", "doc": "A" },
            { "code": "AUTH_B", "httpStatus": 503, "category": "infrastructure_unavailable",
              "userMessageKey": "TK", "factoryName": "B", "doc": "B" }
          ]
        }
        """;
        var siblings = ImmutableArray.Create(
            new SpecFile("/x/auth-error-codes.spec.json", siblingJson));

        var (values, diag) = CrossSpecResolver.Resolve(
            "auth-error-codes", siblings, "x", "t", "M");

        diag.Should().BeNull();
        values.Should().BeEquivalentTo(new[] { "AUTH_A", "AUTH_B" });
    }

    [Fact]
    public void Resolve_UnknownSpecName_EmitsCrossSpecInconsistencyDiagnostic()
    {
        var (values, diag) = CrossSpecResolver.Resolve(
            "some-other-spec", ImmutableArray<SpecFile>.Empty, "x", "t", "M");

        values.Should().BeEmpty();
        diag.Should().NotBeNull();
        diag.DescriptorId.Should().Be(DiagnosticIds.CrossSpecInconsistency);
    }

    [Fact]
    public void
        Resolve_AuthErrorCodesSpecMissingFromSiblings_EmitsCrossSpecInconsistencyDiagnostic()
    {
        var (values, diag) = CrossSpecResolver.Resolve(
            "auth-error-codes",
            ImmutableArray<SpecFile>.Empty,
            "x",
            "t",
            "M");

        values.Should().BeEmpty();
        diag!.DescriptorId.Should().Be(DiagnosticIds.CrossSpecInconsistency);
    }

    [Fact]
    public void Resolve_AuthErrorCodesSpecMalformed_EmitsCrossSpecInconsistencyDiagnostic()
    {
        var siblings = ImmutableArray.Create(
            new SpecFile("/x/auth-error-codes.spec.json", "{not valid"));

        var (values, diag) = CrossSpecResolver.Resolve(
            "auth-error-codes", siblings, "x", "t", "M");

        values.Should().BeEmpty();
        diag!.DescriptorId.Should().Be(DiagnosticIds.CrossSpecInconsistency);
    }

    [Fact]
    public void
        Resolve_AuthErrorCodesSpecMissingErrorCodesArray_EmitsCrossSpecInconsistencyDiagnostic()
    {
        var siblings = ImmutableArray.Create(
            new SpecFile("/x/auth-error-codes.spec.json", "{}"));

        var (values, diag) = CrossSpecResolver.Resolve(
            "auth-error-codes", siblings, "x", "t", "M");

        values.Should().BeEmpty();
        diag!.DescriptorId.Should().Be(DiagnosticIds.CrossSpecInconsistency);
    }
}
