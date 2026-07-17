// -----------------------------------------------------------------------
// <copyright file="KeyDecomposer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n.SourceGen;

using System;
using System.Collections.Generic;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Pure logic for decomposing a translation key (e.g. <c>"common_errors_NOT_FOUND"</c>)
/// into a TK constant path: a domain segment (e.g. <c>"Common"</c>), a category
/// segment (e.g. <c>"Errors"</c>), and a constant name (e.g. <c>"NOT_FOUND"</c>).
/// </summary>
/// <remarks>
/// <para>
/// Stateless and unit-testable in isolation. The Roslyn-host integration
/// (<see cref="TKGenerator"/>) calls this on every key in en-US.json.
/// </para>
/// <para>
/// Decomposition rules:
/// <list type="bullet">
/// <item>Split the key by <c>'_'</c>.</item>
/// <item>Require at least 3 non-empty segments.</item>
/// <item>Segment 0 → domain (PascalCase first letter).</item>
/// <item>Segment 1 → category (PascalCase first letter).</item>
/// <item>Segments 2..N joined by <c>'_'</c> and uppercased → constant name.</item>
/// <item>The constant name MUST be a valid C# identifier (start with letter or
/// underscore; rest letters/digits/underscores; ASCII only for predictability).</item>
/// </list>
/// </para>
/// </remarks>
internal static class KeyDecomposer
{
    private const int _MIN_SEGMENTS = 3;

    private static readonly HashSet<string> sr_csharpReservedWords = new(StringComparer.Ordinal)
    {
        // Defensive: uppercase identifiers shouldn't collide with lowercase
        // C# reserved words, but cover the contextual / new-since-roslyn ones
        // that are lowercase-only just in case the SrcGen ever emits non-uppercase.
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
        "checked", "class", "const", "continue", "decimal", "default", "delegate",
        "do", "double", "else", "enum", "event", "explicit", "extern", "false",
        "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
        "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
        "new", "null", "object", "operator", "out", "override", "params", "private",
        "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    };

    /// <summary>
    /// Decomposes a translation key into its TK path components.
    /// </summary>
    /// <param name="key">The translation key (e.g. <c>"common_errors_NOT_FOUND"</c>).</param>
    /// <returns>
    /// A <see cref="DecomposedKey"/> with <c>IsValid=true</c> on success or
    /// <c>IsValid=false</c> with an explanatory <c>InvalidReason</c> otherwise.
    /// </returns>
    public static DecomposedKey Decompose(string? key)
    {
        if (key.Falsey())
        {
            return DecomposedKey.Invalid(string.Empty, "key is null or empty");
        }

        var segments = key!.Split('_');
        if (segments.Length < _MIN_SEGMENTS)
        {
            return DecomposedKey.Invalid(
                key,
                $"key must have at least {_MIN_SEGMENTS} underscore-separated segments");
        }

        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length == 0)
            {
                return DecomposedKey.Invalid(
                    key,
                    $"segment at index {i} is empty (consecutive underscores " +
                        "or leading/trailing underscore)");
            }
        }

        var domain = ToPascalCase(segments[0]);
        var category = ToPascalCase(segments[1]);

        // Validate domain + category as C# identifiers (start letter, ASCII only).
        if (!IsValidCSharpIdentifier(domain))
        {
            return DecomposedKey.Invalid(
                key,
                $"domain segment '{segments[0]}' produces invalid C# identifier '{domain}'");
        }

        if (!IsValidCSharpIdentifier(category))
        {
            return DecomposedKey.Invalid(
                key,
                $"category segment '{segments[1]}' produces invalid C# identifier '{category}'");
        }

        // Constant name is everything after the first two segments, uppercased.
        const int constantStartIndex = _MIN_SEGMENTS - 1;
        var constantSegmentCount = segments.Length - constantStartIndex;
        var constantName = string
            .Join("_", segments, constantStartIndex, constantSegmentCount)
            .ToUpperInvariant();
        if (!IsValidCSharpIdentifier(constantName))
        {
            return DecomposedKey.Invalid(
                key,
                $"constant name '{constantName}' is not a valid C# identifier");
        }

        if (sr_csharpReservedWords.Contains(constantName.ToLowerInvariant()))
        {
            return DecomposedKey.Invalid(
                key,
                $"constant name '{constantName}' collides with a C# reserved word");
        }

        return DecomposedKey.Valid(key, domain, category, constantName);
    }

    /// <summary>
    /// Uppercases the first character of <paramref name="segment"/> and leaves
    /// the rest unchanged. Domain and category segments are conventionally
    /// single lowercase words ("common", "auth", "geo"); this PascalCases them.
    /// </summary>
    private static string ToPascalCase(string segment)
    {
        if (segment.Length == 0 || char.IsUpper(segment[0]))
        {
            return segment;
        }

        return char.ToUpperInvariant(segment[0]) + segment.Substring(1);
    }

    /// <summary>
    /// Strict ASCII C# identifier check: first char letter or underscore,
    /// remaining chars letters / digits / underscores. Restricting to ASCII
    /// keeps emitted code predictable across encoding tooling.
    /// </summary>
    private static bool IsValidCSharpIdentifier(string identifier)
    {
        if (identifier.Falsey())
        {
            return false;
        }

        var first = identifier[0];
        if (!IsAsciiLetter(first) && first != '_')
        {
            return false;
        }

        for (var i = 1; i < identifier.Length; i++)
        {
            var c = identifier[i];
            if (!IsAsciiLetter(c) && !IsAsciiDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLetter(char c) =>
        c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';
}
