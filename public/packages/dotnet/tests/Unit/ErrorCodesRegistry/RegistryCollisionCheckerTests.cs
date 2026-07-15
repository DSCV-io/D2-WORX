// -----------------------------------------------------------------------
// <copyright file="RegistryCollisionCheckerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias RegistrySourceGen;

namespace DcsvIo.D2.Tests.Unit.ErrorCodesRegistry;

using AwesomeAssertions;
using RegistrySourceGen::DcsvIo.D2.ErrorCodes.Registry.SourceGen;
using Xunit;

/// <summary>
/// Unit tests for <see cref="RegistryCollisionChecker"/> — the pure-logic
/// cross-catalog safety gate. Tests run without a Roslyn driver, directly
/// exercising the diagnostic-production logic over synthetic
/// <see cref="RegistrySpecEntry"/> inputs.
/// </summary>
public sealed class RegistryCollisionCheckerTests
{
    // -----------------------------------------------------------------------
    // Cross-catalog collision (D2ERC004)
    // -----------------------------------------------------------------------

    [Fact]
    public void Check_NoDuplicates_ReturnsNoCollisionDiagnostics()
    {
        var entries = new[]
        {
            MakeEntry("NOT_FOUND", "common", "error-codes.spec.json"),
            MakeEntry("AUTH_BEARER_MISSING", "auth", "auth-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().NotContain(
            d => d.DescriptorId == RegistryDiagnosticIds.CrossCatalogDuplicateCode);
    }

    [Fact]
    public void Check_SameCodeInTwoCatalogs_EmitsD2ERC004()
    {
        var entries = new[]
        {
            MakeEntry("NOT_FOUND", "common", "error-codes.spec.json"),
            MakeEntry("NOT_FOUND", "auth", "auth-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().Contain(
            d => d.DescriptorId == RegistryDiagnosticIds.CrossCatalogDuplicateCode,
            because: "two catalogs declaring the same code must fire D2ERC004");
    }

    [Fact]
    public void Check_DuplicateWithinSameCatalog_EmitsDuplicateDiagnostic()
    {
        // Two entries with the same code from the same spec file. The
        // cross-catalog checker fires on any duplicated code in the aggregate
        // set — intra-catalog duplication should have been caught by the
        // per-catalog emitter but is still surfaced here if it reaches the
        // registry.
        var entries = new[]
        {
            MakeEntry("NOT_FOUND", "common", "error-codes.spec.json"),
            MakeEntry("NOT_FOUND", "common", "error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().Contain(
            d => d.DescriptorId == RegistryDiagnosticIds.CrossCatalogDuplicateCode,
            because: "any duplicate code in the aggregate set is a collision");
    }

    [Fact]
    public void Check_EmptyEntries_ReturnsNoDiagnostics()
    {
        var diagnostics = RegistryCollisionChecker.Check([]);
        diagnostics.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Reserved-namespace violation (D2ERC005)
    // -----------------------------------------------------------------------

    [Fact]
    public void Check_UnprefixedCodeInDomainSpec_EmitsD2ERC005()
    {
        var entries = new[]
        {
            MakeEntry("NOT_FOUND", "common", "error-codes.spec.json"),

            // UNPREFIXED_CODE has no AUTH_ prefix — violation in auth catalog.
            MakeEntry("UNPREFIXED_CODE", "auth", "auth-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().Contain(
            d => d.DescriptorId == RegistryDiagnosticIds.ReservedNamespaceViolation,
            because: "an unprefixed code in a per-domain spec must fire D2ERC005");
    }

    [Fact]
    public void Check_PrefixedCodeInGenericSpec_EmitsD2ERC005()
    {
        // AUTH_BEARER_MISSING in the generic (common) catalog — violation.
        var entries = new[]
        {
            MakeEntry("AUTH_BEARER_MISSING", "common", "error-codes.spec.json"),
            MakeEntry("AUTH_REAL_CODE", "auth", "auth-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().Contain(
            d => d.DescriptorId == RegistryDiagnosticIds.ReservedNamespaceViolation,
            because: "a domain-prefixed code in the generic spec must fire D2ERC005");
    }

    [Fact]
    public void Check_ValidGenericCodeNoAuthDomain_NoReservedNamespaceDiagnostic()
    {
        // Without any per-domain catalog present there are no known domain
        // prefixes, so the generic catalog's codes cannot be "prefixed"
        // by definition. No D2ERC005 should fire.
        var entries = new[]
        {
            MakeEntry("NOT_FOUND", "common", "error-codes.spec.json"),
            MakeEntry("CONFLICT", "common", "error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().NotContain(
            d => d.DescriptorId == RegistryDiagnosticIds.ReservedNamespaceViolation);
    }

    [Fact]
    public void Check_ValidAuthCode_NoReservedNamespaceDiagnostic()
    {
        var entries = new[]
        {
            MakeEntry("NOT_FOUND", "common", "error-codes.spec.json"),
            MakeEntry("AUTH_BEARER_MISSING", "auth", "auth-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().NotContain(
            d => d.DescriptorId == RegistryDiagnosticIds.ReservedNamespaceViolation);
    }

    private static RegistrySpecEntry MakeEntry(
        string code, string domain, string specFileName) =>
        new(
            Code: code,
            HttpStatus: 400,
            Category: "validation_failure",
            UserMessageKey: "TK.Common.Errors.VALIDATION_FAILED",
            FactoryName: "TestFactory",
            FactoryShape: "standard",
            Doc: "Test.",
            Domain: domain,
            SpecFileName: specFileName);
}
