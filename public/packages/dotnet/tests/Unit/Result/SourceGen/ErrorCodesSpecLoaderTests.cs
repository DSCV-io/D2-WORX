// -----------------------------------------------------------------------
// <copyright file="ErrorCodesSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias ResultErrorCodesSourceGen;

namespace DcsvIo.D2.Tests.Unit.Result.SourceGen;

using AwesomeAssertions;
using Xunit;
using DiagnosticIds = ResultErrorCodesSourceGen::DcsvIo.D2.ResultErrorCodes.SourceGen.DiagnosticIds;
using ErrorCodeSpecLoader =
    ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.ErrorCodeSpecLoader;

/// <summary>
/// Pure-logic tests for the shared error-codes spec loader's JSON-shape
/// validation driven with the generic catalog's diagnostic id. Drives the
/// loader directly (no Roslyn host) and asserts the <c>EmitDiagnostic</c>
/// records surfaced for malformed input.
/// </summary>
public sealed class ErrorCodesSpecLoaderTests
{
    private const string _PATH = "spec.json";

    private static string MalformedSpecId => DiagnosticIds.MalformedSpec;

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

        var result = ErrorCodeSpecLoader.Load(_PATH, json, MalformedSpecId);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec.ErrorCodes.Should().HaveCount(1);
        var entry = result.Spec.ErrorCodes[0];
        entry.Code.Should().Be("NOT_FOUND");
        entry.HttpStatus.Should().Be(404);
        entry.Doc.Should().Be("Indicates that the requested resource was not found.");

        // The generic catalog omits the factory fields entirely.
        entry.Category.Should().BeNull();
        entry.FactoryShape.Should().BeNull();
    }

    [Fact]
    public void Load_MalformedJson_ReturnsMalformedSpecDiagnostic()
    {
        var result = ErrorCodeSpecLoader.Load(_PATH, "{not valid json", MalformedSpecId);

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_RootNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var result = ErrorCodeSpecLoader.Load(_PATH, "[]", MalformedSpecId);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingErrorCodesArray_ReturnsMalformedSpecDiagnostic()
    {
        var result = ErrorCodeSpecLoader.Load(_PATH, "{}", MalformedSpecId);

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

        var result = ErrorCodeSpecLoader.Load(_PATH, json, MalformedSpecId);

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

        var result = ErrorCodeSpecLoader.Load(_PATH, json, MalformedSpecId);

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

        var result = ErrorCodeSpecLoader.Load(_PATH, json, MalformedSpecId);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        { "errorCodes": ["NOT_AN_OBJECT"] }
        """;

        var result = ErrorCodeSpecLoader.Load(_PATH, json, MalformedSpecId);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EmptyErrorCodesArray_ReturnsEmptySpec()
    {
        // Loader does not enforce minItems - that's a higher-level concern.
        var result = ErrorCodeSpecLoader.Load(
            _PATH, """{ "errorCodes": [] }""", MalformedSpecId);

        result.Diagnostic.Should().BeNull();
        result.Spec!.ErrorCodes.Should().BeEmpty();
    }
}
