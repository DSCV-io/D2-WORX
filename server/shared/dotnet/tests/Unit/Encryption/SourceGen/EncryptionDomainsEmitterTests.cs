// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Encryption.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using D2.Shared.EncryptionDomains.SourceGen;
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

    private static EncryptionDomainsSpec MakeSpec(params EncryptionDomainEntry[] entries) =>
        new(entries.ToImmutableArray());
}
