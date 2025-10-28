using FluentAssertions;
using Geo.Domain.Entities;
using Geo.Domain.Enums;
using Geo.Domain.Exceptions;
using Geo.Domain.ValueObjects;
using Xunit;

namespace Geo.Tests.Unit.Domain.Entities;

/// <summary>
/// Unit tests for the <see cref="Geo.Domain.Entities.Contact"/> entity.
/// </summary>
public class ContactTests
{
    #region Valid Creation - Minimal

    [Fact]
    public void Create_WithContextKeyAndRelatedEntityId_Success()
    {
        // Arrange
        const string context_key = "auth-user";
        var relatedEntityId = Guid.CreateVersion7();

        // Act
        var contact = Contact.Create(context_key, relatedEntityId);

        // Assert
        contact.Should().NotBeNull();
        contact.Id.Should().NotBeEmpty();
        contact.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        contact.ContextKey.Should().Be(context_key);
        contact.RelatedEntityId.Should().Be(relatedEntityId);
        contact.ContactMethods.Should().BeNull();
        contact.PersonalDetails.Should().BeNull();
        contact.ProfessionalDetails.Should().BeNull();
        contact.LocationHashId.Should().BeNull();
    }

    #endregion

    #region Valid Creation - With Optional Properties

    [Fact]
    public void Create_WithContactMethods_Success()
    {
        // Arrange
        const string context_key = "auth-user";
        var relatedEntityId = Guid.CreateVersion7();
        var email = EmailAddress.Create("test@example.com");
        var contactMethods = ContactMethods.Create(
            emails: [email]);

        // Act
        var contact = Contact.Create(
            context_key,
            relatedEntityId,
            contactMethods: contactMethods);

        // Assert
        contact.ContactMethods.Should().NotBeNull();
        contact.ContactMethods!.Emails.Should().HaveCount(1);
        contact.ContactMethods.Emails[0].Value.Should().Be("test@example.com");
    }

    [Fact]
    public void Create_WithPersonalDetails_Success()
    {
        // Arrange
        const string context_key = "auth-user";
        var relatedEntityId = Guid.CreateVersion7();
        var personal = Personal.Create("John", lastName: "Doe");

        // Act
        var contact = Contact.Create(
            context_key,
            relatedEntityId,
            personalDetails: personal);

        // Assert
        contact.PersonalDetails.Should().NotBeNull();
        contact.PersonalDetails!.FirstName.Should().Be("John");
        contact.PersonalDetails.LastName.Should().Be("Doe");
    }

    [Fact]
    public void Create_WithProfessionalDetails_Success()
    {
        // Arrange
        const string context_key = "sales-order";
        var relatedEntityId = Guid.CreateVersion7();
        var professional = Professional.Create("ACME Corp", jobTitle: "CEO");

        // Act
        var contact = Contact.Create(
            context_key,
            relatedEntityId,
            professionalDetails: professional);

        // Assert
        contact.ProfessionalDetails.Should().NotBeNull();
        contact.ProfessionalDetails!.CompanyName.Should().Be("ACME Corp");
        contact.ProfessionalDetails.JobTitle.Should().Be("CEO");
    }

    [Fact]
    public void Create_WithLocationHashId_Success()
    {
        // Arrange
        const string context_key = "auth-user";
        var relatedEntityId = Guid.CreateVersion7();
        var locationHashId = new byte[32];

        // Act
        var contact = Contact.Create(
            context_key,
            relatedEntityId,
            locationHashId: locationHashId);

        // Assert
        contact.LocationHashId.Should().BeEquivalentTo(locationHashId);
    }

    #endregion

    #region Valid Creation - Full Contact

