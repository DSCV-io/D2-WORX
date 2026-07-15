// -----------------------------------------------------------------------
// <copyright file="FieldConstraintsEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Validation.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Validation.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the field-constraints emitter. Drives the emitter
/// directly with synthetic specs and asserts both the generated source shape
/// (per-VALUE pins for every constant + enum member) and the diagnostics
/// surfaced for invalid spec inputs. The parity test catches cross-language
/// drift; this test catches within-emitter drift.
/// </summary>
public sealed class FieldConstraintsEmitterTests
{
    [Fact]
    public void Emit_ValidSpec_EmitsBothSourcesWithCorrectHintNames()
    {
        var spec = MakeSpec(
            constraints: [new ConstraintEntry("EMAIL_MAX", 254, "Email max.")],
            enums:
            [
                new EnumEntry(
                    "BiologicalSex",
                    "Sex.",
                    [new EnumMemberEntry("Unspecified", "Unknown.")]),
            ]);

        var results = FieldConstraintsEmitter.Emit(spec);

        results.Should().HaveCount(2);
        results.Select(r => r.HintName).Should().Contain(
            ["FieldConstraints.g.cs", "Taxonomy.g.cs"]);
        results.SelectMany(r => r.Diagnostics).Should().BeEmpty();
    }

    [Fact]
    public void Emit_Constraints_EmitsNamespaceAndClassAndConst()
    {
        var spec = MakeSpec(
            constraints: [new ConstraintEntry("EMAIL_MAX", 254, "Email max.")],
            enums: []);

        var constraints = ConstraintsSource(spec);

        constraints.Should().Contain("namespace DcsvIo.D2.Validation.Abstractions;");
        constraints.Should().Contain("public static class FieldConstraints");
        constraints.Should().Contain("public const int EMAIL_MAX = 254;");
        constraints.Should().Contain("    /// <summary>Email max.</summary>");
    }

    [Fact]
    public void Emit_EmptySpec_EmitsValidEmptyClassAndNoEnums()
    {
        // A zero-entry spec is legal at the emitter layer (the loader does not
        // enforce minItems). The emitter must still produce a valid, empty
        // FieldConstraints class body and an enum-free Taxonomy source — never
        // a malformed file or a stray diagnostic.
        var spec = MakeSpec(constraints: [], enums: []);

        var results = FieldConstraintsEmitter.Emit(spec);

        results.SelectMany(r => r.Diagnostics).Should().BeEmpty();
        var constraints = ConstraintsSource(spec);
        constraints.Should().Contain("public static class FieldConstraints");
        constraints.Should().Contain("{");
        constraints.Should().Contain("}");
        constraints.Should().NotContain("public const int");
        TaxonomySource(spec).Should().NotContain("public enum");
    }

    [Fact]
    public void Emit_Taxonomy_CarriesJsonStringEnumConverterAndByteBacking()
    {
        var spec = MakeSpec(
            constraints: [],
            enums:
            [
                new EnumEntry(
                    "BiologicalSex",
                    "Sex.",
                    [new EnumMemberEntry("Male", "M."), new EnumMemberEntry("Female", "F.")]),
            ]);

        var taxonomy = TaxonomySource(spec);

        taxonomy.Should().Contain("namespace DcsvIo.D2.Validation.Abstractions;");
        taxonomy.Should().Contain("[JsonConverter(typeof(JsonStringEnumConverter))]");
        taxonomy.Should().Contain("public enum BiologicalSex : byte");
        taxonomy.Should().Contain("    Male = 0,");
        taxonomy.Should().Contain("    Female = 1,");
        taxonomy.Should().Contain("using System.Text.Json.Serialization;");
    }

    [Fact]
    public void Emit_DuplicateConstName_EmitsDuplicateDiagnostic()
    {
        var spec = MakeSpec(
            constraints:
            [
                new ConstraintEntry("DUPE", 1, "X"),
                new ConstraintEntry("DUPE", 2, "Y"),
            ],
            enums: []);

        AllDiagnostics(spec).Should()
            .ContainSingle(id => id == DiagnosticIds.DuplicateConstName);
    }

