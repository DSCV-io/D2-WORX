// -----------------------------------------------------------------------
// <copyright file="OtelMessagingTagsEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.OtelMessagingTags.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the OtelMessagingTags emitter. §1.20 fail-path
/// proof: drives the emitter with valid + 3 deliberate-drift specs and
/// asserts both the emit shape and the diagnostics.
/// </summary>
public sealed class OtelMessagingTagsEmitterTests
{
    [Fact]
    public void Emit_ValidSingleEntry_EmitsConstantAndAllTags()
    {
        var spec = MakeSpec(
            new OtelMessagingTagEntry("MESSAGING_SYSTEM", "messaging.system", "doc"));

        var result = OtelMessagingTagsEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should()
            .Contain("public const string MESSAGING_SYSTEM = \"messaging.system\";");
        result.GeneratedSource.Should()
            .Contain("public static class MessagingActivityTags");
        result.GeneratedSource.Should()
            .Contain("public static IReadOnlyList<string> AllTags => sr_allTags;");
    }

    [Fact]
    public void Emit_MessagingOperationType_PinsTheCanonicalAttributeName()
    {
        // Regression pin: the OTel sem-conv canonical attribute is
        // "messaging.operation.type", NOT the non-standard
        // "messaging.operation". Spec-driving the attribute name structurally
        // prevents drift from the standard.
        var spec = MakeSpec(
            new OtelMessagingTagEntry(
                "MESSAGING_OPERATION_TYPE", "messaging.operation.type", "doc"));

        var result = OtelMessagingTagsEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should()
            .Contain(
                "public const string MESSAGING_OPERATION_TYPE = \"messaging.operation.type\";");
        result.GeneratedSource.Should()
            .NotContain("\"messaging.operation\"");
    }

    // ---------------------------------------------------------------
    // §1.20 fail-path proof — 3 deliberate drift cases.
    // ---------------------------------------------------------------

    [Fact]
    public void Emit_DuplicateConstName_EmitsDuplicateConstNameDiagnostic()
    {
        // DRIFT CASE 1.
        var spec = MakeSpec(
            new OtelMessagingTagEntry("TAG", "a", "doc"),
            new OtelMessagingTagEntry("TAG", "b", "doc"));

        var result = OtelMessagingTagsEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateConstName);
    }

    [Fact]
    public void Emit_DuplicateValue_EmitsDuplicateValueDiagnostic()
    {
        // DRIFT CASE 2.
        var spec = MakeSpec(
            new OtelMessagingTagEntry("X", "a", "doc"),
            new OtelMessagingTagEntry("Y", "a", "doc"));

        var result = OtelMessagingTagsEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateValue);
    }

    [Fact]
    public void Emit_InvalidConstName_EmitsInvalidConstNameDiagnostic()
    {
        // DRIFT CASE 3.
        var spec = MakeSpec(new OtelMessagingTagEntry("lowerCase", "a", "doc"));

        var result = OtelMessagingTagsEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidConstName);
    }

    private static OtelMessagingTagsSpec MakeSpec(params OtelMessagingTagEntry[] entries) =>
        new(entries.ToImmutableArray());
}
