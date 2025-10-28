using System.Collections.Immutable;
using FluentAssertions;
using Geo.Domain.ValueObjects;
using Xunit;

namespace Geo.Tests.Unit.Domain.ValueObjects;

/// <summary>
/// Unit tests for <see cref="Geo.Domain.ValueObjects.EmailAddress"/>.
/// </summary>
public class EmailAddressTests
{
    #region Valid Creation

    [Fact]
    public void Create_WithValueOnly_Success()
    {
        // Arrange
        const string email = "test@example.com";

        // Act
        var emailAddress = EmailAddress.Create(email);

        // Assert
        emailAddress.Should().NotBeNull();
        emailAddress.Value.Should().Be("test@example.com");
        emailAddress.Labels.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithValueAndLabels_Success()
    {
        // Arrange
        const string email = "test@example.com";
        string[] labels = ["work", "primary"];

        // Act
        var emailAddress = EmailAddress.Create(email, labels);

        // Assert
        emailAddress.Should().NotBeNull();
        emailAddress.Value.Should().Be("test@example.com");
        emailAddress.Labels.Should().BeEquivalentTo("work", "primary");
    }

    [Fact]
    public void Create_WithNullLabels_ReturnsEmptyHashSet()
    {
        // Arrange
        const string email = "test@example.com";

        // Act
        var emailAddress = EmailAddress.Create(email);

        // Assert
        emailAddress.Labels.Should().NotBeNull();
        emailAddress.Labels.Should().BeEmpty();
    }

    #endregion

    #region Email Validation

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@example.com")]
    [InlineData("user+tag@example.co.uk")]
    [InlineData("test123@test-domain.com")]
    public void Create_WithValidEmail_Success(string email)
    {
        // Act
        var emailAddress = EmailAddress.Create(email);

        // Assert
        emailAddress.Value.Should().Be(email.ToLowerInvariant());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("notanemail")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    [InlineData("test@.com")]
    [InlineData("test @example.com")]
    public void Create_WithInvalidEmail_ThrowsArgumentException(string? email)
    {
        // Act
        var act = () => EmailAddress.Create(email!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Clean Email - No Changes

    [Fact]
    public void Create_WithCleanEmail_NoChanges()
    {
        // Arrange
        const string clean_email = "test@example.com";

        // Act
        var emailAddress = EmailAddress.Create(clean_email);

        // Assert
        emailAddress.Value.Should().Be(clean_email);
    }

    #endregion

    #region Dirty Email - Cleanup

    [Fact]
    public void Create_WithDirtyEmail_CleansAndLowercases()
    {
        // Arrange
        const string dirty_email = "  TEST@EXAMPLE.COM  ";

        // Act
        var emailAddress = EmailAddress.Create(dirty_email);

        // Assert
        emailAddress.Value.Should().Be("test@example.com");
    }

    [Theory]
    [InlineData("TEST@EXAMPLE.COM", "test@example.com")]
    [InlineData("  test@example.com  ", "test@example.com")]
    [InlineData("  TEST@EXAMPLE.COM  ", "test@example.com")]
    public void Create_WithVariousDirtyEmails_CleansCorrectly(string input, string expected)
    {
        // Act
        var emailAddress = EmailAddress.Create(input);

        // Assert
        emailAddress.Value.Should().Be(expected);
    }

    #endregion

    #region Labels - HashSet Behavior

    [Fact]
    public void Create_WithEmptyLabels_ReturnsEmptyHashSet()
    {
        // Arrange
        const string email = "test@example.com";
        var labels = Array.Empty<string>();

        // Act
        var emailAddress = EmailAddress.Create(email, labels);

        // Assert
        emailAddress.Labels.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithValidLabels_ReturnsImmutableHashSet()
    {
        // Arrange
        const string email = "test@example.com";
        string[] labels = ["work", "primary"];

        // Act
        var emailAddress = EmailAddress.Create(email, labels);

        // Assert
        emailAddress.Labels.Should().BeOfType<ImmutableHashSet<string>>();
        emailAddress.Labels.Should().BeEquivalentTo("work", "primary");
    }

    [Fact]
    public void Create_WithDuplicateLabels_RemovesDuplicates()
    {
        // Arrange
        const string email = "test@example.com";
        string[] labels = ["work", "work", "primary"];

        // Act
        var emailAddress = EmailAddress.Create(email, labels);

        // Assert
        emailAddress.Labels.Should().HaveCount(2);
        emailAddress.Labels.Should().BeEquivalentTo("work", "primary");
    }

    [Fact]
    public void Create_WithDirtyLabels_CleansLabels()
    {
        // Arrange
        const string email = "test@example.com";
        string[] dirtyLabels = ["  work  ", "  primary  "];

        // Act
        var emailAddress = EmailAddress.Create(email, dirtyLabels);

        // Assert
        emailAddress.Labels.Should().BeEquivalentTo("work", "primary");
    }

    [Fact]
    public void Create_WithLabelsContainingWhitespace_RemovesWhitespaceEntries()
    {
        // Arrange
        const string email = "test@example.com";
        string[] labels = ["work", "   ", "primary"];

        // Act
        var emailAddress = EmailAddress.Create(email, labels);

        // Assert
        emailAddress.Labels.Should().HaveCount(2);
        emailAddress.Labels.Should().BeEquivalentTo("work", "primary");
    }

    #endregion

    #region Create Overload Tests

    [Fact]
    public void Create_WithExistingEmailAddress_CreatesNewInstance()
    {
        // Arrange
        var original = EmailAddress.Create("test@example.com", ["work", "primary"]);

        // Act
        var copy = EmailAddress.Create(original);

        // Assert
        copy.Should().NotBeNull();
        copy.Value.Should().Be(original.Value);
        copy.Labels.Should().BeEquivalentTo(original.Labels);
        copy.Should().Be(original); // Value equality
    }

    [Fact]
    public void Create_WithInvalidExistingEmailAddress_ThrowsArgumentException()
    {
        // Arrange - Create invalid email by bypassing factory
        var invalid = new EmailAddress
        {
            Value = "invalid-email",
            Labels = []
        };

        // Act
        var act = () => EmailAddress.Create(invalid);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region CreateMany Tests

    [Fact]
    public void CreateMany_WithNullInput_ReturnsEmptyList()
    {
        // Act
        var result = EmailAddress.CreateMany((IEnumerable<(string, IEnumerable<string>?)>?)null);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void CreateMany_WithEmptyInput_ReturnsEmptyList()
    {
        // Arrange
        var emails = Array.Empty<(string, IEnumerable<string>?)>();

        // Act
        var result = EmailAddress.CreateMany(emails);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void CreateMany_WithValidTuples_ReturnsImmutableList()
    {
        // Arrange
        var emails = new[]
        {
            ("test1@example.com", (IEnumerable<string>?)["work"]),
            ("test2@example.com", (IEnumerable<string>?)["personal"])
        };

        // Act
        var result = EmailAddress.CreateMany(emails);

        // Assert
        result.Should().BeOfType<ImmutableList<EmailAddress>>();
        result.Should().HaveCount(2);
        result[0].Value.Should().Be("test1@example.com");
        result[1].Value.Should().Be("test2@example.com");
    }

    [Fact]
    public void CreateMany_WithEmailAddresses_ReturnsImmutableList()
    {
        // Arrange
        var emails = new[]
        {
            EmailAddress.Create("test1@example.com", ["work"]),
            EmailAddress.Create("test2@example.com", ["personal"])
        };

        // Act
        var result = EmailAddress.CreateMany(emails);

        // Assert
        result.Should().BeOfType<ImmutableList<EmailAddress>>();
        result.Should().HaveCount(2);
    }

    #endregion

    #region Value Equality

    [Fact]
    public void EmailAddress_WithSameValues_AreEqual()
    {
        // Arrange
        var email1 = EmailAddress.Create("test@example.com", ["work"]);
        var email2 = EmailAddress.Create("test@example.com", ["work"]);

        // Assert
        email1.Should().Be(email2);
        (email1 == email2).Should().BeTrue();
    }

    [Fact]
    public void EmailAddress_WithDifferentValue_AreNotEqual()
    {
        // Arrange
        var email1 = EmailAddress.Create("test1@example.com");
        var email2 = EmailAddress.Create("test2@example.com");

        // Assert
        email1.Should().NotBe(email2);
        (email1 != email2).Should().BeTrue();
    }

    [Fact]
    public void EmailAddress_WithDifferentLabels_AreNotEqual()
    {
        // Arrange
        var email1 = EmailAddress.Create("test@example.com", ["work"]);
        var email2 = EmailAddress.Create("test@example.com", ["personal"]);

        // Assert
        email1.Should().NotBe(email2);
    }

    [Fact]
    public void EmailAddress_LabelsOrderDoesNotMatter_AreEqual()
    {
        // Arrange
        var email1 = EmailAddress.Create("test@example.com", ["work", "primary"]);
        var email2 = EmailAddress.Create("test@example.com", ["primary", "work"]);

        // Assert - HashSet is unordered, so order shouldn't matter
        email1.Should().Be(email2);
    }

    #endregion
}
