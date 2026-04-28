// -----------------------------------------------------------------------
// <copyright file="StringExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Utilities.Extensions;

using System.Text.RegularExpressions;

/// <summary>
/// Extension methods for strings.
/// </summary>
public static partial class StringExtensions
{
    /// <summary>
    /// Extension methods for strings.
    /// </summary>
    ///
    /// <param name="str">
    /// The string.
    /// </param>
    extension(string? str)
    {
        /// <summary>
        /// Checks if a string is "truthy" (not null, not empty, not whitespace).
        /// </summary>
        ///
        /// <returns>
        /// Whether the string is truthy.
        /// </returns>
        public bool Truthy() => !str.Falsey();

        /// <summary>
        /// Checks if a string is "falsey" (null, empty, or whitespace).
        /// </summary>
        ///
        /// <returns>
        /// Whether the string is falsey.
        /// </returns>
        public bool Falsey() => string.IsNullOrWhiteSpace(str);

        /// <summary>
        /// Returns null if the string is null, empty, or whitespace-only; otherwise
        /// returns the trimmed string. Use at boundaries (DB rows, proto mapping,
        /// user input) to convert empty strings to null before they propagate.
        /// </summary>
        ///
        /// <returns>
        /// The trimmed string, or null if falsey.
        /// </returns>
        public string? ToNullIfEmpty()
        {
            if (str.Falsey())
            {
                return null;
            }

            return str!.Trim();
        }

        /// <summary>
        /// Cleans a string by trimming leading/trailing whitespace and replacing duplicate whitespace.
        /// </summary>
        ///
        /// <returns>
        /// The string, cleaned or null (if empty or null).
        /// </returns>
        public string? CleanStr()
        {
            var trimmed = str?.Trim();

            if (trimmed.Falsey())
            {
                return null;
            }

            return WhitespaceRegex().Replace(trimmed!, " ");
        }

        /// <summary>
        /// Cleans a display name by stripping dangerous/unreasonable characters
        /// (HTML tags, markdown syntax, brackets, quotes, backticks, etc.),
        /// then trims whitespace and collapses duplicates.
        /// <para>
        /// Allowed: letters (any Unicode script), digits, spaces, hyphens,
        /// apostrophes, periods, commas.
        /// </para>
        /// <para>
        /// Mirrors <c>cleanDisplayStr()</c> in <c>@d2/utilities</c> (TypeScript).
        /// </para>
        /// </summary>
        ///
        /// <returns>
        /// The cleaned display name, or null if the result is empty after cleaning.
        /// </returns>
        public string? CleanDisplayStr()
        {
            if (str.Falsey())
            {
                return null;
            }

            var stripped = DisplayNameInvalidRegex().Replace(str!, string.Empty);
            return stripped.CleanStr();
        }
    }

    /// <summary>
    /// Extension methods for email addresses.
    /// </summary>
    ///
    /// <param name="email">
    /// The email address.
    /// </param>
    extension(string? email)
    {
        /// <summary>
        /// Cleans, normalizes and [validates the basic structure of] an email address.
        /// </summary>
        ///
        /// <returns>
        /// A string containing a normalized, cleaned email address.
        /// </returns>
        ///
        /// <exception cref="ArgumentException">
        /// Thrown if the email is null, empty, whitespace, or not in a valid format.
        /// </exception>
        public string CleanAndValidateEmail()
        {
            var cleaned = email.CleanStr()?.ToLowerInvariant();
            if (cleaned.Falsey() || !EmailRegex().IsMatch(cleaned!))
            {
                throw new ArgumentException("Invalid email address format.", nameof(email));
            }

            return cleaned!;
        }
    }

    /// <summary>
    /// Extension methods for phone numbers.
    /// </summary>
    ///
    /// <param name="phoneNumber">
    /// The phone number.
    /// </param>
    extension(string? phoneNumber)
    {
        /// <summary>
        /// Cleans and normalizes a phone number by removing all non-digit characters and validating
        /// its length.
        /// </summary>
        ///
        /// <returns>
        /// A string containing a normalized, cleaned phone number (E.164 format - digits only - no
        /// leading "+").
        /// </returns>
        ///
        /// <exception cref="ArgumentException">
        /// Thrown if the phone number is null, empty, less than 7 or greater than 15 digits or in
        /// an invalid format.
        /// </exception>
        public string CleanAndValidatePhoneNumber()
        {
            if (phoneNumber.Falsey())
            {
                throw new ArgumentException(
                    "Phone number cannot be null or empty.", nameof(phoneNumber));
            }

            var cleaned = NonDigitsRegex().Replace(phoneNumber!, string.Empty);

            if (cleaned.Falsey())
            {
                throw new ArgumentException("Invalid phone number format.", nameof(phoneNumber));
            }

            if (cleaned.Length is < 7 or > 15)
            {
                throw new ArgumentException(
                    "Phone number must be between 7 and 15 digits in length.", nameof(phoneNumber));
            }

            return cleaned;
        }
    }

    /// <summary>
    /// Extension methods for string arrays used in hash normalization.
    /// </summary>
    ///
    /// <param name="parts">
    /// The individual values you would like to normalize for hashing.
    /// </param>
    extension(string?[] parts)
    {
        /// <summary>
        /// Generates a normalized string for hashing by cleaning and lowercasing each part, then
        /// joining them with a pipe ("|") character.
        /// </summary>
        ///
        /// <returns>
        /// A normalized string suitable for hashing.
        /// </returns>
        ///
        /// <example>
        /// If you enter the values " Test One ", "   ", "TEST3", the resulting string will be
        /// "testone||test3".
        /// </example>
        public string GetNormalizedStrForHashing()
        {
            return string.Join("|", parts.Select(x => CleanStr(x)?.ToLowerInvariant() ?? string.Empty));
        }
    }

    /// <summary>
    /// A regular expression that matches a basic email format.
    /// </summary>
    ///
    /// <returns>
    /// A regex match if the string matches a basic email format.
    /// </returns>
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.None, matchTimeoutMilliseconds: 250)]
    private static partial Regex EmailRegex();

    /// <summary>
    /// A regular expression that matches one or more whitespace characters.
    /// </summary>
    ///
    /// <returns>
    /// A regex match if whitespace is atomically detected.
    /// </returns>
    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex WhitespaceRegex();

    /// <summary>
    /// Matches characters NOT allowed in display names.
    /// Allowed: letters (any Unicode script), digits, spaces, hyphens, apostrophes, periods, commas.
    /// Mirrors <c>DISPLAY_NAME_INVALID_RE</c> in <c>@d2/utilities</c> (TypeScript).
    /// </summary>
    [GeneratedRegex(@"[^\p{L}\p{N}\s\-'.,]", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex DisplayNameInvalidRegex();

    /// <summary>
    /// A regular expression that matches anything that is not a digit.
    /// </summary>
    ///
    /// <returns>
    /// A regex match if non-digit characters are detected.
    /// </returns>
    [GeneratedRegex(@"[^\d]", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex NonDigitsRegex();
}
