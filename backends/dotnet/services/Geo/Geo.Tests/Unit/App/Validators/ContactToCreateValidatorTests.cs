// -----------------------------------------------------------------------
// <copyright file="ContactToCreateValidatorTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Tests.Unit.App.Validators;

using D2.Geo.Client.Validators;
using D2.Geo.Domain.Entities;
using D2.Geo.Domain.Exceptions;
using D2.Geo.Domain.ValueObjects;
using D2.Services.Protos.Geo.V1;
using FluentAssertions;
using Xunit;

/// <summary>
/// Unit tests for the <see cref="ContactToCreateValidator"/> aggregate validator.
/// Includes domain parity tests ensuring Fluent validation is at least as strict
/// as domain factory constraints.
/// </summary>
public class ContactToCreateValidatorTests
{
    #region Valid Input

    /// <summary>
    /// Tests that a minimal valid ContactToCreateDTO passes validation.
    /// </summary>
    [Fact]
    public void Validate_MinimalValidInput_Passes()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = "auth-user",
            RelatedEntityId = Guid.NewGuid().ToString(),
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// Tests that a full valid ContactToCreateDTO passes validation.
    /// </summary>
    [Fact]
    public void Validate_FullValidInput_Passes()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = "auth-user",
            RelatedEntityId = Guid.NewGuid().ToString(),
            PersonalDetails = new PersonalDTO
            {
                FirstName = "John",
                MiddleName = "M",
                LastName = "Doe",
            },
            ProfessionalDetails = new ProfessionalDTO
            {
                CompanyName = "ACME Corp",
                JobTitle = "Developer",
                Department = "Engineering",
            },
            ContactMethods = new ContactMethodsDTO
            {
                Emails = { new EmailAddressDTO { Value = "john@example.com" } },
                PhoneNumbers = { new PhoneNumberDTO { Value = "15551234567" } },
            },
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region ContextKey Validation

