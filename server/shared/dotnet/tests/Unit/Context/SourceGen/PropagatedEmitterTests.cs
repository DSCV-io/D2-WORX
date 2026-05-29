// -----------------------------------------------------------------------
// <copyright file="PropagatedEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Context.SourceGen;

using AwesomeAssertions;
using D2.Shared.Context.SourceGen;
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
    // [JsonIgnore] on HasAnyField — regression-pin target (F-A1-04)
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
    // Helpers — mirrors MutableEmitterTests factory style
    // (private members after public test methods per SA1202)
    // ------------------------------------------------------------------

    /// <summary>
    /// Specs with two propagatable string fields — the minimum needed to
    /// exercise the record / extensions / serializer emitters end-to-end.
    /// </summary>
    private static (ContextSpec Auth, ContextSpec Request) PropagateSpecs()
    {
        var auth = Spec("IAuthContext", "D2.Shared.AuthContext.Abstractions");
        var request = Spec(
            "IRequestContext",
            "D2.Shared.Context.Abstractions",
            extends: "D2.Shared.AuthContext.Abstractions.IAuthContext",
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
        var auth = Spec("IAuthContext", "D2.Shared.AuthContext.Abstractions");
        var request = Spec(
            "IRequestContext",
            "D2.Shared.Context.Abstractions",
            extends: "D2.Shared.AuthContext.Abstractions.IAuthContext",
            Section("Tracing", Property("InternalOnly", "string?", propagate: false)));
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
        int? maxLength = null) =>
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
            Redact: false);

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Trim();
}
