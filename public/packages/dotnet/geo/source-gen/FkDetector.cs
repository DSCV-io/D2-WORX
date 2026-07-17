// -----------------------------------------------------------------------
// <copyright file="FkDetector.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen;

using System;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Naming-convention-driven classification of geo spec fields into one of
/// four categories — primitive, foreign-key code, M:M list of codes, or
/// ambiguous. Emitters consume the classification to decide between
/// scalar-FK and list-FK record fields.
/// </summary>
/// <remarks>
/// The classification rules (per the Plan):
/// <list type="bullet">
///   <item>
///     <c>*Iso31661Alpha2Code</c> / <c>*Iso4217AlphaCode</c> /
///     <c>*Iso6391Code</c> / <c>*IetfBcp47Tag</c> /
///     <c>*IanaIdentifier</c> / <c>*ShortCode</c> → single-valued FK.
///   </item>
///   <item>
///     Anything ending in <c>Codes</c> or <c>Tags</c> /
///     <c>Identifiers</c> (the plural form) → M:M list.
///   </item>
///   <item>
///     Explicit <c>fkTo</c> annotation overrides the naming convention.
///   </item>
///   <item>
///     Otherwise → primitive (string / int / bool).
///   </item>
/// </list>
/// </remarks>
internal static class FkDetector
{
    private static readonly string[] _SINGLE_SUFFIXES =
    {
        "Iso31661Alpha2Code",
        "Iso31661Alpha3Code",
        "Iso31661NumericCode",
        "Iso4217AlphaCode",
        "Iso4217NumericCode",
        "Iso6391Code",
        "IetfBcp47Tag",
        "IanaIdentifier",
        "ShortCode",
    };

    private static readonly string[] _LIST_SUFFIXES =
    {
        "Iso31661Alpha2Codes",
        "Iso31662Codes",
        "Iso4217AlphaCodes",
        "Iso6391Codes",
        "IetfBcp47Tags",
        "IanaIdentifiers",
        "ShortCodes",
    };

    /// <summary>
    /// Classifies <paramref name="fieldName"/> into one of the four
    /// categories per the rules described in the type-level remarks.
    /// </summary>
    /// <param name="fieldName">The spec field name (camelCase or PascalCase).</param>
    /// <param name="fkToAnnotation">
    /// Optional explicit <c>fkTo</c> annotation value. When non-null /
    /// non-empty, takes precedence over the naming convention and produces
    /// a deterministic <see cref="FieldClassification.ForeignKeySingle"/>.
    /// </param>
    /// <returns>The resolved classification.</returns>
    public static FieldClassification Classify(string fieldName, string? fkToAnnotation)
    {
        if (fkToAnnotation.Truthy())
            return FieldClassification.ForeignKeySingle;

        if (fieldName.Falsey())
            return FieldClassification.Primitive;

        // Check list suffixes BEFORE single suffixes — every list suffix is a
        // strict superset of a single suffix (e.g. "Codes" contains "Code").
        foreach (var suffix in _LIST_SUFFIXES)
        {
            if (fieldName.EndsWith(suffix, StringComparison.Ordinal))
                return FieldClassification.ForeignKeyList;
        }

        foreach (var suffix in _SINGLE_SUFFIXES)
        {
            if (fieldName.EndsWith(suffix, StringComparison.Ordinal))
                return FieldClassification.ForeignKeySingle;
        }

        return FieldClassification.Primitive;
    }
}
