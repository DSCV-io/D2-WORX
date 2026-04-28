// -----------------------------------------------------------------------
// <copyright file="ContactMethods.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Domain.ValueObjects;

using System.Collections.Immutable;
using D2.Geo.Domain.Entities;
using D2.Shared.Utilities.Attributes;
using D2.Shared.Utilities.Enums;

/// <summary>
/// Represents a collection of contact methods, including email addresses and phone numbers.
/// </summary>
/// <remarks>
/// Is a value object of the Geography "Geo" Domain, used by the <see cref="Contact"/> entity.
/// </remarks>
[RedactData(Reason = RedactReason.PersonalInformation)]
public record ContactMethods
{
    /// <summary>
    /// Gets the email addresses and their associated labels.
    /// </summary>
    public required ImmutableList<EmailAddress> Emails { get; init; }

    /// <summary>
    /// Gets the phone numbers and their associated labels.
    /// </summary>
    public required ImmutableList<PhoneNumber> PhoneNumbers { get; init; }

    #region Functionality

    /// <summary>
    /// Gets the primary email address, which is the first email in the list or null if none exists.
    /// </summary>
    public EmailAddress? PrimaryEmail => Emails.FirstOrDefault();

    /// <summary>
    /// Gets the primary phone number, which is the first phone number in the list or null if none
    /// exists.
    /// </summary>
    public PhoneNumber? PrimaryPhoneNumber => PhoneNumbers.FirstOrDefault();

    /// <summary>
    /// Factory method to create a new <see cref="ContactMethods"/> instance with validation.
    /// </summary>
    ///
    /// <param name="emails">
    /// The email addresses and their associated labels.
    /// </param>
    /// <param name="phoneNumbers">
    /// The phone numbers and their associated labels.
    /// </param>
    ///
    /// <returns>
    /// A new <see cref="ContactMethods"/> instance.
    /// </returns>
    ///
    /// <exception cref="ArgumentException">
    /// Thrown if any of the phone numbers are null, empty, less than 7 or greater than 15 digits
    /// or in an invalid format OR if any of the email addresses are null, empty, whitespace, or
    /// not in a valid format.
    /// </exception>
    ///
    /// <seealso cref="EmailAddress.CreateMany(IEnumerable{EmailAddress})"/>
    /// <seealso cref="PhoneNumber.CreateMany(IEnumerable{PhoneNumber})"/>
    public static ContactMethods Create(
        ImmutableList<EmailAddress>? emails = null,
        ImmutableList<PhoneNumber>? phoneNumbers = null)
    {
        return new ContactMethods
        {
            Emails = EmailAddress.CreateMany(emails),
            PhoneNumbers = PhoneNumber.CreateMany(phoneNumbers),
        };
    }

    /// <summary>
    /// Factory method to create a new <see cref="ContactMethods"/> instance with validation.
    /// </summary>
    ///
    /// <param name="contactMethods">
    /// The contact methods to validate and create a new instance from.
    /// </param>
    ///
    /// <returns>
    /// A new <see cref="ContactMethods"/> instance.
    /// </returns>
    ///
    /// <exception cref="ArgumentException">
    /// Thrown if any of the phone numbers are null, empty, less than 7 or greater than 15 digits
    /// or in an invalid format OR if any of the email addresses are null, empty, whitespace, or
    /// not in a valid format.
    /// </exception>
    ///
    /// <seealso cref="EmailAddress.CreateMany(IEnumerable{EmailAddress})"/>
    /// <seealso cref="PhoneNumber.CreateMany(IEnumerable{PhoneNumber})"/>
    public static ContactMethods Create(
        ContactMethods contactMethods)
    {
        return new ContactMethods
        {
            Emails = EmailAddress.CreateMany(contactMethods.Emails),
            PhoneNumbers = PhoneNumber.CreateMany(contactMethods.PhoneNumbers),
        };
    }

    #endregion

    #region Equality Overrides

    /// <inheritdoc/>
    /// <remarks>
    /// Because <see cref="ContactMethods"/> contains <see cref="ImmutableList{T}"/> properties for
    /// <see cref="Emails"/> and <see cref="PhoneNumbers"/>, this override is necessary to ensure
    /// proper value equality comparison.
    /// </remarks>
    public virtual bool Equals(ContactMethods? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Emails.SequenceEqual(other.Emails)
               && PhoneNumbers.SequenceEqual(other.PhoneNumbers);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Because <see cref="ContactMethods"/> contains <see cref="ImmutableList{T}"/> properties for
    /// <see cref="Emails"/> and <see cref="PhoneNumbers"/>, this override is necessary to ensure
    /// proper value equality comparison.
    /// </remarks>
    public override int GetHashCode()
    {
        var hashCode = default(HashCode);

        foreach (var email in Emails)
        {
            hashCode.Add(email);
        }

        foreach (var phone in PhoneNumbers)
        {
            hashCode.Add(phone);
        }

        return hashCode.ToHashCode();
    }

    #endregion
}
