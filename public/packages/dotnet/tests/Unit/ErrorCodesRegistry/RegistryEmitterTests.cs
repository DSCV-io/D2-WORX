// -----------------------------------------------------------------------
// <copyright file="RegistryEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias RegistrySourceGen;

namespace DcsvIo.D2.Tests.Unit.ErrorCodesRegistry;

using AwesomeAssertions;
using RegistrySourceGen::DcsvIo.D2.ErrorCodes.Registry.SourceGen;
using Xunit;

/// <summary>
/// Unit tests for the pure-logic parts of <see cref="RegistryEmitter"/> —
/// specifically the <see cref="RegistryEmitter.CategoryToMemberName"/> mapping
/// which must be stable and reversible (the parity tests compare wire strings
/// against enum values via this map).
/// </summary>
public sealed class RegistryEmitterTests
{
    [Theory]
    [InlineData("validation_failure", "ValidationFailure")]
    [InlineData("not_found", "NotFound")]
    [InlineData("conflict", "Conflict")]
    [InlineData("policy_denied", "PolicyDenied")]
    [InlineData("rate_limited", "RateLimited")]
    [InlineData("payload_too_large", "PayloadTooLarge")]
    [InlineData("infrastructure_unavailable", "InfrastructureUnavailable")]
    [InlineData("internal_error", "InternalError")]
    [InlineData("partial_success", "PartialSuccess")]
    public void CategoryToMemberName_AllSchemaValues_MapsCorrectly(
        string wireCategory, string expectedMember)
    {
        var result = RegistryEmitter.CategoryToMemberName(wireCategory);
        result.Should().Be(expectedMember);
    }

    [Theory]
    [InlineData("single", "Single")]
    [InlineData("a_b_c", "ABC")]
    [InlineData("abc", "Abc")]
    public void CategoryToMemberName_EdgeCases_MapsCorrectly(
        string input, string expected)
    {
        var result = RegistryEmitter.CategoryToMemberName(input);
        result.Should().Be(expected);
    }

    // -----------------------------------------------------------------------
    // IsDeprecated — the registry-side deprecation flag baked onto ErrorCodeInfo.
    // Driven by SYNTHETIC entries — no real spec entry is deprecated.
    // -----------------------------------------------------------------------

    [Fact]
    public void Emit_ErrorCodeInfoRecord_CarriesIsDeprecatedField()
    {
        var source = RegistryEmitter.Emit([Entry("NOT_FOUND", isDeprecated: false)]);

        source.Should().Contain("public readonly record struct ErrorCodeInfo(");
        source.Should().Contain("    bool IsDeprecated);");
    }

    [Fact]
    public void Emit_DeprecatedEntry_BakesIsDeprecatedTrue()
    {
        var source = RegistryEmitter.Emit([Entry("NOT_FOUND", isDeprecated: true)]);

        source.Should().Contain("IsDeprecated: true)");
    }

    [Fact]
    public void Emit_NonDeprecatedEntry_BakesIsDeprecatedFalse()
    {
        var source = RegistryEmitter.Emit([Entry("NOT_FOUND", isDeprecated: false)]);

        source.Should().Contain("IsDeprecated: false)");
        source.Should().NotContain("IsDeprecated: true");
    }

    private static RegistrySpecEntry Entry(string code, bool isDeprecated) =>
        new(
            Code: code,
            HttpStatus: 404,
            Category: "not_found",
            UserMessageKey: "TK.Common.Errors.NOT_FOUND",
            FactoryName: "NotFound",
            FactoryShape: "standard",
            Doc: "Resource not found.",
            Domain: "common",
            SpecFileName: "error-codes.spec.json",
            IsDeprecated: isDeprecated);
}
