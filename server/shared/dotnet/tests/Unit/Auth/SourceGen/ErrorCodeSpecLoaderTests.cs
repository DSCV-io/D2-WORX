// -----------------------------------------------------------------------
// <copyright file="ErrorCodeSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.SourceGen;

using AwesomeAssertions;
using D2.Shared.Auth.ErrorCodes.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the AuthErrorCodes spec loader's JSON-shape validation.
/// Drives the loader directly (no Roslyn host) and asserts the
/// <see cref="EmitDiagnostic"/> records surfaced for malformed input.
/// </summary>
public sealed class ErrorCodeSpecLoaderTests
{
    private const string _PATH = "spec.json";

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
              "doc": "Test entry."
            }
          ]
        }
        """;

        var result = ErrorCodeSpecLoader.Load(_PATH, json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.ErrorCodes.Should().HaveCount(1);
        var entry = result.Spec.ErrorCodes[0];
        entry.Code.Should().Be("AUTH_TEST");
        entry.HttpStatus.Should().Be(401);
        entry.Category.Should().Be("validation_failure");
        entry.UserMessageKey.Should().Be("TK.Auth.Errors.UNAUTHORIZED");
        entry.FactoryName.Should().Be("TestFactory");
        entry.Doc.Should().Be("Test entry.");
    }

    [Fact]
    public void Load_MalformedJson_ReturnsMalformedSpecDiagnostic()
    {
        var result = ErrorCodeSpecLoader.Load(_PATH, "{not valid json");

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_RootNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var result = ErrorCodeSpecLoader.Load(_PATH, "[]");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingErrorCodesArray_ReturnsMalformedSpecDiagnostic()
    {
        var result = ErrorCodeSpecLoader.Load(_PATH, "{}");

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

        var result = ErrorCodeSpecLoader.Load(_PATH, json);

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

        var result = ErrorCodeSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EmptyErrorCodesArray_ReturnsEmptySpec()
    {
        // Loader does not enforce minItems - that's a higher-level concern.
        var result = ErrorCodeSpecLoader.Load(_PATH, """{ "errorCodes": [] }""");

        result.Diagnostic.Should().BeNull();
        result.Spec!.ErrorCodes.Should().BeEmpty();
    }

    [Fact]
    public void Load_EntryMissingFactoryName_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "errorCodes": [
            {
              "code": "AUTH_X",
              "httpStatus": 401,
              "category": "validation_failure",
              "userMessageKey": "TK.X",
              "doc": "X"
            }
          ]
        }
        """;

        var result = ErrorCodeSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }
}
