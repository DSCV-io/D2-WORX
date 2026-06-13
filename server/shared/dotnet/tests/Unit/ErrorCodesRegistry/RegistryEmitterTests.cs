// -----------------------------------------------------------------------
// <copyright file="RegistryEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

extern alias RegistrySourceGen;

namespace D2.Shared.Tests.Unit.ErrorCodesRegistry;

using AwesomeAssertions;
using RegistrySourceGen::D2.Shared.ErrorCodes.Registry.SourceGen;
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
}
