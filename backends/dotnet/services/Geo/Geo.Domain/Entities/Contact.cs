// -----------------------------------------------------------------------
// <copyright file="Contact.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable MemberCanBePrivate.Global
namespace D2.Geo.Domain.Entities;

using D2.Geo.Domain.Exceptions;
using D2.Geo.Domain.ValueObjects;
using D2.Shared.Utilities.Attributes;
using D2.Shared.Utilities.Enums;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Represents a collection of contact and / or location information for an individual or
/// organization.
/// </summary>
/// <remarks>
/// Is an aggregate root of the Geography "Geo" Domain.
///
/// Used to store email addresses and phone numbers via the <see cref="ContactMethods"/> value
/// object.
///
/// Uses the <see cref="Personal"/> and <see cref="Professional"/> value objects to store personal
/// and professional details about the contact.
///
/// Uses the <see cref="Location"/> entity to associate a geographical location with the contact.
/// </remarks>
public record Contact
{
    #region Identity

    /// <summary>
    /// Gets the globally unique identifier for the contact.
    /// </summary>
    public required Guid Id { get; init; }

    #endregion

    #region General Properties

    /// <summary>
    /// Gets the date and time when the contact was created.
    /// </summary>
    /// <remarks>
    /// Should always be stored as UTC.
    /// </remarks>
    public required DateTime CreatedAt { get; init; }

    #endregion

    #region Metadata Properties

    /// <summary>
    /// Gets a key representing the context or purpose of the contact. Can assist the application in
    /// categorizing contacts for caching, retrieval, or business logic.
    /// </summary>
    /// <example>
    /// "auth-user" or "sales-order".
    /// </example>
    public required string ContextKey { get; init; }

    /// <summary>
    /// Gets a globally unique identifier linking the contact to a related entity in another domain.
    /// </summary>
    public required Guid RelatedEntityId { get; init; }

    /// <summary>
    /// Gets the IETF BCP 47 locale tag (e.g., "en-US", "fr-CA"). Foreign key to the Locale
    /// reference entity. Synced from the owning user's locale preference. Used by the Comms
    /// service for notification language.
    /// </summary>
    /// <example>
    /// "en-US", "fr-CA", "ja-JP".
    /// </example>
    public required string IETFBCP47Tag { get; init; }

    /// <summary>
    /// Gets the IANA timezone identifier (e.g., "America/New_York"). Foreign key to the Timezone
    /// reference entity. Synced from the owning user's timezone preference. Used by the Comms
    /// service for formatting dates/times in notifications.
    /// </summary>
    /// <example>
    /// "America/New_York", "Europe/London", "Asia/Tokyo".
    /// </example>
    public required string IANAIdentifier { get; init; }

    #endregion

    #region Nested Properties (Value Objects)

    /// <summary>
    /// Gets the collection of contact methods, including email addresses and phone numbers.
    /// </summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public ContactMethods? ContactMethods { get; init; }

    /// <summary>
    /// Gets the personal details associated with the contact.
    /// </summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public Personal? PersonalDetails { get; init; }

    /// <summary>
    /// Gets the professional details associated with the contact.
    /// </summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public Professional? ProfessionalDetails { get; init; }

    #endregion

    #region Foreign Keys

    /// <summary>
    /// Gets foreign key to the <see cref="Location"/> entity associated with the contact.
    /// </summary>
    /// <example>
    /// A1B2C3D4E5F6...
    /// </example>
    public string? LocationHashId { get; init; }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Gets navigation property to the associated <see cref="Location"/> entity for the contact.
    /// </summary>
    public Location? Location { get; init; }

    /// <summary>
    /// Gets navigation property to the <see cref="D2.Geo.Domain.Entities.Locale"/> reference
    /// entity for the contact's locale.
    /// </summary>
    public Locale? Locale { get; init; }

    /// <summary>
    /// Gets navigation property to the <see cref="D2.Geo.Domain.Entities.Timezone"/> reference
    /// entity for the contact's timezone.
    /// </summary>
    public Timezone? Timezone { get; init; }

    #endregion

    #region Functionality

