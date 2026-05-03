// -----------------------------------------------------------------------
// <copyright file="StringExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Utilities.Extensions;

using AwesomeAssertions;
using D2.Shared.Utilities.Extensions;
using Xunit;

public sealed class StringExtensionsTests
{
    // ----------------------------------------------------------------------
    // Truthy / Falsey
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("   \r\n  \t  ")]
    public void Falsey_OnNullEmptyOrWhitespaceOnly_IsTrue(string? input)
    {
        input.Falsey().Should().BeTrue();
        input.Truthy().Should().BeFalse();
    }

    [Theory]
    [InlineData("x")]
    [InlineData("hello")]
    [InlineData("  padded  ")]
    [InlineData(" a ")]
    public void Truthy_OnAnyNonWhitespace_IsTrue(string input)
    {
        input.Truthy().Should().BeTrue();
        input.Falsey().Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // ToNullIfEmpty
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void ToNullIfEmpty_OnFalsey_ReturnsNull(string? input)
    {
        input.ToNullIfEmpty().Should().BeNull();
    }

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("  hello  ", "hello")]
    [InlineData("\thello\n", "hello")]
    public void ToNullIfEmpty_OnTruthy_ReturnsTrimmed(string input, string expected)
    {
        input.ToNullIfEmpty().Should().Be(expected);
    }

    // ----------------------------------------------------------------------
    // CleanStr
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void CleanStr_OnFalsey_ReturnsNull(string? input)
    {
        input.CleanStr().Should().BeNull();
    }

    [Theory]
    [InlineData("hello world", "hello world")]
    [InlineData("  hello   world  ", "hello world")]
    [InlineData("a\t\nb", "a b")]
    [InlineData("a   b   c", "a b c")]
    [InlineData("single", "single")]
    public void CleanStr_OnTruthy_TrimAndCollapseInternalWhitespace(string input, string expected)
    {
        input.CleanStr().Should().Be(expected);
    }

    // ----------------------------------------------------------------------
    // CleanDisplayStr
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CleanDisplayStr_OnFalsey_ReturnsNull(string? input)
    {
        input.CleanDisplayStr().Should().BeNull();
    }

    [Theory]
    [InlineData("John Doe", "John Doe")]
    [InlineData("  John   Doe  ", "John Doe")]
    [InlineData("Mary-Jane O'Neil, Jr.", "Mary-Jane O'Neil, Jr.")]
    [InlineData("Иван Петров", "Иван Петров")] // Cyrillic
    [InlineData("日本語名前", "日本語名前")] // CJK
    [InlineData("José Núñez", "José Núñez")] // Latin diacritics
    public void CleanDisplayStr_PreservesAllowedCharacters(string input, string expected)
    {
        input.CleanDisplayStr().Should().Be(expected);
    }

    [Theory]
    [InlineData("<script>x</script>John", "scriptxscriptJohn")]
    [InlineData("(John) [Doe] {Esq}", "John Doe Esq")]
    [InlineData("John\"Doe\"", "JohnDoe")]
    [InlineData("John`Doe`", "JohnDoe")]
    [InlineData("John+Doe=1", "JohnDoe1")]
    [InlineData("John|Doe", "JohnDoe")]
    [InlineData("John\\Doe", "JohnDoe")]
    public void CleanDisplayStr_StripsDisallowedCharacters(string input, string expected)
    {
        input.CleanDisplayStr().Should().Be(expected);
    }

    [Fact]
    public void CleanDisplayStr_OnAllInvalid_ReturnsNull()
    {
        // After stripping all chars, only empty remains → CleanStr returns null.
        "@@@***".CleanDisplayStr().Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // CleanAndValidateEmail
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("user@example.com", "user@example.com")]
    [InlineData("USER@EXAMPLE.COM", "user@example.com")]
    [InlineData("  user@example.com  ", "user@example.com")]
    [InlineData("First.Last+tag@sub.example.co.uk", "first.last+tag@sub.example.co.uk")]
    public void CleanAndValidateEmail_OnValid_LowercasesAndReturns(
        string input,
        string expected)
    {
        input.CleanAndValidateEmail().Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("noatsign")]
    [InlineData("no@dot")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user@.com")]
    [InlineData("user@example.")]
    [InlineData("user@@example.com")]
    [InlineData("us er@example.com")]
    public void CleanAndValidateEmail_OnInvalid_ThrowsArgumentException(string? input)
    {
        var act = () => input.CleanAndValidateEmail();
        act.Should().Throw<ArgumentException>()
            .WithMessage("Invalid email address format.*");
    }

    // ----------------------------------------------------------------------
    // CleanAndValidatePhoneNumber
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("5551234", "5551234")] // exactly 7
    [InlineData("555-123-4567", "5551234567")]
    [InlineData("(555) 123-4567", "5551234567")]
    [InlineData("+44 20 7946 0958", "442079460958")]
    [InlineData("123456789012345", "123456789012345")] // exactly 15
    public void CleanAndValidatePhoneNumber_OnValid_StripsNonDigits(
        string input,
        string expected)
    {
        input.CleanAndValidatePhoneNumber().Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CleanAndValidatePhoneNumber_OnFalsey_Throws(string? input)
    {
        var act = () => input.CleanAndValidatePhoneNumber();
        act.Should().Throw<ArgumentException>()
            .WithMessage("Phone number cannot be null or empty.*");
    }

    [Fact]
    public void CleanAndValidatePhoneNumber_OnNoDigits_Throws()
    {
        var act = () => "abc-def".CleanAndValidatePhoneNumber();
        act.Should().Throw<ArgumentException>()
            .WithMessage("Invalid phone number format.*");
    }

    [Theory]
    [InlineData("123456")] // 6 digits
    [InlineData("1")]
    [InlineData("1234567890123456")] // 16 digits
    public void CleanAndValidatePhoneNumber_OnLengthOutOfBounds_Throws(string input)
    {
        var act = () => input.CleanAndValidatePhoneNumber();
        act.Should().Throw<ArgumentException>()
            .WithMessage("Phone number must be between 7 and 15 digits in length.*");
    }

    // ----------------------------------------------------------------------
    // GetNormalizedStrForHashing
    // ----------------------------------------------------------------------

    [Fact]
    public void GetNormalizedStrForHashing_PreservesPositionsForFalseyParts()
    {
        // Falsey segments collapse to empty, so the pipe positions remain stable
        // — important when caller builds composite hash keys like
        // "city|region|country" where any field may be missing.
        string?[] parts = [" Test One ", "   ", "TEST3"];

        parts.GetNormalizedStrForHashing().Should().Be("test one||test3");
    }

    [Fact]
    public void GetNormalizedStrForHashing_OnEmptyArray_ReturnsEmpty()
    {
        string?[] parts = [];

        parts.GetNormalizedStrForHashing().Should().BeEmpty();
    }

    [Fact]
    public void GetNormalizedStrForHashing_OnAllFalsey_ReturnsPipesOnly()
    {
        string?[] parts = [null, string.Empty, "  "];

        parts.GetNormalizedStrForHashing().Should().Be("||");
    }

    [Fact]
    public void GetNormalizedStrForHashing_LowercasesAndCleansEachPart()
    {
        string?[] parts = ["  HELLO  ", "World"];

        parts.GetNormalizedStrForHashing().Should().Be("hello|world");
    }
}
