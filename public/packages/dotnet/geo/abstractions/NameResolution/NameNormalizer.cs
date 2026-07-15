// -----------------------------------------------------------------------
// <copyright file="NameNormalizer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Abstractions.NameResolution;

using System.Globalization;
using System.Text;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Pure helper that normalizes free-form place names (countries,
/// subdivisions, etc.) into a canonical comparison form before they are
/// fed into the geo name resolver's exact / fuzzy lookup table. The
/// algorithm is deterministic, stateless, and thread-safe — concurrent
/// callers can share a single reference to <see cref="Normalize"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cross-language parity:</b> the TypeScript mirror in
/// <c>@dcsv-io/d2-geo-abstractions</c> (<c>src/name-resolution/name-normalizer.ts</c>)
/// implements the byte-equivalent algorithm using
/// <c>String.prototype.normalize("NFD")</c> + a combining-mark strip +
/// <c>toLocaleLowerCase("en-US")</c>. The two implementations MUST
/// produce the same output for the same input — divergence breaks the
/// name-resolver fixtures the cross-language parity suite enforces.
/// </para>
/// <para>
/// <b>Pipeline order (each step is observable on its own):</b>
/// </para>
/// <list type="number">
///   <item>
///     <description>Null / empty / whitespace input short-circuits to
///     <see cref="string.Empty"/> so callers can pipe raw user input in
///     without first guarding against <c>null</c>.</description>
///   </item>
///   <item>
///     <description>Unicode NFD decomposition (<see cref="NormalizationForm.FormD"/>) —
///     splits accented characters into base letter + combining mark so the
///     marks can be stripped in the next pass.</description>
///   </item>
///   <item>
///     <description>Strip all combining marks
///     (<see cref="UnicodeCategory.NonSpacingMark"/>) so "São Paulo" and
///     "Sao Paulo" compare equal.</description>
///   </item>
///   <item>
///     <description>Replace the standalone ampersand token (<c> &amp; </c>
///     with surrounding whitespace) with <c> and </c> so "Trinidad &amp;
///     Tobago" and "Trinidad and Tobago" compare equal. We deliberately
///     match the spaced form only — touching every <c>&amp;</c> inside a
///     name would mangle things like <c>"AT&amp;T"</c>.</description>
///   </item>
///   <item>
///     <description>Casefold via
///     <see cref="CultureInfo.InvariantCulture"/>'s <see cref="TextInfo.ToLower(string)"/>
///     — invariant culture avoids the Turkish "dotless i" pitfall where
///     <c>"I".ToLower("tr-TR")</c> returns <c>"ı"</c>.</description>
///   </item>
///   <item>
///     <description>Trim leading / trailing whitespace.</description>
///   </item>
///   <item>
///     <description>Collapse runs of internal whitespace
///     (<see cref="char.IsWhiteSpace(char)"/>) down to a single ASCII
///     space so "United  States" and "United States" compare equal.</description>
///   </item>
/// </list>
/// </remarks>
public static class NameNormalizer
{
    private const char _SPACE = ' ';
    private const string _AMPERSAND_TOKEN = " & ";
    private const string _AND_TOKEN = " and ";

    /// <summary>
    /// Returns the canonical comparison form of <paramref name="input"/>.
    /// Returns <see cref="string.Empty"/> when <paramref name="input"/> is
    /// <c>null</c>, empty, or whitespace-only. Pure / stateless / threadsafe.
    /// </summary>
    /// <param name="input">
    /// The raw place name to normalize. May be <c>null</c>, empty, or
    /// whitespace-only — the helper never throws on bad input.
    /// </param>
    /// <returns>The canonical comparison form.</returns>
    public static string Normalize(string? input)
    {
        if (input.Falsey())
            return string.Empty;

        // NFD decomposition.
        var decomposed = input!.Normalize(NormalizationForm.FormD);

        // Strip combining marks.
        var stripped = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                stripped.Append(ch);
        }

        // Ampersand-token substitution (spaced form only).
        var ampersandSwapped = stripped
            .Replace(_AMPERSAND_TOKEN, _AND_TOKEN)
            .ToString();

        // Invariant-culture casefold.
        var lowered = CultureInfo.InvariantCulture.TextInfo.ToLower(ampersandSwapped);

        // Trim + collapse internal whitespace.
        return CollapseWhitespace(lowered.AsSpan().Trim());
    }

    private static string CollapseWhitespace(ReadOnlySpan<char> trimmed)
    {
        if (trimmed.IsEmpty)
            return string.Empty;

        var builder = new StringBuilder(trimmed.Length);
        var previousWasSpace = false;
        foreach (var ch in trimmed)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasSpace)
                {
                    builder.Append(_SPACE);
                    previousWasSpace = true;
                }
            }
            else
            {
                builder.Append(ch);
                previousWasSpace = false;
            }
        }

        return builder.ToString();
    }
}
