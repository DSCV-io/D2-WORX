// -----------------------------------------------------------------------
// <copyright file="PropagatedEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Context.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.Context.SourceGen;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="PropagatedEmitter.EmitAll"/>. Asserts
/// the shape of all three generated files (PropagatedContext.g.cs,
/// PropagatedContextExtensions.g.cs, PropagatedContextSerializer.g.cs) with
/// special focus on the <c>[JsonIgnore]</c> guard on <c>HasAnyField</c> —
/// the regression-pin that proves the attribute never silently disappears from
/// the emitted record and therefore never reaches the wire.
/// </summary>
public sealed class PropagatedEmitterTests
{
    // ------------------------------------------------------------------
    // EmitAll — output count and hint names
    // ------------------------------------------------------------------

    [Fact]
    public void EmitAll_AlwaysProducesThreeFiles()
    {
        var (auth, request) = PropagateSpecs();

        var results = PropagatedEmitter.EmitAll(auth, request);

        results.Should().HaveCount(3);
    }

    [Fact]
    public void EmitAll_HintNamesMatchExpectedConvention()
    {
        var (auth, request) = PropagateSpecs();

        var results = PropagatedEmitter.EmitAll(auth, request);
        var names = results.Select(r => r.HintName).ToArray();

        names.Should().Contain("PropagatedContext.g.cs");
        names.Should().Contain("PropagatedContextExtensions.g.cs");
        names.Should().Contain("PropagatedContextSerializer.g.cs");
    }

