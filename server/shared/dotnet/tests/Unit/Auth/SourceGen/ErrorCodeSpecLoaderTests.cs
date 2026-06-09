// -----------------------------------------------------------------------
// <copyright file="ErrorCodeSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.SourceGen;

using AwesomeAssertions;
using D2.Shared.Auth.ErrorCodes.SourceGen;
using D2.Shared.ErrorCodes.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the shared error-codes spec loader's JSON-shape
/// validation driven with the auth catalog's diagnostic id. Drives the loader
/// directly (no Roslyn host) and asserts the <c>EmitDiagnostic</c> records
/// surfaced for malformed input, plus that the loader parses the auth
/// factory fields (including the new <c>factoryShape</c>).
/// </summary>
public sealed class ErrorCodeSpecLoaderTests
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
              "code": "AUTH_TEST",
              "httpStatus": 401,
              "category": "validation_failure",
              "userMessageKey": "TK.Auth.Errors.UNAUTHORIZED",
              "factoryName": "TestFactory",
              "factoryShape": "with_error_code",
              "doc": "Test entry."
            }
          ]
        }
        """;

        var result = ErrorCodeSpecLoader.Load(_PATH, json, MalformedSpecId);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.ErrorCodes.Should().HaveCount(1);
        var entry = result.Spec.ErrorCodes[0];
        entry.Code.Should().Be("AUTH_TEST");
        entry.HttpStatus.Should().Be(401);
        entry.Category.Should().Be("validation_failure");
        entry.UserMessageKey.Should().Be("TK.Auth.Errors.UNAUTHORIZED");
        entry.FactoryName.Should().Be("TestFactory");
        entry.FactoryShape.Should().Be("with_error_code");
        entry.Doc.Should().Be("Test entry.");
    }

    [Fact]
    public void Load_MalformedJson_ReturnsMalformedSpecDiagnostic()
    {
        var result = ErrorCodeSpecLoader.Load(_PATH, "{not valid json", MalformedSpecId);

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
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
              "httpStatus": 401,
              "category": "validation_failure",
              "userMessageKey": "TK.X",
              "factoryName": "X",
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
              "code": "AUTH_X",
              "httpStatus": "401",
              "category": "validation_failure",
              "userMessageKey": "TK.X",
              "factoryName": "X",
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
    public void Load_EmptyErrorCodesArray_ReturnsEmptySpec()
    {
        // Loader does not enforce minItems - that's a higher-level concern.
        var result = ErrorCodeSpecLoader.Load(
            _PATH, """{ "errorCodes": [] }""", MalformedSpecId);

        result.Diagnostic.Should().BeNull();
        result.Spec!.ErrorCodes.Should().BeEmpty();
    }

    [Fact]
    public void Load_EntryMissingFactoryFields_StillParsesWithNullFactoryFields()
    {
        // The shared loader treats the factory fields as OPTIONAL — the generic
        // constants-only catalog omits them entirely; absence is null, not an error.
        var json = """
        {
          "errorCodes": [
            {
              "code": "AUTH_X",
              "httpStatus": 401,
              "doc": "X"
            }
          ]
        }
        """;

        var result = ErrorCodeSpecLoader.Load(_PATH, json, MalformedSpecId);

        result.Diagnostic.Should().BeNull();
        var entry = result.Spec!.ErrorCodes[0];
        entry.Category.Should().BeNull();
        entry.UserMessageKey.Should().BeNull();
        entry.FactoryName.Should().BeNull();
        entry.FactoryShape.Should().BeNull();
    }

    [Fact]
    public void Load_HttpStatusOverflowValue_ReturnsMalformedSpecDiagnostic()
    {
        // System.Text.Json's TryGetInt32 fails on values outside the Int32
        // range, which we surface as a malformed-spec diagnostic rather than
        // silently truncating.
        var json = """
        {
          "errorCodes": [
            {
              "code": "AUTH_X",
              "httpStatus": 99999999999,
              "category": "validation_failure",
              "userMessageKey": "TK.Auth.Errors.UNAUTHORIZED",
              "factoryName": "X",
              "factoryShape": "with_error_code",
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
    public void Load_NullEntryInsideErrorCodes_ReturnsMalformedSpecDiagnostic()
    {
        // A JSON null value inside the errorCodes array is not a JSON object
        // and must be rejected at the loader level.
        var json = """{ "errorCodes": [null] }""";

        var result = ErrorCodeSpecLoader.Load(_PATH, json, MalformedSpecId);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_DuplicateCodeEntries_ParsesBothIntoSpec()
    {
        // The loader does NOT enforce code uniqueness — that is the emitter's
        // responsibility. Duplicates parse successfully and reach validation.
        var json = """
        {
          "errorCodes": [
            {
              "code": "AUTH_X",
              "httpStatus": 401,
              "category": "validation_failure",
              "userMessageKey": "TK.Auth.Errors.UNAUTHORIZED",
              "factoryName": "X",
              "factoryShape": "with_error_code",
              "doc": "X"
            },
            {
              "code": "AUTH_X",
              "httpStatus": 401,
              "category": "validation_failure",
              "userMessageKey": "TK.Auth.Errors.UNAUTHORIZED",
              "factoryName": "Y",
              "factoryShape": "with_error_code",
              "doc": "Y"
            }
          ]
        }
        """;

        var result = ErrorCodeSpecLoader.Load(_PATH, json, MalformedSpecId);

        // Loader succeeds — duplicates are surfaced by the emitter.
        result.Diagnostic.Should().BeNull();
        result.Spec!.ErrorCodes.Should().HaveCount(2);
    }
}