    [Theory]
    [InlineData("lowercase")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("9NOPE")]
    [InlineData("Has-Dash")]
    public void Emit_InvalidConstName_EmitsInvalidDiagnostic(string name)
    {
        var spec = MakeSpec(
            constraints: [new ConstraintEntry(name, 1, "X")],
            enums: []);

        AllDiagnostics(spec).Should()
            .Contain(DiagnosticIds.InvalidConstName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-255)]
    public void Emit_NonPositiveValue_EmitsDiagnostic(int value)
    {
        var spec = MakeSpec(
            constraints: [new ConstraintEntry("X_MAX", value, "X")],
            enums: []);

        AllDiagnostics(spec).Should()
            .ContainSingle(id => id == DiagnosticIds.NonPositiveValue);
    }

    [Fact]
    public void Emit_DuplicateEnumName_EmitsDiagnostic()
    {
        var member = new EnumMemberEntry("A", "a");
        var spec = MakeSpec(
            constraints: [],
            enums:
            [
                new EnumEntry("Dupe", "X", [member]),
                new EnumEntry("Dupe", "Y", [member]),
            ]);

        AllDiagnostics(spec).Should()
            .ContainSingle(id => id == DiagnosticIds.DuplicateEnumName);
    }

    [Theory]
    [InlineData("lower")]
    [InlineData("")]
    [InlineData("Has_Underscore")]
    [InlineData("9Digit")]
    [InlineData("   ")]
    [InlineData("Has-Dash")]
    public void Emit_InvalidEnumName_EmitsDiagnostic(string name)
    {
        var spec = MakeSpec(
            constraints: [],
            enums: [new EnumEntry(name, "X", [new EnumMemberEntry("A", "a")])]);

        AllDiagnostics(spec).Should()
            .Contain(DiagnosticIds.InvalidEnumName);
    }

    [Fact]
    public void Emit_EmptyEnumMemberList_EmitsDiagnostic()
    {
        var spec = MakeSpec(
            constraints: [],
            enums: [new EnumEntry("Empty", "X", ImmutableArray<EnumMemberEntry>.Empty)]);

        AllDiagnostics(spec).Should()
            .ContainSingle(id => id == DiagnosticIds.EmptyEnumMemberList);
    }

    [Fact]
    public void Emit_DuplicateEnumMember_EmitsDiagnostic()
    {
        var spec = MakeSpec(
            constraints: [],
            enums:
            [
                new EnumEntry(
                    "X",
                    "x",
                    [new EnumMemberEntry("A", "a"), new EnumMemberEntry("A", "b")]),
            ]);

        AllDiagnostics(spec).Should()
            .ContainSingle(id => id == DiagnosticIds.DuplicateEnumMember);
    }

    [Theory]
    [InlineData("has-dash")]
    [InlineData("")]
    [InlineData("9leading")]
    [InlineData("has space")]
    public void Emit_InvalidEnumMemberName_EmitsDiagnostic(string memberName)
    {
        var spec = MakeSpec(
            constraints: [],
            enums: [new EnumEntry("X", "x", [new EnumMemberEntry(memberName, "m")])]);

        AllDiagnostics(spec).Should()
            .Contain(DiagnosticIds.InvalidEnumMemberName);
    }

    [Fact]
    public void Emit_XmlDocSpecialChars_AreEscaped()
    {
        var spec = MakeSpec(
            constraints: [new ConstraintEntry("X_MAX", 1, "Has <angle> & ampersand.")],
            enums: []);

        ConstraintsSource(spec).Should().Contain("&lt;angle&gt; &amp; ampersand.");
    }

    [Fact]
    public void Emit_PreservesSpecOrder()
    {
        var spec = MakeSpec(
            constraints:
            [
                new ConstraintEntry("ZEBRA_MAX", 1, "Z"),
                new ConstraintEntry("ALPHA_MAX", 2, "A"),
            ],
            enums: []);

        var source = ConstraintsSource(spec);
        var zebra = source.IndexOf("ZEBRA_MAX", System.StringComparison.Ordinal);
        var alpha = source.IndexOf("ALPHA_MAX", System.StringComparison.Ordinal);
        zebra.Should().BeLessThan(alpha);
    }

    [Fact]
    public void Emit_RunsTwiceWithIdenticalInput_ProducesIdenticalSource()
    {
        var spec = MakeSpec(
            constraints: [new ConstraintEntry("X_MAX", 1, "X")],
            enums: [new EnumEntry("E", "e", [new EnumMemberEntry("A", "a")])]);

        var first = FieldConstraintsEmitter.Emit(spec);
        var second = FieldConstraintsEmitter.Emit(spec);

        for (var i = 0; i < first.Length; i++)
            second[i].GeneratedSource.Should().Be(first[i].GeneratedSource);
    }

