// -----------------------------------------------------------------------
// <copyright file="ErrorCategoryEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias CategorySourceGen;

namespace DcsvIo.D2.Tests.Unit.ErrorCodesCategory;

using AwesomeAssertions;
using CategorySourceGen::DcsvIo.D2.ErrorCodes.Category.SourceGen;
using Xunit;

/// <summary>
/// Unit tests for the pure-logic <c>WireToMemberName</c> mapping in
/// <see cref="ErrorCategoryEmitter"/> — the snake_case wire string → PascalCase
/// enum member transform that must be stable and reversible (the parity tests
/// compare wire strings against enum values via this map).
/// </summary>
public sealed class ErrorCategoryEmitterTests
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
    public void WireToMemberName_AllSchemaValues_MapsCorrectly(
        string wire, string expectedMember)
    {
        var result = ErrorCategoryEmitter.WireToMemberName(wire);
        result.Should().Be(expectedMember);
    }

    [Theory]
    [InlineData("single", "Single")]
    [InlineData("a_b_c", "ABC")]
    [InlineData("abc", "Abc")]
    public void WireToMemberName_EdgeCases_MapsCorrectly(string input, string expected)
    {
        var result = ErrorCategoryEmitter.WireToMemberName(input);
        result.Should().Be(expected);
    }
}
