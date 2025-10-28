using System.Collections.Immutable;
using FluentAssertions;
using Geo.Domain.ValueObjects;
using Xunit;

namespace Geo.Tests.Unit.Domain.ValueObjects;

/// <summary>
/// Unit tests for <see cref="Geo.Domain.ValueObjects.ContactMethods"/>.
/// </summary>
public class ContactMethodsTests
{
    #region Valid Creation

    [Fact]
    public void Create_WithNoParameters_ReturnsEmptyLists()
    {
        // Act
        var contactMethods = ContactMethods.Create();

        // Assert
        contactMethods.Should().NotBeNull();
        contactMethods.Emails.Should().BeEmpty();
        contactMethods.PhoneNumbers.Should().BeEmpty();
        contactMethods.PrimaryEmail.Should().BeNull();
        contactMethods.PrimaryPhoneNumber.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmailsOnly_Success()
    {
        // Arrange
        var emails = ImmutableList.Create(
            EmailAddress.Create("test@example.com", ["work"]));

        // Act
        var contactMethods = ContactMethods.Create(emails);

        // Assert
        contactMethods.Should().NotBeNull();
        contactMethods.Emails.Should().HaveCount(1);
        contactMethods.PhoneNumbers.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithPhoneNumbersOnly_Success()
    {
        // Arrange
        var phones = ImmutableList.Create(
            PhoneNumber.Create("5551234567", ["mobile"]));

        // Act
        var contactMethods = ContactMethods.Create(phoneNumbers: phones);

        // Assert
        contactMethods.Should().NotBeNull();
        contactMethods.Emails.Should().BeEmpty();
        contactMethods.PhoneNumbers.Should().HaveCount(1);
    }

    [Fact]
    public void Create_WithBothEmailsAndPhones_Success()
    {
        // Arrange
        var emails = ImmutableList.Create(
            EmailAddress.Create("test@example.com", ["work"]));
        var phones = ImmutableList.Create(
            PhoneNumber.Create("5551234567", ["mobile"]));

        // Act
        var contactMethods = ContactMethods.Create(emails, phones);

        // Assert
        contactMethods.Should().NotBeNull();
        contactMethods.Emails.Should().HaveCount(1);
        contactMethods.PhoneNumbers.Should().HaveCount(1);
    }

    [Fact]
    public void Create_WithMultipleEmailsAndPhones_Success()
    {
        // Arrange
        var emails = ImmutableList.Create(
            EmailAddress.Create("work@example.com", ["work"]),
            EmailAddress.Create("personal@example.com", ["personal"]));
        var phones = ImmutableList.Create(
            PhoneNumber.Create("5551234567", ["mobile"]),
            PhoneNumber.Create("5559876543", ["work"]));

        // Act
        var contactMethods = ContactMethods.Create(emails, phones);

        // Assert
        contactMethods.Emails.Should().HaveCount(2);
        contactMethods.PhoneNumbers.Should().HaveCount(2);
    }

    #endregion

    #region Primary Contact Methods

    [Fact]
    public void PrimaryEmail_WithNoEmails_ReturnsNull()
    {
        // Arrange
        var contactMethods = ContactMethods.Create();

        // Assert
        contactMethods.PrimaryEmail.Should().BeNull();
    }

    [Fact]
    public void PrimaryEmail_WithOneEmail_ReturnsThatEmail()
    {
        // Arrange
        var email = EmailAddress.Create("test@example.com");
        var emails = ImmutableList.Create(email);
        var contactMethods = ContactMethods.Create(emails);

        // Assert
        contactMethods.PrimaryEmail.Should().Be(email);
    }

    [Fact]
    public void PrimaryEmail_WithMultipleEmails_ReturnsFirstEmail()
    {
        // Arrange
        var firstEmail = EmailAddress.Create("first@example.com");
        var secondEmail = EmailAddress.Create("second@example.com");
        var emails = ImmutableList.Create(firstEmail, secondEmail);
        var contactMethods = ContactMethods.Create(emails);

        // Assert
        contactMethods.PrimaryEmail.Should().Be(firstEmail);
    }

    [Fact]
    public void PrimaryPhoneNumber_WithNoPhones_ReturnsNull()
    {
        // Arrange
        var contactMethods = ContactMethods.Create();

        // Assert
        contactMethods.PrimaryPhoneNumber.Should().BeNull();
    }

    [Fact]
    public void PrimaryPhoneNumber_WithOnePhone_ReturnsThatPhone()
    {
        // Arrange
        var phone = PhoneNumber.Create("5551234567");
        var phones = ImmutableList.Create(phone);
        var contactMethods = ContactMethods.Create(phoneNumbers: phones);

        // Assert
        contactMethods.PrimaryPhoneNumber.Should().Be(phone);
    }

    [Fact]
    public void PrimaryPhoneNumber_WithMultiplePhones_ReturnsFirstPhone()
    {
        // Arrange
        var firstPhone = PhoneNumber.Create("5551234567");
        var secondPhone = PhoneNumber.Create("5559876543");
        var phones = ImmutableList.Create(firstPhone, secondPhone);
        var contactMethods = ContactMethods.Create(phoneNumbers: phones);

        // Assert
        contactMethods.PrimaryPhoneNumber.Should().Be(firstPhone);
    }

    #endregion

    #region Null Parameters

    [Fact]
    public void Create_WithNullEmails_ReturnsEmptyEmailsList()
    {
        // Act
        var contactMethods = ContactMethods.Create(emails: null);

        // Assert
        contactMethods.Emails.Should().NotBeNull();
        contactMethods.Emails.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithNullPhones_ReturnsEmptyPhonesList()
    {
        // Act
        var contactMethods = ContactMethods.Create(phoneNumbers: null);

        // Assert
        contactMethods.PhoneNumbers.Should().NotBeNull();
        contactMethods.PhoneNumbers.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithBothNull_ReturnsEmptyLists()
    {
        // Act
        var contactMethods = ContactMethods.Create();

        // Assert
        contactMethods.Emails.Should().BeEmpty();
        contactMethods.PhoneNumbers.Should().BeEmpty();
    }

    #endregion

    #region Validation Through CreateMany

    [Fact]
    public void Create_WithInvalidEmail_ThrowsArgumentException()
    {
        // Arrange
        var emails = ImmutableList.Create(
            new EmailAddress { Value = "invalid-email", Labels = [] });

        // Act
        var act = () => ContactMethods.Create(emails);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithInvalidPhone_ThrowsArgumentException()
    {
        // Arrange
        var phones = ImmutableList.Create(
            new PhoneNumber { Value = "123", Labels = [] }); // Too short

        // Act
        var act = () => ContactMethods.Create(phoneNumbers: phones);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Create Overload Tests

    [Fact]
    public void Create_WithExistingContactMethods_CreatesNewInstance()
    {
        // Arrange
        var emails = ImmutableList.Create(
            EmailAddress.Create("test@example.com", ["work"]));
        var phones = ImmutableList.Create(
            PhoneNumber.Create("5551234567", ["mobile"]));
        var original = ContactMethods.Create(emails, phones);

        // Act
        var copy = ContactMethods.Create(original);

        // Assert
        copy.Should().NotBeNull();
        copy.Emails.Should().HaveCount(original.Emails.Count);
        copy.PhoneNumbers.Should().HaveCount(original.PhoneNumbers.Count);
        copy.Should().Be(original); // Value equality
    }

    [Fact]
    public void Create_WithExistingContactMethodsWithNoContacts_Success()
    {
        // Arrange
        var original = ContactMethods.Create();

        // Act
        var copy = ContactMethods.Create(original);

        // Assert
        copy.Should().NotBeNull();
        copy.Emails.Should().BeEmpty();
        copy.PhoneNumbers.Should().BeEmpty();
        copy.Should().Be(original);
    }

    #endregion

    #region Value Equality

    [Fact]
    public void ContactMethods_WithSameValues_AreEqual()
    {
        // Arrange
        var emails = ImmutableList.Create(
            EmailAddress.Create("test@example.com", ["work"]));
        var phones = ImmutableList.Create(
            PhoneNumber.Create("5551234567", ["mobile"]));

        var contactMethods1 = ContactMethods.Create(emails, phones);
        var contactMethods2 = ContactMethods.Create(emails, phones);

        // Assert
        contactMethods1.Should().Be(contactMethods2);
        (contactMethods1 == contactMethods2).Should().BeTrue();
    }

    [Fact]
    public void ContactMethods_WithDifferentEmails_AreNotEqual()
    {
        // Arrange
        var emails1 = ImmutableList.Create(
            EmailAddress.Create("test1@example.com"));
        var emails2 = ImmutableList.Create(
            EmailAddress.Create("test2@example.com"));

        var contactMethods1 = ContactMethods.Create(emails1);
        var contactMethods2 = ContactMethods.Create(emails2);

        // Assert
        contactMethods1.Should().NotBe(contactMethods2);
        (contactMethods1 != contactMethods2).Should().BeTrue();
    }

    [Fact]
    public void ContactMethods_WithDifferentPhones_AreNotEqual()
    {
        // Arrange
        var phones1 = ImmutableList.Create(
            PhoneNumber.Create("5551234567"));
        var phones2 = ImmutableList.Create(
            PhoneNumber.Create("5559876543"));

        var contactMethods1 = ContactMethods.Create(phoneNumbers: phones1);
        var contactMethods2 = ContactMethods.Create(phoneNumbers: phones2);

        // Assert
        contactMethods1.Should().NotBe(contactMethods2);
    }

    [Fact]
    public void ContactMethods_WithDifferentOrderEmails_AreNotEqual()
    {
        // Arrange
        var email1 = EmailAddress.Create("first@example.com");
        var email2 = EmailAddress.Create("second@example.com");

        var emails1 = ImmutableList.Create(email1, email2);
        var emails2 = ImmutableList.Create(email2, email1);

        var contactMethods1 = ContactMethods.Create(emails1);
        var contactMethods2 = ContactMethods.Create(emails2);

        // Assert - Order matters for ImmutableList (affects primary)
        contactMethods1.Should().NotBe(contactMethods2);
    }

    [Fact]
    public void ContactMethods_WithDifferentOrderPhones_AreNotEqual()
    {
        // Arrange
        var phone1 = PhoneNumber.Create("5551234567");
        var phone2 = PhoneNumber.Create("5559876543");

        var phones1 = ImmutableList.Create(phone1, phone2);
        var phones2 = ImmutableList.Create(phone2, phone1);

        var contactMethods1 = ContactMethods.Create(phoneNumbers: phones1);
        var contactMethods2 = ContactMethods.Create(phoneNumbers: phones2);

        // Assert - Order matters for ImmutableList (affects primary)
        contactMethods1.Should().NotBe(contactMethods2);
    }

    [Fact]
    public void ContactMethods_BothEmpty_AreEqual()
    {
        // Arrange
        var contactMethods1 = ContactMethods.Create();
        var contactMethods2 = ContactMethods.Create();

        // Assert
        contactMethods1.Should().Be(contactMethods2);
    }

    #endregion
}
