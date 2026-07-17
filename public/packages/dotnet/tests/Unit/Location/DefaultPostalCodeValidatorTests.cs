// -----------------------------------------------------------------------
// <copyright file="DefaultPostalCodeValidatorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Location;

using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Location;
using Xunit;

/// <summary>
/// Adversarial test coverage for <see cref="DefaultPostalCodeValidator"/>
/// per §7.1 matrix: happy-path global formats (US/CA/UK/JP/AU/DE),
/// null / empty / whitespace / emoji garbage, length boundaries 3-10,
/// non-alphanumeric end-of-string rejection, CRLF / NUL injection
/// rejection, country-blind behavior, regex matchTimeout guard on
/// oversized input.
/// </summary>
public sealed class DefaultPostalCodeValidatorTests
{
    private static readonly DefaultPostalCodeValidator sr_Validator = new();

    // -----------------------------------------------------------------------
    // Happy path — valid global formats
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("90210")] // US 5-digit
    [InlineData("90210-1234")] // US ZIP+4
    [InlineData("A1A 1A1")] // CA postal
    [InlineData("SW1A 1AA")] // UK postal
    [InlineData("100-0001")] // JP postal
    [InlineData("2000")] // AU postal
    [InlineData("10115")] // DE postal
    public void Validate_ValidGlobalFormats_ReturnsOk(string code)
    {
        var result = sr_Validator.Validate(code);
        result.Success.Should().BeTrue();
        result.Data.Should().Be(code);
    }

    // -----------------------------------------------------------------------
    // Garbage — null / empty / whitespace
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n\r")]
    public void Validate_NullOrEmpty_ReturnsValidationFailed(string? code)
    {
        var result = sr_Validator.Validate(code);
        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.POSTAL_CODE_INVALID.Key);
    }

    [Fact]
    public void Validate_EmojiInput_ReturnsValidationFailed()
    {
        var result = sr_Validator.Validate("💩90210");
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Validate_SpecialCharsOnly_ReturnsValidationFailed()
    {
        var result = sr_Validator.Validate("!@#$%");
        result.Success.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Length boundaries
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_TooShort_AB_ReturnsValidationFailed()
    {
        sr_Validator.Validate("AB").Success.Should().BeFalse();
    }

    [Fact]
    public void Validate_TooLong_11Chars_ReturnsValidationFailed()
    {
        sr_Validator.Validate("12345678901").Success.Should().BeFalse();
    }

    [Fact]
    public void Validate_MinLengthBoundary_3Chars_ReturnsOk()
    {
        sr_Validator.Validate("ABC").Success.Should().BeTrue();
    }

    [Fact]
    public void Validate_MaxLengthBoundary_10Chars_ReturnsOk()
    {
        sr_Validator.Validate("1234567890").Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Non-alphanumeric at start / end rejected
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("-12345")]
    [InlineData("12345-")]
    public void Validate_LeadingOrTrailingHyphen_ReturnsValidationFailed(string code)
    {
        sr_Validator.Validate(code).Success.Should().BeFalse();
    }

    [Theory]
    [InlineData(" 12345")] // leading space — trimmed before regex
    [InlineData("12345 ")] // trailing space — trimmed before regex
    public void Validate_LeadingOrTrailingSpace_TrimmedBeforeRegex_ReturnsOk(string code)
    {
        sr_Validator.Validate(code).Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Country-blind by design
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_CountryArgument_Ignored_USNumericPassesForCA()
    {
        var result = sr_Validator.Validate("12345", CountryCode.CA);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Validate_CountryArgumentNull_OkPath()
    {
        var result = sr_Validator.Validate("90210");
        result.Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Adversarial — CRLF / NUL injection rejected
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_CrLfInjected_RegexRejects()
    {
        sr_Validator.Validate("90210\r\nINJECTED").Success.Should().BeFalse();
    }

    [Fact]
    public void Validate_NullByteInjected_RegexRejects()
    {
        sr_Validator.Validate("90210\0INJECTED").Success.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Adversarial — all-spaces input
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_AllSpaces_ReturnsValidationFailed()
    {
        // Trim → string.Empty → Falsey rejects.
        sr_Validator.Validate("     ").Success.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Adversarial — super-long input does not timeout / hang
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_OversizedInput_DoesNotHang_RegexBounded()
    {
        var huge = new string('A', 10_000);

        // Should fail fast (regex rejects on length); if super-linear backtracking
        // were reached, RegexMatchTimeoutException would be caught and mapped to
        // a validation failure — never an unhandled exception.
        var result = sr_Validator.Validate(huge);
        result.Success.Should().BeFalse();
    }
}
