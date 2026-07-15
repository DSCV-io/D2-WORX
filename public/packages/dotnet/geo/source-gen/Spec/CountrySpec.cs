// -----------------------------------------------------------------------
// <copyright file="CountrySpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Spec;

using System.Collections.Generic;

/// <summary>
/// One entry parsed from <c>countries.spec.json</c>. Carries every
/// primary scalar plus the M:M-arity collections the codegen relies on.
/// FK fields use the source-data naming convention
/// (<c>*Iso{xxx}Code</c> / <c>*Iso{xxx}Codes</c>) recognized by
/// <see cref="FkDetector"/>.
/// </summary>
/// <param name="Iso31661Alpha2Code">ISO 3166-1 alpha-2 code (e.g. <c>"US"</c>).</param>
/// <param name="Iso31661Alpha3Code">ISO 3166-1 alpha-3 code (e.g. <c>"USA"</c>).</param>
/// <param name="Iso31661NumericCode">ISO 3166-1 three-digit numeric code.</param>
/// <param name="DisplayName">English display name.</param>
/// <param name="OfficialName">Long-form official name.</param>
/// <param name="EndonymDisplayName">Native-script name; null when unknown.</param>
/// <param name="PhoneNumberPrefix">
/// E.164 calling-code prefix without the leading <c>+</c>; null for
/// unassigned territories.
/// </param>
/// <param name="PhoneNumberNationalFormat">
/// libphonenumber-style positional format string (e.g.
/// <c>"$1 $2 $3"</c>); null when no canonical national format exists.
/// </param>
/// <param name="PhoneNumberMinDigits">
/// Minimum subscriber-number digit count (1..17); null when unknown.
/// </param>
/// <param name="PhoneNumberMaxDigits">
/// Maximum subscriber-number digit count (1..17); null when unknown.
/// </param>
/// <param name="FirstDayOfWeek">
/// Day-of-week enum value (<c>"Sunday"</c> .. <c>"Saturday"</c>).
/// </param>
/// <param name="WeekendStart">
/// Day-of-week enum value marking the first weekend day.
/// </param>
/// <param name="WeekendEnd">
/// Day-of-week enum value marking the last weekend day.
/// </param>
/// <param name="MeasurementSystem">
/// One of <c>"Metric"</c> / <c>"Imperial"</c> / <c>"Mixed"</c>.
/// </param>
/// <param name="PrimaryLanguageIso6391Code">
/// Primary language ISO 639-1 code; null when unspecified.
/// </param>
/// <param name="PrimaryCurrencyIso4217AlphaCode">
/// Primary currency ISO 4217 alpha code; null when unspecified.
/// </param>
/// <param name="PrimaryLocaleIetfBcp47Tag">
/// Primary IETF BCP 47 locale tag; null when unspecified.
/// </param>
/// <param name="SovereignCountryIso31661Alpha2Code">
/// Owning sovereign country alpha-2 code for dependent territories;
/// null for sovereign countries themselves.
/// </param>
/// <param name="GeopoliticalEntityShortCodes">
/// Inverse-nav M:M to <see cref="GeopoliticalEntitySpec"/> short codes.
/// </param>
/// <param name="SubdivisionIso31662Codes">
/// Owned subdivision ISO 3166-2 codes (M:M with
/// <see cref="SubdivisionSpec"/>).
/// </param>
/// <param name="TimezoneIanaIdentifiers">
/// IANA timezone identifiers covering the country
/// (M:M with <see cref="TimezoneSpec"/>).
/// </param>
/// <param name="LocaleIetfBcp47Tags">
/// Locales applicable to the country (M:M with <see cref="LocaleSpec"/>).
/// </param>
/// <param name="SpokenLanguageIso6391Codes">
/// Languages spoken in the country (M:M with <see cref="LanguageSpec"/>).
/// </param>
/// <param name="TerritoryIso31661Alpha2Codes">
/// Dependent-territory alpha-2 codes; empty for non-sovereign entries.
/// </param>
/// <param name="Currencies">
/// Per-currency acceptance levels active in the country.
/// </param>
internal sealed record CountrySpec(
    string Iso31661Alpha2Code,
    string Iso31661Alpha3Code,
    string Iso31661NumericCode,
    string DisplayName,
    string OfficialName,
    string? EndonymDisplayName,
    string? PhoneNumberPrefix,
    string? PhoneNumberNationalFormat,
    int? PhoneNumberMinDigits,
    int? PhoneNumberMaxDigits,
    string FirstDayOfWeek,
    string WeekendStart,
    string WeekendEnd,
    string MeasurementSystem,
    string? PrimaryLanguageIso6391Code,
    string? PrimaryCurrencyIso4217AlphaCode,
    string? PrimaryLocaleIetfBcp47Tag,
    string? SovereignCountryIso31661Alpha2Code,
    IReadOnlyList<string> GeopoliticalEntityShortCodes,
    IReadOnlyList<string> SubdivisionIso31662Codes,
    IReadOnlyList<string> TimezoneIanaIdentifiers,
    IReadOnlyList<string> LocaleIetfBcp47Tags,
    IReadOnlyList<string> SpokenLanguageIso6391Codes,
    IReadOnlyList<string> TerritoryIso31661Alpha2Codes,
    IReadOnlyList<CountryCurrencyAcceptance> Currencies);
