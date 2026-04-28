// -----------------------------------------------------------------------
// <copyright file="Professional.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Domain.ValueObjects;

using D2.Geo.Domain.Entities;
using D2.Geo.Domain.Exceptions;
using D2.Shared.Utilities.Attributes;
using D2.Shared.Utilities.Enums;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Represents a business or professional entity.
/// </summary>
/// <remarks>
/// Is a value object of the Geography "Geo" Domain, used by the <see cref="Contact"/> entity.
/// </remarks>
[RedactData(Reason = RedactReason.PersonalInformation)]
public record Professional
{
    /// <summary>
    /// Gets the company's name.
    /// </summary>
    /// <example>
    /// ACME, LLC.
    /// </example>
    public required string CompanyName { get; init; }

    /// <summary>
    /// Gets the related person's job title or position at the company, if applicable.
    /// </summary>
    /// <example>
    /// Software Engineer.
    /// </example>
    public string? JobTitle { get; init; }

    /// <summary>
    /// Gets a department within the company.
    /// </summary>
    /// <example>
    /// Research and Development.
    /// </example>
    public string? Department { get; init; }

    /// <summary>
    /// Gets the company's website URL.
    /// </summary>
    /// <example>
    /// https://www.acme.com.
    /// </example>
    public Uri? CompanyWebsite { get; init; }

    #region Functionality

    /// <summary>
    /// Factory method to create a new <see cref="Professional"/> instance with validation.
    /// </summary>
    ///
    /// <param name="companyName">
    /// The company's name. Required.
    /// </param>
    /// <param name="jobTitle">
    /// The related person's job title or position at the company. Optional.
    /// </param>
    /// <param name="department">
    /// A department within the company. Optional.
    /// </param>
    /// <param name="companyWebsite">
    /// The company's website URL. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new validated <see cref="Professional"/> instance.
    /// </returns>
    ///
    /// <exception cref="GeoValidationException">
    /// Thrown if <paramref name="companyName"/> is null, empty, or whitespace.
    /// </exception>
    public static Professional Create(
        string companyName,
        string? jobTitle = null,
        string? department = null,
        Uri? companyWebsite = null)
    {
        if (companyName.Falsey())
        {
            throw new GeoValidationException(
                nameof(Professional),
                nameof(CompanyName),
                companyName,
                "is required.");
        }

        return new Professional
        {
            CompanyName = companyName.CleanDisplayStr()!,
            JobTitle = jobTitle?.CleanDisplayStr(),
            Department = department?.CleanDisplayStr(),
            CompanyWebsite = companyWebsite,
        };
    }

    /// <summary>
    /// Factory method to create a new <see cref="Professional"/> instance with validation.
    /// </summary>
    ///
    /// <param name="professional">
    /// The professional to validate and create a new instance from.
    /// </param>
    ///
    /// <returns>
    /// A new validated <see cref="Professional"/> instance.
    /// </returns>
    ///
    /// <exception cref="GeoValidationException">
    /// Thrown if <see cref="CompanyName"/> is null, empty, or whitespace.
    /// </exception>
    public static Professional Create(Professional professional)
        => Create(
            professional.CompanyName,
            professional.JobTitle,
            professional.Department,
            professional.CompanyWebsite);

    #endregion
}
