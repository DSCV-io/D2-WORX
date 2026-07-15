// -----------------------------------------------------------------------
// <copyright file="LanguageSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Spec;

using System.Collections.Generic;

/// <summary>
/// One entry parsed from <c>languages.spec.json</c>.
/// </summary>
/// <param name="Iso6391Code">ISO 639-1 two-letter code (e.g. <c>"en"</c>).</param>
/// <param name="Name">English display name.</param>
/// <param name="Endonym">Native-script name; null when unknown.</param>
/// <param name="WritingDirection">
/// Either <c>"LTR"</c> or <c>"RTL"</c>; emitters map to the matching enum.
/// </param>
/// <param name="IsSupported">
/// Derived flag — true when this language backs a selectable locale.
/// </param>
/// <param name="SpokenInCountryIso31661Alpha2Codes">
/// ISO 3166-1 alpha-2 country codes where the language is spoken.
/// </param>
internal sealed record LanguageSpec(
    string Iso6391Code,
    string Name,
    string? Endonym,
    string WritingDirection,
    bool IsSupported,
    IReadOnlyList<string> SpokenInCountryIso31661Alpha2Codes);
