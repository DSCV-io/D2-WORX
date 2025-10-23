using System.Text.RegularExpressions;

namespace D2.Contracts.Utilities;

/// <summary>
/// Extension methods for strings.
/// </summary>
public static partial class StringExtensions
{
    /// <summary>
    /// Checks if a string is "truthy" (not null, not empty, not whitespace).
    /// </summary>
    ///
    /// <param name="str">
    /// The string.
    /// </param>
    ///
    /// <returns>
    /// Whether the string is truthy.
    /// </returns>
    public static bool Truthy(this string? str) => !str.Falsey();

    /// <summary>
    /// Checks if a string is "falsey" (null, empty, or whitespace).
    /// </summary>
    ///
    /// <param name="str">
    /// The string.
    /// </param>
    ///
    /// <returns>
    /// Whether the string is falsey.
    /// </returns>
    public static bool Falsey(this string? str) => string.IsNullOrWhiteSpace(str);

    /// <summary>
    /// Cleans a string by trimming leading/trailing whitespace and replacing duplicate whitespace.
    /// </summary>
    /// <param name="str">
    /// The string.
    /// </param>
    ///
    /// <returns>
    /// The string, cleaned or null (if empty or null).
    /// </returns>
    public static string? CleanStr(this string? str)
    {
        var trimmed = str?.Trim();

        if (trimmed.Falsey())
            return null;

        return WhitespaceRegex().Replace(trimmed!, " ");
    }

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
    /// Generates a normalized string for hashing by cleaning and lowercasing each part, then
    /// joining them with a pipe ("|") character.
    /// </summary>
    ///
    /// <param name="parts">
    /// The individual values you would like to normalize for hashing.
    /// </param>
    /// <returns>
    ///
    /// A normalized string suitable for hashing.
    /// </returns>
    ///
    /// <example>
    /// If you enter the values " Test One ", "   ", "TEST3", the resulting string will be
    /// "testone||test3".
    /// </example>
    public static string GetNormalizedStrForHashing(this string?[] parts)
    {
        return string.Join("|", parts.Select(x => CleanStr(x)?.ToLowerInvariant() ?? string.Empty));
    }

    /// <summary>
    /// A regular expression that matches a basic email format.
    /// </summary>
    /// <returns>
    /// A regex match if the string matches a basic email format.
    /// </returns>
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.None, matchTimeoutMilliseconds: 250)]
    private static partial Regex EmailRegex();

    /// <summary>
    /// Cleans, normalizes and [validates the basic structure of] an email address.
    /// </summary>
    ///
    /// <param name="email">
    /// The email address.
    /// </param>
    ///
    /// <returns>
    /// A string containing a normalized, cleaned email address.
    /// </returns>
    ///
    /// <exception cref="ArgumentException">
    /// Thrown if the email is null, empty, whitespace, or not in a valid format.
    /// </exception>
    public static string CleanAndValidateEmail(this string? email)
    {
        var cleaned = email.CleanStr()?.ToLowerInvariant();
        if (cleaned.Falsey() || !EmailRegex().IsMatch(cleaned!))
            throw new ArgumentException("Invalid email address format.", nameof(email));

        return cleaned!;
    }

    /// <summary>
    /// A regular expression that matches anything that is not a digit.
    /// </summary>
    /// <returns>
    /// A regex match if non-digit characters are detected.
    /// </returns>
    [GeneratedRegex(@"[^\d]", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex NonDigitsRegex();

    /// <summary>
    /// Cleans and normalizes a phone number by removing all non-digit characters and validating
    /// its length.
    /// </summary>
    ///
    /// <param name="phoneNumber">
    /// The phone number.
    /// </param>
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
    public static string CleanAndValidatePhoneNumber(this string? phoneNumber)
    {
        if (phoneNumber.Falsey())
            throw new ArgumentException(
                "Phone number cannot be null or empty.", nameof(phoneNumber));

        var cleaned = NonDigitsRegex().Replace(phoneNumber!, string.Empty);

        if (cleaned.Falsey())
            throw new ArgumentException("Invalid phone number format.", nameof(phoneNumber));

        if (cleaned.Length is < 7 or > 15)
            throw new ArgumentException(
                "Phone number must be between 7 and 15 digits in length.", nameof(phoneNumber));

        return cleaned;
    }
}
