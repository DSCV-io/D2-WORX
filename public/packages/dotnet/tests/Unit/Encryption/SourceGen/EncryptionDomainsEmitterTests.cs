// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.EncryptionDomains.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the EncryptionDomains emitter. §1.20 fail-path
/// proof.
/// </summary>
public sealed class EncryptionDomainsEmitterTests
{
    [Fact]
    public void Emit_ValidSingleEntry_EmitsConstantAndAllDomains()
    {
        var spec = MakeSpec(new EncryptionDomainEntry("AUDIT", "audit", "doc"));

        var result = EncryptionDomainsEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should()
            .Contain("public const string AUDIT = \"audit\";");
        result.GeneratedSource.Should()
            .Contain("public static class EncryptionDomains");
        result.GeneratedSource.Should()
            .Contain("public static IReadOnlyList<string> AllDomains => sr_allDomains;");
    }

    [Fact]
    public void Emit_Always_EmitsModeEnumAndModesClass()
    {
        var spec = MakeSpec(new EncryptionDomainEntry("AUDIT", "audit", "doc"));

        var result = EncryptionDomainsEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public enum EncryptionDomainMode");
        result.GeneratedSource.Should().Contain("Symmetric,");
        result.GeneratedSource.Should().Contain("Sealed,");
        result.GeneratedSource.Should().Contain("public static class EncryptionDomainModes");
        result.GeneratedSource.Should()
            .Contain("public static EncryptionDomainMode ModeFor(string domain)");
        result.GeneratedSource.Should()
            .Contain(
                "public static IReadOnlyDictionary<string, string> ConsumerServiceByDomain =>");
        result.GeneratedSource.Should()
            .Contain(
                "public static bool TryGetConsumerService(string domain, out string consumerService)");
    }

    [Fact]
    public void Emit_SealedEntry_MapsDomainToSealedAndConsumerService()
    {
        var spec = MakeSpec(
            new EncryptionDomainEntry("AUDIT", "audit", "doc", "sealed", "audit"),
            new EncryptionDomainEntry("PLAINTEXT", "plaintext", "doc"));

        var result = EncryptionDomainsEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should()
            .Contain("{ \"audit\", EncryptionDomainMode.Sealed },");
        result.GeneratedSource.Should()
            .Contain("{ \"plaintext\", EncryptionDomainMode.Symmetric },");
        result.GeneratedSource.Should()
            .Contain("{ \"audit\", \"audit\" },");

        // The symmetric/plaintext domain never appears in the consumer map.
        result.GeneratedSource.Should().NotContain("{ \"plaintext\", \"");
    }

    [Fact]
    public void Emit_ExplicitSymmetricMode_TreatedAsSymmetric()
    {
        var spec = MakeSpec(
            new EncryptionDomainEntry("METRICS", "metrics", "doc", "symmetric"));

        var result = EncryptionDomainsEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should()
            .Contain("{ \"metrics\", EncryptionDomainMode.Symmetric },");
    }

    // ---------------------------------------------------------------
    // §1.20 fail-path proof — 3 deliberate drift cases.
    // ---------------------------------------------------------------

    [Fact]
    public void Emit_DuplicateConstName_EmitsDuplicateConstNameDiagnostic()
    {
        var spec = MakeSpec(
            new EncryptionDomainEntry("X", "a", "doc"),
            new EncryptionDomainEntry("X", "b", "doc"));

        var result = EncryptionDomainsEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateConstName);
    }

    [Fact]
    public void Emit_DuplicateValue_EmitsDuplicateValueDiagnostic()
    {
        var spec = MakeSpec(
            new EncryptionDomainEntry("X", "a", "doc"),
            new EncryptionDomainEntry("Y", "a", "doc"));

        var result = EncryptionDomainsEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateValue);
    }

    [Fact]
    public void Emit_InvalidConstName_EmitsInvalidConstNameDiagnostic()
    {
        var spec = MakeSpec(new EncryptionDomainEntry("lowerCase", "a", "doc"));

        var result = EncryptionDomainsEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidConstName);
    }

    // ---------------------------------------------------------------
    // Mode / consumerService fail-path proof (§1.20 / §26.15).
    // ---------------------------------------------------------------

    [Fact]
    public void Emit_InvalidModeValue_EmitsInvalidModeDiagnostic()
    {
        var spec = MakeSpec(new EncryptionDomainEntry("X", "x", "doc", "asymmetric"));

        var result = EncryptionDomainsEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidMode);
    }

    [Fact]
    public void Emit_SealedWithoutConsumerService_EmitsMissingConsumerServiceDiagnostic()
    {
        var spec = MakeSpec(new EncryptionDomainEntry("X", "x", "doc", "sealed"));

        var result = EncryptionDomainsEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.MissingConsumerService);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("symmetric")]
    public void Emit_ConsumerServiceWithoutSealed_EmitsUnexpectedConsumerServiceDiagnostic(
        string? mode)
    {
        var spec = MakeSpec(new EncryptionDomainEntry("X", "x", "doc", mode, "svc"));

        var result = EncryptionDomainsEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.UnexpectedConsumerService);
    }

    [Theory]
    [InlineData("Audit")]
    [InlineData("has space")]
    [InlineData("under_score")]
    [InlineData("")]
    public void Emit_SealedWithBadConsumerServiceGrammar_EmitsInvalidConsumerServiceDiagnostic(
        string consumerService)
    {
        // An empty consumerService is caught as MISSING (Falsey) before the
        // grammar check, so only the non-empty malformed cases assert
        // InvalidConsumerService; the empty case asserts Missing.
        var spec = MakeSpec(
            new EncryptionDomainEntry("X", "x", "doc", "sealed", consumerService));

        var result = EncryptionDomainsEmitter.Emit(spec);

        var expected = consumerService.Length == 0
            ? DiagnosticIds.MissingConsumerService
            : DiagnosticIds.InvalidConsumerService;
        result.Diagnostics.Should().ContainSingle(d => d.DescriptorId == expected);
    }

    [Fact]
    public void Emit_ConsumerServiceOverSixtyFourChars_EmitsInvalidConsumerServiceDiagnostic()
    {
        var tooLong = new string('a', 65);
        var spec = MakeSpec(
            new EncryptionDomainEntry("X", "x", "doc", "sealed", tooLong));

        var result = EncryptionDomainsEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidConsumerService);
    }

    private static EncryptionDomainsSpec MakeSpec(params EncryptionDomainEntry[] entries) =>
        new(entries.ToImmutableArray());
}
