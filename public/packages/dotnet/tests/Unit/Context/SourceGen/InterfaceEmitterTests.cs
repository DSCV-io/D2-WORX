// -----------------------------------------------------------------------
// <copyright file="InterfaceEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Context.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.Context.SourceGen;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="InterfaceEmitter.Emit"/>. Asserts the
/// emitted interface shape (sections → #region blocks, property declarations,
/// XML doc, extends clause) and the type-vocabulary diagnostic surface
/// (D2CTX002 / D2CTX005).
/// </summary>
public sealed class InterfaceEmitterTests
{
    // ----------------------------------------------------------------------
    // Happy path
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_HappyPath_GeneratesInterfaceWithRegionAndProperty()
    {
        var spec = Spec(
            "IAuthContext",
            "DcsvIo.D2.AuthContext.Abstractions",
            description: "Test description",
            sections: [
                Section(
                    "Token",
                    Property("IsAuthenticated", "bool?", doc: "Whether authenticated.")),
            ]);

        var result = InterfaceEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.HintName.Should().Be("IAuthContext.g.cs");

        var src = result.GeneratedSource;
        src.Should().Contain("namespace DcsvIo.D2.AuthContext.Abstractions;");
        src.Should().Contain("public interface IAuthContext");
        src.Should().Contain("#region Token");
        src.Should().Contain("#endregion");
        src.Should().Contain("bool? IsAuthenticated { get; }");
        src.Should().Contain("/// Whether authenticated.");
    }

    [Fact]
    public void Emit_NoExtends_OmitsInheritanceClause()
    {
        var spec = Spec(
            "IFoo",
            "X.Y",
            extends: null,
            sections: [Section("S", Property("X", "string?"))]);

        var result = InterfaceEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();

        // No `: BaseInterface` clause — verify the line ends right after the
        // interface name. Normalize line endings before comparing.
        var src = result.GeneratedSource.Replace("\r\n", "\n");
        src.Should().Contain("public interface IFoo\n{");
    }

