// -----------------------------------------------------------------------
// <copyright file="ErrorCategoryTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.ErrorCodesCategory;

using AwesomeAssertions;
using DcsvIo.D2.ErrorCodes.Category;
using Xunit;

/// <summary>
/// Unit tests for the relocated <see cref="ErrorCategory"/> enum and its
/// <see cref="ErrorCategoryWire"/> wire-mapping + <see cref="ErrorCategoryJsonConverter"/>,
/// generated into <c>DcsvIo.D2.ErrorCodes.Category</c> from
/// <c>contracts/error-category/error-category.spec.json</c>. Covers all nine
/// closed members, the ToWire / TryFromWire round-trip, and the strict JSON
/// converter (unknown wire → <see cref="System.Text.Json.JsonException"/>).
/// </summary>
public sealed class ErrorCategoryTests
{
    // -----------------------------------------------------------------------
    // ErrorCategoryWire — ToWire (all nine values)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(ErrorCategory.ValidationFailure, "validation_failure")]
    [InlineData(ErrorCategory.NotFound, "not_found")]
    [InlineData(ErrorCategory.Conflict, "conflict")]
    [InlineData(ErrorCategory.PolicyDenied, "policy_denied")]
    [InlineData(ErrorCategory.RateLimited, "rate_limited")]
    [InlineData(ErrorCategory.PayloadTooLarge, "payload_too_large")]
    [InlineData(ErrorCategory.InfrastructureUnavailable, "infrastructure_unavailable")]
    [InlineData(ErrorCategory.InternalError, "internal_error")]
    [InlineData(ErrorCategory.PartialSuccess, "partial_success")]
    public void ToWire_AllNineValues_ReturnExpectedWireString(
        ErrorCategory category, string expectedWire)
    {
        category.ToWire().Should().Be(expectedWire);
    }

    [Fact]
    public void ToWire_UndefinedMember_ThrowsArgumentOutOfRange()
    {
        // A cast-from-int that is not a defined member must throw, not return
        // a bogus wire string.
        var bogus = (ErrorCategory)9999;
        var act = () => bogus.ToWire();
        act.Should().Throw<System.ArgumentOutOfRangeException>();
    }

    // -----------------------------------------------------------------------
    // ErrorCategoryWire — TryFromWire (all nine values + unknown)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("validation_failure", ErrorCategory.ValidationFailure)]
    [InlineData("not_found", ErrorCategory.NotFound)]
    [InlineData("conflict", ErrorCategory.Conflict)]
    [InlineData("policy_denied", ErrorCategory.PolicyDenied)]
    [InlineData("rate_limited", ErrorCategory.RateLimited)]
    [InlineData("payload_too_large", ErrorCategory.PayloadTooLarge)]
    [InlineData("infrastructure_unavailable", ErrorCategory.InfrastructureUnavailable)]
    [InlineData("internal_error", ErrorCategory.InternalError)]
    [InlineData("partial_success", ErrorCategory.PartialSuccess)]
    public void TryFromWire_AllNineValues_ReturnExpectedEnum(
        string wire, ErrorCategory expected)
    {
        var parsed = ErrorCategoryWire.TryFromWire(wire, out var category);
        parsed.Should().BeTrue();
        category.Should().Be(expected);
    }

    [Theory]
    [InlineData("ValidationFailure")]
    [InlineData("VALIDATION_FAILURE")]
    [InlineData("unknown_category")]
    [InlineData("")]
    [InlineData("not-found")]
    [InlineData(" not_found ")]
    public void TryFromWire_UnknownWireString_ReturnsFalse(string wire)
    {
        var parsed = ErrorCategoryWire.TryFromWire(wire, out _);
        parsed.Should().BeFalse();
    }

    [Theory]
    [InlineData(ErrorCategory.ValidationFailure, "validation_failure")]
    [InlineData(ErrorCategory.NotFound, "not_found")]
    [InlineData(ErrorCategory.Conflict, "conflict")]
    [InlineData(ErrorCategory.PolicyDenied, "policy_denied")]
    [InlineData(ErrorCategory.RateLimited, "rate_limited")]
    [InlineData(ErrorCategory.PayloadTooLarge, "payload_too_large")]
    [InlineData(ErrorCategory.InfrastructureUnavailable, "infrastructure_unavailable")]
    [InlineData(ErrorCategory.InternalError, "internal_error")]
    [InlineData(ErrorCategory.PartialSuccess, "partial_success")]
    public void RoundTrip_ToWireThenTryFromWire_AllNineValues(
        ErrorCategory category, string expectedWire)
    {
        var wire = category.ToWire();
        wire.Should().Be(expectedWire);
        var parsed = ErrorCategoryWire.TryFromWire(wire, out var roundTripped);
        parsed.Should().BeTrue();
        roundTripped.Should().Be(category);
    }

