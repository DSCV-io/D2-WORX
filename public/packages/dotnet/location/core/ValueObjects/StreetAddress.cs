// -----------------------------------------------------------------------
// <copyright file="StreetAddress.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Location.ValueObjects;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;
using DcsvIo.D2.Utilities.Extensions;
using DcsvIo.D2.Validation.Abstractions;

/// <summary>
/// Immutable 5-line postal address value object. Line1 is required;
/// Line2..Line5 are optional and may be populated in any combination
/// (no gap rule — <see cref="Line1"/> + <see cref="Line5"/> with nulls
/// between is valid). Uses two-stage normalization: the stored form
/// preserves case + strips decorative punctuation; the hash form
/// upper-cases + NFD-strips combining marks + applies a Unicode-category
/// filter so dedup-equivalent inputs across scripts produce byte-identical
/// <see cref="HashId"/> values.
/// </summary>
/// <remarks>
/// <para>
/// <b>Self-redacting PII.</b> <see cref="Line1"/>, <see cref="Line2"/>,
/// <see cref="Line3"/>, <see cref="Line4"/>, and <see cref="Line5"/> are marked
/// <c>[RedactData(PersonalInformation)]</c> and are masked automatically by the
/// Serilog destructuring policy — street-address lines are postal-address PII
/// (GDPR-sensitive). <see cref="HashId"/> is left visible because it is a one-way
/// SHA-256 digest of the normalized address lines: opaque, non-reversible, and safe
/// for correlation in logs and traces without leaking address content.
/// </para>
/// <para>
/// <b>Normalization.</b> The hash form preserves all
/// <see cref="UnicodeCategory"/> Letter (<c>\p{L}</c>) and Decimal-digit
/// (<c>\p{Nd}</c>) code points from any script (Cyrillic, CJK, Greek,
/// Arabic, Devanagari, etc.) plus ASCII space. NFD decomposition strips
/// Latin-derived diacritics so <c>"Café"</c> and <c>"Cafe"</c> hash to
/// the same canonical form. Punctuation, symbols, emoji, control chars,
/// and format chars (BiDi overrides, zero-width joiners) are stripped.
/// </para>
/// </remarks>
public sealed record StreetAddress
{
    /// <summary>
    /// Gets the required first address line
    /// (post-normalization, case preserved, decorative punctuation stripped).
    /// </summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public required string Line1 { get; init; }

    /// <summary>Gets the optional second address line.</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string? Line2 { get; init; }

    /// <summary>Gets the optional third address line.</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string? Line3 { get; init; }

    /// <summary>Gets the optional fourth address line.</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string? Line4 { get; init; }

    /// <summary>Gets the optional fifth address line.</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string? Line5 { get; init; }

    /// <summary>
    /// Gets the stable hash identifier:
    /// <c>"v1." + SHA-256(NormalizeForHash(Line1) | ... | NormalizeForHash(Line5))</c>
    /// as lowercase hex. All 5 slots always participate (deterministic
    /// positional shape; missing lines contribute <c>""</c>).
    /// Emitted unredacted in logs — it is a one-way SHA-256 digest (opaque,
    /// non-reversible) and is safe for correlation in logs and traces without
    /// leaking address content.
    /// </summary>
    public required string HashId { get; init; }

