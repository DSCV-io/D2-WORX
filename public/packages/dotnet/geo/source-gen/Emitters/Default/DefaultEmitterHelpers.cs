// -----------------------------------------------------------------------
// <copyright file="DefaultEmitterHelpers.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Emitters.Default;

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Shared helpers used by every per-catalog DATA emitter in this directory.
/// Encapsulates the per-target namespace, the standard file-header banner,
/// and the safe-C#-identifier coercion that the nested static-class
/// emitters share (numeric-leading short codes get a leading underscore;
/// hyphens / slashes / colons are not allowed as members).
/// </summary>
internal static class DefaultEmitterHelpers
{
    /// <summary>
    /// The <c>DcsvIo.D2.Geo.Default</c> namespace every emitted data file
    /// uses. Matches the assembly's <c>RootNamespace</c>.
    /// </summary>
    public const string DefaultNamespace = "DcsvIo.D2.Geo.Default";

    /// <summary>
    /// The <c>DcsvIo.D2.Geo.Abstractions</c> namespace used in <c>using</c>
    /// directives in every emitted file so the data values can reference
    /// the spec-derived types (<c>Country</c> record, <c>CountryCode</c>
    /// enum, <c>SubdivisionCode</c> wrapper struct, etc.).
    /// </summary>
    public const string AbstractionsNamespace = EmitterHelpers.AbstractionsNamespace;

    /// <summary>
    /// The C# reserved-keyword set — identifiers that match these must be
    /// prefixed with <c>@</c> to compile. Includes all language keywords as
    /// of C# 12 / .NET 8+. Contextual keywords are EXCLUDED — they're legal
    /// identifiers outside their narrow grammar contexts.
    /// </summary>
    private static readonly HashSet<string> sr_cSharpReservedKeywords = new HashSet<string>(
        System.StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
        "char", "checked", "class", "const", "continue", "decimal", "default",
        "delegate", "do", "double", "else", "enum", "event", "explicit",
        "extern", "false", "finally", "fixed", "float", "for", "foreach",
        "goto", "if", "implicit", "in", "int", "interface", "internal", "is",
        "lock", "long", "namespace", "new", "null", "object", "operator",
        "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
        "stackalloc", "static", "string", "struct", "switch", "this", "throw",
        "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
        "ushort", "using", "virtual", "void", "volatile", "while",
    };

    /// <summary>
    /// Appends the auto-generated file banner identical to the Abstractions
    /// emitters so cross-target tooling lights up identically.
    /// </summary>
    /// <param name="sb">The destination buffer.</param>
    public static void AppendFileHeader(StringBuilder sb) =>
        EmitterHelpers.AppendFileHeader(sb);

    /// <summary>
    /// Escapes a value for inclusion in a C# string literal.
    /// </summary>
    /// <param name="value">The raw value.</param>
    /// <returns>The escaped value.</returns>
    public static string EscapeStringLiteral(string value) =>
        EmitterHelpers.EscapeStringLiteral(value);

    /// <summary>
    /// Escapes a value for inclusion inside an XML doc comment.
    /// </summary>
    /// <param name="value">The raw value.</param>
    /// <returns>The escaped value.</returns>
    public static string EscapeXmlDoc(string value) =>
        EmitterHelpers.EscapeXmlDoc(value);

    /// <summary>
    /// Coerces a code segment (subdivision short-code, IANA location, BCP47
    /// region/script subtag) into a safe C# identifier. Numeric-leading
    /// segments get a leading underscore (e.g. <c>"02"</c> → <c>"_02"</c>).
    /// C# reserved keywords get a leading <c>@</c> verbatim sigil
    /// (e.g. <c>"as"</c> → <c>"@as"</c>, <c>"is"</c> → <c>"@is"</c>).
    /// Returns the segment verbatim when it is already a valid identifier.
    /// Returns <c>null</c> when the segment contains characters that cannot
    /// be salvaged (hyphen, slash, colon, dot, ...) — callers MUST handle
    /// the <c>null</c> case (skip or report).
    /// </summary>
    /// <param name="segment">The raw segment.</param>
    /// <returns>The safe identifier, or <c>null</c> when unsalvageable.</returns>
    public static string? CoerceToIdentifier(string segment)
    {
        if (segment.Falsey())
            return null;

        // Fast path — already a valid identifier (starts with letter / underscore,
        // remainder is letters / digits / underscores).
        if (IsValidIdentifier(segment))
        {
            return sr_cSharpReservedKeywords.Contains(segment)
                ? "@" + segment
                : segment;
        }

        // Numeric-leading case (e.g. "02", "13", "541") — prepend underscore.
        if (char.IsDigit(segment[0]))
        {
            var prefixed = "_" + segment;
            if (IsValidIdentifier(prefixed))
                return prefixed;
        }

        // Unsalvageable — contains hyphen / slash / colon / dot / ...
        return null;
    }

