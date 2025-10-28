using System.Collections.Immutable;
using D2.Contracts.Utilities;
using Geo.Domain.Entities;
using Geo.Domain.Enums;
using Geo.Domain.Exceptions;

namespace Geo.Domain.ValueObjects;

/// <summary>
/// Represents personal information about an individual.
/// </summary>
/// <remarks>
/// Is a value object of the Geography "Geo" Domain, used by the <see cref="Contact"/> entity.
/// </remarks>
public record Personal
{
    #region Name

    /// <summary>
    /// The person's title.
    /// </summary>
    /// <example>
    /// "Mr.", "Ms.", "Dr.", "Prof."
    /// </example>
    public NameTitle? Title { get; init; }

    /// <summary>
    /// The person's first name, given name or only name.
    /// </summary>
    /// <example>
    /// John
    /// </example>
    public required string FirstName { get; init; }

    /// <summary>
    /// The person's preferred name or nickname, if different from their first name.
    /// </summary>
    /// <example>
    /// Johnny
    /// </example>
    public string? PreferredName { get; init; }

    /// <summary>
    /// The person's middle name(s) or initial(s).
    /// </summary>
    /// <example>
    /// Michael
    /// </example>
    public string? MiddleName { get; init; }

    /// <summary>
    /// The person's last name or family name (surname).
    /// </summary>
    /// <example>
    /// Doe
    /// </example>
    public string? LastName { get; init; }

    /// <summary>
    /// The person's generational suffix, if any.
    /// </summary>
    /// <example>
    /// "Jr.", "Sr.", "III"
    /// </example>
    public GenerationalSuffix? GenerationalSuffix { get; init; }

    /// <summary>
    /// The person's professional credentials.
    /// </summary>
    /// <example>
    /// ["PhD", "CISSP", "Ret.", "OBE"]
    /// </example>
    public ImmutableList<string> ProfessionalCredentials { get; init; } = [];

    #endregion

    #region Additional Info

    /// <summary>
    /// The person's date of birth.
    /// </summary>
    /// <example>
    /// 1990-05-15
    /// </example>
    public DateOnly? DateOfBirth { get; init; }

    /// <summary>
    /// The person's biological sex.
    /// </summary>
    /// <example>
    /// Male
    /// </example>
    public BiologicalSex? BiologicalSex { get; init; }

    #endregion

    #region Functionality

    /// <summary>
    /// Factory method to create a new <see cref="Personal"/> instance with validation.
    /// </summary>
    ///
    /// <param name="firstName">
    /// The person's first name, given name or only name. Required.
    /// </param>
    /// <param name="title">
    /// The person's title. Optional.
    /// </param>
    /// <param name="preferredName">
    /// The person's preferred name or nickname, if different from their first name. Optional.
    /// </param>
    /// <param name="middleName">
    /// The person's middle name(s) or initial(s). Optional.
    /// </param>
    /// <param name="lastName">
    /// The person's last name or family name (surname). Optional.
    /// </param>
    /// <param name="generationalSuffix">
    /// The person's generational suffix, if any. Optional.
    /// </param>
    /// <param name="professionalCredentials">
    /// The person's professional credentials. Optional.
    /// </param>
    /// <param name="dateOfBirth">
    /// The person's date of birth. Optional.
    /// </param>
    /// <param name="biologicalSex">
    /// The person's biological sex. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new validated <see cref="Personal"/> instance.
    /// </returns>
    ///
    /// <exception cref="GeoValidationException">
    /// Thrown if <paramref name="firstName"/> is null, empty, or whitespace.
    /// </exception>
    public static Personal Create(
        string firstName,
        NameTitle? title = null,
        string? preferredName = null,
        string? middleName = null,
        string? lastName = null,
        GenerationalSuffix? generationalSuffix = null,
        IEnumerable<string>? professionalCredentials = null,
        DateOnly? dateOfBirth = null,
        BiologicalSex? biologicalSex = null)
    {
        if (firstName.Falsey())
            throw new GeoValidationException(
                nameof(Personal),
                nameof(FirstName),
                firstName,
                "is required.");

        return new Personal
        {
            FirstName = firstName.CleanStr()!,
            Title = title,
            PreferredName = preferredName?.CleanStr(),
            MiddleName = middleName?.CleanStr(),
            LastName = lastName?.CleanStr(),
            GenerationalSuffix = generationalSuffix,
            ProfessionalCredentials = professionalCredentials?
                .Clean(x => x.CleanStr())?
                .ToImmutableList() ?? [],
            DateOfBirth = dateOfBirth,
            BiologicalSex = biologicalSex
        };
    }

    /// <summary>
    /// Factory method to create a new <see cref="Personal"/> instance with validation.
    /// </summary>
    ///
    /// <param name="personal">
    /// The personal information to validate and create a new instance from.
    /// </param>
    ///
    /// <returns>
    /// A new validated <see cref="Personal"/> instance.
    /// </returns>
    ///
    /// <exception cref="GeoValidationException">
    /// Thrown if <see cref="FirstName"/> is null, empty, or whitespace.
    /// </exception>
    public static Personal Create(Personal personal)
        => Create(
            personal.FirstName,
            personal.Title,
            personal.PreferredName,
            personal.MiddleName,
            personal.LastName,
            personal.GenerationalSuffix,
            personal.ProfessionalCredentials,
            personal.DateOfBirth,
            personal.BiologicalSex);

    #endregion

    #region Equality Overrides

    /// <inheritdoc/>
    /// <remarks>
    /// Because <see cref="Personal"/> contains an <see cref="ImmutableList{T}"/> for
    /// <see cref="ProfessionalCredentials"/>, this override is necessary to ensure proper
    /// value equality comparison.
    /// </remarks>
    public virtual bool Equals(Personal? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return FirstName == other.FirstName
               && Title == other.Title
               && PreferredName == other.PreferredName
               && MiddleName == other.MiddleName
               && LastName == other.LastName
               && GenerationalSuffix == other.GenerationalSuffix
               && ProfessionalCredentials.SequenceEqual(other.ProfessionalCredentials)
               && DateOfBirth == other.DateOfBirth
               && BiologicalSex == other.BiologicalSex;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Because <see cref="Personal"/> contains an <see cref="ImmutableList{T}"/> for
    /// <see cref="ProfessionalCredentials"/>, this override is necessary to ensure proper
    /// value equality comparison.
    /// </remarks>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FirstName);
        hash.Add(Title);
        hash.Add(PreferredName);
        hash.Add(MiddleName);
        hash.Add(LastName);
        hash.Add(GenerationalSuffix);

        foreach (var credential in ProfessionalCredentials)
            hash.Add(credential);

        hash.Add(DateOfBirth);
        hash.Add(BiologicalSex);

        return hash.ToHashCode();
    }

    #endregion
}
