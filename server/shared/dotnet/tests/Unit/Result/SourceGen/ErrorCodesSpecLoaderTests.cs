// -----------------------------------------------------------------------
// <copyright file="ErrorCodesSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Result.SourceGen;

using AwesomeAssertions;
using D2.Shared.ResultErrorCodes.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the generic ErrorCodes spec loader's JSON-shape
/// validation. Drives the loader directly (no Roslyn host) and asserts the
/// <c>EmitDiagnostic</c> records surfaced for malformed input.
/// </summary>
public sealed class ErrorCodesSpecLoaderTests
{
    private const string _PATH = "spec.json";

    [Fact]
    public void Load_ValidSpec_ReturnsPopulatedSpec()
    {
        var json = """
        {
          "errorCodes": [
            {
              "code": "NOT_FOUND",
              "httpStatus": 404,
              "doc": "Indicates that the requested resource was not found."
            }
          ]
        }
        """;

        var result = ErrorCodesSpecLoader.Load(_PATH, json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.ErrorCodes.Should().HaveCount(1);
        var entry = result.Spec.ErrorCodes[0];
        entry.Code.Should().Be("NOT_FOUND");
        entry.HttpStatus.Should().Be(404);
        entry.Doc.Should().Be("Indicates that the requested resource was not found.");
    }

    [Fact]
    public void Load_MalformedJson_ReturnsMalformedSpecDiagnostic()
    {
        var result = ErrorCodesSpecLoader.Load(_PATH, "{not valid json");

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_RootNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var result = ErrorCodesSpecLoader.Load(_PATH, "[]");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingErrorCodesArray_ReturnsMalformedSpecDiagnostic()
    {
        var result = ErrorCodesSpecLoader.Load(_PATH, "{}");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryMissingCode_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "errorCodes": [
            {
              "httpStatus": 404,
              "doc": "X"
            }
          ]
        }
        """;

        var result = ErrorCodesSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryHttpStatusNotNumber_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "errorCodes": [
            {
              "code": "NOT_FOUND",
              "httpStatus": "404",
              "doc": "X"
            }
          ]
        }
        """;

        var result = ErrorCodesSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryMissingDoc_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "errorCodes": [
            {
              "code": "NOT_FOUND",
              "httpStatus": 404
            }
          ]
        }
        """;

        var result = ErrorCodesSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        { "errorCodes": ["NOT_AN_OBJECT"] }
        """;

        var result = ErrorCodesSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EmptyErrorCodesArray_ReturnsEmptySpec()
    {
        // Loader does not enforce minItems - that's a higher-level concern.
        var result = ErrorCodesSpecLoader.Load(_PATH, """{ "errorCodes": [] }""");

        result.Diagnostic.Should().BeNull();
        result.Spec!.ErrorCodes.Should().BeEmpty();
    }
}