    [Fact]
    public void EmitAll_NoErrors_WhenPropagateFieldsPresent()
    {
        var (auth, request) = PropagateSpecs();

        var results = PropagatedEmitter.EmitAll(auth, request);

        results.SelectMany(r => r.Diagnostics).Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // PropagatedContext.g.cs — record shape
    // ------------------------------------------------------------------

    [Fact]
    public void EmitAll_RecordFile_ContainsPropagatedPropertyDeclarations()
    {
        var (auth, request) = PropagateSpecs();
        var record = PropagatedEmitter.EmitAll(auth, request)
            .Single(r => r.HintName == "PropagatedContext.g.cs");

        record.GeneratedSource.Should().Contain("public string? RequestId { get; init; }");
        record.GeneratedSource.Should().Contain("public string? RequestPath { get; init; }");
    }

    [Fact]
    public void EmitAll_RecordFile_OmitsNonPropagatedProperties()
    {
        var (auth, request) = EmptyPropagateSpecs();
        var record = PropagatedEmitter.EmitAll(auth, request)
            .Single(r => r.HintName == "PropagatedContext.g.cs");

        record.GeneratedSource.Should().NotContain("InternalOnly");
    }

    // ------------------------------------------------------------------
    // [JsonIgnore] on HasAnyField — regression-pin target
    // ------------------------------------------------------------------

    [Fact]
    public void EmitAll_RecordFile_HasAnyFieldCarriesJsonIgnoreAttribute()
    {
        // REGRESSION PIN: removing [JsonIgnore] from HasAnyField causes the
        // computed helper to appear on the wire — breaking the
        // PropagatedHeaderWireShape invariant and the wire-shape test in
        // PropagatedHeaderWireShapeTests that asserts NotContain("\"hasAnyField\"").
        var (auth, request) = PropagateSpecs();
        var record = PropagatedEmitter.EmitAll(auth, request)
            .Single(r => r.HintName == "PropagatedContext.g.cs");
        var src = Normalize(record.GeneratedSource);

        // Both the attribute and the property must be present.
        src.Should().Contain("[JsonIgnore]");
        src.Should().Contain("public bool HasAnyField =>");

        // The attribute must appear IMMEDIATELY above the property declaration.
        src.Should().Contain("[JsonIgnore]\n    public bool HasAnyField =>");
    }

    [Fact]
    public void EmitAll_RecordFile_HasAnyFieldJsonIgnore_PresentEvenWithNoPropagatedFields()
    {
        // Even when the propagated set is empty, the HasAnyField computed member
        // plus its [JsonIgnore] guard must still be emitted.
        var (auth, request) = EmptyPropagateSpecs();
        var record = PropagatedEmitter.EmitAll(auth, request)
            .Single(r => r.HintName == "PropagatedContext.g.cs");
        var src = Normalize(record.GeneratedSource);

        src.Should().Contain("[JsonIgnore]");
        src.Should().Contain("public bool HasAnyField =>");
    }

    [Fact]
    public void EmitAll_RecordFile_IncludesSystemTextJsonSerializationUsing()
    {
        // [JsonIgnore] requires the using statement to compile — verify the
        // emitter includes it so a future refactor doesn't silently break the
        // generated file.
        var (auth, request) = PropagateSpecs();
        var record = PropagatedEmitter.EmitAll(auth, request)
            .Single(r => r.HintName == "PropagatedContext.g.cs");

        record.GeneratedSource.Should().Contain("using System.Text.Json.Serialization;");
    }

    // ------------------------------------------------------------------
    // PropagatedContextExtensions.g.cs — shape
    // ------------------------------------------------------------------

    [Fact]
    public void EmitAll_ExtensionsFile_ContainsToPropagatedContextMethod()
    {
        var (auth, request) = PropagateSpecs();
        var ext = PropagatedEmitter.EmitAll(auth, request)
            .Single(r => r.HintName == "PropagatedContextExtensions.g.cs");

        ext.GeneratedSource.Should().Contain("ToPropagatedContext(this IRequestContext context)");
    }

    [Fact]
    public void EmitAll_ExtensionsFile_ContainsApplyPropagatedContextMethod()
    {
        var (auth, request) = PropagateSpecs();
        var ext = PropagatedEmitter.EmitAll(auth, request)
            .Single(r => r.HintName == "PropagatedContextExtensions.g.cs");

        ext.GeneratedSource.Should().Contain("ApplyPropagatedContext(");
    }

    [Fact]
    public void EmitAll_ExtensionsFile_ProjectsPropagatedFieldsFromContext()
    {
        var (auth, request) = PropagateSpecs();
        var ext = PropagatedEmitter.EmitAll(auth, request)
            .Single(r => r.HintName == "PropagatedContextExtensions.g.cs");

        ext.GeneratedSource.Should().Contain("RequestId = context.RequestId,");
        ext.GeneratedSource.Should().Contain("RequestPath = context.RequestPath,");
    }

    // ------------------------------------------------------------------
    // PropagatedContextSerializer.g.cs — shape
    // ------------------------------------------------------------------

    [Fact]
    public void EmitAll_SerializerFile_ContainsEncodeAndTryDecodeSignatures()
    {
        var (auth, request) = PropagateSpecs();
        var serializer = PropagatedEmitter.EmitAll(auth, request)
            .Single(r => r.HintName == "PropagatedContextSerializer.g.cs");

        serializer.GeneratedSource.Should().Contain("static string Encode(");
        serializer.GeneratedSource.Should().Contain("static PropagatedContext? TryDecode(");
    }

    // ------------------------------------------------------------------
    // Determinism
    // ------------------------------------------------------------------

    [Fact]
    public void EmitAll_IdenticalInputs_ProduceDeterministicOutput()
    {
        var (auth, request) = PropagateSpecs();

        var first = PropagatedEmitter.EmitAll(auth, request);
        var second = PropagatedEmitter.EmitAll(auth, request);

        for (var i = 0; i < first.Count; i++)
            Normalize(second[i].GeneratedSource).Should().Be(Normalize(first[i].GeneratedSource));
    }

    // ------------------------------------------------------------------
    // OQ-1 — propagated list-of-records field (CallPath). The first
    // propagate:true list-of-records field; drives the type-aware emitter
    // branch. The existing SCALAR emission must stay byte-stable when a
    // list field is also present (the scalar regression pin at the emitter).
    // ------------------------------------------------------------------

    [Fact]
    public void EmitAll_RecordFile_ListField_EmittedAsNullableList()
    {
        var (auth, request) = PropagateSpecsWithCallPath();
        var record = PropagatedEmitter.EmitAll(auth, request)
            .Single(r => r.HintName == "PropagatedContext.g.cs");

        record.GeneratedSource.Should().Contain(
            "public IReadOnlyList<CallPathEntry>? CallPath { get; init; }");
    }

    [Fact]
    public void EmitAll_RecordFile_ListField_PullsInListAndVocabularyUsings()
    {
        var (auth, request) = PropagateSpecsWithCallPath();
        var record = PropagatedEmitter.EmitAll(auth, request)
            .Single(r => r.HintName == "PropagatedContext.g.cs");

        record.GeneratedSource.Should().Contain("using System.Collections.Generic;");
        record.GeneratedSource.Should().Contain("using DcsvIo.D2.Auth.Abstractions;");
    }

    [Fact]
    public void EmitAll_RecordFile_ListField_HasAnyFieldUsesCountArm()
    {
        var (auth, request) = PropagateSpecsWithCallPath();
        var record = PropagatedEmitter.EmitAll(auth, request)
            .Single(r => r.HintName == "PropagatedContext.g.cs");

        record.GeneratedSource.Should().Contain("CallPath is { Count: > 0 }");
    }

    [Fact]
    public void EmitAll_RecordFile_ScalarFieldsStayByteStable_WhenListFieldPresent()
    {
        // The scalar regression pin: introducing the list field must not
        // perturb scalar emission — string fields stay `string? X { get; init; }`
        // (NOT nullable-doubled) and their HasAnyField arm stays `is not null`.
        var (auth, request) = PropagateSpecsWithCallPath();
        var record = PropagatedEmitter.EmitAll(auth, request)
            .Single(r => r.HintName == "PropagatedContext.g.cs");

        record.GeneratedSource.Should().Contain("public string? RequestId { get; init; }");
        record.GeneratedSource.Should().Contain("public string? RequestPath { get; init; }");
        record.GeneratedSource.Should().Contain("RequestId is not null");
        record.GeneratedSource.Should().Contain("RequestPath is not null");
        record.GeneratedSource.Should().NotContain("IReadOnlyList<CallPathEntry>?? ");
    }

    [Fact]
    public void EmitAll_SerializerFile_ListField_EmitsDepthBoundAndPerEntryIdCap()
    {
        var (auth, request) = PropagateSpecsWithCallPath();
        var serializer = PropagatedEmitter.EmitAll(auth, request)
            .Single(r => r.HintName == "PropagatedContextSerializer.g.cs");
        var src = serializer.GeneratedSource;

        // Depth bound: maxLength on the list field reinterpreted as the max
        // entry count.
        src.Should().Contain("if (ctx.CallPath is { Count: > 16 }) return false;");

        // Per-entry id cap.
        src.Should().Contain("private const int _CALL_PATH_ENTRY_ID_MAX = 128;");
        src.Should().Contain("foreach (var entry in ctx.CallPath)");
        src.Should().Contain("if (entry.Id is { Length: > _CALL_PATH_ENTRY_ID_MAX })");

        // Enum members serialize as human-readable strings for log-grep-ability.
        src.Should().Contain("Converters = { new JsonStringEnumConverter() },");
    }

    [Fact]
    public void EmitAll_ExtensionsFile_ListField_NullWhenEmptyProjectionAndReplaceApply()
    {
        var (auth, request) = PropagateSpecsWithCallPath();
        var ext = PropagatedEmitter.EmitAll(auth, request)
            .Single(r => r.HintName == "PropagatedContextExtensions.g.cs");
        var src = Normalize(ext.GeneratedSource);

        // Projection: an empty path projects to null so it drops from the wire.
        src.Should().Contain(
            "CallPath = context.CallPath is { Count: > 0 } ? context.CallPath : null,");

        // Apply: a non-empty inbound path replaces (the receiving hop then
        // appends itself).
        src.Should().Contain("if (propagated.CallPath is { Count: > 0 })");
        src.Should().Contain("context.CallPath = propagated.CallPath;");
    }

    [Fact]
    public void EmitAll_ScalarOnlySpec_OmitsListMachinery()
    {
        // A scalar-only spec must NOT carry any of the list-of-records machinery
        // — the conditional emission keeps scalar-only outputs byte-identical to
        // the pre-CallPath generation.
        var (auth, request) = PropagateSpecs();
        var results = PropagatedEmitter.EmitAll(auth, request);
        var record = results.Single(r => r.HintName == "PropagatedContext.g.cs").GeneratedSource;
        var serializer = results
            .Single(r => r.HintName == "PropagatedContextSerializer.g.cs").GeneratedSource;

        record.Should().NotContain("using System.Collections.Generic;");
        record.Should().NotContain("using DcsvIo.D2.Auth.Abstractions;");
        record.Should().NotContain("CallPathEntry");
        serializer.Should().NotContain("_CALL_PATH_ENTRY_ID_MAX");
        serializer.Should().NotContain("JsonStringEnumConverter");
        serializer.Should().NotContain("foreach (var entry in");
    }

    // ------------------------------------------------------------------
    // Helpers — mirrors MutableEmitterTests factory style
    // (private members after public test methods per SA1202)
    // ------------------------------------------------------------------

    /// <summary>
    /// Specs with two propagatable string fields — the minimum needed to
    /// exercise the record / extensions / serializer emitters end-to-end.
    /// </summary>
    private static (ContextSpec Auth, ContextSpec Request) PropagateSpecs()
    {
        var auth = Spec("IAuthContext", "DcsvIo.D2.AuthContext.Abstractions");
        var request = Spec(
            "IRequestContext",
            "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            Section(
                "Tracing",
                Property("RequestId", "string?", propagate: true, maxLength: 128),
                Property("RequestPath", "string?", propagate: true, maxLength: 512)));
        return (auth, request);
    }

