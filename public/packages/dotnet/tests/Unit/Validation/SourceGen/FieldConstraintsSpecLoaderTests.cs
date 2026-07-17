// -----------------------------------------------------------------------
// <copyright file="FieldConstraintsSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Validation.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.Validation.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the field-constraints spec loader's JSON-shape
/// validation. Drives the loader directly (no Roslyn host) and asserts the
/// <c>EmitDiagnostic</c> records surfaced for malformed input.
/// </summary>
public sealed class FieldConstraintsSpecLoaderTests
{
    private const string _PATH = "field-constraints.spec.json";

    private const string _VALID = """
    {
      "constraints": [
        { "name": "EMAIL_MAX", "value": 254, "doc": "Email max." }
      ],
      "enums": [
        {
          "name": "BiologicalSex",
          "backing": "byte",
          "doc": "Sex.",
          "members": [ { "name": "Unspecified", "doc": "Unknown." } ]
        }
      ]
    }
    """;

    [Fact]
    public void Load_ValidSpec_ReturnsPopulatedSpec()
    {
        var result = FieldConstraintsSpecLoader.Load(_PATH, _VALID);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Constraints.Should().HaveCount(1);
        result.Spec.Constraints[0].Name.Should().Be("EMAIL_MAX");
        result.Spec.Constraints[0].Value.Should().Be(254);
        result.Spec.Constraints[0].Doc.Should().Be("Email max.");
        result.Spec.Enums.Should().HaveCount(1);
        result.Spec.Enums[0].Name.Should().Be("BiologicalSex");
        result.Spec.Enums[0].Members.Should().ContainSingle();
        result.Spec.Enums[0].Members[0].Name.Should().Be("Unspecified");
    }

    [Fact]
    public void Load_MalformedJson_ReturnsD2FC001()
    {
        var result = FieldConstraintsSpecLoader.Load(_PATH, "{not valid json");

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_RootNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var result = FieldConstraintsSpecLoader.Load(_PATH, "[]");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingConstraintsArray_ReturnsDiagnostic()
    {
        var result = FieldConstraintsSpecLoader.Load(_PATH, """{ "enums": [] }""");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingEnumsArray_ReturnsDiagnostic()
    {
        var result = FieldConstraintsSpecLoader.Load(_PATH, """{ "constraints": [] }""");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_ConstraintMissingName_ReturnsDiagnostic()
    {
        var json = """
        {
          "constraints": [ { "value": 254, "doc": "X" } ],
          "enums": []
        }
        """;

        var result = FieldConstraintsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_ConstraintValueNotNumber_ReturnsDiagnostic()
    {
        var json = """
        {
          "constraints": [ { "name": "EMAIL_MAX", "value": "254", "doc": "X" } ],
          "enums": []
        }
        """;

        var result = FieldConstraintsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_ConstraintMissingDoc_ReturnsDiagnostic()
    {
        var json = """
        {
          "constraints": [ { "name": "EMAIL_MAX", "value": 254 } ],
          "enums": []
        }
        """;

        var result = FieldConstraintsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_ConstraintNotObject_ReturnsDiagnostic()
    {
        var json = """
        { "constraints": ["NOT_AN_OBJECT"], "enums": [] }
        """;

        var result = FieldConstraintsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EnumMissingName_ReturnsDiagnostic()
    {
        var json = """
        {
          "constraints": [],
          "enums": [ { "backing": "byte", "doc": "X", "members": [] } ]
        }
        """;

        var result = FieldConstraintsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EnumMissingMembersArray_ReturnsDiagnostic()
    {
        var json = """
        {
          "constraints": [],
          "enums": [ { "name": "X", "backing": "byte", "doc": "X" } ]
        }
        """;

        var result = FieldConstraintsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EnumMemberMissingName_ReturnsDiagnostic()
    {
        var json = """
        {
          "constraints": [],
          "enums": [
            {
              "name": "X",
              "backing": "byte",
              "doc": "X",
              "members": [ { "doc": "no name" } ]
            }
          ]
        }
        """;

        var result = FieldConstraintsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EnumEntryNotObject_ReturnsDiagnostic()
    {
        var json = """
        { "constraints": [], "enums": ["NOT_AN_OBJECT"] }
        """;

        var result = FieldConstraintsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EnumMissingBacking_ReturnsDiagnostic()
    {
        var json = """
        {
          "constraints": [],
          "enums": [
            { "name": "X", "doc": "X", "members": [ { "name": "A", "doc": "a" } ] }
          ]
        }
        """;

        var result = FieldConstraintsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EnumWithNonByteBacking_ReturnsDiagnostic()
    {
        // `backing` is schema-required and the only supported value is "byte"
        // (the emitter hardcodes `: byte`). A `backing: "int"` spec that the
        // loader waved through would emit a `: byte` enum from an `int` spec —
        // a silent drift between the declared and emitted backing.
        var json = """
        {
          "constraints": [],
          "enums": [
            {
              "name": "X",
              "backing": "int",
              "doc": "X",
              "members": [ { "name": "A", "doc": "a" } ]
            }
          ]
        }
        """;

        var result = FieldConstraintsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EnumMissingDoc_ReturnsDiagnostic()
    {
        var json = """
        {
          "constraints": [],
          "enums": [
            {
              "name": "X",
              "backing": "byte",
              "members": [ { "name": "A", "doc": "a" } ]
            }
          ]
        }
        """;

        var result = FieldConstraintsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EnumMemberMissingDoc_ReturnsDiagnostic()
    {
        var json = """
        {
          "constraints": [],
          "enums": [
            {
              "name": "X",
              "backing": "byte",
              "doc": "X",
              "members": [ { "name": "A" } ]
            }
          ]
        }
        """;

        var result = FieldConstraintsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EnumBackingNotString_ReturnsDiagnostic()
    {
        // Drives the backingEl.ValueKind != JsonValueKind.String branch in
        // ParseEnums — a numeric `"backing": 42` must be rejected.
        var json = """
        {
          "constraints": [],
          "enums": [
            {
              "name": "X",
              "backing": 42,
              "doc": "X",
              "members": [ { "name": "A", "doc": "a" } ]
            }
          ]
        }
        """;

        var result = FieldConstraintsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EnumMemberEntryNotObject_ReturnsDiagnostic()
    {
        // Drives the element.ValueKind != JsonValueKind.Object branch inside
        // ParseMembers — a member array element that is a raw string must be
        // rejected with D2FC001 (malformed spec).
        var json = """
        {
          "constraints": [],
          "enums": [
            {
              "name": "X",
              "backing": "byte",
              "doc": "X",
              "members": ["NOT_AN_OBJECT"]
            }
          ]
        }
        """;

        var result = FieldConstraintsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_ConstraintValueIsFloat_ReturnsDiagnostic()
    {
        // `TryGetInt32` returns false for fractional JSON numbers, so a
        // float value like 254.5 must be rejected with D2FC001.
        var json = """
        {
          "constraints": [ { "name": "EMAIL_MAX", "value": 254.5, "doc": "X" } ],
          "enums": []
        }
        """;

        var result = FieldConstraintsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EmptyArrays_ReturnsEmptySpec()
    {
        // Loader does not enforce minItems - that's a higher-level concern.
        var result = FieldConstraintsSpecLoader.Load(
            _PATH, """{ "constraints": [], "enums": [] }""");

        result.Diagnostic.Should().BeNull();
        result.Spec!.Constraints.Should().BeEmpty();
        result.Spec.Enums.Should().BeEmpty();
    }
}
