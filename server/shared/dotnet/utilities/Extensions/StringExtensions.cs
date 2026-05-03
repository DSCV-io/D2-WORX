// -----------------------------------------------------------------------
// <copyright file="StringExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Utilities.Extensions;

using System.Text.RegularExpressions;
using D2.Shared.I18n;
using D2.Shared.Result;

/// <summary>
/// Extension methods for <see cref="string"/> covering boundary checks
/// (<c>Truthy</c> / <c>Falsey</c> / <c>ToNullIfEmpty</c>), display-friendly
/// cleaning (<c>CleanStr</c> / <c>CleanDisplayStr</c>), <see cref="D2Result"/>-returning
/// validation helpers for emails and phone numbers, and a hash-friendly
/// <c>GetNormalizedStrForHashing</c> helper for string-array inputs.
/// </summary>
/// <remarks>
/// The validation helpers (<c>TryParseEmail</c>, <c>TryParsePhoneNumber</c>)
/// return <c>D2Result&lt;string&gt;</c> with TK-keyed messages so they compose
/// naturally with the smart-constructor pattern in domain layers. Producers
/// chain via <c>D2Result.Bind</c> / <c>BubbleFail</c> instead of try/catch.
/// </remarks>
public static partial class StringExtensions
{
    /// <param name="str">The string being checked or cleaned.</param>
    extension(string? str)
    {
        /// <summary>
        /// Returns true when the string is non-null, non-empty, and contains at
        /// least one non-whitespace character.
        /// </summary>
        public bool Truthy() => !str.Falsey();

        /// <summary>
        /// Returns true when the string is null, empty, or contains only
        /// whitespace.
        /// </summary>
        public bool Falsey() => string.IsNullOrWhiteSpace(str);

        /// <summary>
        /// Returns null when the string is null/empty/whitespace; otherwise
        /// returns the trimmed string. Use at boundaries (DB rows, proto
        /// mapping, user input) to convert empty strings to null before they
        /// propagate into domain models.
        /// </summary>
        public string? ToNullIfEmpty()
        {
            if (str.Falsey())
                return null;

            return str!.Trim();
        }

        /// <summary>
        /// Trims leading/trailing whitespace and collapses any internal
        /// whitespace runs (spaces, tabs, newlines, etc.) into a single space.
        /// Returns null if the string is empty after cleaning.
        /// </summary>
        public string? CleanStr()
        {
            var trimmed = str?.Trim();

            if (trimmed.Falsey())
                return null;

            return WhitespaceRegex().Replace(trimmed!, " ");
        }

        /// <summary>
        /// Strips characters not allowed in display names (HTML tags, markdown
        /// syntax, brackets, quotes, backticks, etc.) and then applies
        /// <see cref="CleanStr"/>. Returns null if empty after cleaning.
        /// </summary>
        /// <remarks>
        /// Allowed characters: letters from any Unicode script, digits, spaces,
        /// hyphens, apostrophes, periods, commas.
        /// </remarks>
        public string? CleanDisplayStr()
        {
            if (str.Falsey())
                return null;

            var stripped = DisplayNameInvalidRegex().Replace(str!, string.Empty);
            return stripped.CleanStr();
        }
    }

    /// <param name="email">The email address to clean and validate.</param>
    extension(string? email)
    {
        /// <summary>
        /// Trims, collapses whitespace, lowercases, and validates the basic
        /// structure of an email address. Returns a <see cref="D2Result{TData}"/>
        /// so callers can chain with the rest of the result-pipeline
        /// (<c>BubbleFail</c>, <c>Bind</c>) instead of try/catch.
        /// </summary>
        /// <returns>
        /// <see cref="D2Result{TData}"/> wrapping the normalized email on
        /// success, or a validation-failure result carrying
        /// <see cref="TK.Common.Validation.EMAIL_INVALID"/> when the input is
        /// null / empty / whitespace / does not match <c>local@domain.tld</c>.
        /// </returns>
        public D2Result<string> TryParseEmail()
        {
            var cleaned = email.CleanStr()?.ToLowerInvariant();
            if (cleaned.Falsey() || !EmailRegex().IsMatch(cleaned!))
            {
                return D2Result<string>.ValidationFailed(
                    messages: [TK.Common.Validation.EMAIL_INVALID]);
            }

            return D2Result<string>.Ok(cleaned!);
        }
    }