    /// <summary>
    /// Specs with zero propagatable fields — the HasAnyField guard must still
    /// appear even when the propagated set is empty.
    /// </summary>
    private static (ContextSpec Auth, ContextSpec Request) EmptyPropagateSpecs()
    {
        var auth = Spec("IAuthContext", "DcsvIo.D2.AuthContext.Abstractions");
        var request = Spec(
            "IRequestContext",
            "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            Section("Tracing", Property("InternalOnly", "string?", propagate: false)));
        return (auth, request);
    }

    /// <summary>
    /// Specs that add the propagated list-of-records field <c>CallPath</c>
    /// (<c>IReadOnlyList&lt;CallPathEntry&gt;</c>, depth bound 16) alongside the
    /// two scalar fields — exercises the OQ-1 type-aware emitter branch while
    /// keeping the scalar emission in the same output for the regression pin.
    /// </summary>
    private static (ContextSpec Auth, ContextSpec Request) PropagateSpecsWithCallPath()
    {
        var auth = Spec("IAuthContext", "DcsvIo.D2.AuthContext.Abstractions");
        var request = Spec(
            "IRequestContext",
            "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            Section(
                "Tracing",
                Property("RequestId", "string?", propagate: true, maxLength: 128),
                Property("RequestPath", "string?", propagate: true, maxLength: 512)),
            Section(
                "Establishment",
                Property(
                    "CallPath",
                    "IReadOnlyList<CallPathEntry>",
                    propagate: true,
                    maxLength: 16,
                    entryIdMaxLength: 128)));
        return (auth, request);
    }

    private static ContextSpec Spec(
        string name,
        string @namespace,
        string? extends = null,
        params Section[] sections) =>
        new(name, @namespace, Description: null, extends, [.. sections]);

    private static Section Section(string name, params PropertySpec[] props) =>
        new(name, [.. props]);

    private static PropertySpec Property(
        string name,
        string type,
        bool propagate = false,
        int? maxLength = null,
        int? entryIdMaxLength = null) =>
        new(
            name,
            type,
            Claim: null,
            TrinaryAuth: false,
            Derived: null,
            Default: null,
            Doc: null,
            propagate,
            maxLength,
            entryIdMaxLength,
            Redact: false);

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Trim();
}
