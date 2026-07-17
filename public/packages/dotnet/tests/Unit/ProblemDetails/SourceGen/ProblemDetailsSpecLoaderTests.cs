// -----------------------------------------------------------------------
// <copyright file="ProblemDetailsSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.ProblemDetails.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.ProblemDetails.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the ProblemDetails spec loader's JSON-shape
/// validation. Drives the loader directly (no Roslyn host) and asserts the
/// <c>EmitDiagnostic</c> records surfaced for malformed input.
/// </summary>
public sealed class ProblemDetailsSpecLoaderTests
{
    private const string _PATH = "spec.json";

    [Fact]
    public void Load_ValidSpec_ReturnsPopulatedSpec()
    {
        var json = """
        {
          "typeUriPrefix": "https://problems.d2.dcsv.io/",
          "contentType": "application/problem+json",
          "extensionKeys": [
            {
              "constName": "ERROR_CODE",
              "value": "d2_error_code",
              "doc": "Error code."
            }
          ],
          "titles": [
            {
              "constName": "UNAUTHORIZED",
              "httpStatus": 401,
              "value": "Unauthorized",
              "doc": "401 title."
            },
            {
              "constName": "REQUEST_FAILED",
              "httpStatus": null,
              "value": "Request Failed",
              "doc": "Fallback."
            }
          ]
        }
        """;

        var result = ProblemDetailsSpecLoader.Load(_PATH, json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.TypeUriPrefix.Should().Be("https://problems.d2.dcsv.io/");
        result.Spec.ContentType.Should().Be("application/problem+json");
        result.Spec.ExtensionKeys.Should().HaveCount(1);
        result.Spec.ExtensionKeys[0].ConstName.Should().Be("ERROR_CODE");
        result.Spec.ExtensionKeys[0].Value.Should().Be("d2_error_code");
        result.Spec.ExtensionKeys[0].Doc.Should().Be("Error code.");
        result.Spec.Titles.Should().HaveCount(2);
        result.Spec.Titles[0].ConstName.Should().Be("UNAUTHORIZED");
        result.Spec.Titles[0].HttpStatus.Should().Be(401);
        result.Spec.Titles[0].Value.Should().Be("Unauthorized");
        result.Spec.Titles[1].ConstName.Should().Be("REQUEST_FAILED");
        result.Spec.Titles[1].HttpStatus.Should().BeNull();
    }

    [Fact]
    public void Load_MissingContentType_ReturnsMalformedSpecDiagnostic()
    {
        var result = ProblemDetailsSpecLoader.Load(
            _PATH,
            """{ "typeUriPrefix": "https://x/", "extensionKeys": [], "titles": [] }""");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MalformedJson_ReturnsMalformedSpecDiagnostic()
    {
        var result = ProblemDetailsSpecLoader.Load(_PATH, "{not valid json");

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_RootNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var result = ProblemDetailsSpecLoader.Load(_PATH, "[]");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingTypeUriPrefix_ReturnsMalformedSpecDiagnostic()
    {
        var result = ProblemDetailsSpecLoader.Load(
            _PATH,
            """{ "extensionKeys": [], "titles": [] }""");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_TypeUriPrefixNotString_ReturnsMalformedSpecDiagnostic()
    {
        var result = ProblemDetailsSpecLoader.Load(
            _PATH,
            """{ "typeUriPrefix": 42, "extensionKeys": [], "titles": [] }""");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingExtensionKeysArray_ReturnsMalformedSpecDiagnostic()
    {
        var result = ProblemDetailsSpecLoader.Load(
            _PATH,
            """
            {
              "typeUriPrefix": "https://x/",
              "contentType": "application/problem+json",
              "titles": []
            }
            """);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingTitlesArray_ReturnsMalformedSpecDiagnostic()
    {
        var result = ProblemDetailsSpecLoader.Load(
            _PATH,
            """
            {
              "typeUriPrefix": "https://x/",
              "contentType": "application/problem+json",
              "extensionKeys": []
            }
            """);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_ExtensionKeyMissingConstName_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "typeUriPrefix": "https://x/",
          "contentType": "application/problem+json",
          "extensionKeys": [{ "value": "v", "doc": "d" }],
          "titles": []
        }
        """;

        var result = ProblemDetailsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_ExtensionKeyMissingValue_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "typeUriPrefix": "https://x/",
          "contentType": "application/problem+json",
          "extensionKeys": [{ "constName": "X", "doc": "d" }],
          "titles": []
        }
        """;

        var result = ProblemDetailsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_ExtensionKeyMissingDoc_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "typeUriPrefix": "https://x/",
          "contentType": "application/problem+json",
          "extensionKeys": [{ "constName": "X", "value": "v" }],
          "titles": []
        }
        """;

        var result = ProblemDetailsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_TitleMissingConstName_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "typeUriPrefix": "https://x/",
          "contentType": "application/problem+json",
          "extensionKeys": [],
          "titles": [{ "httpStatus": 401, "value": "v", "doc": "d" }]
        }
        """;

        var result = ProblemDetailsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_TitleMissingHttpStatus_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "typeUriPrefix": "https://x/",
          "contentType": "application/problem+json",
          "extensionKeys": [],
          "titles": [{ "constName": "X", "value": "v", "doc": "d" }]
        }
        """;

        var result = ProblemDetailsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_TitleHttpStatusString_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "typeUriPrefix": "https://x/",
          "contentType": "application/problem+json",
          "extensionKeys": [],
          "titles": [{ "constName": "X", "httpStatus": "401", "value": "v", "doc": "d" }]
        }
        """;

        var result = ProblemDetailsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_TitleHttpStatusNull_IsAccepted()
    {
        // null is the schema-allowed sentinel for the fallback title entry.
        var json = """
        {
          "typeUriPrefix": "https://x/",
          "contentType": "application/problem+json",
          "extensionKeys": [],
          "titles": [{ "constName": "FB", "httpStatus": null, "value": "Fallback", "doc": "d" }]
        }
        """;

        var result = ProblemDetailsSpecLoader.Load(_PATH, json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Titles.Should().HaveCount(1);
        result.Spec.Titles[0].HttpStatus.Should().BeNull();
    }

    [Fact]
    public void Load_TitleMissingValue_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "typeUriPrefix": "https://x/",
          "contentType": "application/problem+json",
          "extensionKeys": [],
          "titles": [{ "constName": "X", "httpStatus": 401, "doc": "d" }]
        }
        """;

        var result = ProblemDetailsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_TitleMissingDoc_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "typeUriPrefix": "https://x/",
          "contentType": "application/problem+json",
          "extensionKeys": [],
          "titles": [{ "constName": "X", "httpStatus": 401, "value": "v" }]
        }
        """;

        var result = ProblemDetailsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EmptyArrays_AreAccepted()
    {
        // Loader does not enforce minItems — that's a higher-level concern.
        var result = ProblemDetailsSpecLoader.Load(
            _PATH,
            """
            {
              "typeUriPrefix": "https://x/",
              "contentType": "application/problem+json",
              "extensionKeys": [],
              "titles": []
            }
            """);

        result.Diagnostic.Should().BeNull();
        result.Spec!.ExtensionKeys.Should().BeEmpty();
        result.Spec.Titles.Should().BeEmpty();
    }
}
