namespace D2.Contracts.Utilities;

/// <summary>
/// Extension methods for strings.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Checks if a string is "truthy" (not null, not empty, not whitespace).
    /// </summary>
    /// <param name="str">The string.</param>
    /// <returns>Whether the string is truthy.</returns>
    public static bool Truthy(this string? str) => !string.IsNullOrWhiteSpace(str);

    /// <summary>
    /// Checks if a string is "falsey" (null, empty, or whitespace).
    /// </summary>
    /// <param name="str">The string.</param>
    /// <returns>Whether the string is falsey.</returns>
    public static bool Falsey(this string? str) => string.IsNullOrWhiteSpace(str);
}
