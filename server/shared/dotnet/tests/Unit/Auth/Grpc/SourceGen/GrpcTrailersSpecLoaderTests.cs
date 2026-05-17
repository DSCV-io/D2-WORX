// -----------------------------------------------------------------------
// <copyright file="GrpcTrailersSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Grpc.SourceGen;

using AwesomeAssertions;
using D2.Shared.Grpc.Trailers.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the GrpcTrailers spec loader's JSON-shape
/// validation. Drives the loader directly (no Roslyn host) and asserts the
/// <c>EmitDiagnostic</c> records surfaced for malformed input.
/// </summary>
public sealed class GrpcTrailersSpecLoaderTests
{
    private const string _PATH = "spec.json";

    [Fact]
    public void Load_ValidSpec_ReturnsPopulatedSpec()
    {
        var json = """
        {
          "trailers": [
            {
              "constName": "ERROR_CODE",
              "value": "d2_error_code",
              "doc": "Trailer key carrying the error code."
            }
          ]
        }
        """;

        var result = GrpcTrailersSpecLoader.Load(_PATH, json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Trailers.Should().HaveCount(1);
        var entry = result.Spec.Trailers[0];
        entry.ConstName.Should().Be("ERROR_CODE");
        entry.Value.Should().Be("d2_error_code");
        entry.Doc.Should().Be("Trailer key carrying the error code.");
    }

    [Fact]
    public void Load_MalformedJson_ReturnsMalformedSpecDiagnostic()
    {
        var result = GrpcTrailersSpecLoader.Load(_PATH, "{not valid json");

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_RootNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var result = GrpcTrailersSpecLoader.Load(_PATH, "[]");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingTrailersArray_ReturnsMalformedSpecDiagnostic()
    {
        var result = GrpcTrailersSpecLoader.Load(_PATH, "{}");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryMissingConstName_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "trailers": [
            {
              "value": "v",
              "doc": "d"
            }
          ]
        }
        """;

        var result = GrpcTrailersSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryMissingValue_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "trailers": [
            {
              "constName": "X",
              "doc": "d"
            }
          ]
        }
        """;

        var result = GrpcTrailersSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryMissingDoc_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "trailers": [
            {
              "constName": "X",
              "value": "v"
            }
          ]
        }
        """;

        var result = GrpcTrailersSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var json = """{ "trailers": ["NOT_AN_OBJECT"] }""";

        var result = GrpcTrailersSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EmptyTrailersArray_ReturnsEmptySpec()
    {
        // Loader does not enforce minItems — that's a higher-level concern.
        var result = GrpcTrailersSpecLoader.Load(_PATH, """{ "trailers": [] }""");

        result.Diagnostic.Should().BeNull();
        result.Spec!.Trailers.Should().BeEmpty();
    }
}
