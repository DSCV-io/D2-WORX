// -----------------------------------------------------------------------
// <copyright file="StringExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Utilities.Extensions;

using System.Text.RegularExpressions;

/// <summary>
/// Extension methods for <see cref="string"/> covering boundary checks
/// (<c>Truthy</c> / <c>Falsey</c> / <c>ToNullIfEmpty</c>), display-friendly
/// cleaning (<c>CleanStr</c> / <c>CleanDisplayStr</c>), throw-on-invalid
/// validation helpers for emails and phone numbers, and a hash-friendly
/// <c>GetNormalizedStrForHashing</c> helper for string-array inputs.
/// </summary>
/// <remarks>
/// The validation helpers (<c>CleanAndValidateEmail</c>,
/// <c>CleanAndValidatePhoneNumber</c>) intentionally throw — they are
/// intended for domain-layer constructors where invariant violations are
/// exceptional cases. Application-layer callers that prefer errors-as-values
/// should use <c>FluentValidation</c> + <c>D2Result.ValidationFailed</c>
/// instead.
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
        /// structure of an email address.
        /// </summary>
        ///
        /// <returns>The normalized, validated email address.</returns>
        ///
        /// <exception cref="ArgumentException">
        /// Thrown when the email is null/empty/whitespace, or does not match
        /// the simple <c>local@domain.tld</c> shape.
        /// </exception>
        public string CleanAndValidateEmail()
        {
            var cleaned = email.CleanStr()?.ToLowerInvariant();
            if (cleaned.Falsey() || !EmailRegex().IsMatch(cleaned!))
            {
                throw new ArgumentException(
                    "Invalid email address format.",
                    nameof(email));
            }

            return cleaned!;
        }
    }

    /// <param name="phoneNumber">The phone number to clean and validate.</param>
    extension(string? phoneNumber)
    {
        /// <summary>
        /// Strips every non-digit character and validates that the remainder is
        /// 7–15 digits long (E.164 length envelope).
        /// </summary>
        ///
        /// <returns>
        /// The digit-only phone number (no leading <c>+</c>).
        /// </returns>
        ///
        /// <exception cref="ArgumentException">
        /// Thrown when the input is null/empty/whitespace, contains no digits,
        /// or has fewer than 7 / more than 15 digits after cleaning.
        /// </exception>
        public string CleanAndValidatePhoneNumber()
        {
            if (phoneNumber.Falsey())
            {
                throw new ArgumentException(
                    "Phone number cannot be null or empty.",
                    nameof(phoneNumber));
            }

            var cleaned = NonDigitsRegex().Replace(phoneNumber!, string.Empty);

            if (cleaned.Falsey())
            {
                throw new ArgumentException(
                    "Invalid phone number format.",
                    nameof(phoneNumber));
            }

            if (cleaned.Length is < 7 or > 15)
            {
                throw new ArgumentException(
                    "Phone number must be between 7 and 15 digits in length.",
                    nameof(phoneNumber));
            }

            return cleaned;
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
    /// Matches a basic <c>local@domain.tld</c> email shape.
    /// </summary>
    [GeneratedRegex(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.None,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex EmailRegex();

    /// <summary>
    /// Matches one or more whitespace characters.
    /// </summary>
    [GeneratedRegex(
        @"\s+",
        RegexOptions.None,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex WhitespaceRegex();

    /// <summary>
    /// Matches characters not allowed in display names. Allowed: letters from
    /// any Unicode script, digits, spaces, hyphens, apostrophes, periods,
    /// commas.
    /// </summary>
    [GeneratedRegex(
        @"[^\p{L}\p{N}\s\-'.,]",
        RegexOptions.None,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex DisplayNameInvalidRegex();

    /// <summary>
    /// Matches any non-digit character.
    /// </summary>
    [GeneratedRegex(
        @"[^\d]",
        RegexOptions.None,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex NonDigitsRegex();
}