    [Fact]
    public void Create_WithAllProperties_Success()
    {
        // Arrange
        const string context_key = "auth-user";
        var relatedEntityId = Guid.CreateVersion7();
        var email = EmailAddress.Create("john.doe@example.com", ["work"]);
        var phone = PhoneNumber.Create("+1 (555) 123-4567", ["mobile"]);
        var contactMethods = ContactMethods.Create(
            emails: [email],
            phoneNumbers: [phone]);
        var personal = Personal.Create(
            "John",
            title: NameTitle.Mr,
            middleName: "Michael",
            lastName: "Doe",
            dateOfBirth: new DateOnly(1990, 5, 15));
        var professional = Professional.Create(
            "ACME Corp",
            jobTitle: "Senior Developer",
            department: "Engineering");
        var locationHashId = new byte[32];

        // Act
        var contact = Contact.Create(
            context_key,
            relatedEntityId,
            contactMethods,
            personal,
            professional,
            locationHashId);

        // Assert
        contact.Should().NotBeNull();
        contact.Id.Should().NotBeEmpty();
        contact.ContextKey.Should().Be("auth-user");
        contact.RelatedEntityId.Should().Be(relatedEntityId);
        contact.ContactMethods.Should().NotBeNull();
        contact.PersonalDetails.Should().NotBeNull();
        contact.ProfessionalDetails.Should().NotBeNull();
        contact.LocationHashId.Should().NotBeNull();
    }

    #endregion

    #region ContextKey Validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyContextKey_ThrowsGeoValidationException(string? contextKey)
    {
        // Arrange
        var relatedEntityId = Guid.CreateVersion7();

        // Act
        var act = () => Contact.Create(contextKey!, relatedEntityId);

        // Assert
        act.Should().Throw<GeoValidationException>()
            .WithMessage("*ContextKey*required*");
    }

    [Fact]
    public void Create_WithValidContextKey_Success()
    {
        // Arrange
        const string context_key = "auth-user";
        var relatedEntityId = Guid.CreateVersion7();

        // Act
        var contact = Contact.Create(context_key, relatedEntityId);

        // Assert
        contact.ContextKey.Should().Be("auth-user");
    }

    [Theory]
    [InlineData("auth-user")]
    [InlineData("sales-order")]
    [InlineData("invoice")]
    [InlineData("shipping-label")]
    public void Create_WithVariousContextKeys_Success(string contextKey)
    {
        // Arrange
        var relatedEntityId = Guid.CreateVersion7();

        // Act
        var contact = Contact.Create(contextKey, relatedEntityId);

        // Assert
        contact.ContextKey.Should().Be(contextKey);
    }

    #endregion

    #region RelatedEntityId Validation

    [Fact]
    public void Create_WithEmptyGuidRelatedEntityId_ThrowsGeoValidationException()
    {
        // Arrange
        const string context_key = "auth-user";
        var emptyGuid = Guid.Empty;

        // Act
        var act = () => Contact.Create(context_key, emptyGuid);

        // Assert
        act.Should().Throw<GeoValidationException>()
            .WithMessage("*RelatedEntityId*required*");
    }

    [Fact]
    public void Create_WithValidRelatedEntityId_Success()
    {
        // Arrange
        const string context_key = "auth-user";
        var relatedEntityId = Guid.CreateVersion7();

        // Act
        var contact = Contact.Create(context_key, relatedEntityId);

        // Assert
        contact.RelatedEntityId.Should().Be(relatedEntityId);
        contact.RelatedEntityId.Should().NotBeEmpty();
    }

    #endregion

    #region Guid Generation