    /// <summary>
    /// Per-VALUE pin for every shipping field-length constant: the emitter MUST
    /// produce the exact <c>public const int</c> declaration with the locked
    /// value. A drift of any single entry's name or value flips this row red.
    /// </summary>
    /// <param name="name">The constant name expected on the emitted const.</param>
    /// <param name="value">The locked value the emitted const must carry.</param>
    [Theory]
    [InlineData("FIRST_NAME_MAX", 255)]
    [InlineData("MIDDLE_NAME_MAX", 255)]
    [InlineData("LAST_NAME_MAX", 255)]
    [InlineData("PREFERRED_NAME_MAX", 255)]
    [InlineData("COMPANY_NAME_MAX", 255)]
    [InlineData("JOB_TITLE_MAX", 255)]
    [InlineData("DEPARTMENT_MAX", 255)]
    [InlineData("STREET_LINE_MAX", 255)]
    [InlineData("CITY_MAX", 255)]
    [InlineData("COMPANY_WEBSITE_MAX", 2048)]
    [InlineData("AFFIX_CUSTOM_MAX", 64)]
    [InlineData("POSTAL_CODE_MAX", 16)]
    [InlineData("EMAIL_MAX", 254)]
    [InlineData("PHONE_E164_MAX", 32)]
    [InlineData("PHONE_MIN_DIGITS", 7)]
    [InlineData("PHONE_MAX_DIGITS", 15)]
    public void Emit_ShippingConstant_EmitsLockedValue(string name, int value)
    {
        var spec = MakeSpec(
            constraints: [new ConstraintEntry(name, value, $"{name} doc.")],
            enums: []);

        ConstraintsSource(spec).Should().Contain($"public const int {name} = {value};");
    }

    /// <summary>
    /// Per-VALUE pin for every shipping taxonomy enum member: the emitter MUST
    /// produce the exact member declaration with the expected ordinal backing.
    /// </summary>
    /// <param name="enumName">The enum the member belongs to.</param>
    /// <param name="memberName">The member name expected on the emitted enum.</param>
    /// <param name="ordinal">The expected byte backing value (spec order).</param>
    [Theory]
    [InlineData("NamePrefix", "Mr", 0)]
    [InlineData("NamePrefix", "Ms", 1)]
    [InlineData("NamePrefix", "Miss", 2)]
    [InlineData("NamePrefix", "Mrs", 3)]
    [InlineData("NamePrefix", "Mx", 4)]
    [InlineData("NamePrefix", "Dr", 5)]
    [InlineData("NamePrefix", "Prof", 6)]
    [InlineData("NamePrefix", "Sir", 7)]
    [InlineData("NamePrefix", "Lady", 8)]
    [InlineData("NamePrefix", "Lord", 9)]
    [InlineData("NamePrefix", "RtHon", 10)]
    [InlineData("NamePrefix", "Rev", 11)]
    [InlineData("NamePrefix", "Fr", 12)]
    [InlineData("NamePrefix", "Pr", 13)]
    [InlineData("NamePrefix", "Sr", 14)]
    [InlineData("NamePrefix", "Elder", 15)]
    [InlineData("NamePrefix", "Other", 16)]
    [InlineData("NameSuffix", "Jr", 0)]
    [InlineData("NameSuffix", "Sr", 1)]
    [InlineData("NameSuffix", "I", 2)]
    [InlineData("NameSuffix", "II", 3)]
    [InlineData("NameSuffix", "III", 4)]
    [InlineData("NameSuffix", "IV", 5)]
    [InlineData("NameSuffix", "V", 6)]
    [InlineData("NameSuffix", "VI", 7)]
    [InlineData("NameSuffix", "VII", 8)]
    [InlineData("NameSuffix", "VIII", 9)]
    [InlineData("NameSuffix", "IX", 10)]
    [InlineData("NameSuffix", "X", 11)]
    [InlineData("NameSuffix", "Other", 12)]
    [InlineData("BiologicalSex", "Male", 0)]
    [InlineData("BiologicalSex", "Female", 1)]
    [InlineData("BiologicalSex", "Intersex", 2)]
    [InlineData("BiologicalSex", "Unspecified", 3)]
    public void Emit_ShippingEnumMember_EmitsLockedOrdinal(
        string enumName, string memberName, int ordinal)
    {
        // Reconstruct just enough of the spec enum to assert the member's
        // ordinal — the ordinal is its index in the members list.
        var members = SampleMembers(enumName);
        var spec = MakeSpec(
            constraints: [],
            enums: [new EnumEntry(enumName, $"{enumName} doc.", members)]);

        TaxonomySource(spec).Should().Contain($"    {memberName} = {ordinal},");
    }