    // -----------------------------------------------------------------------
    // ErrorCategoryJsonConverter — serialize / deserialize
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(ErrorCategory.ValidationFailure, "\"validation_failure\"")]
    [InlineData(ErrorCategory.NotFound, "\"not_found\"")]
    [InlineData(ErrorCategory.Conflict, "\"conflict\"")]
    [InlineData(ErrorCategory.PolicyDenied, "\"policy_denied\"")]
    [InlineData(ErrorCategory.RateLimited, "\"rate_limited\"")]
    [InlineData(ErrorCategory.PayloadTooLarge, "\"payload_too_large\"")]
    [InlineData(ErrorCategory.InfrastructureUnavailable, "\"infrastructure_unavailable\"")]
    [InlineData(ErrorCategory.InternalError, "\"internal_error\"")]
    [InlineData(ErrorCategory.PartialSuccess, "\"partial_success\"")]
    public void JsonConverter_Serialize_AllNineValues_ProducesWireString(
        ErrorCategory category, string expectedJson)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(category);
        json.Should().Be(expectedJson);
    }

    [Theory]
    [InlineData("\"validation_failure\"", ErrorCategory.ValidationFailure)]
    [InlineData("\"not_found\"", ErrorCategory.NotFound)]
    [InlineData("\"conflict\"", ErrorCategory.Conflict)]
    [InlineData("\"policy_denied\"", ErrorCategory.PolicyDenied)]
    [InlineData("\"rate_limited\"", ErrorCategory.RateLimited)]
    [InlineData("\"payload_too_large\"", ErrorCategory.PayloadTooLarge)]
    [InlineData("\"infrastructure_unavailable\"", ErrorCategory.InfrastructureUnavailable)]
    [InlineData("\"internal_error\"", ErrorCategory.InternalError)]
    [InlineData("\"partial_success\"", ErrorCategory.PartialSuccess)]
    public void JsonConverter_Deserialize_AllNineValues_ReturnsExpectedEnum(
        string json, ErrorCategory expected)
    {
        var category = System.Text.Json.JsonSerializer.Deserialize<ErrorCategory>(json);
        category.Should().Be(expected);
    }

    [Theory]
    [InlineData("\"ValidationFailure\"")]
    [InlineData("\"VALIDATION_FAILURE\"")]
    [InlineData("\"unknown_category\"")]
    [InlineData("\"\"")]
    public void JsonConverter_Deserialize_UnknownWireString_ThrowsJsonException(
        string json)
    {
        var act = () => System.Text.Json.JsonSerializer.Deserialize<ErrorCategory>(json);
        act.Should().Throw<System.Text.Json.JsonException>();
    }

    [Fact]
    public void JsonConverter_Deserialize_NonStringToken_ThrowsJsonException()
    {
        // A JSON number token instead of a string should throw.
        var act = () => System.Text.Json.JsonSerializer.Deserialize<ErrorCategory>("42");
        act.Should().Throw<System.Text.Json.JsonException>();
    }

    [Fact]
    public void JsonConverter_Deserialize_NullToken_ThrowsJsonException()
    {
        // A JSON null token must throw, not yield a default member.
        var act = () => System.Text.Json.JsonSerializer.Deserialize<ErrorCategory>("null");
        act.Should().Throw<System.Text.Json.JsonException>();
    }

    [Fact]
    public void JsonConverter_RoundTrip_AllNineValues_Preserved()
    {
        foreach (ErrorCategory category in System.Enum.GetValues<ErrorCategory>())
        {
            var json = System.Text.Json.JsonSerializer.Serialize(category);
            var back = System.Text.Json.JsonSerializer.Deserialize<ErrorCategory>(json);
            back.Should().Be(category);
        }
    }

    // -----------------------------------------------------------------------
    // Closed-set completeness — exactly nine members, matching the spec.
    // -----------------------------------------------------------------------

    [Fact]
    public void ErrorCategory_HasExactlyNineMembers()
    {
        System.Enum.GetValues<ErrorCategory>().Length.Should().Be(9);
    }

    [Fact]
    public void EveryMember_HasAWireString_AndRoundTrips()
    {
        foreach (ErrorCategory category in System.Enum.GetValues<ErrorCategory>())
        {
            var wire = category.ToWire();
            wire.Should().NotBeNullOrWhiteSpace();
            ErrorCategoryWire.TryFromWire(wire, out var back).Should().BeTrue();
            back.Should().Be(category);
        }
    }
}
