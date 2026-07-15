// -----------------------------------------------------------------------
// <copyright file="NameAffixes.cs" company="DCSV">
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
/// Immutable name-affix value object: an optional honorific prefix and an
/// optional generational / ordinal suffix, each drawn from a closed taxonomy
/// (<see cref="NamePrefix"/> / <see cref="NameSuffix"/>) with an
/// <c>Other</c> escape hatch backed by a custom free-text value. A custom value
/// is required when (and only when) the corresponding enum is <c>Other</c>. The
/// all-null caller is rejected as a degenerate empty record.
/// </summary>
/// <remarks>
/// <b>Self-redacting PII.</b> <see cref="PrefixCustom"/> and
/// <see cref="SuffixCustom"/> are marked <c>[RedactData(PersonalInformation)]</c>
/// — a free-text custom affix can carry identifying detail. The enum members
/// <see cref="Prefix"/> and <see cref="Suffix"/> are left visible — a closed-list
/// honorific / generational marker is coarse-grained and not individually
/// identifying on its own.
/// </remarks>
public sealed record NameAffixes
{
    /// <summary>
    /// Gets the optional honorific prefix.
    /// Emitted unredacted in logs — a closed-list honorific is coarse-grained.
    /// </summary>
    public NamePrefix? Prefix { get; init; }

    /// <summary>
    /// Gets the optional custom prefix (required iff <see cref="Prefix"/> is
    /// <see cref="NamePrefix.Other"/>; forbidden otherwise).
    /// </summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string? PrefixCustom { get; init; }

    /// <summary>
    /// Gets the optional generational / ordinal suffix.
    /// Emitted unredacted in logs — a closed-list suffix is coarse-grained.
    /// </summary>
    public NameSuffix? Suffix { get; init; }

    /// <summary>
    /// Gets the optional custom suffix (required iff <see cref="Suffix"/> is
    /// <see cref="NameSuffix.Other"/>; forbidden otherwise).
    /// </summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string? SuffixCustom { get; init; }

    /// <summary>
    /// Creates a <see cref="NameAffixes"/> from any combination of prefix /
    /// suffix enum + custom value. Enforces the custom-required-iff-<c>Other</c>
    /// coherence rule on each side and rejects the all-null degenerate record.
    /// </summary>
    /// <param name="prefix">Optional honorific prefix.</param>
    /// <param name="prefixCustom">
    /// Optional custom prefix; required when <paramref name="prefix"/> is
    /// <see cref="NamePrefix.Other"/>, forbidden otherwise.
    /// </param>
    /// <param name="suffix">Optional generational / ordinal suffix.</param>
    /// <param name="suffixCustom">
    /// Optional custom suffix; required when <paramref name="suffix"/> is
    /// <see cref="NameSuffix.Other"/>, forbidden otherwise.
    /// </param>
    /// <returns>
    /// <c>Ok</c> on success;
    /// <see cref="D2Result{TData}.ValidationFailed"/> for: all-null inputs,
    /// a missing custom value when the enum is <c>Other</c>, a custom value
    /// supplied when the enum is not <c>Other</c>, or an over-length custom value.
    /// </returns>
    public static D2Result<NameAffixes> Create(
        NamePrefix? prefix = null,
        string? prefixCustom = null,
        NameSuffix? suffix = null,
        string? suffixCustom = null)
    {
        var cleanedPrefixCustom = prefixCustom.CleanStr();
        var cleanedSuffixCustom = suffixCustom.CleanStr();

        // Degenerate empty record — all four fields null/empty after cleaning.
        // Checked first (AdminLocation parity); coherence checks below reference
        // an Other enum, which is unreachable when prefix/suffix are both null.
        if (prefix is null && suffix is null
            && cleanedPrefixCustom.Falsey() && cleanedSuffixCustom.Falsey())
        {
            return D2Result<NameAffixes>.ValidationFailed(
                messages: [TK.Contacts.Validation.AFFIXES_EMPTY_RECORD]);
        }

        // Prefix custom coherence.
        if (prefix == NamePrefix.Other && cleanedPrefixCustom.Falsey())
        {
            return D2Result<NameAffixes>.ValidationFailed(
                messages: [TK.Contacts.Validation.PREFIX_CUSTOM_REQUIRED]);
        }

        if (prefix != NamePrefix.Other && cleanedPrefixCustom.Truthy())
        {
            return D2Result<NameAffixes>.ValidationFailed(
                messages: [TK.Contacts.Validation.PREFIX_CUSTOM_NOT_ALLOWED]);
        }

        if (cleanedPrefixCustom.Truthy()
            && cleanedPrefixCustom!.Length > FieldConstraints.AFFIX_CUSTOM_MAX)
        {
            return D2Result<NameAffixes>.ValidationFailed(
                messages: [TK.Contacts.Validation.PREFIX_CUSTOM_TOO_LONG]);
        }

        // Suffix custom coherence.
        if (suffix == NameSuffix.Other && cleanedSuffixCustom.Falsey())
        {
            return D2Result<NameAffixes>.ValidationFailed(
                messages: [TK.Contacts.Validation.SUFFIX_CUSTOM_REQUIRED]);
        }

        if (suffix != NameSuffix.Other && cleanedSuffixCustom.Truthy())
        {
            return D2Result<NameAffixes>.ValidationFailed(
                messages: [TK.Contacts.Validation.SUFFIX_CUSTOM_NOT_ALLOWED]);
        }

        if (cleanedSuffixCustom.Truthy()
            && cleanedSuffixCustom!.Length > FieldConstraints.AFFIX_CUSTOM_MAX)
        {
            return D2Result<NameAffixes>.ValidationFailed(
                messages: [TK.Contacts.Validation.SUFFIX_CUSTOM_TOO_LONG]);
        }

        return D2Result<NameAffixes>.Ok(new NameAffixes
        {
            Prefix = prefix,
            PrefixCustom = cleanedPrefixCustom,
            Suffix = suffix,
            SuffixCustom = cleanedSuffixCustom,
        });
    }
}
