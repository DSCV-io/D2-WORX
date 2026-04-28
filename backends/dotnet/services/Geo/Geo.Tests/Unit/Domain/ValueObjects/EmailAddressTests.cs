// -----------------------------------------------------------------------
// <copyright file="EmailAddressTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Tests.Unit.Domain.ValueObjects;

using System.Collections.Immutable;
using D2.Geo.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

/// <summary>
/// Unit tests for <see cref="EmailAddress"/>.
/// </summary>
public class EmailAddressTests
{
    #region Valid Creation

    /// <summary>
    /// Tests that creating EmailAddress with only value succeeds.
    /// </summary>
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

    /// <summary>
    /// Tests that creating EmailAddress with value and labels succeeds.
    /// </summary>
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

    /// <summary>
    /// Tests that creating EmailAddress with null labels returns an empty HashSet.
    /// </summary>
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

    /// <summary>
    /// Tests that creating EmailAddress with valid email formats succeeds.
    /// </summary>
    ///
    /// <param name="email">
    /// The valid email address to test.
    /// </param>
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

    /// <summary>
    /// Tests that creating EmailAddress with invalid email formats throws ArgumentException.
    /// </summary>
    ///
    /// <param name="email">
    /// The invalid email address to test.
    /// </param>
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

    /// <summary>
    /// Tests that creating EmailAddress with a clean email returns it unchanged.
    /// </summary>
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

    /// <summary>
    /// Tests that creating EmailAddress with a dirty email cleans and lowercases it.
    /// </summary>
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

    /// <summary>
    /// Tests that creating EmailAddress with various dirty emails cleans them correctly.
    /// </summary>
    ///
    /// <param name="input">
    /// The dirty email input.
    /// </param>
    /// <param name="expected">
    /// The expected cleaned email.
    /// </param>
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

    /// <summary>
    /// Tests that creating EmailAddress with empty labels returns an empty HashSet.
    /// </summary>
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

    /// <summary>
    /// Tests that creating EmailAddress with valid labels returns an ImmutableHashSet.
    /// </summary>
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

    /// <summary>
    /// Tests that creating EmailAddress with duplicate labels removes duplicates.
    /// </summary>
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

    /// <summary>
    /// Tests that creating EmailAddress with dirty labels cleans them.
    /// </summary>
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

    /// <summary>
    /// Tests that creating EmailAddress with labels containing only whitespace removes those
    /// entries.
    /// </summary>
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

    /// <summary>
    /// Tests that creating EmailAddress from an existing instance returns a new instance.
    /// </summary>
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