    [Fact]
    public void Emit_WithExtends_EmitsInheritanceClause()
    {
        var spec = Spec(
            "IRequestContext",
            "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [Section("Tracing", Property("TraceId", "string?"))]);

        var result = InterfaceEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should()
            .Contain(
                "public interface IRequestContext"
                + " : global::DcsvIo.D2.AuthContext.Abstractions.IAuthContext");
    }

    [Fact]
    public void Emit_MultipleSections_EmitsMultipleRegionBlocks()
    {
        var spec = Spec(
            "IFoo",
            "X.Y",
            sections: [
                Section("Token", Property("Aud", "string?")),
                Section("Identity", Property("Sub", "string?")),
                Section("Org", Property("OrgId", "Guid?")),
            ]);

        var result = InterfaceEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("#region Token");
        result.GeneratedSource.Should().Contain("#region Identity");
        result.GeneratedSource.Should().Contain("#region Org");
    }

    [Fact]
    public void Emit_TrinaryAuthBool_EmitsAsNullableBoolWithProvidedDoc()
    {
        // The generator's interface emitter does not vary structure on
        // TrinaryAuth (it's metadata). The doc text is what calls out the
        // null-vs-false semantics — verify it round-trips into the XML.
        var spec = Spec(
            "IFoo",
            "X.Y",
            sections: [
                Section("S", Property(
                    name: "IsAuthenticated",
                    type: "bool?",
                    trinaryAuth: true,
                    doc: "Whether authenticated. null = pre-auth.")),
            ]);

        var result = InterfaceEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("bool? IsAuthenticated { get; }");
        result.GeneratedSource.Should().Contain("/// Whether authenticated. null = pre-auth.");
    }

    [Fact]
    public void Emit_DerivedProperty_StillEmittedOnInterface()
    {
        // Derived properties are read-only on the interface — consumers see
        // them as computed values, not separate getter machinery.
        var spec = Spec(
            "IFoo",
            "X.Y",
            sections: [
                Section("S", Property(
                    "ImmediateCallerClientId", "string?", derived: "actorChain")),
            ]);

        var result = InterfaceEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("string? ImmediateCallerClientId { get; }");
    }

    // ----------------------------------------------------------------------
    // D2CTX002 — unknown type
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_PropertyTypeOutsideVocabulary_EmitsD2CTX002()
    {
        var spec = Spec(
            "IFoo",
            "X.Y",
            sections: [Section("S", Property("X", "Banana"))]);

        var result = InterfaceEmitter.Emit(spec);

        var diag = result.Diagnostics.Single(d => d.DescriptorId == DiagnosticIds.UnknownType);
        ((string)diag.Args[0]).Should().Be("IFoo");
        ((string)diag.Args[1]).Should().Be("X");
        ((string)diag.Args[2]).Should().Be("Banana");

        // Property is skipped on unknown type.
        result.GeneratedSource.Should().NotContain("Banana X");
    }

    // ----------------------------------------------------------------------
    // D2CTX005 — unknown derived rule (warning, not fatal)
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_PropertyDerivedRuleOutsideVocabulary_EmitsD2CTX005()
    {
        var spec = Spec(
            "IFoo",
            "X.Y",
            sections: [
                Section("S", Property("X", "string?", derived: "notARealRule")),
            ]);

        var result = InterfaceEmitter.Emit(spec);

        var diag = result.Diagnostics
            .Single(d => d.DescriptorId == DiagnosticIds.UnknownDerivedRule);
        ((string)diag.Args[2]).Should().Be("notARealRule");

        // Warning-only — property still emitted on the interface.
        result.GeneratedSource.Should().Contain("string? X { get; }");
    }

    // ----------------------------------------------------------------------
    // Adversarial — boundary shapes
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_NoSections_EmitsEmptyInterfaceShell()
    {
        var spec = Spec("IFoo", "X.Y", sections: []);

        var result = InterfaceEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public interface IFoo");
    }

    [Fact]
    public void Emit_PropertyWithoutDoc_FallsBackToPropertyName()
    {
        var spec = Spec(
            "IFoo",
            "X.Y",
            sections: [Section("S", Property("Subject", "string?", doc: null))]);

        var result = InterfaceEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();

        // When doc is null, the emitter uses the property name as the summary.
        result.GeneratedSource.Should().Contain("/// Subject");
    }

    [Fact]
    public void Emit_DocContainsHtmlSpecials_EscapesXmlEntities()
    {
        var spec = Spec(
            "IFoo",
            "X.Y",
            sections: [
                Section("S", Property(
                    name: "X",
                    type: "string?",
                    doc: "Has <bracket> & ampersand")),
            ]);

        var result = InterfaceEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("&lt;bracket&gt; &amp; ampersand");
    }

    [Fact]
    public void Emit_AllVocabularyTypes_EmitWithoutDiagnostics()
    {
        // Sweep every declared type in TypeVocabulary to confirm none of them
        // trigger D2CTX002 — guards against drift between vocab + emitter.
        var spec = Spec(
            "IFoo",
            "X.Y",
            sections: [
                Section(
                    "S",
                    Property("A", "string?"),
                    Property("B", "bool?"),
                    Property("C", "int?"),
                    Property("D", "double?"),
                    Property("E", "Guid?"),
                    Property("F", "DateTimeOffset?"),
                    Property("G", "OrgType?"),
                    Property("H", "Role?"),
                    Property("I", "ActorKind?"),
                    Property("J", "ImpersonationKind?"),
                    Property("K", "IReadOnlyList<ActorEntry>"),
                    Property("L", "IReadOnlyList<string>"),
                    Property("M", "IReadOnlySet<string>")),
            ]);

        var result = InterfaceEmitter.Emit(spec);

        result.Diagnostics.Should().NotContain(d => d.DescriptorId == DiagnosticIds.UnknownType);
    }

    // ----------------------------------------------------------------------
    // [RedactData] emission from spec `redact` field
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_PropertyWithRedactTrue_EmitsRedactDataAttribute()
    {
        var spec = Spec(
            "IFoo",
            "X.Y",
            sections: [
                Section("S", Property("Pii", "string?", redact: true)),
            ]);

        var result = InterfaceEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain(
            "[RedactData(Reason = RedactReason.PersonalInformation)]");
        result.GeneratedSource.Should().Contain("string? Pii { get; }");
    }

    [Fact]
    public void Emit_PropertyWithoutRedact_DoesNotEmitRedactDataAttribute()
    {
        var spec = Spec(
            "IFoo",
            "X.Y",
            sections: [Section("S", Property("Plain", "string?"))]);

        var result = InterfaceEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().NotContain("[RedactData");
        result.GeneratedSource.Should().Contain("string? Plain { get; }");
    }

    [Fact]
    public void Emit_RedactAnnotation_AddsUtilitiesAttributeUsings()
    {
        var spec = Spec(
            "IFoo",
            "X.Y",
            sections: [
                Section("S", Property("Pii", "string?", redact: true)),
            ]);

        var result = InterfaceEmitter.Emit(spec);

        result.GeneratedSource.Should().Contain("using DcsvIo.D2.Utilities.Attributes;");
        result.GeneratedSource.Should().Contain("using DcsvIo.D2.Utilities.Enums;");
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static ContextSpec Spec(
        string name,
        string @namespace,
        string? description = null,
        string? extends = null,
        ImmutableArray<Section> sections = default)
    {
        return new ContextSpec(
            Name: name,
            Namespace: @namespace,
            Description: description,
            Extends: extends,
            Sections: sections.IsDefault ? [] : sections);
    }

    private static Section Section(string name, params PropertySpec[] props) =>
        new(name, [.. props]);

    private static PropertySpec Property(
        string name,
        string type,
        string? claim = null,
        bool trinaryAuth = false,
        string? derived = null,
        string? @default = null,
        string? doc = null,
        bool propagate = false,
        int? maxLength = null,
        int? entryIdMaxLength = null,
        bool redact = false) =>
        new(
            name,
            type,
            claim,
            trinaryAuth,
            derived,
            @default,
            doc,
            propagate,
            maxLength,
            entryIdMaxLength,
            redact);
}
