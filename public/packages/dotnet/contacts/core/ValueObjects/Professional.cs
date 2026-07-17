// -----------------------------------------------------------------------
// <copyright file="Professional.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Contacts.ValueObjects;

using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;
using DcsvIo.D2.Utilities.Extensions;
using DcsvIo.D2.Validation.Abstractions;

/// <summary>
/// Immutable professional-details value object: a required company name plus
/// optional job title, department, and company website. The website is accepted
/// as raw text and stored as an absolute <c>http</c> / <c>https</c>
/// <see cref="Uri"/> (or null when omitted). No correlation hash is derived —
/// professional details are not identity-defining.
/// </summary>
/// <remarks>
/// <b>Self-redacting PII.</b> <see cref="CompanyName"/>, <see cref="JobTitle"/>,
/// and <see cref="Department"/> are marked <c>[RedactData(PersonalInformation)]</c>
/// — tied to a person these are contextually identifying. <see cref="CompanyWebsite"/>
/// is left visible — a public website URL is not individually identifying.
/// </remarks>
public sealed record Professional
{
    /// <summary>Gets the required company / organization name (post-clean).</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public required string CompanyName { get; init; }

    /// <summary>Gets the optional job title (post-clean).</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string? JobTitle { get; init; }

    /// <summary>Gets the optional department / organizational-unit name (post-clean).</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string? Department { get; init; }

    /// <summary>
    /// Gets the optional company website as an absolute <c>http</c> / <c>https</c>
    /// URI. Emitted unredacted in logs — a public website URL is not individually
    /// identifying.
    /// </summary>
    public Uri? CompanyWebsite { get; init; }

    /// <summary>
    /// Creates a <see cref="Professional"/> from a required company name and
    /// optional job title, department, and raw website text. The website is
    /// validated as an absolute <c>http</c> / <c>https</c> URL before storage.
    /// </summary>
    /// <param name="companyName">Required company name (post-clean must be non-empty).</param>
    /// <param name="jobTitle">Optional job title.</param>
    /// <param name="department">Optional department / organizational-unit name.</param>
    /// <param name="companyWebsite">
    /// Optional raw website text; when supplied, must be an absolute
    /// <c>http</c> / <c>https</c> URL within the raw-length bound.
    /// </param>
    /// <returns>
    /// <c>Ok</c> on success;
    /// <see cref="D2Result{TData}.ValidationFailed"/> for: a missing company name,
    /// an over-length field, or an invalid website URL.
    /// </returns>
    public static D2Result<Professional> Create(
        string? companyName,
        string? jobTitle = null,
        string? department = null,
        string? companyWebsite = null)
    {
        var cleanedCompany = companyName.CleanStr();
        if (cleanedCompany.Falsey())
        {
            return D2Result<Professional>.ValidationFailed(
                messages: [TK.Contacts.Validation.COMPANY_NAME_REQUIRED]);
        }

        if (cleanedCompany!.Length > FieldConstraints.COMPANY_NAME_MAX)
        {
            return D2Result<Professional>.ValidationFailed(
                messages: [TK.Contacts.Validation.COMPANY_NAME_TOO_LONG]);
        }

        var cleanedJob = jobTitle.CleanStr();
        if (cleanedJob.Truthy() && cleanedJob!.Length > FieldConstraints.JOB_TITLE_MAX)
        {
            return D2Result<Professional>.ValidationFailed(
                messages: [TK.Contacts.Validation.JOB_TITLE_TOO_LONG]);
        }

        var cleanedDept = department.CleanStr();
        if (cleanedDept.Truthy() && cleanedDept!.Length > FieldConstraints.DEPARTMENT_MAX)
        {
            return D2Result<Professional>.ValidationFailed(
                messages: [TK.Contacts.Validation.DEPARTMENT_TOO_LONG]);
        }

        // Website: trim only (ToNullIfEmpty) — do NOT collapse internal whitespace,
        // which would mangle a query string. Raw-length guard runs before parsing.
        Uri? website = null;
        var rawWebsite = companyWebsite.ToNullIfEmpty();
        if (rawWebsite is not null)
        {
            if (rawWebsite.Length > FieldConstraints.COMPANY_WEBSITE_MAX)
            {
                return D2Result<Professional>.ValidationFailed(
                    messages: [TK.Contacts.Validation.WEBSITE_INVALID]);
            }

            if (!Uri.TryCreate(rawWebsite, UriKind.Absolute, out var parsed)
                || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                return D2Result<Professional>.ValidationFailed(
                    messages: [TK.Contacts.Validation.WEBSITE_INVALID]);
            }

            website = parsed;
        }

        return D2Result<Professional>.Ok(new Professional
        {
            CompanyName = cleanedCompany,
            JobTitle = cleanedJob,
            Department = cleanedDept,
            CompanyWebsite = website,
        });
    }
}