    /// <summary>
    /// Creates a <see cref="StreetAddress"/> from up to 5 free-text lines.
    /// <paramref name="line1"/> is required (post-clean); the others
    /// are optional and may be supplied in any combination.
    /// </summary>
    /// <param name="line1">Required first line (post-clean must be non-empty).</param>
    /// <param name="line2">Optional second line.</param>
    /// <param name="line3">Optional third line.</param>
    /// <param name="line4">Optional fourth line.</param>
    /// <param name="line5">Optional fifth line.</param>
    /// <returns>
    /// <c>Ok</c> when <paramref name="line1"/> is non-empty after cleaning;
    /// <see cref="D2Result{TData}.ValidationFailed"/> otherwise.
    /// </returns>
    public static D2Result<StreetAddress> Create(
        string? line1,
        string? line2 = null,
        string? line3 = null,
        string? line4 = null,
        string? line5 = null)
    {
        var cleanedLine1 = CleanStored(line1);
        if (cleanedLine1.Falsey())
        {
            return D2Result<StreetAddress>.ValidationFailed(
                messages: [TK.Geo.Validation.ADDRESS_LINE1_REQUIRED]);
        }

        if (cleanedLine1!.Length > FieldConstraints.STREET_LINE_MAX)
        {
            return D2Result<StreetAddress>.ValidationFailed(
                messages: [TK.Geo.Validation.STREET_LINE_TOO_LONG]);
        }

        var cleanedLine2 = CleanStored(line2);
        if (cleanedLine2.Truthy() && cleanedLine2!.Length > FieldConstraints.STREET_LINE_MAX)
        {
            return D2Result<StreetAddress>.ValidationFailed(
                messages: [TK.Geo.Validation.STREET_LINE_TOO_LONG]);
        }

        var cleanedLine3 = CleanStored(line3);
        if (cleanedLine3.Truthy() && cleanedLine3!.Length > FieldConstraints.STREET_LINE_MAX)
        {
            return D2Result<StreetAddress>.ValidationFailed(
                messages: [TK.Geo.Validation.STREET_LINE_TOO_LONG]);
        }

        var cleanedLine4 = CleanStored(line4);
        if (cleanedLine4.Truthy() && cleanedLine4!.Length > FieldConstraints.STREET_LINE_MAX)
        {
            return D2Result<StreetAddress>.ValidationFailed(
                messages: [TK.Geo.Validation.STREET_LINE_TOO_LONG]);
        }

        var cleanedLine5 = CleanStored(line5);
        if (cleanedLine5.Truthy() && cleanedLine5!.Length > FieldConstraints.STREET_LINE_MAX)
        {
            return D2Result<StreetAddress>.ValidationFailed(
                messages: [TK.Geo.Validation.STREET_LINE_TOO_LONG]);
        }

        var hashInput =
            NormalizeForHash(cleanedLine1) + "|" +
            NormalizeForHash(cleanedLine2) + "|" +
            NormalizeForHash(cleanedLine3) + "|" +
            NormalizeForHash(cleanedLine4) + "|" +
            NormalizeForHash(cleanedLine5);

        // BCL static one-shot per §15.8 — no IDisposable instance to manage.
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        var hashId = "v1." + Convert.ToHexStringLower(hashBytes);

        return D2Result<StreetAddress>.Ok(new StreetAddress
        {
            Line1 = cleanedLine1,
            Line2 = cleanedLine2,
            Line3 = cleanedLine3,
            Line4 = cleanedLine4,
            Line5 = cleanedLine5,
            HashId = hashId,
        });
    }

    /// <summary>
    /// Two-stage normalization stage 2 — thin forwarder to the shared
    /// <see cref="DcsvIo.D2.Utilities.Extensions.StringExtensions.NormalizeForHash(string?)"/>
    /// extension in <c>DcsvIo.D2.Utilities</c>. The algorithm is byte-identical
    /// to the original inline implementation (UPPERCASE + NFD-strip combining
    /// marks + Unicode-category filter: Letter / Decimal-digit / ASCII space).
    /// Kept as an internal method so existing test call-sites and
    /// <c>AdminLocation</c> call-sites continue to compile without churn.
    /// </summary>
    /// <param name="cleaned">
    /// A line value already passed through <c>CleanStored</c> (or null).
    /// </param>
    /// <returns>
    /// The hash-form canonical string; empty when <paramref name="cleaned"/> is null/empty.
    /// </returns>
    internal static string NormalizeForHash(string? cleaned) => cleaned.NormalizeForHash();

    /// <summary>
    /// Two-stage normalization stage 1 — produces the stored form
    /// (trim → collapse internal whitespace → strip decorative
    /// punctuation, CASE PRESERVED). Whitespace-only / null input → null.
    /// </summary>
    private static string? CleanStored(string? line)
    {
        if (line.Falsey())
            return null;

        var sb = new StringBuilder(line!.Length);
        var lastWasSpace = false;

        foreach (var rune in line.EnumerateRunes())
        {
            var cp = rune.Value;

            // Whitespace + control chars (incl. CR, LF, TAB, NUL) collapse to a single space.
            if (Rune.IsWhiteSpace(rune) || Rune.IsControl(rune))
            {
                if (!lastWasSpace && sb.Length > 0)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            // Format chars (BiDi overrides, zero-width joiners, etc.) — strip.
            var cat = Rune.GetUnicodeCategory(rune);
            if (cat == UnicodeCategory.Format)
                continue;

            // Decorative end-of-sentence / list punctuation — strip from stored form.
            // Keep hyphens, apostrophes, slashes, '#', '&', parentheses, brackets,
            // braces, '*', '+', etc. (semantic chars callers may want preserved).
            if (cp is '.' or ',' or ';' or ':' or '!' or '?')
                continue;

            sb.Append(rune.ToString());
            lastWasSpace = false;
        }

        // Trim trailing space (loop appends an internal space, then sees
        // EOF — buffer may have a trailing space if input ended in whitespace).
        while (sb.Length > 0 && sb[sb.Length - 1] == ' ')
            sb.Length--;

        return sb.Length == 0 ? null : sb.ToString();
    }
}