    /// <param name="phoneNumber">The phone number to clean and validate.</param>
    extension(string? phoneNumber)
    {
        /// <summary>
        /// Strips every non-digit character and validates that the remainder is
        /// 7–15 digits long (E.164 length envelope). Returns a <see cref="D2Result{TData}"/>
        /// so callers can chain with the rest of the result-pipeline.
        /// </summary>
        /// <returns>
        /// <see cref="D2Result{TData}"/> wrapping the digit-only phone number
        /// on success, or a validation-failure result carrying
        /// <see cref="TK.Common.Validation.PHONE_INVALID"/> when the input is
        /// null / empty / contains no digits / falls outside the 7-15 envelope.
        /// </returns>
        public D2Result<string> TryParsePhoneNumber()
        {
            if (phoneNumber.Falsey())
            {
                return D2Result<string>.ValidationFailed(
                    messages: [TK.Common.Validation.PHONE_INVALID]);
            }

            var cleaned = NonDigitsRegex().Replace(phoneNumber!, string.Empty);

            if (cleaned.Falsey() || cleaned.Length is < 7 or > 15)
            {
                return D2Result<string>.ValidationFailed(
                    messages: [TK.Common.Validation.PHONE_INVALID]);
            }

            return D2Result<string>.Ok(cleaned);
        }
    }

    /// <param name="parts">
    /// The individual values to normalize for hashing.
    /// </param>
    extension(string?[] parts)
    {
        /// <summary>
        /// Cleans and lowercases each part, then joins them with a pipe
        /// (<c>|</c>) character. Empty / null parts are preserved as empty
        /// segments so positional alignment is retained across input arrays.
        /// </summary>
        ///
        /// <example>
        /// <c>[ " Test One ", "   ", "TEST3" ]</c> →
        /// <c>"test one||test3"</c>.
        /// </example>
        public string GetNormalizedStrForHashing()
        {
            return string.Join(
                "|",
                parts.Select(x => x.CleanStr()?.ToLowerInvariant() ?? string.Empty));
        }
    }

    /// <summary>
    /// Matches a basic <c>local@domain.tld</c> email shape. Has bounded
    /// backtracking — <c>[^@\s]+</c> includes <c>.</c>, so the second
    /// <c>[^@\s]+\.</c> backtracks once per dot in the input looking for the
    /// trailing-dot anchor. Worst-case execution is O(n) in input length,
    /// where n is upstream-bounded by HTTP field-length validation
    /// (typically ≤ 4096 chars), giving an absolute worst case in the
    /// microsecond range. Cannot ReDoS — no <c>matchTimeout</c> needed.
    /// </summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.None)]
    private static partial Regex EmailRegex();

    /// <summary>
    /// Matches one or more whitespace characters. Single greedy quantifier with
    /// no following pattern → no backtracking → no <c>matchTimeout</c> needed.
    /// </summary>
    [GeneratedRegex(@"\s+", RegexOptions.None)]
    private static partial Regex WhitespaceRegex();

    /// <summary>
    /// Matches characters not allowed in display names. Allowed: letters from
    /// any Unicode script, digits, spaces, hyphens, apostrophes, periods,
    /// commas. Single char-class match with no quantifier → no backtracking
    /// → no <c>matchTimeout</c> needed.
    /// </summary>
    [GeneratedRegex(@"[^\p{L}\p{N}\s\-'.,]", RegexOptions.None)]
    private static partial Regex DisplayNameInvalidRegex();

    /// <summary>
    /// Matches any non-digit character. Single char-class match with no
    /// quantifier → no backtracking → no <c>matchTimeout</c> needed.
    /// </summary>
    [GeneratedRegex(@"[^\d]", RegexOptions.None)]
    private static partial Regex NonDigitsRegex();
}
