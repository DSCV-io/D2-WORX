using D2.Contracts.Utilities;
using FluentAssertions;

namespace Utilities.Tests;

/// <summary>
/// Unit tests for <see cref="StringExtensions"/>.
/// </summary>
public class StringExtensionsTests
{
    #region Truthy Tests

    [Fact]
    public void Truthy_WithNonEmptyString_ReturnsTrue()
    {
        // Arrange
        const string str = "hello";

        // Act
        var result = str.Truthy();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Truthy_WithNullOrWhitespace_ReturnsFalse(string? str)
    {
        // Act
        var result = str.Truthy();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Falsey Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Falsey_WithNullOrWhitespace_ReturnsTrue(string? str)
    {
        // Act
        var result = str.Falsey();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Falsey_WithNonEmptyString_ReturnsFalse()
    {
        // Arrange
        const string str = "hello";

        // Act
        var result = str.Falsey();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CleanStr Tests

    [Theory]
    [InlineData("  hello  ", "hello")]
    [InlineData("hello", "hello")]
    [InlineData("  HELLO  ", "HELLO")]
    [InlineData("hello world", "hello world")]
    [InlineData("  hello   world  ", "hello world")]
    [InlineData("hello\t\tworld", "hello world")]
    [InlineData("hello\n\nworld", "hello world")]
    [InlineData("  hello  \n  world  ", "hello world")]
    public void CleanStr_WithValidString_TrimsAndNormalizesWhitespace(
        string input,
        string expected)
    {
        // Act
        var result = input.CleanStr();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("  \t  \n  ")]
    public void CleanStr_WithNullOrWhitespace_ReturnsNull(string? input)
    {
        // Act
        var result = input.CleanStr();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetNormalizedStrForHashing Tests

    [Fact]
    public void GetNormalizedStrForHashing_WithValidParts_ReturnsNormalizedString()
    {
        // Arrange
        var parts = new[] { "Hello", "World", "Test" };

        // Act
        var result = parts.GetNormalizedStrForHashing();

        // Assert
        result.Should().Be("hello|world|test");
    }

    [Fact]
    public void GetNormalizedStrForHashing_WithNullParts_ReplacesWithEmptyString()
    {
        // Arrange
        var parts = new[] { "Hello", null, "Test" };

        // Act
        var result = parts.GetNormalizedStrForHashing();

        // Assert
        result.Should().Be("hello||test");
    }

    [Fact]
    public void GetNormalizedStrForHashing_WithWhitespaceParts_ReplacesWithEmptyString()
    {
        // Arrange
        var parts = new[] { "Hello", "   ", "Test" };

        // Act
        var result = parts.GetNormalizedStrForHashing();

        // Assert
        result.Should().Be("hello||test");
    }

    [Fact]
    public void GetNormalizedStrForHashing_WithMixedCaseParts_ConvertsToLowercase()
    {
        // Arrange
        var parts = new[] { "HELLO", "WoRlD", "TeSt" };

        // Act
        var result = parts.GetNormalizedStrForHashing();

        // Assert
        result.Should().Be("hello|world|test");
    }

    [Fact]
    public void GetNormalizedStrForHashing_WithPartsContainingWhitespace_TrimsAndNormalizes()
    {
        // Arrange
        var parts = new[] { "  Hello  ", "World   Test", "  " };

        // Act
        var result = parts.GetNormalizedStrForHashing();

        // Assert
        result.Should().Be("hello|world test|");
    }

    [Fact]
    public void GetNormalizedStrForHashing_WithEmptyArray_ReturnsEmptyString()
    {
        // Arrange
        var parts = Array.Empty<string>();

        // Act
        var result = parts.GetNormalizedStrForHashing();

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public void GetNormalizedStrForHashing_WithIdenticalContent_ProducesSameResult()
    {
        // Arrange
        var parts1 = new[] { "Hello", "World", null, "Test" };
        var parts2 = new[] { "Hello", "World", null, "Test" };

        // Act
        var result1 = parts1.GetNormalizedStrForHashing();
        var result2 = parts2.GetNormalizedStrForHashing();

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void GetNormalizedStrForHashing_WithDifferentContent_ProducesDifferentResult()
    {
        // Arrange
        var parts1 = new[] { "Hello", "World" };
        var parts2 = new[] { "Hello", "Universe" };

        // Act
        var result1 = parts1.GetNormalizedStrForHashing();
        var result2 = parts2.GetNormalizedStrForHashing();

        // Assert
        result1.Should().NotBe(result2);
    }

    #endregion

    #region CleanAndValidateEmail Tests

    [Theory]
    [InlineData("test@example.com", "test@example.com")]
    [InlineData("TEST@EXAMPLE.COM", "test@example.com")]
    [InlineData("  test@example.com  ", "test@example.com")]
    [InlineData("user.name@example.com", "user.name@example.com")]
    [InlineData("user+tag@example.co.uk", "user+tag@example.co.uk")]
    public void CleanAndValidateEmail_WithValidEmail_ReturnsCleanedLowercaseEmail(
        string input,
        string expected)
    {
        // Act
        var result = input.CleanAndValidateEmail();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notavalidemail")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    [InlineData("test@.com")]
    [InlineData("test @example.com")]
    [InlineData("test@ example.com")]
    public void CleanAndValidateEmail_WithInvalidEmail_ThrowsArgumentException(string? input)
    {
        // Act
        var act = input.CleanAndValidateEmail;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Invalid email address format.*");
    }

    #endregion

    #region CleanAndValidatePhoneNumber Tests

    [Theory]
    [InlineData("1234567", "1234567")]
    [InlineData("123456789012345", "123456789012345")]
    [InlineData("+1 (555) 123-4567", "15551234567")]
    [InlineData("555-123-4567", "5551234567")]
    [InlineData("(555) 123 4567", "5551234567")]
    [InlineData("+44 20 7946 0958", "442079460958")]
    [InlineData("  555.123.4567  ", "5551234567")]
    public void CleanAndValidatePhoneNumber_WithValidPhone_ReturnsDigitsOnly(
        string input,
        string expected)
    {
        // Act
        var result = input.CleanAndValidatePhoneNumber();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CleanAndValidatePhoneNumber_WithNullOrEmpty_ThrowsArgumentException(string? input)
    {
        // Act
        var act = input.CleanAndValidatePhoneNumber;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Phone number cannot be null or empty.*");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("---")]
    [InlineData("()")]
    public void CleanAndValidatePhoneNumber_WithNoDigits_ThrowsArgumentException(string input)
    {
        // Act
        var act = input.CleanAndValidatePhoneNumber;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Invalid phone number format.*");
    }

    [Theory]
    [InlineData("123456")]                    // 6 digits (too short)
    [InlineData("1234567890123456")]          // 16 digits (too long)
    public void CleanAndValidatePhoneNumber_WithInvalidLength_ThrowsArgumentException(string input)
    {
        // Act
        var act = input.CleanAndValidatePhoneNumber;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Phone number must be between 7 and 15 digits in length.*");
    }

    #endregion
}
