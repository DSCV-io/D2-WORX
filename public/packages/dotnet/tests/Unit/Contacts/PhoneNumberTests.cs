// -----------------------------------------------------------------------
// <copyright file="PhoneNumberTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Contacts;

using AwesomeAssertions;
using DcsvIo.D2.Contacts.ValueObjects;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Validation.Abstractions;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="PhoneNumber"/>: the structural floor
/// (strip non-digits, 7-15 envelope, raw-length cap, bubbling the common
/// invalid-phone key) and the injected-validator seam (output + failure bubbled
/// verbatim, region forwarded to the validator and ignored by the floor).
/// </summary>
public sealed class PhoneNumberTests
{
    // -----------------------------------------------------------------------
    // Floor — valid
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_FloorFormattedNumber_ReturnsOk_DigitsOnly()
    {
        var result = PhoneNumber.Create("+1 212 555 1234");

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("12125551234");
    }

    [Fact]
    public void Create_FloorExactly7Digits_ReturnsOk()
    {
        var result = PhoneNumber.Create("1234567");

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("1234567");
    }

    [Fact]
    public void Create_FloorExactly15Digits_ReturnsOk()
    {
        var result = PhoneNumber.Create("123456789012345");

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("123456789012345");
    }

    // -----------------------------------------------------------------------
    // Floor — invalid (bubbles common invalid-phone key)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("123456")]
    [InlineData("1234567890123456")]
    public void Create_FloorInvalidPhone_ReturnsCommonPhoneInvalid(string? value)
    {
        var result = PhoneNumber.Create(value);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Validation.PHONE_INVALID.Key);
    }

    // -----------------------------------------------------------------------
    // Floor — raw-length cap (32 chars)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_FloorRawOverMaxLength_ReturnsPhoneTooLong()
    {
        // 33 chars of digits — exceeds the raw E.164 ceiling before the digit
        // envelope is even consulted.
        var overMax = new string('1', FieldConstraints.PHONE_E164_MAX + 1);
        var result = PhoneNumber.Create(overMax);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.PHONE_TOO_LONG.Key);
    }

    [Fact]
    public void Create_FloorRawExactlyMaxLength_WithValidDigitCount_ReturnsOk()
    {
        // Exactly 32 raw chars (the inclusive ceiling) carrying exactly 15 digits
        // (the digit-envelope maximum): 15 digits + 17 separators.
        var raw = "+(1 23) 456-789 012 345 [x-y-z] "; // 32 chars, 15 digits
        raw.Length.Should().Be(FieldConstraints.PHONE_E164_MAX);

        var result = PhoneNumber.Create(raw);

        // The raw guard passes (length == max is allowed); the stripped digits
        // (123456789012345) fall within the 7-15 envelope → Ok.
        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("123456789012345");
    }

    // -----------------------------------------------------------------------
    // FromTrusted — verbatim trusted-store reconstruction (no validation)
    // -----------------------------------------------------------------------

    [Fact]
    public void FromTrusted_ReturnsInstanceWithVerbatimValue()
    {
        PhoneNumber.FromTrusted("+12025551234").Value.Should().Be("+12025551234");
    }

    [Fact]
    public void FromTrusted_DoesNotValidate_PassesInvalidValueVerbatim()
    {
        // FromTrusted bypasses the Create gate — an invalid stored value is reconstructed
        // verbatim (the trusted-store contract assumes the value was validated on write).
        PhoneNumber.FromTrusted("not-a-phone").Value.Should().Be("not-a-phone");
    }

    [Fact]
    public void Create_StillRejectsInvalidValue_GateIntact()
    {
        // Pins the gate: Create must still reject what FromTrusted accepts verbatim.
        PhoneNumber.Create("not-a-phone").Success.Should().BeFalse();
    }

    [Fact]
    public void FromTrusted_NullValue_ThrowsArgumentNullException()
    {
        var ex = Record.Exception(() => PhoneNumber.FromTrusted(null!));

        ex.Should().BeOfType<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------
    // Validator seam
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_WithValidatorSuccess_UsesValidatorNormalizedValue()
    {
        var validator = new FakePhoneValidator(D2Result<string>.Ok("+12125551234"));

        var result = PhoneNumber.Create("212 555 1234", validator, CountryCode.US);

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("+12125551234");
    }

    [Fact]
    public void Create_WithValidator_ForwardsRegion()
    {
        var validator = new FakePhoneValidator(D2Result<string>.Ok("+12125551234"));

        PhoneNumber.Create("212 555 1234", validator, CountryCode.US);

        validator.LastInput.Should().Be("212 555 1234");
        validator.LastRegion.Should().Be(CountryCode.US);
    }

    [Fact]
    public void Create_WithValidatorFailure_BubblesValidatorResult()
    {
        var failure = D2Result<string>.ValidationFailed(
            messages: [TK.Common.Validation.PHONE_INVALID],
            inputErrors: [new InputError("phone", [TK.Common.Validation.PHONE_INVALID])]);
        var validator = new FakePhoneValidator(failure);

        var result = PhoneNumber.Create("bad", validator);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Validation.PHONE_INVALID.Key);
        result.InputErrors.Should().ContainSingle()
            .Which.Field.Should().Be("phone");
    }

    [Fact]
    public void Create_WithValidatorFailure_BubblesDistinguishingValidatorKey()
    {
        // The validator returns a key that PhoneNumber.Create itself never emits
        // (TK.Geo.Validation.ADMIN_EMPTY_RECORD is outside {PHONE_INVALID, PHONE_TOO_LONG}).
        // This proves the validator's exact result was bubbled verbatim, not re-raised
        // with a key from Contacts' own validation vocabulary.
        var distinguishingKey = TK.Geo.Validation.ADMIN_EMPTY_RECORD;
        var failure = D2Result<string>.ValidationFailed(messages: [distinguishingKey]);
        var validator = new FakePhoneValidator(failure);

        var result = PhoneNumber.Create("anything", validator);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(distinguishingKey.Key);
    }

    [Fact]
    public void Create_FloorMode_IgnoresRegion()
    {
        // No validator — region must have no effect on the floor outcome.
        var withRegion = PhoneNumber.Create("+1 212 555 1234", region: CountryCode.GB);
        var withoutRegion = PhoneNumber.Create("+1 212 555 1234");

        withRegion.Success.Should().BeTrue();
        withRegion.Data!.Value.Should().Be(withoutRegion.Data!.Value);
    }

    // -----------------------------------------------------------------------
    // Fakes
    // -----------------------------------------------------------------------

    private sealed class FakePhoneValidator(D2Result<string> response) : IPhoneValidator
    {
        public string? LastInput { get; private set; }

        public CountryCode? LastRegion { get; private set; }

        public D2Result<string> Validate(string? phone, CountryCode? defaultRegion = null)
        {
            LastInput = phone;
            LastRegion = defaultRegion;
            return response;
        }
    }
}
