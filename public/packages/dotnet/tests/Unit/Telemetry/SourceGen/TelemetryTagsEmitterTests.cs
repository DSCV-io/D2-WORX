// -----------------------------------------------------------------------
// <copyright file="TelemetryTagsEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias TelemetryTagsSourceGen;

namespace DcsvIo.D2.Tests.Unit.Telemetry.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.Telemetry.Tags.SourceGen;
using Xunit;
using SpecFile = TelemetryTagsSourceGen::DcsvIo.D2.SourceGen.SpecFile;

/// <summary>
/// Pure-logic tests for the telemetry-tags emitter.
/// </summary>
public sealed class TelemetryTagsEmitterTests
{
    [Fact]
    public void Emit_MeterWithSingleTaggedCounter_EmitsTypedConstantsClass()
    {
        var tag = new TagEntry("outcome", ImmutableArray.Create("ok", "err"), null);
        var inst = new InstrumentEntry(
            Name: "m.a.counter",
            ConstName: "MyCounter",
            Kind: "counter",
            Description: "A counter.",
            Unit: null,
            Tags: ImmutableArray.Create(tag));
        var meter = MakeMeter("M.A", "Asm.A", inst);

        var result = TelemetryTagsEmitter.Emit(meter, ImmutableArray<SpecFile>.Empty);

        result.Diagnostics.Should().BeEmpty();
        result.HintName.Should().Be("ATelemetryTags.g.cs");
        result.GeneratedSource.Should().Contain("public static class ATelemetryTags");
        result.GeneratedSource.Should().Contain("public static class MyCounter");
        result.GeneratedSource.Should().Contain("public const string TAG_OUTCOME = \"outcome\";");
        result.GeneratedSource.Should().Contain("public static class Outcome");
        result.GeneratedSource.Should().Contain("public const string OK = \"ok\";");
        result.GeneratedSource.Should().Contain("public const string ERR = \"err\";");
    }

    [Fact]
    public void Emit_MeterWithUntaggedInstrumentsOnly_EmitsNothing()
    {
        var inst = new InstrumentEntry(
            Name: "m.a.counter",
            ConstName: null,
            Kind: "counter",
            Description: "X",
            Unit: null,
            Tags: ImmutableArray<TagEntry>.Empty);
        var meter = MakeMeter("M.A", "Asm.A", inst);

        var result = TelemetryTagsEmitter.Emit(meter, ImmutableArray<SpecFile>.Empty);

        result.Diagnostics.Should().BeEmpty();
        result.HintName.Should().BeEmpty();
        result.GeneratedSource.Should().BeEmpty();
    }

    [Fact]
    public void Emit_DuplicateInstrumentName_EmitsDuplicateInstrumentDiagnostic()
    {
        var tag = new TagEntry("t", ImmutableArray.Create("a"), null);
        var tag2 = new TagEntry("t", ImmutableArray.Create("b"), null);
        var meter = MakeMeter(
            "M.A",
            "Asm.A",
            MakeInstrument("m.a.counter", "counter", tag),
            MakeInstrument("m.a.counter", "counter", tag2));

        var result = TelemetryTagsEmitter.Emit(meter, ImmutableArray<SpecFile>.Empty);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateInstrument);
    }

    [Fact]
    public void Emit_UnknownKind_EmitsUnknownInstrumentKindDiagnostic()
    {
        var tag = new TagEntry("t", ImmutableArray.Create("a"), null);
        var meter = MakeMeter(
            "M.A",
            "Asm.A",
            MakeInstrument("x", "bogus_kind", tag));

        var result = TelemetryTagsEmitter.Emit(meter, ImmutableArray<SpecFile>.Empty);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.UnknownInstrumentKind);
    }

    [Fact]
    public void Emit_DuplicateTagValue_EmitsDuplicateTagValueDiagnostic()
    {
        var tag = new TagEntry("t", ImmutableArray.Create("a", "a"), null);
        var meter = MakeMeter("M.A", "Asm.A", MakeInstrument("x", "counter", tag));

        var result = TelemetryTagsEmitter.Emit(meter, ImmutableArray<SpecFile>.Empty);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateTagValue);
    }

    [Fact]
    public void Emit_CrossSpecValuesWithoutSibling_EmitsCrossSpecInconsistencyDiagnostic()
    {
        var tag = new TagEntry("t", ImmutableArray<string>.Empty, "auth-error-codes");
        var meter = MakeMeter("M.A", "Asm.A", MakeInstrument("x", "counter", tag));

        var result = TelemetryTagsEmitter.Emit(meter, ImmutableArray<SpecFile>.Empty);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.CrossSpecInconsistency);
    }

    [Fact]
    public void Emit_TagsClassNameOverride_AppliesToEmittedClass()
    {
        var tag = new TagEntry("t", ImmutableArray.Create("a"), null);
        var inst = new InstrumentEntry(
            Name: "x",
            ConstName: "MyInst",
            Kind: "counter",
            Description: "X",
            Unit: null,
            Tags: ImmutableArray.Create(tag));
        var meter = MakeMeterFull(
            "M.A",
            "Asm.A",
            tagsNamespace: "Custom.Ns",
            tagsClassName: "CustomTags",
            inst);

        var result = TelemetryTagsEmitter.Emit(meter, ImmutableArray<SpecFile>.Empty);

        result.HintName.Should().Be("CustomTags.g.cs");
        result.GeneratedSource.Should().Contain("namespace Custom.Ns;");
        result.GeneratedSource.Should().Contain("public static class CustomTags");
    }

    [Fact]
    public void ResolveClassName_DerivesPascalCaseFromMeterLastSegment_WhenNoOverride()
    {
        var meter = MakeMeter("DcsvIo.D2.Auth", "Asm.A");

        var name = TelemetryTagsEmitter.ResolveClassName(meter);

        name.Should().Be("AuthTelemetryTags");
    }

    [Fact]
    public void ResolveNamespace_DefaultsToConsumingAssemblyDotTelemetry_WhenNoOverride()
    {
        var meter = MakeMeter("DcsvIo.D2.Auth", "DcsvIo.D2.Auth");

        var ns = TelemetryTagsEmitter.ResolveNamespace(meter);

        ns.Should().Be("DcsvIo.D2.Auth.Telemetry");
    }

    private static MeterEntry MakeMeter(
        string meter, string consumingAssembly, params InstrumentEntry[] instruments) =>
        new(
            Meter: meter,
            ConsumingAssembly: consumingAssembly,
            TagsNamespace: null,
            TagsClassName: null,
            Instruments: instruments.ToImmutableArray());

    private static MeterEntry MakeMeterFull(
        string meter,
        string consumingAssembly,
        string? tagsNamespace,
        string? tagsClassName,
        params InstrumentEntry[] instruments) =>
        new(
            Meter: meter,
            ConsumingAssembly: consumingAssembly,
            TagsNamespace: tagsNamespace,
            TagsClassName: tagsClassName,
            Instruments: instruments.ToImmutableArray());

    private static InstrumentEntry MakeInstrument(string name, string kind, TagEntry tag) =>
        new(
            Name: name,
            ConstName: null,
            Kind: kind,
            Description: "x",
            Unit: null,
            Tags: ImmutableArray.Create(tag));
}
