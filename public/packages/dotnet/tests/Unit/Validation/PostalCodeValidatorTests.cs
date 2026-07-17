// -----------------------------------------------------------------------
// <copyright file="PostalCodeValidatorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Validation;

using System.Diagnostics;
using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Validation;
using Xunit;

/// <summary>
/// Adversarial + property tests for <see cref="DefaultPostalCodeValidator"/>
/// beyond the cross-language parity corpus — idempotency, fast-return on
/// garbage, the null / empty / whitespace falsey paths, and the FAIL-CLOSED
/// policy on a null country.
/// </summary>
public sealed class PostalCodeValidatorTests
{
    private static readonly DefaultPostalCodeValidator sr_Validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_NullEmptyWhitespace(string? input)
    {
        var result = sr_Validator.Validate(input, CountryCode.US);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
        result.InputErrors[0].Field.Should().Be("postalCode");
        result.InputErrors[0].Errors[0].Key.Should()
            .Be(TK.Common.Validation.POSTAL_CODE_INVALID.Key);
    }

    [Fact]
    public void RegexMap_BuildsWithValidMatchTimeout_NoStaticInitOrderFailure()
    {
        // Regression pin: the match-timeout literal MUST be a compile-time const
        // (not a static readonly field) declared before the map that consumes it.
        // A prior refactor to `static readonly TimeSpan` placed it AFTER SR_Map,
        // so static-init order left the timeout at TimeSpan.Zero when BuildMap()
        // ran — `new Regex(..., TimeSpan.Zero)` throws ArgumentOutOfRangeException.
        // If a future change reintroduces `static readonly` field ordering, this
        // test will fail with ArgumentOutOfRangeException on the first access.
        // The count assertion is belt-and-suspenders: the map must actually build
        // with entries (a zero-count map passes Validate() but proves nothing).
        PostalCodeRegexData.SR_Map.Count.Should().BeGreaterThan(0);

        var result = sr_Validator.Validate("90210", CountryCode.US);
        result.Success.Should().BeTrue();
        result.Data.Should().Be("90210");
    }

    [Fact]
    public void FailsClosed_OnNullCountry()
    {
        // A valid-looking US ZIP with no country must NOT validate — there is
        // no permissive fallback. Parity with the TS side.
        var result = sr_Validator.Validate("90210", countryCode: null);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public void Rejects_FormatMismatch()
        => sr_Validator.Validate("SW1A 1AA", CountryCode.US).Success.Should().BeFalse();

    [Fact]
    public void Normalizes_TrimThenUppercase()
    {
        var result = sr_Validator.Validate("  k1a 0b1  ", CountryCode.CA);
        result.Success.Should().BeTrue();
        result.Data.Should().Be("K1A 0B1");
    }

    [Fact]
    public void IsIdempotent_OnNormalizedValue()
    {
        var first = sr_Validator.Validate("  k1a 0b1  ", CountryCode.CA);
        first.Success.Should().BeTrue();

        var second = sr_Validator.Validate(first.Data, CountryCode.CA);
        second.Success.Should().BeTrue();
        second.Data.Should().Be(first.Data);
    }

    [Fact]
    public void ReturnsFast_OnPathologicalInput()
    {
        var pathological = new string('9', 50_000);
        var sw = Stopwatch.StartNew();
        var result = sr_Validator.Validate(pathological, CountryCode.US);
        sw.Stop();

        result.Success.Should().BeFalse();

        // 500 ms = 10x the 50 ms matchTimeout; generous enough to absorb CI
        // scheduling jitter while still catching a genuine ReDoS hang.
        sw.ElapsedMilliseconds.Should().BeLessThan(500);
    }
}
