using System.Collections.Immutable;
using D2.Contracts.Utilities;

namespace Geo.Domain.ValueObjects;

/// <summary>
/// Represents an email address with associated labels.
/// </summary>
/// <remarks>
/// Is a value object of the Geography "Geo" Domain, used by the <see cref="ContactMethods"/>
/// value object.
/// </remarks>
public record EmailAddress
{
    /// <summary>
    /// The email address value.
    /// </summary>
    /// <example>
    /// some.guy@gmail.com
    /// </example>
    public required string Value { get; init; }

    /// <summary>
    /// The labels associated with the email address.
    /// </summary>
    /// <remarks>
    /// Used to help differentiate multiple email addresses used by a single
    /// <see cref="ContactMethods"/> object.
    /// </remarks>
    /// <example>
    /// ["work", "personal"]
    /// </example>
    public required ImmutableHashSet<string> Labels { get; init; }

    #region Functionality

    /// <summary>
    /// Factory method to create a new <see cref="EmailAddress"/> instance with validation.
    /// </summary>
    ///
    /// <param name="value">
    /// The email address value. Required.
    /// </param>
    /// <param name="labels">
    /// The labels associated with the email address. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new validated <see cref="EmailAddress"/> instance.
    /// </returns>
    ///
    /// <exception cref="ArgumentException">
    /// Thrown if the email is null, empty, whitespace, or not in a valid format.
    /// </exception>
    public static EmailAddress Create(string value, IEnumerable<string>? labels = null)
    {
        return new EmailAddress
        {
            Value = value.CleanAndValidateEmail(),
            Labels = labels?.Clean(x => x.CleanStr())?.ToImmutableHashSet() ?? []
        };
    }

    /// <summary>
    /// Factory method to create a new <see cref="EmailAddress"/> instance with validation.
    /// </summary>
    ///
    /// <param name="email">
    /// The email address to validate and create a new instance from.
    /// </param>
    ///
    /// <returns>
    /// A new validated <see cref="EmailAddress"/> instance.
    /// </returns>
    ///
    /// <exception cref="ArgumentException">
    /// Thrown if the email is null, empty, whitespace, or not in a valid format.
    /// </exception>
    public static EmailAddress Create(EmailAddress email)
        => Create(email.Value, email.Labels);

    /// <summary>
    /// Factory method to create many <see cref="EmailAddress"/> instances with validation.
    /// </summary>
    ///
    /// <param name="emails">
    /// An enumerable collection of tuples containing email values and their associated labels.
    /// </param>
    ///
    /// <returns>
    /// An immutable list of new validated <see cref="EmailAddress"/> instances.
    /// </returns>
    ///
    /// <exception cref="ArgumentException">
    /// Thrown if any of the emails are null, empty, whitespace, or not in a valid format.
    /// </exception>
    public static ImmutableList<EmailAddress> CreateMany(
        IEnumerable<(string value, IEnumerable<string>? labels)>? emails)
    {
        return emails?
            .Select(em => Create(em.value, em.labels))
            .ToImmutableList() ?? [];
    }

    /// <summary>
    /// Factory method to create many <see cref="EmailAddress"/> instances with validation.
    /// </summary>
    ///
    /// <param name="emails">
    /// An enumerable collection of <see cref="EmailAddress"/> instances to validate.
    /// </param>
    ///
    /// <returns>
    /// An immutable list of new validated <see cref="EmailAddress"/> instances.
    /// </returns>
    ///
    /// <exception cref="ArgumentException">
    /// Thrown if any of the emails are null, empty, whitespace, or not in a valid format.
    /// </exception>
    public static ImmutableList<EmailAddress> CreateMany(
        IEnumerable<EmailAddress>? emails)
    {
        return emails?
            .Select(Create)
            .ToImmutableList() ?? [];
    }

    #endregion

    #region Equality Overrides

    /// <inheritdoc/>
    /// <remarks>
    /// Because <see cref="EmailAddress"/> contains an <see cref="ImmutableHashSet{T}"/> for
    /// <see cref="Labels"/>, this override is necessary to ensure proper value equality comparison.
    /// </remarks>
    public virtual bool Equals(EmailAddress? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Value == other.Value
               && Labels.SetEquals(other.Labels);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Because <see cref="EmailAddress"/> contains an <see cref="ImmutableHashSet{T}"/> for
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
