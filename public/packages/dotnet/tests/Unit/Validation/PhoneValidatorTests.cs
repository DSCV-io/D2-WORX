// -----------------------------------------------------------------------
// <copyright file="PhoneValidatorTests.cs" company="DCSV">
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
/// Adversarial + property tests for <see cref="DefaultPhoneValidator"/> beyond
/// the cross-language parity corpus — idempotency, fast-return on garbage, and
/// the null / empty / whitespace falsey paths.
/// </summary>
public sealed class PhoneValidatorTests
{
    private static readonly DefaultPhoneValidator sr_Validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_NullEmptyWhitespace(string? input)
    {
        var result = sr_Validator.Validate(input, CountryCode.US);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
        result.InputErrors[0].Field.Should().Be("phone");
        result.InputErrors[0].Errors[0].Key.Should().Be(TK.Common.Validation.PHONE_INVALID.Key);
    }

    [Fact]
    public void Rejects_NationalFormat_WithNoDefaultRegion()
        => sr_Validator.Validate("02079460958").Success.Should().BeFalse();

    [Fact]
    public void Rejects_NonNumericGarbage()
    {
        // NOT a vanity-letter number: libphonenumber-csharp maps letters to
        // digits (CALL -> 2255, a VALID number), so a "202-555-CALL"-style
        // input would PASS here while libphonenumber-js rejects it. Use a
        // non-letter-mappable garbage string both ports reject.
        sr_Validator.Validate("not-a-phone", CountryCode.US).Success.Should().BeFalse();
    }

    [Fact]
    public void Normalizes_NationalNumber_ToE164()
    {
        var result = sr_Validator.Validate("(202) 555-0143", CountryCode.US);
        result.Success.Should().BeTrue();
        result.Data.Should().Be("+12025550143");
    }

    [Fact]
    public void IsIdempotent_OnE164Result()
    {
        var first = sr_Validator.Validate("(202) 555-0143", CountryCode.US);
        first.Success.Should().BeTrue();

        // The E.164 form carries the country, so no default region is needed.
        var second = sr_Validator.Validate(first.Data);
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
        sw.ElapsedMilliseconds.Should().BeLessThan(1_000);
    }
}
