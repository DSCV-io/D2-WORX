// -----------------------------------------------------------------------
// <copyright file="ProtocolAudiencesEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.SourceGen;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Auth.ProtocolAudiences.SourceGen;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="ProtocolAudiencesEmitter.Emit"/> — the
/// pure-logic half of the protocol-audiences source generator. Covers semantic
/// validation (D2PAUD001 / D2PAUD002 / D2PAUD003 / D2PAUD004) and emission shape.
/// </summary>
public sealed class ProtocolAudiencesEmitterTests
{
    // ----------------------------------------------------------------------
    // Happy path
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_HappyPath_ProducesConstants()
    {
        var spec = new ProtocolAudiencesSpec(
        [
            Entry("D2_INTERNAL_AUDIENCE", "d2.internal", "Internal receive audience."),
            Entry("D2_EDGE_SELF_AUDIENCE", "d2-edge", "Edge self-audience."),
        ]);

        var result = ProtocolAudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static partial class WellKnownAudiences");
        result.GeneratedSource.Should().Contain(
            "public const string D2_INTERNAL_AUDIENCE = \"d2.internal\";");
        result.GeneratedSource.Should().Contain(
            "public const string D2_EDGE_SELF_AUDIENCE = \"d2-edge\";");
    }

    [Fact]
    public void Emit_OrdersConstantsByNameOrdinal()
    {
        var spec = new ProtocolAudiencesSpec(
        [
            Entry("D2_INTERNAL_AUDIENCE", "d2.internal"),
            Entry("D2_EDGE_SELF_AUDIENCE", "d2-edge"),
        ]);

        var result = ProtocolAudiencesEmitter.Emit(spec);

        var src = result.GeneratedSource;
        var edgePos = src.IndexOf("D2_EDGE_SELF_AUDIENCE", StringComparison.Ordinal);
        var internalPos = src.IndexOf("D2_INTERNAL_AUDIENCE", StringComparison.Ordinal);
        edgePos.Should().BeGreaterThan(0);
        internalPos.Should().BeGreaterThan(edgePos);
    }

    [Fact]
    public void Emit_RendersDescriptionAsXmlDoc()
    {
        var spec = new ProtocolAudiencesSpec(
        [
            Entry("D2_INTERNAL_AUDIENCE", "d2.internal", "Universal internal receive audience."),
        ]);

        var result = ProtocolAudiencesEmitter.Emit(spec);

        result.GeneratedSource.Should().Contain("/// Universal internal receive audience.");
    }

    [Fact]
    public void Emit_FallsBackToValueWhenDescriptionAbsent()
    {
        var spec = new ProtocolAudiencesSpec([Entry("D2_INTERNAL_AUDIENCE", "d2.internal")]);

        var result = ProtocolAudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("/// d2.internal");
    }

    // ----------------------------------------------------------------------
    // D2PAUD002 — invalid protocol-audience name (must be SCREAMING_SNAKE_CASE)
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("d2Internal")] // lowercase first letter
    [InlineData("123_AUD")] // starts with digit
    [InlineData("My-Aud")] // hyphen
    [InlineData("My.Aud")] // dot
    [InlineData("My Aud")] // space
    [InlineData("lowercase")] // all lowercase
    [InlineData("")] // empty
    public void Emit_InvalidName_EmitsD2PAUD002(string badName)
    {
        var spec = new ProtocolAudiencesSpec([Entry(badName, "d2.internal")]);

        var result = ProtocolAudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.InvalidName);
        result.GeneratedSource.Should().NotContain($"    public const string {badName} = ");
    }

    [Theory]
    [InlineData("D2_INTERNAL_AUDIENCE")]
    [InlineData("ABC123")]
    [InlineData("X")]
    [InlineData("A_B_C")]
    public void Emit_ValidName_EmitsConstant(string goodName)
    {
        var spec = new ProtocolAudiencesSpec([Entry(goodName, "d2.internal")]);

        var result = ProtocolAudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain($"public const string {goodName} = ");
    }

    // ----------------------------------------------------------------------
    // D2PAUD001 — duplicate name; D2PAUD003 — duplicate value; D2PAUD004 — empty
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_DuplicateName_EmitsD2PAUD001AndDropsSecond()
    {
        var spec = new ProtocolAudiencesSpec(
        [
            Entry("D2_INTERNAL_AUDIENCE", "d2.internal"),
            Entry("D2_INTERNAL_AUDIENCE", "d2.other"),
        ]);

        var result = ProtocolAudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.DuplicateName);
        result.GeneratedSource.Should().Contain(
            "public const string D2_INTERNAL_AUDIENCE = \"d2.internal\";");
        result.GeneratedSource.Should().NotContain("d2.other");
    }

    [Fact]
    public void Emit_DuplicateValue_EmitsD2PAUD003AndDropsSecond()
    {
        var spec = new ProtocolAudiencesSpec(
        [
            Entry("FIRST", "same.value"),
            Entry("SECOND", "same.value"),
        ]);

        var result = ProtocolAudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.DuplicateValue);
        result.GeneratedSource.Should().Contain(
            "public const string FIRST = \"same.value\";");
        result.GeneratedSource.Should().NotContain("SECOND");
    }

    [Fact]
    public void Emit_EmptyValue_EmitsD2PAUD004AndDropsEntry()
    {
        var spec = new ProtocolAudiencesSpec([Entry("EMPTY_ONE", string.Empty)]);

        var result = ProtocolAudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.EmptyValue);
        result.GeneratedSource.Should().NotContain("public const string EMPTY_ONE");
    }

    private static ProtocolAudienceEntry Entry(
        string name, string value, string? description = null) =>
        new(Name: name, Value: value, Description: description);
}