    /// <summary>
    /// Tests that empty context key fails (domain parity: Contact.Create throws).
    /// </summary>
    [Fact]
    public void Validate_EmptyContextKey_Fails()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = string.Empty,
            RelatedEntityId = Guid.NewGuid().ToString(),
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("contextKey"));
    }

    /// <summary>
    /// Domain parity: Contact.Create throws for empty context key.
    /// </summary>
    /// <param name="contextKey">The context key value to test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void DomainParity_EmptyContextKey_DomainThrows(string? contextKey)
    {
        var act = () => Contact.Create(contextKey!, Guid.NewGuid());
        act.Should().Throw<GeoValidationException>();
    }

    /// <summary>
    /// Tests that context key exceeding max length fails.
    /// </summary>
    [Fact]
    public void Validate_ContextKeyTooLong_Fails()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = new string('A', 256),
            RelatedEntityId = Guid.NewGuid().ToString(),
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region RelatedEntityId Validation

    /// <summary>
    /// Tests that empty related entity ID fails.
    /// </summary>
    [Fact]
    public void Validate_EmptyRelatedEntityId_Fails()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = "auth-user",
            RelatedEntityId = string.Empty,
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("relatedEntityId"));
    }

    /// <summary>
    /// Tests that Guid.Empty related entity ID fails (domain parity).
    /// </summary>
    [Fact]
    public void Validate_GuidEmptyRelatedEntityId_Fails()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = "auth-user",
            RelatedEntityId = Guid.Empty.ToString(),
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// Domain parity: Contact.Create throws for empty GUID.
    /// </summary>
    [Fact]
    public void DomainParity_EmptyGuidRelatedEntityId_DomainThrows()
    {
        var act = () => Contact.Create("auth-user", Guid.Empty);
        act.Should().Throw<GeoValidationException>();
    }

    /// <summary>
    /// Tests that invalid GUID string fails.
    /// </summary>
    [Fact]
    public void Validate_InvalidGuidRelatedEntityId_Fails()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = "auth-user",
            RelatedEntityId = "not-a-guid",
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region PersonalDetails Validation

    /// <summary>
    /// Tests that empty first name fails when personal details are provided (domain parity).
    /// </summary>
    [Fact]
    public void Validate_EmptyFirstName_Fails()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = "auth-user",
            RelatedEntityId = Guid.NewGuid().ToString(),
            PersonalDetails = new PersonalDTO { FirstName = string.Empty },
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("personalDetails.firstName"));
    }

    /// <summary>
    /// Domain parity: Personal.Create throws for empty first name.
    /// </summary>
    /// <param name="firstName">The first name value to test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void DomainParity_EmptyFirstName_DomainThrows(string? firstName)
    {
        var act = () => Personal.Create(firstName!);
        act.Should().Throw<GeoValidationException>();
    }

    /// <summary>
    /// Tests that first name exceeding max length fails.
    /// </summary>
    [Fact]
    public void Validate_FirstNameTooLong_Fails()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = "auth-user",
            RelatedEntityId = Guid.NewGuid().ToString(),
            PersonalDetails = new PersonalDTO { FirstName = new string('A', 256) },
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region ProfessionalDetails Validation

    /// <summary>
    /// Tests that empty company name fails when professional details are provided (domain parity).
    /// </summary>
    [Fact]
    public void Validate_EmptyCompanyName_Fails()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = "auth-user",
            RelatedEntityId = Guid.NewGuid().ToString(),
            ProfessionalDetails = new ProfessionalDTO { CompanyName = string.Empty },
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName.Contains("professionalDetails.companyName"));
    }

    /// <summary>
    /// Domain parity: Professional.Create throws for empty company name.
    /// </summary>
    /// <param name="companyName">The company name value to test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void DomainParity_EmptyCompanyName_DomainThrows(string? companyName)
    {
        var act = () => Professional.Create(companyName!);
        act.Should().Throw<GeoValidationException>();
    }

    /// <summary>
    /// Tests that company website exceeding max length fails.
    /// </summary>
    [Fact]
    public void Validate_CompanyWebsiteTooLong_Fails()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = "auth-user",
            RelatedEntityId = Guid.NewGuid().ToString(),
            ProfessionalDetails = new ProfessionalDTO
            {
                CompanyName = "ACME",
                CompanyWebsite = new string('A', 2049),
            },
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region Email Validation

    /// <summary>
    /// Tests that invalid email fails validation (domain parity).
    /// </summary>
    [Fact]
    public void Validate_InvalidEmail_Fails()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = "auth-user",
            RelatedEntityId = Guid.NewGuid().ToString(),
            ContactMethods = new ContactMethodsDTO
            {
                Emails = { new EmailAddressDTO { Value = "not-an-email" } },
            },
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// Domain parity: EmailAddress.Create throws for invalid email.
    /// The domain uses StringExtensions.CleanAndValidateEmail which throws ArgumentException.
    /// </summary>
    /// <param name="email">The email value to test.</param>
    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("@domain.com")]
    public void DomainParity_InvalidEmail_DomainThrows(string email)
    {
        var act = () => EmailAddress.Create(email);
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Tests that empty email value fails validation.
    /// </summary>
    [Fact]
    public void Validate_EmptyEmailValue_Fails()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = "auth-user",
            RelatedEntityId = Guid.NewGuid().ToString(),
            ContactMethods = new ContactMethodsDTO
            {
                Emails = { new EmailAddressDTO { Value = string.Empty } },
            },
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region Phone Validation

    /// <summary>
    /// Tests that invalid phone number fails validation (domain parity).
    /// </summary>
    [Fact]
    public void Validate_InvalidPhone_Fails()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = "auth-user",
            RelatedEntityId = Guid.NewGuid().ToString(),
            ContactMethods = new ContactMethodsDTO
            {
                PhoneNumbers = { new PhoneNumberDTO { Value = "123" } },
            },
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// Domain parity: PhoneNumber.Create throws for invalid phone.
    /// The domain uses StringExtensions.CleanAndValidatePhoneNumber which throws ArgumentException.
    /// </summary>
    [Fact]
    public void DomainParity_InvalidPhone_DomainThrows()
    {
        // PhoneNumber.Create extracts digits; "123" = 3 digits < 7 minimum.
        var act = () => PhoneNumber.Create("123");
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Tests that empty phone value fails validation.
    /// </summary>
    [Fact]
    public void Validate_EmptyPhoneValue_Fails()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = "auth-user",
            RelatedEntityId = Guid.NewGuid().ToString(),
            ContactMethods = new ContactMethodsDTO
            {
                PhoneNumbers = { new PhoneNumberDTO { Value = string.Empty } },
            },
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region Index Prefix

    /// <summary>
    /// Tests that indexed error paths use the provided prefix.
    /// </summary>
    [Fact]
    public void Validate_WithIndexPrefix_ErrorPathsIncludePrefix()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = string.Empty,
            RelatedEntityId = string.Empty,
        };
        var validator = new ContactToCreateValidator("items[5].");
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.StartsWith("items[5]."));
    }

    #endregion

    #region IANA Identifier (Timezone) Validation

    /// <summary>
    /// Tests that a valid IANA identifier passes validation.
    /// </summary>
    [Fact]
    public void Validate_ValidIanaIdentifier_Passes()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = "timezone-test",
            RelatedEntityId = Guid.NewGuid().ToString(),
            IanaIdentifier = "America/New_York",
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// Tests that an IANA identifier exceeding max length fails validation.
    /// </summary>
    [Fact]
    public void Validate_IanaIdentifierTooLong_Fails()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = "timezone-test",
            RelatedEntityId = Guid.NewGuid().ToString(),
            IanaIdentifier = new string('A', 65),
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ianaIdentifier");
    }

    #endregion

    #region Multiple Errors

    /// <summary>
    /// Tests that multiple validation errors are collected.
    /// </summary>
    [Fact]
    public void Validate_MultipleErrors_CollectsAll()
    {
        var dto = new ContactToCreateDTO
        {
            ContextKey = string.Empty,
            RelatedEntityId = "not-a-guid",
            PersonalDetails = new PersonalDTO { FirstName = string.Empty },
            ProfessionalDetails = new ProfessionalDTO { CompanyName = string.Empty },
        };
        var validator = new ContactToCreateValidator();
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();

        // Should have errors for contextKey, relatedEntityId, firstName, companyName.
        result.Errors.Count.Should().BeGreaterThanOrEqualTo(4);
    }

    #endregion
}