    /// <summary>
    /// Factory method to create a new <see cref="Contact"/> instance.
    /// </summary>
    ///
    /// <param name="contextKey">
    /// The context key representing the purpose of the contact. Required.
    /// </param>
    /// <param name="relatedEntityId">
    /// The globally unique identifier linking the contact to a related entity. Required.
    /// </param>
    /// <param name="contactMethods">
    /// The collection of contact methods, including email addresses and phone numbers. Optional.
    /// </param>
    /// <param name="personalDetails">
    /// The personal details associated with the contact. Optional.
    /// </param>
    /// <param name="professionalDetails">
    /// The professional details associated with the contact. Optional.
    /// </param>
    /// <param name="locationHashId">
    /// Foreign key to the associated <see cref="Location"/> entity. Optional.
    /// </param>
    /// <param name="ietfBcp47Tag">
    /// IETF BCP 47 locale tag (e.g., "en-US", "fr-CA"). Defaults to "en-US".
    /// </param>
    /// <param name="ianaIdentifier">
    /// IANA timezone identifier (e.g., "America/New_York"). Defaults to "America/New_York".
    /// </param>
    ///
    /// <returns>
    /// A new validated <see cref="Contact"/> instance.
    /// </returns>
    ///
    /// <exception cref="GeoValidationException">
    /// Thrown if the <paramref name="contextKey"/> is null or empty, or if the
    /// <paramref name="relatedEntityId"/> is an empty GUID.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if any of the phone numbers are null, empty, less than 7 or greater than 15 digits
    /// or in an invalid format OR if any of the email addresses are null, empty, whitespace, or
    /// not in a valid format.
    /// </exception>
    /// <exception cref="GeoValidationException">
    /// Thrown if <see cref="Personal.FirstName"/> is null, empty, or whitespace
    /// (if <see cref="Personal"/> is not null).
    /// </exception>
    /// <exception cref="GeoValidationException">
    /// Thrown if <see cref="Professional.CompanyName"/> is null, empty, or whitespace (if
    /// <see cref="Professional"/> is not null).
    /// </exception>
    public static Contact Create(
        string contextKey,
        Guid relatedEntityId,
        ContactMethods? contactMethods = null,
        Personal? personalDetails = null,
        Professional? professionalDetails = null,
        string? locationHashId = null,
        string? ietfBcp47Tag = null,
        string? ianaIdentifier = null)
    {
        if (contextKey.Falsey())
        {
            throw new GeoValidationException(
                nameof(Contact),
                nameof(ContextKey),
                contextKey,
                "is required.");
        }

        if (relatedEntityId.Falsey())
        {
            throw new GeoValidationException(
                nameof(Contact),
                nameof(RelatedEntityId),
                relatedEntityId,
                "is required.");
        }

        return new Contact
        {
            Id = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ContextKey = contextKey,
            RelatedEntityId = relatedEntityId,
            ContactMethods = contactMethods is not null
                ? ContactMethods.Create(contactMethods)
                : null,
            PersonalDetails = personalDetails is not null
                ? Personal.Create(personalDetails)
                : null,
            ProfessionalDetails = professionalDetails is not null
                ? Professional.Create(professionalDetails)
                : null,

            // Domain stays dependency-free — runtime default is SupportedLocales.BASE at app layer.
            // EF Core ContactConfig also has HasDefaultValue("en-US") for DB-level default.
            // Falsey() catches null, empty, and whitespace — all would violate the locales FK.
            IETFBCP47Tag = ietfBcp47Tag.Falsey() ? "en-US" : ietfBcp47Tag!,

            // Same pattern as locale — Falsey() catches null/empty/whitespace, defaults to
            // "America/New_York". EF Core ContactConfig also has HasDefaultValue("America/New_York").
            IANAIdentifier = ianaIdentifier.Falsey() ? "America/New_York" : ianaIdentifier!,
            LocationHashId = locationHashId,
        };
    }

    /// <summary>
    /// Factory method to create a new <see cref="Contact"/> instance with validation.
    /// </summary>
    ///
    /// <param name="contact">
    /// The contact to validate and create a new instance from.
    /// </param>
    ///
    /// <returns>
    /// A new validated <see cref="Contact"/> instance.
    /// </returns>
    ///
    /// <exception cref="GeoValidationException">
    /// Thrown if the <see cref="ContextKey"/> is null or empty, or if the
    /// <see cref="RelatedEntityId"/> is an empty GUID.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if any of the phone numbers are null, empty, less than 7 or greater than 15 digits
    /// or in an invalid format OR if any of the email addresses are null, empty, whitespace, or
    /// not in a valid format.
    /// </exception>
    /// <exception cref="GeoValidationException">
    /// Thrown if <see cref="Personal.FirstName"/> is null, empty, or whitespace
    /// (if <see cref="Personal"/> is not null).
    /// </exception>
    /// <exception cref="GeoValidationException">
    /// Thrown if <see cref="Professional.CompanyName"/> is null, empty, or whitespace (if
    /// <see cref="Professional"/> is not null).
    /// </exception>
    public static Contact Create(Contact contact)
        => Create(
            contact.ContextKey,
            contact.RelatedEntityId,
            contact.ContactMethods,
            contact.PersonalDetails,
            contact.ProfessionalDetails,
            contact.LocationHashId,
            contact.IETFBCP47Tag,
            contact.IANAIdentifier);

    #endregion
}
