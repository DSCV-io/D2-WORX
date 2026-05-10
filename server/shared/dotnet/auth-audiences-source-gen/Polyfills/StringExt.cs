// -----------------------------------------------------------------------
// <copyright file="StringExt.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Audiences.SourceGen.Polyfills;

/// <summary>
/// netstandard2.0 polyfill of
/// <c>D2.Shared.Utilities.Extensions.StringExtensions.Falsey()</c>. SrcGens
/// cannot reference <c>D2.Shared.Utilities</c> (which targets <c>net10</c>)
/// because Roslyn analyzer hosts require <c>netstandard2.0</c>. This polyfill
/// keeps call sites rule-compliant per <c>docs/dev/rules.md §5.1</c>.
/// </summary>
internal static class StringExt
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> is null, empty, or
    /// whitespace-only — matching the real
    /// <c>D2.Shared.Utilities.Extensions.StringExtensions.Falsey(string?)</c>
    /// semantics.
    /// </summary>
    /// <param name="value">The string to test, or <c>null</c>.</param>
    /// <returns><c>true</c> when null, empty, or whitespace-only; otherwise <c>false</c>.</returns>
    public static bool Falsey(this string? value)
    {
        if (value is null) return true;
        for (int i = 0; i < value.Length; i++)
            if (!char.IsWhiteSpace(value[i])) return false;
        return true;
    }
}
