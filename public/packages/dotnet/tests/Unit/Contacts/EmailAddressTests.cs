// -----------------------------------------------------------------------
// <copyright file="EmailAddressTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Contacts;

using AwesomeAssertions;
using DcsvIo.D2.Contacts.ValueObjects;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Validation.Abstractions;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="EmailAddress"/>: the structural floor
/// (trim / collapse / lowercase / shape / length cap, bubbling the common
/// invalid-email key) and the injected-validator seam (output + failure bubbled
/// verbatim, no extra length cap applied in validator mode).
/// </summary>
public sealed class EmailAddressTests
{
    // -----------------------------------------------------------------------
    // Floor — valid
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_FloorValidEmail_ReturnsOk()
    {
        var result = EmailAddress.Create("a@b.co");

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("a@b.co");
    }

    [Fact]
    public void Create_FloorTrimsAndLowercases()
    {
        var result = EmailAddress.Create("  A@B.CO  ");

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("a@b.co");
    }

    [Fact]
    public void Create_FloorExactlyMaxLength_ReturnsOk()
    {
        // local part padded so the full address is exactly EMAIL_MAX (254) chars.
        var domain = "@example.com";
        var local = new string('a', FieldConstraints.EMAIL_MAX - domain.Length);
        var address = local + domain;
        address.Length.Should().Be(FieldConstraints.EMAIL_MAX);

        var result = EmailAddress.Create(address);

        result.Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Floor — invalid (bubbles common invalid-email key)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NoAt")]
    [InlineData("missing@domain")]
    [InlineData("@no-local.com")]
    public void Create_FloorInvalidEmail_ReturnsCommonEmailInvalid(string? value)
    {
        var result = EmailAddress.Create(value);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Validation.EMAIL_INVALID.Key);
    }

    [Fact]
    public void Create_FloorOverMaxLength_ReturnsEmailTooLong()
    {
        var domain = "@example.com";
        var local = new string('a', (FieldConstraints.EMAIL_MAX - domain.Length) + 1);
        var address = local + domain;
        address.Length.Should().Be(FieldConstraints.EMAIL_MAX + 1);

        var result = EmailAddress.Create(address);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.EMAIL_TOO_LONG.Key);
    }

    // -----------------------------------------------------------------------
    // FromTrusted — verbatim trusted-store reconstruction (no validation)
    // -----------------------------------------------------------------------

    [Fact]
    public void FromTrusted_ReturnsInstanceWithVerbatimValue()
    {
        EmailAddress.FromTrusted("test@example.com").Value.Should().Be("test@example.com");
    }

    [Fact]
    public void FromTrusted_DoesNotValidate_PassesInvalidValueVerbatim()
    {
        // FromTrusted bypasses the Create gate — an invalid stored value is reconstructed
        // verbatim (the trusted-store contract assumes the value was validated on write).
        EmailAddress.FromTrusted("not-an-email").Value.Should().Be("not-an-email");
    }

    [Fact]
    public void Create_StillRejectsInvalidValue_GateIntact()
    {
        // Pins the gate: Create must still reject what FromTrusted accepts verbatim.
        EmailAddress.Create("not-an-email").Success.Should().BeFalse();
    }

    [Fact]
    public void FromTrusted_NullValue_ThrowsArgumentNullException()
    {
        var ex = Record.Exception(() => EmailAddress.FromTrusted(null!));

        ex.Should().BeOfType<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------
    // Validator seam
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_WithValidatorSuccess_UsesValidatorNormalizedValue()
    {
        var validator = new FakeEmailValidator(D2Result<string>.Ok("norm@x.com"));

        var result = EmailAddress.Create("anything", validator);

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("norm@x.com");
        validator.LastInput.Should().Be("anything");
    }

    [Fact]
    public void Create_WithValidatorFailure_BubblesValidatorResult()
    {
        var failure = D2Result<string>.ValidationFailed(
            messages: [TK.Common.Validation.EMAIL_INVALID],
            inputErrors: [new InputError("email", [TK.Common.Validation.EMAIL_INVALID])]);
        var validator = new FakeEmailValidator(failure);

        var result = EmailAddress.Create("bad", validator);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Validation.EMAIL_INVALID.Key);
        result.InputErrors.Should().ContainSingle()
            .Which.Field.Should().Be("email");
    }

    [Fact]
    public void Create_WithValidatorFailure_BubblesDistinguishingValidatorKey()
    {
        // The validator returns a key that EmailAddress.Create itself never emits
        // (TK.Geo.Validation.ADMIN_EMPTY_RECORD is outside {EMAIL_INVALID, EMAIL_TOO_LONG}).
        // This proves the validator's exact result was bubbled verbatim, not re-raised
        // with a key from Contacts' own validation vocabulary.
        var distinguishingKey = TK.Geo.Validation.ADMIN_EMPTY_RECORD;
        var failure = D2Result<string>.ValidationFailed(messages: [distinguishingKey]);
        var validator = new FakeEmailValidator(failure);

        var result = EmailAddress.Create("anything", validator);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(distinguishingKey.Key);
    }

    [Fact]
    public void Create_WithValidator_NoExtraLengthCapApplied()
    {
        // A validator may legitimately emit a longer normalized form — Contacts
        // does NOT second-guess it with the floor's length cap.
        var longEmail = new string('a', FieldConstraints.EMAIL_MAX + 50) + "@x.com";
        var validator = new FakeEmailValidator(D2Result<string>.Ok(longEmail));

        var result = EmailAddress.Create("short@x.com", validator);

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be(longEmail);
    }

    // -----------------------------------------------------------------------
    // Fakes
    // -----------------------------------------------------------------------

    private sealed class FakeEmailValidator(D2Result<string> response) : IEmailValidator
    {
        public string? LastInput { get; private set; }

        public D2Result<string> Validate(string? email)
        {
            LastInput = email;
            return response;
        }
    }
}