    [Fact]
    public void Create_GeneratesUniqueGuids()
    {
        // Arrange
        const string context_key = "auth-user";
        var relatedEntityId = Guid.CreateVersion7();

        // Act - Create 10 contacts
        var contacts = Enumerable.Range(0, 10)
            .Select(_ => Contact.Create(context_key, relatedEntityId))
            .ToList();

        // Assert - All IDs should be unique
        contacts.Select(c => c.Id).Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region CreatedAt

    [Fact]
    public void Create_SetsCreatedAtToUtcNow()
    {
        // Arrange
        const string context_key = "auth-user";
        var relatedEntityId = Guid.CreateVersion7();
        var before = DateTime.UtcNow;

        // Act
        var contact = Contact.Create(context_key, relatedEntityId);

        // Assert
        var after = DateTime.UtcNow;
        contact.CreatedAt.Should().BeOnOrAfter(before);
        contact.CreatedAt.Should().BeOnOrBefore(after);
        contact.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    #endregion

    #region Nested Value Object Validation Through Create

    [Fact]
    public void Create_WithContactMethodsValidatesNested_Success()
    {
        // Arrange
        const string context_key = "auth-user";
        var relatedEntityId = Guid.CreateVersion7();
        var email = EmailAddress.Create("test@example.com");
        var contactMethods = ContactMethods.Create(emails: [email]);

        // Act
        var contact = Contact.Create(context_key, relatedEntityId, contactMethods);

        // Assert - Nested validation passed, ContactMethods recreated
        contact.ContactMethods.Should().NotBeNull();
        contact.ContactMethods!.Emails.Should().HaveCount(1);
    }

    [Fact]
    public void Create_WithPersonalDetailsValidatesNested_Success()
    {
        // Arrange
        const string context_key = "auth-user";
        var relatedEntityId = Guid.CreateVersion7();
        var personal = Personal.Create("John");

        // Act
        var contact = Contact.Create(context_key, relatedEntityId, personalDetails: personal);

        // Assert - Nested validation passed, Personal recreated
        contact.PersonalDetails.Should().NotBeNull();
        contact.PersonalDetails!.FirstName.Should().Be("John");
    }

    [Fact]
    public void Create_WithProfessionalDetailsValidatesNested_Success()
    {
        // Arrange
        const string context_key = "auth-user";
        var relatedEntityId = Guid.CreateVersion7();
        var professional = Professional.Create("ACME Corp");

        // Act
        var contact = Contact.Create(context_key, relatedEntityId, professionalDetails: professional);

        // Assert - Nested validation passed, Professional recreated
        contact.ProfessionalDetails.Should().NotBeNull();
        contact.ProfessionalDetails!.CompanyName.Should().Be("ACME Corp");
    }

    #endregion

    #region Create Overload Tests

    [Fact]
    public void Create_WithExistingContact_CreatesNewInstanceWithNewId()
    {
        // Arrange
        var email = EmailAddress.Create("john.doe@example.com");
        var contactMethods = ContactMethods.Create(emails: [email]);
        var personal = Personal.Create("John", lastName: "Doe");
        var original = Contact.Create(
            "auth-user",
            Guid.CreateVersion7(),
            contactMethods,
            personal);

        // Act
        var copy = Contact.Create(original);

        // Assert
        copy.Should().NotBeNull();
        copy.Id.Should().NotBe(original.Id); // New ID generated
        copy.CreatedAt.Should().NotBe(original.CreatedAt); // New timestamp
        copy.ContextKey.Should().Be(original.ContextKey);
        copy.RelatedEntityId.Should().Be(original.RelatedEntityId);
        copy.ContactMethods.Should().NotBeNull();
        copy.PersonalDetails.Should().NotBeNull();
    }

    #endregion

    #region Loose Coupling Pattern

    [Fact]
    public void Create_WithDifferentContextKeys_AllowsFlexibleDomainUsage()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var orderId = Guid.CreateVersion7();

        // Act - Same contact info, different contexts
        var userContact = Contact.Create("auth-user", userId);
        var orderContact = Contact.Create("sales-order", orderId);

        // Assert - No FK constraints, flexible domain usage
        userContact.ContextKey.Should().Be("auth-user");
        orderContact.ContextKey.Should().Be("sales-order");
        userContact.RelatedEntityId.Should().NotBe(orderContact.RelatedEntityId);
    }

    #endregion
}
