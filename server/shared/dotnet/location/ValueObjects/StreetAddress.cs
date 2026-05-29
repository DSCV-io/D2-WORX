// -----------------------------------------------------------------------
// <copyright file="StreetAddress.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Location.ValueObjects;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using D2.Shared.I18n;
using D2.Shared.Result;
using D2.Shared.Utilities.Extensions;

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
/// <b>PII.</b> StreetAddress lines are postal-address PII (GDPR-sensitive).
/// Consumers MUST apply <c>[RedactData]</c> on any field of this type
/// before it reaches a logger / serializer sink.
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
    public required string Line1 { get; init; }

    /// <summary>Gets the optional second address line.</summary>
    public string? Line2 { get; init; }

    /// <summary>Gets the optional third address line.</summary>
    public string? Line3 { get; init; }

    /// <summary>Gets the optional fourth address line.</summary>
    public string? Line4 { get; init; }

    /// <summary>Gets the optional fifth address line.</summary>
    public string? Line5 { get; init; }

    /// <summary>
    /// Gets the stable hash identifier:
    /// <c>"v1." + SHA-256(NormalizeForHash(Line1) | ... | NormalizeForHash(Line5))</c>
    /// as lowercase hex. All 5 slots always participate (deterministic
    /// positional shape; missing lines contribute <c>""</c>).
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

        var cleanedLine2 = CleanStored(line2);
        var cleanedLine3 = CleanStored(line3);
        var cleanedLine4 = CleanStored(line4);
        var cleanedLine5 = CleanStored(line5);

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
            Line1 = cleanedLine1!,
            Line2 = cleanedLine2,
            Line3 = cleanedLine3,
            Line4 = cleanedLine4,
            Line5 = cleanedLine5,
            HashId = hashId,
        });
    }

    /// <summary>
    /// Two-stage normalization stage 2 — produces the hash-input form
    /// (UPPERCASE + NFD-stripped combining marks + Unicode-category
    /// filter keeping only Letter / Decimal-digit / ASCII space).
    /// Internal — only <see cref="Create"/> invokes; exposed to
    /// <c>D2.Shared.Tests</c> via <c>InternalsVisibleTo</c> for direct
    /// adversarial coverage.
    /// </summary>
    /// <param name="cleaned">
    /// A line value already passed through <c>CleanStored</c> (or null).
    /// </param>
    /// <returns>
    /// The hash-form canonical string; empty when <paramref name="cleaned"/> is null/empty.
    /// </returns>
    internal static string NormalizeForHash(string? cleaned)
    {
        if (cleaned.Falsey())
            return string.Empty;

        // Stage 2a — case-fold (no-op on caseless scripts).
        var upper = cleaned!.ToUpperInvariant();

        // Stage 2b — NFD decompose so Latin-derived diacritics split into
        // base + combining mark; combining marks are then dropped by the
        // category filter below.
        var nfd = upper.Normalize(NormalizationForm.FormD);

        // Stage 2c — Unicode-category-aware filter: keep only Letter +
        // Decimal-digit + single ASCII space. Iterate by Rune so surrogate
        // pairs are handled correctly (char.IsLetter is surrogate-unsafe).
        var sb = new StringBuilder(nfd.Length);
        foreach (var rune in nfd.EnumerateRunes())
        {
            if (rune.Value == ' ')
            {
                sb.Append(' ');
                continue;
            }

            if (Rune.IsLetter(rune) || Rune.IsDigit(rune))
                sb.Append(rune.ToString());
        }

        return sb.ToString();
    }

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
