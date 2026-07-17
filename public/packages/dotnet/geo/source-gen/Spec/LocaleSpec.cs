// -----------------------------------------------------------------------
// <copyright file="LocaleSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Spec;

/// <summary>
/// One entry parsed from <c>locales.spec.json</c>.
/// </summary>
/// <param name="IetfBcp47Tag">
/// IETF BCP 47 tag (e.g. <c>"en-US"</c>, <c>"zh-Hans-CN"</c>).
/// </param>
/// <param name="Name">English display name.</param>
/// <param name="Endonym">Native-script name; null when unknown.</param>
/// <param name="LanguageIso6391Code">ISO 639-1 backing language code.</param>
/// <param name="CountryIso31661Alpha2Code">
/// ISO 3166-1 alpha-2 country code; null for language-only tags (no
/// region subtag).
/// </param>
/// <param name="IsSelectable">
/// Derived flag — true when a matching <c>contracts/messages/{tag}.json</c>
/// file exists.
/// </param>
/// <param name="FirstDayOfWeek">
/// Day-of-week enum value (<c>"Sunday"</c> .. <c>"Saturday"</c>).
/// Denormalized from the locale's country at clean-pass time.
/// </param>
/// <param name="DecimalSeparator">Single-character decimal separator.</param>
/// <param name="ThousandsSeparator">
/// Thousands separator (zero or one character — some locales lack one).
/// </param>
/// <param name="DateFormatPattern">
/// Date-format pattern enum (<c>"DMY"</c> / <c>"MDY"</c> / <c>"YMD"</c>).
/// </param>
internal sealed record LocaleSpec(
    string IetfBcp47Tag,
    string Name,
    string? Endonym,
    string LanguageIso6391Code,
    string? CountryIso31661Alpha2Code,
    bool IsSelectable,
    string FirstDayOfWeek,
    string DecimalSeparator,
    string ThousandsSeparator,
    string DateFormatPattern);
