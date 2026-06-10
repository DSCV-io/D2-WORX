// -----------------------------------------------------------------------
// <copyright file="KidTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

using AwesomeAssertions;
using D2.Edge.KeyCustodian.Domain.Errors;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.ErrorCodes.Category;
using Xunit;

/// <summary>
/// Adversarial unit tests for <see cref="Kid"/>.
/// </summary>
public sealed class KidTests
{
    // -----------------------------------------------------------------------
    // Create — valid input
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_ValidKid_ReturnsOk()
    {
        var result = Kid.Create("my-key-01_v2");
        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("my-key-01_v2");
    }

    [Fact]
    public void Create_MaxLengthKid_ReturnsOk()
    {
        var kid = new string('a', 64);
        var result = Kid.Create(kid);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Create_SingleCharKid_ReturnsOk()
    {
        var result = Kid.Create("a");
        result.Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Create — null / empty / whitespace
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_Null_ReturnsValidationFailed()
    {
        var result = Kid.Create(null);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_KID_INVALID);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void Create_Empty_ReturnsValidationFailed()
    {
        var result = Kid.Create(string.Empty);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_KID_INVALID);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void Create_Whitespace_ReturnsValidationFailed()
    {
        var result = Kid.Create("   ");
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_KID_INVALID);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // Create — length cap
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_OverMaxLength_ReturnsValidationFailed()
    {
        var kid = new string('a', 65);
        var result = Kid.Create(kid);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_KID_TOO_LONG);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // Create — invalid charset
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("kid with spaces")]
    [InlineData("kid/slash")]
    [InlineData("kid.dot")]
    [InlineData("kid@at")]
    [InlineData("kidé")] // unicode accented char
    [InlineData("kid:colon")]
    public void Create_InvalidCharset_ReturnsValidationFailed(string value)
    {
        var result = Kid.Create(value);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_KID_INVALID);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // FromTrusted
    // -----------------------------------------------------------------------

    [Fact]
    public void FromTrusted_ValidValue_WrapsVerbatim()
    {
        var kid = Kid.FromTrusted("my-trusted-key");
        kid.Value.Should().Be("my-trusted-key");
    }

    [Fact]
    public void FromTrusted_Null_ThrowsArgumentNullException()
    {
        var act = () => Kid.FromTrusted(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromTrusted_Empty_ThrowsArgumentException()
    {
        // A corrupt DB row with an empty kid bypasses the charset guard — data-corruption error.
        var act = () => Kid.FromTrusted(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromTrusted_Whitespace_ThrowsArgumentException()
    {
        // A whitespace-only kid from the store is equally corrupt — must not bypass rehydration.
        var act = () => Kid.FromTrusted("   ");
        act.Should().Throw<ArgumentException>();
    }

    // Gate-intact pin: FromTrusted bypasses charset validation, Create still rejects
    [Fact]
    public void FromTrusted_AcceptsInvalidCharset_CreateRejectsIt()
    {
        // FromTrusted — verbatim, no validation
        var trusted = Kid.FromTrusted("kid with spaces");
        trusted.Value.Should().Be("kid with spaces");

        // Create still rejects the same value
        var result = Kid.Create("kid with spaces");
        result.Success.Should().BeFalse();
    }
}