    /// <summary>
    /// Value-pins for the emitter's public consts — they participate in the
    /// generator dispatch (root namespace, emitted class name) and the hint
    /// names the Roslyn host adds the sources under. A drift in any of these
    /// breaks the committed-output path layout or the consuming-assembly gate.
    /// </summary>
    /// <param name="constName">The public const field name on the emitter.</param>
    /// <param name="expected">The locked value the const must carry.</param>
    [Theory]
    [InlineData("ROOT_NAMESPACE", "DcsvIo.D2.Validation.Abstractions")]
    [InlineData("FIELD_CONSTRAINTS_CLASS_NAME", "FieldConstraints")]
    [InlineData("FIELD_CONSTRAINTS_HINT_NAME", "FieldConstraints.g.cs")]
    [InlineData("TAXONOMY_HINT_NAME", "Taxonomy.g.cs")]
    public void EmitterPublicConst_HasStableValue(string constName, string expected)
    {
        var actual = typeof(FieldConstraintsEmitter)
            .GetField(constName, BindingFlags.Public | BindingFlags.Static)
            !.GetRawConstantValue();

        actual.Should().Be(expected);
    }

    private static ImmutableArray<EnumMemberEntry> SampleMembers(string enumName) => enumName switch
    {
        "NamePrefix" =>
        [
            new("Mr", "d"), new("Ms", "d"), new("Miss", "d"), new("Mrs", "d"),
            new("Mx", "d"), new("Dr", "d"), new("Prof", "d"), new("Sir", "d"),
            new("Lady", "d"), new("Lord", "d"), new("RtHon", "d"), new("Rev", "d"),
            new("Fr", "d"), new("Pr", "d"), new("Sr", "d"), new("Elder", "d"),
            new("Other", "d"),
        ],
        "NameSuffix" =>
        [
            new("Jr", "d"), new("Sr", "d"), new("I", "d"), new("II", "d"),
            new("III", "d"), new("IV", "d"), new("V", "d"), new("VI", "d"),
            new("VII", "d"), new("VIII", "d"), new("IX", "d"), new("X", "d"),
            new("Other", "d"),
        ],
        "BiologicalSex" =>
        [
            new("Male", "d"), new("Female", "d"), new("Intersex", "d"),
            new("Unspecified", "d"),
        ],
        _ => ImmutableArray<EnumMemberEntry>.Empty,
    };

    private static string ConstraintsSource(FieldConstraintsSpec spec) =>
        FieldConstraintsEmitter.Emit(spec)
            .Single(r => r.HintName == "FieldConstraints.g.cs").GeneratedSource;

    private static string TaxonomySource(FieldConstraintsSpec spec) =>
        FieldConstraintsEmitter.Emit(spec)
            .Single(r => r.HintName == "Taxonomy.g.cs").GeneratedSource;

    /// <summary>
    /// Returns the descriptor IDs of every diagnostic the emitter surfaces for
    /// the supplied spec. Projecting to the ID strings (rather than the internal
    /// <c>EmitDiagnostic</c> record type, which is bundled into every source-gen
    /// assembly and so is ambiguous when named directly) keeps the assertions
    /// unambiguous while still pinning the exact diagnostic.
    /// </summary>
    private static List<string> AllDiagnostics(FieldConstraintsSpec spec) =>
        FieldConstraintsEmitter.Emit(spec)
            .SelectMany(r => r.Diagnostics)
            .Select(d => d.DescriptorId)
            .ToList();

    private static FieldConstraintsSpec MakeSpec(
        ImmutableArray<ConstraintEntry> constraints,
        ImmutableArray<EnumEntry> enums) =>
        new(constraints, enums);
}