    /// <summary>
    /// Tests that creating EmailAddress from an existing instance with invalid email
    /// throws ArgumentException.
    /// </summary>
    [Fact]
    public void Create_WithInvalidExistingEmailAddress_ThrowsArgumentException()
    {
        // Arrange - Create invalid email by bypassing factory
        var invalid = new EmailAddress
        {
            Value = "invalid-email",
            Labels = [],
        };

        // Act
        var act = () => EmailAddress.Create(invalid);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region CreateMany Tests

    /// <summary>
    /// Tests that creating many EmailAddresses with null input returns an empty list.
    /// </summary>
    [Fact]
    public void CreateMany_WithNullInput_ReturnsEmptyList()
    {
        // Act
        var result = EmailAddress.CreateMany((IEnumerable<(string, IEnumerable<string>?)>?)null);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that creating many EmailAddresses with empty input returns an empty list.
    /// </summary>
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

    /// <summary>
    /// Tests that creating many EmailAddresses with valid tuples returns an ImmutableList.
    /// </summary>
    [Fact]
    public void CreateMany_WithValidTuples_ReturnsImmutableList()
    {
        // Arrange
        var emails = new[]
        {
            ("test1@example.com", ["work"]),
            ("test2@example.com", (IEnumerable<string>?)["personal"]),
        };

        // Act
        var result = EmailAddress.CreateMany(emails);

        // Assert
        result.Should().BeOfType<ImmutableList<EmailAddress>>();
        result.Should().HaveCount(2);
        result[0].Value.Should().Be("test1@example.com");
        result[1].Value.Should().Be("test2@example.com");
    }

    /// <summary>
    /// Tests that creating many EmailAddresses from existing instances returns an ImmutableList.
    /// </summary>
    [Fact]
    public void CreateMany_WithEmailAddresses_ReturnsImmutableList()
    {
        // Arrange
        var emails = new[]
        {
            EmailAddress.Create("test1@example.com", ["work"]),
            EmailAddress.Create("test2@example.com", ["personal"]),
        };

        // Act
        var result = EmailAddress.CreateMany(emails);

        // Assert
        result.Should().BeOfType<ImmutableList<EmailAddress>>();
        result.Should().HaveCount(2);
    }

    #endregion

    #region Value Equality

    /// <summary>
    /// Tests that two EmailAddress instances with the same values are equal.
    /// </summary>
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

    /// <summary>
    /// Tests that two EmailAddress instances with different values are not equal.
    /// </summary>
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

    /// <summary>
    /// Tests that two EmailAddress instances with different labels are not equal.
    /// </summary>
    [Fact]
    public void EmailAddress_WithDifferentLabels_AreNotEqual()
    {
        // Arrange
        var email1 = EmailAddress.Create("test@example.com", ["work"]);
        var email2 = EmailAddress.Create("test@example.com", ["personal"]);

        // Assert
        email1.Should().NotBe(email2);
    }

    /// <summary>
    /// Tests that two EmailAddress instances with the same labels in different order are equal.
    /// </summary>
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

    #region GetHashCode

    /// <summary>
    /// Tests that GetHashCode returns the same value for equal EmailAddress instances.
    /// </summary>
    [Fact]
    public void GetHashCode_WithEqualInstances_ReturnsSameValue()
    {
        // Arrange
        var email1 = EmailAddress.Create("test@example.com", ["work"]);
        var email2 = EmailAddress.Create("test@example.com", ["work"]);

        // Act & Assert
        email1.GetHashCode().Should().Be(email2.GetHashCode());
    }

    /// <summary>
    /// Tests that GetHashCode returns different values for different EmailAddress instances.
    /// </summary>
    [Fact]
    public void GetHashCode_WithDifferentValues_ReturnsDifferentValue()
    {
        // Arrange
        var email1 = EmailAddress.Create("test1@example.com");
        var email2 = EmailAddress.Create("test2@example.com");

        // Act & Assert
        email1.GetHashCode().Should().NotBe(email2.GetHashCode());
    }

    /// <summary>
    /// Tests that GetHashCode returns different values for same email with different labels.
    /// </summary>
    [Fact]
    public void GetHashCode_WithDifferentLabels_ReturnsDifferentValue()
    {
        // Arrange
        var email1 = EmailAddress.Create("test@example.com", ["work"]);
        var email2 = EmailAddress.Create("test@example.com", ["personal"]);

        // Act & Assert
        email1.GetHashCode().Should().NotBe(email2.GetHashCode());
    }

    #endregion

    #region Equals

    /// <summary>
    /// Tests that Equals returns false when comparing with null.
    /// </summary>
    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        // Arrange
        var email = EmailAddress.Create("test@example.com");

        // Act & Assert
        email.Equals(null).Should().BeFalse();
    }

    /// <summary>
    /// Tests that Equals returns true for instances with same value and labels.
    /// </summary>
    [Fact]
    public void Equals_WithEqualValueAndLabels_ReturnsTrue()
    {
        // Arrange
        var email1 = EmailAddress.Create("test@example.com", ["work", "primary"]);
        var email2 = EmailAddress.Create("test@example.com", ["primary", "work"]); // Different order

        // Act & Assert
        email1.Equals(email2).Should().BeTrue();
    }

    /// <summary>
    /// Tests that Equals returns false for instances with different values.
    /// </summary>
    [Fact]
    public void Equals_WithDifferentValues_ReturnsFalse()
    {
        // Arrange
        var email1 = EmailAddress.Create("test1@example.com");
        var email2 = EmailAddress.Create("test2@example.com");

        // Act & Assert
        email1.Equals(email2).Should().BeFalse();
    }

    #endregion
}
