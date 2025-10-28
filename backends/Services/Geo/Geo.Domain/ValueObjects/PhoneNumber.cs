using System.Collections.Immutable;
using D2.Contracts.Utilities;

namespace Geo.Domain.ValueObjects;

/// <summary>
/// Represents a phone number with associated labels.
/// </summary>
/// <remarks>
/// Is a value object of the Geography "Geo" Domain, used by the <see cref="ContactMethods"/>
/// value object.
/// </remarks>
public record PhoneNumber
{
    /// <summary>
    /// The phone number value (in E.164 format - digits only).
    /// </summary>
    /// <example>
    /// 13213214321
    /// </example>
    public required string Value { get; init; }

    /// <summary>
    /// The labels associated with the phone number.
    /// </summary>
    /// <remarks>
    /// Used to help differentiate multiple phone numbers used by a single
    /// <see cref="ContactMethods"/> object.
    /// </remarks>
    /// <example>
    /// ["mobile", "work"]
    /// </example>
    public required ImmutableHashSet<string> Labels { get; init; }

    #region Functionality

    /// <summary>
    /// Factory method to create a new <see cref="PhoneNumber"/> instance with validation.
    /// </summary>
    ///
    /// <param name="value">
    /// The phone number value. Required.
    /// </param>
    /// <param name="labels">
    /// The labels associated with the phone number. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new validated <see cref="PhoneNumber"/> instance.
    /// </returns>
    ///
    /// <exception cref="ArgumentException">
    /// Thrown if the phone number is null, empty, less than 7 or greater than 15 digits or in
    /// an invalid format.
    /// </exception>
    public static PhoneNumber Create(string value, IEnumerable<string>? labels = null)
    {
        return new PhoneNumber
        {
            Value = value.CleanAndValidatePhoneNumber(),
            Labels = labels?.Clean(x => x.CleanStr())?.ToImmutableHashSet() ?? []
        };
    }

    /// <summary>
    /// Factory method to create a new <see cref="PhoneNumber"/> instance with validation.
    /// </summary>
    ///
    /// <param name="phoneNumber">
    /// The phone number to validate and create a new instance from.
    /// </param>
    ///
    /// <returns>
    /// A new validated <see cref="PhoneNumber"/> instance.
    /// </returns>
    ///
    /// <exception cref="ArgumentException">
    /// Thrown if the phone number is null, empty, less than 7 or greater than 15 digits or in
    /// an invalid format.
    /// </exception>
    public static PhoneNumber Create(PhoneNumber phoneNumber)
        => Create(phoneNumber.Value, phoneNumber.Labels);


    /// <summary>
    /// Factory method to create many <see cref="PhoneNumber"/> instances with validation.
    /// </summary>
    ///
    /// <param name="phoneNumbers">
    /// An enumerable collection of tuples containing phone number values and their associated
    /// labels.
    /// </param>
    ///
    /// <returns>
    /// An immutable list of new, validated <see cref="PhoneNumber"/> instances.
    /// </returns>
    ///
    /// <exception cref="ArgumentException">
    /// Thrown if any of the phone numbers are null, empty, less than 7 or greater than 15 digits
    /// or in an invalid format.
    /// </exception>
    public static ImmutableList<PhoneNumber> CreateMany(
        IEnumerable<(string value, IEnumerable<string>? labels)>? phoneNumbers)
    {
        return phoneNumbers?
            .Select(pn => Create(pn.value, pn.labels))
            .ToImmutableList() ?? [];
    }

    /// <summary>
    /// Factory method to create many <see cref="PhoneNumber"/> instances with validation.
    /// </summary>
    ///
    /// <param name="phoneNumbers">
    /// An enumerable collection of <see cref="PhoneNumber"/> instances to validate.
    /// </param>
    ///
    /// <returns>
    /// An immutable list of new, validated <see cref="PhoneNumber"/> instances.
    /// </returns>
    ///
    /// <exception cref="ArgumentException">
    /// Thrown if any of the phone numbers are null, empty, less than 7 or greater than 15 digits
    /// or in an invalid format.
    /// </exception>
    public static ImmutableList<PhoneNumber> CreateMany(
        IEnumerable<PhoneNumber>? phoneNumbers)
    {
        return phoneNumbers?
            .Select(Create)
            .ToImmutableList() ?? [];
    }

    #endregion

    #region Equality Overrides

    /// <inheritdoc/>
    /// <remarks>
    /// Because <see cref="PhoneNumber"/> contains an <see cref="ImmutableHashSet{T}"/> for
    /// <see cref="Labels"/>, this override is necessary to ensure proper value equality comparison.
    /// </remarks>
    public virtual bool Equals(PhoneNumber? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Value == other.Value
               && Labels.SetEquals(other.Labels);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Because <see cref="PhoneNumber"/> contains an <see cref="ImmutableHashSet{T}"/> for
    /// <see cref="Labels"/>, this override is necessary to ensure proper value equality comparison.
    /// </remarks>
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Value);

        foreach (var label in Labels.OrderBy(l => l))
            hashCode.Add(label);

        return hashCode.ToHashCode();
    }

    #endregion
}