    /// <summary>
    /// True when the supplied candidate would compile as a C# identifier
    /// (first char a letter or underscore, remainder letters / digits /
    /// underscores). Same logic the EnumEmitter uses for member-name
    /// validation.
    /// </summary>
    /// <param name="candidate">The candidate identifier.</param>
    /// <returns>True when the candidate is a valid C# identifier.</returns>
    public static bool IsValidIdentifier(string candidate)
    {
        if (candidate.Falsey())
            return false;

        var first = candidate[0];
        if (!(char.IsLetter(first) || first == '_'))
            return false;

        for (var i = 1; i < candidate.Length; i++)
        {
            var ch = candidate[i];
            if (!(char.IsLetterOrDigit(ch) || ch == '_'))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Emits a C# string literal — quoted + escaped. Returns the verbatim
    /// <c>null</c> token when the value is <c>null</c>.
    /// </summary>
    /// <param name="value">The value (may be null).</param>
    /// <returns>The C#-source representation.</returns>
    public static string StringLiteralOrNull(string? value)
    {
        if (value is null)
            return "null";

        return "\"" + EscapeStringLiteral(value) + "\"";
    }

    /// <summary>
    /// Emits a nullable-int C# literal. Returns the verbatim <c>null</c>
    /// token when the value is <c>null</c>.
    /// </summary>
    /// <param name="value">The value (may be null).</param>
    /// <returns>The C#-source representation.</returns>
    public static string IntLiteralOrNull(int? value)
    {
        if (value is null)
            return "null";

        return value.Value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Maps the spec's day-of-week wire string (<c>"Sunday"</c> ..
    /// <c>"Saturday"</c>) to the matching <c>GeoDayOfWeek</c> enum literal.
    /// Falls back to <c>GeoDayOfWeek.Sunday</c> for unrecognized input
    /// (defensive — Tier-2 specs always carry valid values).
    /// </summary>
    /// <param name="wireValue">The wire string.</param>
    /// <returns>The matching enum literal source.</returns>
    public static string MapDayOfWeek(string? wireValue)
    {
        var safe = wireValue.Falsey() ? "Sunday" : wireValue!;
        return $"GeoDayOfWeek.{safe}";
    }

    /// <summary>
    /// Maps the spec's measurement-system wire string (<c>"Metric"</c> /
    /// <c>"Imperial"</c> / <c>"Mixed"</c>) to the matching enum literal.
    /// </summary>
    /// <param name="wireValue">The wire string.</param>
    /// <returns>The matching enum literal source.</returns>
    public static string MapMeasurementSystem(string? wireValue)
    {
        var safe = wireValue.Falsey() ? "Metric" : wireValue!;
        return $"MeasurementSystem.{safe}";
    }

    /// <summary>
    /// Maps the spec's writing-direction wire string (<c>"LTR"</c> /
    /// <c>"RTL"</c>) to the matching enum literal.
    /// </summary>
    /// <param name="wireValue">The wire string.</param>
    /// <returns>The matching enum literal source.</returns>
    public static string MapWritingDirection(string? wireValue)
    {
        var safe = wireValue.Falsey() ? "LTR" : wireValue!;
        return $"WritingDirection.{safe}";
    }

    /// <summary>
    /// Maps the spec's date-format-pattern wire string (<c>"YMD"</c> /
    /// <c>"DMY"</c> / <c>"MDY"</c>) to the matching enum literal.
    /// </summary>
    /// <param name="wireValue">The wire string.</param>
    /// <returns>The matching enum literal source.</returns>
    public static string MapDateFormatPattern(string? wireValue)
    {
        var safe = wireValue.Falsey() ? "YMD" : wireValue!;
        return $"DateFormatPattern.{safe}";
    }

    /// <summary>
    /// Maps the spec's currency-acceptance-level wire string
    /// (<c>"LegalTender"</c> / <c>"WidelyAccepted"</c> / <c>"Tourist"</c>)
    /// to the matching enum literal.
    /// </summary>
    /// <param name="wireValue">The wire string.</param>
    /// <returns>The matching enum literal source.</returns>
    public static string MapCurrencyAcceptanceLevel(string? wireValue)
    {
        var safe = wireValue.Falsey() ? "LegalTender" : wireValue!;
        return $"CurrencyAcceptanceLevel.{safe}";
    }

    /// <summary>
    /// Maps an ISO 639-1 language wire code (lowercase, e.g. <c>"en"</c>) to
    /// the matching <c>LanguageCode</c> enum literal (PascalCased member
    /// name, e.g. <c>LanguageCode.En</c>). Matches the casing rule used by
    /// <see cref="EnumEmitter"/>.
    /// </summary>
    /// <param name="iso6391Code">The ISO 639-1 wire code.</param>
    /// <returns>The matching enum literal source.</returns>
    public static string MapLanguage(string? iso6391Code)
    {
        if (iso6391Code.Falsey())
            return "default";

        var c = iso6391Code!;
        var first = char.ToUpperInvariant(c[0]);
        var member = c.Length == 1 ? first.ToString() : first + c.Substring(1).ToLowerInvariant();
        return $"LanguageCode.{member}";
    }

    /// <summary>
    /// Maps a <c>GeopoliticalEntitySpec.Type</c> wire string to the matching
    /// <c>EnumEmitter</c>-emitted enum literal — passes through verbatim since
    /// the wire form already matches the C# member name (e.g.
    /// <c>"Continent"</c> → <c>GeopoliticalEntityType.Continent</c>).
    /// </summary>
    /// <param name="wireValue">The wire string.</param>
    /// <returns>The matching enum literal source.</returns>
    public static string MapGeopoliticalEntityType(string? wireValue)
    {
        var safe = wireValue.Falsey() ? "Continent" : wireValue!;
        return $"GeopoliticalEntityType.{safe}";
    }
}
