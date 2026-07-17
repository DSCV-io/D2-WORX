// -----------------------------------------------------------------------
// <copyright file="ErrorCodesDeprecationEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias ResultErrorCodesSourceGen;

namespace DcsvIo.D2.Tests.Unit.Result.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using Xunit;
using BaseFactoriesEmitter =
    ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.BaseFactoriesEmitter;
using CatalogConfig = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.CatalogConfig;
using ConstantsEmitter = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.ConstantsEmitter;
using ErrorCodeEntry = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.ErrorCodeEntry;
using ErrorCodesGenerator =
    ResultErrorCodesSourceGen::DcsvIo.D2.ResultErrorCodes.SourceGen.ErrorCodesGenerator;
using ErrorCodesSpec = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.ErrorCodesSpec;

/// <summary>
/// Codegen tests for the contract-side deprecate-not-delete marker
/// (<c>"deprecated": true</c> + <c>deprecatedReason</c> + <c>replacedBy</c>) on
/// the .NET error-code emitters. A deprecated entry MUST emit
/// <c>[System.Obsolete("&lt;reason&gt;. Use &lt;replacedBy&gt; instead.")]</c> on
/// the generated constant + every generated factory; a non-deprecated entry
/// MUST emit no <c>[Obsolete]</c> at all. Driven by SYNTHETIC fixture entries —
/// no real production spec entry is deprecated by this step.
/// </summary>
public sealed class ErrorCodesDeprecationEmitterTests
{
    // The composed [Obsolete] message a deprecated fixture entry must render.
    private const string _EXPECTED_OBSOLETE =
        "[System.Obsolete(\"Ambiguous between resource-missing and route-missing; "
        + "split into two codes. Use RESOURCE_NOT_FOUND instead.\")]";

    private static CatalogConfig Config => ErrorCodesGenerator.Config;

    [Fact]
    public void Constants_DeprecatedEntry_EmitsObsoleteAttributeWithComposedMessage()
    {
        var spec = MakeSpec(DeprecatedEntry());

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain(_EXPECTED_OBSOLETE);
        result.GeneratedSource.Should().Contain(
            "public const string NOT_FOUND = \"NOT_FOUND\";");

        // The attribute must immediately precede the constant declaration.
        var attrIndex = result.GeneratedSource.IndexOf(
            _EXPECTED_OBSOLETE, System.StringComparison.Ordinal);
        var constIndex = result.GeneratedSource.IndexOf(
            "public const string NOT_FOUND", System.StringComparison.Ordinal);
        attrIndex.Should().BeLessThan(constIndex);
    }

    [Fact]
    public void Constants_DeprecatedEntryNoReplacedBy_EmitsReasonOnlyMessage()
    {
        var spec = MakeSpec(DeprecatedEntry() with { ReplacedBy = null });

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain(
            "[System.Obsolete(\"Ambiguous between resource-missing and route-missing; "
            + "split into two codes.\")]");
        result.GeneratedSource.Should().NotContain("Use RESOURCE_NOT_FOUND instead.");
    }

    [Fact]
    public void Constants_DeprecatedEntryEmptyReplacedBy_EmitsReasonOnlyMessage()
    {
        var spec = MakeSpec(DeprecatedEntry() with { ReplacedBy = "   " });

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().NotContain("Use ");
    }

    [Fact]
    public void Constants_NonDeprecatedEntry_EmitsNoObsoleteAttribute()
    {
        var spec = MakeSpec(new ErrorCodeEntry("NOT_FOUND", 404, "X doc."));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().NotContain("System.Obsolete");
        result.GeneratedSource.Should().Contain(
            "public const string NOT_FOUND = \"NOT_FOUND\";");
    }

    [Fact]
    public void BaseFactory_DeprecatedEntry_EmitsObsoleteAttributeOnFactory()
    {
        var spec = MakeSpec(DeprecatedFactoryEntry());

        var result = BaseFactoriesEmitter.EmitFactories(spec, Config);

        result.GeneratedSource.Should().Contain(_EXPECTED_OBSOLETE);

        // Attribute precedes the static factory method declaration.
        var attrIndex = result.GeneratedSource.IndexOf(
            _EXPECTED_OBSOLETE, System.StringComparison.Ordinal);
        var fnIndex = result.GeneratedSource.IndexOf(
            "public static D2Result NotFound", System.StringComparison.Ordinal);
        attrIndex.Should().BeLessThan(fnIndex);
    }

    [Fact]
    public void BaseFactory_NonDeprecatedEntry_EmitsNoObsoleteAttribute()
    {
        var spec = MakeSpec(DeprecatedFactoryEntry() with { Deprecated = false });

        var result = BaseFactoriesEmitter.EmitFactories(spec, Config);

        result.GeneratedSource.Should().NotContain("System.Obsolete");
    }

    [Fact]
    public void Boolean_DeprecatedEntry_EmitsObsoleteAttributeOnBoolean()
    {
        var spec = MakeSpec(DeprecatedFactoryEntry());

        var result = BaseFactoriesEmitter.EmitBooleans(spec, Config);

        result.GeneratedSource.Should().Contain(_EXPECTED_OBSOLETE);
        result.GeneratedSource.Should().Contain("public bool IsNotFound");
    }

    // Mirrors the worked example's NOT_FOUND entry, marked deprecated. The
    // generic catalog is constants-only at this surface (no delegating factory),
    // so the factory fields stay null for the constants assertion.
    private static ErrorCodeEntry DeprecatedEntry() =>
        new(
            Code: "NOT_FOUND",
            HttpStatus: 404,
            Doc: "Indicates that the requested resource was not found.",
            Deprecated: true,
            DeprecatedReason:
                "Ambiguous between resource-missing and route-missing; split into two codes.",
            ReplacedBy: "RESOURCE_NOT_FOUND",
            Sunset: "2027-01-01");

    // The factory-bearing twin: a standard-shape generic base factory + boolean.
    private static ErrorCodeEntry DeprecatedFactoryEntry() =>
        DeprecatedEntry() with
        {
            Category = "not_found",
            UserMessageKey = "TK.Common.Errors.NOT_FOUND",
            FactoryName = "NotFound",
            FactoryShape = "standard",
        };

    private static ErrorCodesSpec MakeSpec(params ErrorCodeEntry[] entries) =>
        new(entries.ToImmutableArray());
}
