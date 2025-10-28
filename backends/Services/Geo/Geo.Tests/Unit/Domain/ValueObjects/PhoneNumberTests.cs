using System.Collections.Immutable;
using FluentAssertions;
using Geo.Domain.ValueObjects;
using Xunit;

namespace Geo.Tests.Unit.Domain.ValueObjects;

/// <summary>
/// Unit tests for <see cref="Geo.Domain.ValueObjects.PhoneNumber"/>.
/// </summary>
public class PhoneNumberTests
{
    #region Valid Creation

    [Fact]
    public void Create_WithValueOnly_Success()
    {
        // Arrange
        const string phone = "5551234567";

        // Act
        var phoneNumber = PhoneNumber.Create(phone);

        // Assert
        phoneNumber.Should().NotBeNull();
        phoneNumber.Value.Should().Be("5551234567");
        phoneNumber.Labels.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithValueAndLabels_Success()
    {
        // Arrange
        const string phone = "5551234567";
        string[] labels = ["mobile", "primary"];

        // Act
        var phoneNumber = PhoneNumber.Create(phone, labels);

        // Assert
        phoneNumber.Should().NotBeNull();
        phoneNumber.Value.Should().Be("5551234567");
        phoneNumber.Labels.Should().BeEquivalentTo("mobile", "primary");
    }

    [Fact]
    public void Create_WithNullLabels_ReturnsEmptyHashSet()
    {
        // Arrange
        const string phone = "5551234567";

        // Act
        var phoneNumber = PhoneNumber.Create(phone);

        // Assert
        phoneNumber.Labels.Should().NotBeNull();
        phoneNumber.Labels.Should().BeEmpty();
    }

    #endregion

    #region Phone Validation - E.164 Format

    [Theory]
    [InlineData("1234567", "1234567")]              // 7 digits (min)
    [InlineData("123456789012345", "123456789012345")] // 15 digits (max)
    [InlineData("+1 (555) 123-4567", "15551234567")] // US format
    [InlineData("555-123-4567", "5551234567")]
    [InlineData("(555) 123 4567", "5551234567")]
    [InlineData("+44 20 7946 0958", "442079460958")] // UK format
    [InlineData("  555.123.4567  ", "5551234567")]
    public void Create_WithValidPhone_StripsToDigitsOnly(string input, string expected)
    {
        // Act
        var phoneNumber = PhoneNumber.Create(input);

        // Assert
        phoneNumber.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithNullOrEmpty_ThrowsArgumentException(string? phone)
    {
        // Act
        var act = () => PhoneNumber.Create(phone!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("---")]
    [InlineData("()")]
    public void Create_WithNoDigits_ThrowsArgumentException(string phone)
    {
        // Act
        var act = () => PhoneNumber.Create(phone);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("123456")]           // 6 digits (too short)
    [InlineData("1234567890123456")] // 16 digits (too long)
    public void Create_WithInvalidLength_ThrowsArgumentException(string phone)
    {
        // Act
        var act = () => PhoneNumber.Create(phone);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Clean Phone - No Changes

    [Fact]
    public void Create_WithCleanPhone_NoChanges()
    {
        // Arrange
        const string clean_phone = "5551234567";

        // Act
        var phoneNumber = PhoneNumber.Create(clean_phone);

        // Assert
        phoneNumber.Value.Should().Be(clean_phone);
    }

    #endregion

    #region Dirty Phone - Cleanup

    [Fact]
    public void Create_WithDirtyPhone_StripsNonDigits()
    {
        // Arrange
        const string dirty_phone = "+1 (555) 123-4567";

        // Act
        var phoneNumber = PhoneNumber.Create(dirty_phone);

        // Assert
        phoneNumber.Value.Should().Be("15551234567");
    }

    [Theory]
    [InlineData("+1-555-123-4567", "15551234567")]
    [InlineData("(555) 123-4567", "5551234567")]
    [InlineData("555.123.4567", "5551234567")]
    [InlineData("  555 123 4567  ", "5551234567")]
    public void Create_WithVariousDirtyPhones_CleansCorrectly(string input, string expected)
    {
        // Act
        var phoneNumber = PhoneNumber.Create(input);

        // Assert
        phoneNumber.Value.Should().Be(expected);
    }

    #endregion

    #region Labels - HashSet Behavior

    [Fact]
    public void Create_WithEmptyLabels_ReturnsEmptyHashSet()
    {
        // Arrange
        const string phone = "5551234567";
        var labels = Array.Empty<string>();

        // Act
        var phoneNumber = PhoneNumber.Create(phone, labels);

        // Assert
        phoneNumber.Labels.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithValidLabels_ReturnsImmutableHashSet()
    {
        // Arrange
        const string phone = "5551234567";
        string[] labels = ["mobile", "primary"];

        // Act
        var phoneNumber = PhoneNumber.Create(phone, labels);

        // Assert
        phoneNumber.Labels.Should().BeOfType<ImmutableHashSet<string>>();
        phoneNumber.Labels.Should().BeEquivalentTo(["mobile", "primary"]);
    }

    [Fact]
    public void Create_WithDuplicateLabels_RemovesDuplicates()
    {
        // Arrange
        const string phone = "5551234567";
        string[] labels = ["mobile", "mobile", "primary"];

        // Act
        var phoneNumber = PhoneNumber.Create(phone, labels);

        // Assert
        phoneNumber.Labels.Should().HaveCount(2);
        phoneNumber.Labels.Should().BeEquivalentTo(["mobile", "primary"]);
    }

    [Fact]
    public void Create_WithDirtyLabels_CleansLabels()
    {
        // Arrange
        const string phone = "5551234567";
        string[] dirtyLabels = ["  mobile  ", "  primary  "];

        // Act
        var phoneNumber = PhoneNumber.Create(phone, dirtyLabels);

        // Assert
        phoneNumber.Labels.Should().BeEquivalentTo(["mobile", "primary"]);
    }

    [Fact]
    public void Create_WithLabelsContainingWhitespace_RemovesWhitespaceEntries()
    {
        // Arrange
        const string phone = "5551234567";
        string[] labels = ["mobile", "   ", "primary"];

        // Act
        var phoneNumber = PhoneNumber.Create(phone, labels);

        // Assert
        phoneNumber.Labels.Should().HaveCount(2);
        phoneNumber.Labels.Should().BeEquivalentTo(["mobile", "primary"]);
    }

    #endregion

    #region Create Overload Tests

    [Fact]
    public void Create_WithExistingPhoneNumber_CreatesNewInstance()
    {
        // Arrange
        var original = PhoneNumber.Create("5551234567", ["mobile", "primary"]);

        // Act
        var copy = PhoneNumber.Create(original);

        // Assert
        copy.Should().NotBeNull();
        copy.Value.Should().Be(original.Value);
        copy.Labels.Should().BeEquivalentTo(original.Labels);
        copy.Should().Be(original); // Value equality
    }

    [Fact]
    public void Create_WithInvalidExistingPhoneNumber_ThrowsArgumentException()
    {
        // Arrange
        var invalid = new PhoneNumber
        {
            Value = "123", // Too short
            Labels = []
        };

        // Act
        var act = () => PhoneNumber.Create(invalid);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region CreateMany Tests

    [Fact]
    public void CreateMany_WithNullInput_ReturnsEmptyList()
    {
        // Act
        var result = PhoneNumber.CreateMany((IEnumerable<(string, IEnumerable<string>?)>?)null);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void CreateMany_WithEmptyInput_ReturnsEmptyList()
    {
        // Arrange
        var phones = Array.Empty<(string, IEnumerable<string>?)>();

        // Act
        var result = PhoneNumber.CreateMany(phones);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void CreateMany_WithValidTuples_ReturnsImmutableList()
    {
        // Arrange
        var phones = new[]
        {
            ("5551234567", (IEnumerable<string>?)["mobile"]),
            ("5559876543", (IEnumerable<string>?)["work"])
        };

        // Act
        var result = PhoneNumber.CreateMany(phones);

        // Assert
        result.Should().BeOfType<ImmutableList<PhoneNumber>>();
        result.Should().HaveCount(2);
        result[0].Value.Should().Be("5551234567");
        result[1].Value.Should().Be("5559876543");
    }

    [Fact]
    public void CreateMany_WithPhoneNumbers_ReturnsImmutableList()
    {
        // Arrange
        var phones = new[]
        {
            PhoneNumber.Create("5551234567", ["mobile"]),
            PhoneNumber.Create("5559876543", ["work"])
        };

        // Act
        var result = PhoneNumber.CreateMany(phones);

        // Assert
        result.Should().BeOfType<ImmutableList<PhoneNumber>>();
        result.Should().HaveCount(2);
    }

    #endregion

    #region Value Equality

    [Fact]
    public void PhoneNumber_WithSameValues_AreEqual()
    {
        // Arrange
        var phone1 = PhoneNumber.Create("5551234567", ["mobile"]);
        var phone2 = PhoneNumber.Create("5551234567", ["mobile"]);

        // Assert
        phone1.Should().Be(phone2);
        (phone1 == phone2).Should().BeTrue();
    }

    [Fact]
    public void PhoneNumber_WithDifferentValue_AreNotEqual()
    {
        // Arrange
        var phone1 = PhoneNumber.Create("5551234567");
        var phone2 = PhoneNumber.Create("5559876543");

        // Assert
        phone1.Should().NotBe(phone2);
        (phone1 != phone2).Should().BeTrue();
    }

    [Fact]
    public void PhoneNumber_WithDifferentLabels_AreNotEqual()
    {
        // Arrange
        var phone1 = PhoneNumber.Create("5551234567", ["mobile"]);
        var phone2 = PhoneNumber.Create("5551234567", ["work"]);

        // Assert
        phone1.Should().NotBe(phone2);
    }

    [Fact]
    public void PhoneNumber_LabelsOrderDoesNotMatter_AreEqual()
    {
        // Arrange
        var phone1 = PhoneNumber.Create("5551234567", ["mobile", "primary"]);
        var phone2 = PhoneNumber.Create("5551234567", ["primary", "mobile"]);

        // Assert
        phone1.Should().Be(phone2);
    }

    #endregion
}
