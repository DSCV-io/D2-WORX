// -----------------------------------------------------------------------
// <copyright file="Personal.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Contacts.ValueObjects;

using System.Security.Cryptography;
using System.Text;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;
using DcsvIo.D2.Utilities.Extensions;
using DcsvIo.D2.Validation.Abstractions;

/// <summary>
/// Immutable personal-name value object: a required first name plus optional
/// middle, last, and preferred (display) names. Each supplied name is cleaned
/// (trimmed, internal whitespace collapsed) before storage. A stable
/// correlation <see cref="HashId"/> is derived from the first, middle, and last
/// names — the preferred name is deliberately excluded so a display-name change
/// does not alter the identity digest.
/// </summary>
/// <remarks>
/// <para>
/// <b>Self-redacting PII.</b> <see cref="FirstName"/>, <see cref="MiddleName"/>,
/// <see cref="LastName"/>, and <see cref="PreferredName"/> are marked
/// <c>[RedactData(PersonalInformation)]</c> and are masked automatically by the
/// Serilog destructuring policy — personal names are individually identifying.
/// <see cref="HashId"/> is left visible because it is a one-way SHA-256 digest
/// of the normalized name fields: opaque, non-reversible, and safe for
/// correlation in logs and traces without leaking the underlying names.
/// </para>
/// <para>
/// <b>Hash normalization.</b> The hash form upper-cases, NFD-decomposes, and
/// keeps only Unicode Letter / Decimal-digit code points plus ASCII space (via
/// <see cref="StringExtensions.NormalizeForHash(string?)"/>), so case-,
/// diacritic-, and whitespace-equivalent inputs (<c>"José"</c> / <c>"JOSÉ"</c> /
/// <c>"Jose"</c>) collapse to a byte-identical <see cref="HashId"/>.
/// </para>
/// </remarks>
public sealed record Personal
{
    /// <summary>
    /// Gets the required first (given) name (post-clean, case preserved).
    /// </summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public required string FirstName { get; init; }

    /// <summary>Gets the optional middle name (post-clean).</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string? MiddleName { get; init; }

    /// <summary>Gets the optional last (family) name (post-clean).</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string? LastName { get; init; }

    /// <summary>Gets the optional preferred (display) name (post-clean).</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string? PreferredName { get; init; }

    /// <summary>
    /// Gets the stable hash identifier:
    /// <c>"v1." + SHA-256(NormalizeForHash(FirstName) | NormalizeForHash(MiddleName)
    /// | NormalizeForHash(LastName))</c> as lowercase hex. The preferred name does
    /// NOT participate — a display-name change leaves the identity digest stable.
    /// Missing slots contribute <c>""</c> (deterministic positional shape).
    /// Emitted unredacted in logs — it is a one-way SHA-256 digest (opaque,
    /// non-reversible) and is safe for correlation without leaking the names.
    /// </summary>
    public required string HashId { get; init; }

    /// <summary>
    /// Creates a <see cref="Personal"/> from a required first name and optional
    /// middle / last / preferred names. Each value is cleaned before storage;
    /// the first name must be non-empty after cleaning.
    /// </summary>
    /// <param name="firstName">Required first name (post-clean must be non-empty).</param>
    /// <param name="middleName">Optional middle name.</param>
    /// <param name="lastName">Optional last name.</param>
    /// <param name="preferredName">Optional preferred (display) name.</param>
    /// <returns>
    /// <c>Ok</c> when the first name is non-empty after cleaning and every
    /// supplied name is within its length bound;
    /// <see cref="D2Result{TData}.ValidationFailed"/> otherwise.
    /// </returns>
    public static D2Result<Personal> Create(
        string? firstName,
        string? middleName = null,
        string? lastName = null,
        string? preferredName = null)
    {
        var cleanedFirst = firstName.CleanStr();
        if (cleanedFirst.Falsey())
        {
            return D2Result<Personal>.ValidationFailed(
                messages: [TK.Contacts.Validation.FIRST_NAME_REQUIRED]);
        }

        if (cleanedFirst!.Length > FieldConstraints.FIRST_NAME_MAX)
        {
            return D2Result<Personal>.ValidationFailed(
                messages: [TK.Contacts.Validation.FIRST_NAME_TOO_LONG]);
        }

        var cleanedMiddle = middleName.CleanStr();
        if (cleanedMiddle.Truthy() && cleanedMiddle!.Length > FieldConstraints.MIDDLE_NAME_MAX)
        {
            return D2Result<Personal>.ValidationFailed(
                messages: [TK.Contacts.Validation.MIDDLE_NAME_TOO_LONG]);
        }

        var cleanedLast = lastName.CleanStr();
        if (cleanedLast.Truthy() && cleanedLast!.Length > FieldConstraints.LAST_NAME_MAX)
        {
            return D2Result<Personal>.ValidationFailed(
                messages: [TK.Contacts.Validation.LAST_NAME_TOO_LONG]);
        }

        var cleanedPreferred = preferredName.CleanStr();
        if (cleanedPreferred.Truthy()
            && cleanedPreferred!.Length > FieldConstraints.PREFERRED_NAME_MAX)
        {
            return D2Result<Personal>.ValidationFailed(
                messages: [TK.Contacts.Validation.PREFERRED_NAME_TOO_LONG]);
        }

        // HashId excludes PreferredName — a display-name change must not alter
        // the identity digest. Cleaned values feed NormalizeForHash (stage-2-only;
        // it does not trim, so the stage-1 CleanStr above is required).
        var hashInput =
            cleanedFirst.NormalizeForHash() + "|" +
            cleanedMiddle.NormalizeForHash() + "|" +
            cleanedLast.NormalizeForHash();

        // BCL static one-shot per §15.8 — no IDisposable instance to manage.
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        var hashId = "v1." + Convert.ToHexStringLower(hashBytes);

        return D2Result<Personal>.Ok(new Personal
        {
            FirstName = cleanedFirst,
            MiddleName = cleanedMiddle,
            LastName = cleanedLast,
            PreferredName = cleanedPreferred,
            HashId = hashId,
        });
    }
}
