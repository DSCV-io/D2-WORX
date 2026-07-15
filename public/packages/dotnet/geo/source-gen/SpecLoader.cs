// -----------------------------------------------------------------------
// <copyright file="SpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.Geo.SourceGen.Spec;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Pure logic for parsing the geo Tier-2 JSON spec files into typed DTOs
/// under <c>DcsvIo.D2.Geo.SourceGen.Spec</c>. The loader walks each
/// <see cref="SpecFile"/> exactly once per generator run and dispatches
/// on the file name to the correct per-catalog parser. JSON-shape
/// failures surface as <see cref="DiagnosticIds.MalformedSpec"/>;
/// missing-metadata failures surface as
/// <see cref="DiagnosticIds.MissingCatalogMetadata"/>. Semantic checks
/// (FK resolution, vocabulary discipline, structural parity) belong in
/// the per-emitter sub-dispatches that consume the
/// <see cref="GeoSpecContext"/> aggregate.
/// </summary>
/// <remarks>
/// Spec mirroring under <c>Spec/</c> is permitted by the source-gen
/// internal carve-out — these DTOs do not leak across an assembly
/// boundary and serve only as a typed deserialization surface for
/// emitter consumption.
/// </remarks>
internal static class SpecLoader
{
    private const string _COUNTRIES_FILE = "countries.spec.json";
    private const string _SUBDIVISIONS_FILE = "subdivisions.spec.json";
    private const string _CURRENCIES_FILE = "currencies.spec.json";
    private const string _LANGUAGES_FILE = "languages.spec.json";
    private const string _LOCALES_FILE = "locales.spec.json";
    private const string _TIMEZONES_FILE = "timezones.spec.json";
    private const string _GEOPOLITICAL_FILE = "geopolitical-entities.spec.json";

    private const string _CATALOG_VERSION_KEY = "catalogVersion";
    private const string _GENERATED_AT_KEY = "generatedAt";
    private const string _LAST_EDITED_AT_KEY = "lastEditedAt";
    private const string _GENERATED_KEY = "$generated";
    private const string _SOURCE_KEY = "$source";
    private const string _ENTRIES_KEY = "entries";

    /// <summary>
    /// Parses every spec file present in <paramref name="specFiles"/> into the
    /// matching <see cref="SpecEnvelope{T}"/> and returns the populated
    /// <see cref="GeoSpecContext"/>. Diagnostics surfaced during parsing are
    /// appended to <paramref name="diagnostics"/>; downstream emitters
    /// degrade gracefully when a catalog property is <c>null</c>.
    /// </summary>
    /// <param name="specFiles">
    /// The pipeline's collected spec-file inputs (one per JSON file under
    /// <c>contracts/geo/</c>).
    /// </param>
    /// <param name="diagnostics">
    /// Builder that accumulates per-file parse diagnostics. The caller
    /// translates each entry to a Roslyn <c>Diagnostic</c> via the
    /// per-source-gen descriptor resolver.
    /// </param>
    /// <returns>The aggregate spec context.</returns>
    public static GeoSpecContext LoadAll(
        ImmutableArray<SpecFile> specFiles,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        SpecEnvelope<CountrySpec>? countries = null;
        SpecEnvelope<SubdivisionSpec>? subdivisions = null;
        SpecEnvelope<CurrencySpec>? currencies = null;
        SpecEnvelope<LanguageSpec>? languages = null;
        SpecEnvelope<LocaleSpec>? locales = null;
        SpecEnvelope<TimezoneSpec>? timezones = null;
        SpecEnvelope<GeopoliticalEntitySpec>? geopolitical = null;

        if (specFiles.IsDefaultOrEmpty)
        {
            return new GeoSpecContext(
                Countries: countries,
                Subdivisions: subdivisions,
                Currencies: currencies,
                Languages: languages,
                Locales: locales,
                Timezones: timezones,
                GeopoliticalEntities: geopolitical);
        }

        foreach (var file in specFiles)
        {
            var fileName = Path.GetFileName(file.Path);
            if (fileName.Falsey())
                continue;

            // Each file routes to exactly one parser. Files outside the
            // canonical seven are silently ignored — additional .spec.json
            // companions may live in contracts/geo/ in the future.
            if (string.Equals(fileName, _COUNTRIES_FILE, StringComparison.OrdinalIgnoreCase))
                countries = LoadCatalog(file, fileName, diagnostics, ParseCountry);
            else if (string.Equals(fileName, _SUBDIVISIONS_FILE, StringComparison.OrdinalIgnoreCase))
                subdivisions = LoadCatalog(file, fileName, diagnostics, ParseSubdivision);
            else if (string.Equals(fileName, _CURRENCIES_FILE, StringComparison.OrdinalIgnoreCase))
                currencies = LoadCatalog(file, fileName, diagnostics, ParseCurrency);
            else if (string.Equals(fileName, _LANGUAGES_FILE, StringComparison.OrdinalIgnoreCase))
                languages = LoadCatalog(file, fileName, diagnostics, ParseLanguage);
            else if (string.Equals(fileName, _LOCALES_FILE, StringComparison.OrdinalIgnoreCase))
                locales = LoadCatalog(file, fileName, diagnostics, ParseLocale);
            else if (string.Equals(fileName, _TIMEZONES_FILE, StringComparison.OrdinalIgnoreCase))
                timezones = LoadCatalog(file, fileName, diagnostics, ParseTimezone);
            else if (string.Equals(fileName, _GEOPOLITICAL_FILE, StringComparison.OrdinalIgnoreCase))
                geopolitical = LoadCatalog(file, fileName, diagnostics, ParseGeopoliticalEntity);
        }

        return new GeoSpecContext(
            Countries: countries,
            Subdivisions: subdivisions,
            Currencies: currencies,
            Languages: languages,
            Locales: locales,
            Timezones: timezones,
            GeopoliticalEntities: geopolitical);
    }

    private static SpecEnvelope<T>? LoadCatalog<T>(
        SpecFile file,
        string fileName,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics,
        Func<JsonElement, string, int, (T? Entry, EmitDiagnostic? Diagnostic)> entryParser)
    {
        try
        {
            using var doc = JsonDocument.Parse(file.Content);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(EmitDiagnostics.MalformedSpec(
                    fileName, $"root must be a JSON object, got {root.ValueKind}"));
                return null;
            }

            var (metadata, metadataDiag) = ParseMetadata(root, fileName);
            if (metadataDiag is not null)
            {
                diagnostics.Add(metadataDiag);
                return null;
            }

            if (!root.TryGetProperty(_ENTRIES_KEY, out var entriesElement) ||
                entriesElement.ValueKind != JsonValueKind.Array)
            {
                diagnostics.Add(EmitDiagnostics.MalformedSpec(
                    fileName, "missing required 'entries' array at root"));
                return null;
            }

            var entries = ImmutableArray.CreateBuilder<T>();
            var entryIndex = 0;
            foreach (var entryElement in entriesElement.EnumerateArray())
            {
                var (entry, entryDiag) = entryParser(entryElement, fileName, entryIndex);
                if (entryDiag is not null)
                {
                    diagnostics.Add(entryDiag);
                    entryIndex++;
                    continue;
                }

                entries.Add(entry!);
                entryIndex++;
            }

            return new SpecEnvelope<T>(metadata!, entries.ToImmutable());
        }
        catch (JsonException ex)
        {
            diagnostics.Add(EmitDiagnostics.MalformedSpec(fileName, ex.Message));
            return null;
        }
    }

    private static (SpecMetadata? Metadata, EmitDiagnostic? Diagnostic) ParseMetadata(
        JsonElement root, string fileName)
    {
        if (!root.TryGetProperty(_CATALOG_VERSION_KEY, out var versionElement) ||
            versionElement.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MissingCatalogMetadata(
                fileName, _CATALOG_VERSION_KEY));
        }

        var isGenerated = false;
        if (root.TryGetProperty(_GENERATED_KEY, out var genElement) &&
            (genElement.ValueKind == JsonValueKind.True ||
             genElement.ValueKind == JsonValueKind.False))
            isGenerated = genElement.GetBoolean();

        var source = string.Empty;
        if (root.TryGetProperty(_SOURCE_KEY, out var sourceElement) &&
            sourceElement.ValueKind == JsonValueKind.String)
            source = sourceElement.GetString() ?? string.Empty;

        string? generatedAt = null;
        if (root.TryGetProperty(_GENERATED_AT_KEY, out var genAtElement) &&
            genAtElement.ValueKind == JsonValueKind.String)
            generatedAt = genAtElement.GetString();

        string? lastEditedAt = null;
        if (root.TryGetProperty(_LAST_EDITED_AT_KEY, out var lastEditedElement) &&
            lastEditedElement.ValueKind == JsonValueKind.String)
            lastEditedAt = lastEditedElement.GetString();

        // Pipeline-derived specs MUST carry generatedAt; the hand-rolled
        // geopolitical catalog MUST carry lastEditedAt instead.
        if (isGenerated && generatedAt.Falsey())
        {
            return (null, EmitDiagnostics.MissingCatalogMetadata(
                fileName, _GENERATED_AT_KEY));
        }

        if (!isGenerated && lastEditedAt.Falsey())
        {
            return (null, EmitDiagnostics.MissingCatalogMetadata(
                fileName, _LAST_EDITED_AT_KEY));
        }

        return (new SpecMetadata(
            CatalogVersion: versionElement.GetString()!,
            GeneratedAt: generatedAt,
            LastEditedAt: lastEditedAt,
            IsGenerated: isGenerated,
            Source: source), null);
    }

    private static (CountrySpec? Entry, EmitDiagnostic? Diagnostic) ParseCountry(
        JsonElement element, string fileName, int entryIndex)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return (null, EntryMustBeObject(fileName, entryIndex, element.ValueKind));

        var alpha2 = GetRequiredString(element, "iso31661Alpha2Code", fileName, entryIndex);
        if (alpha2 is null)
            return (null, MissingField(fileName, entryIndex, "iso31661Alpha2Code"));

        var alpha3 = GetRequiredString(element, "iso31661Alpha3Code", fileName, entryIndex);
        if (alpha3 is null)
            return (null, MissingField(fileName, entryIndex, "iso31661Alpha3Code"));

        var numericCode = GetRequiredString(element, "iso31661NumericCode", fileName, entryIndex);
        if (numericCode is null)
            return (null, MissingField(fileName, entryIndex, "iso31661NumericCode"));

        var displayName = GetRequiredString(element, "displayName", fileName, entryIndex);
        if (displayName is null)
            return (null, MissingField(fileName, entryIndex, "displayName"));

        var officialName = GetRequiredString(element, "officialName", fileName, entryIndex);
        if (officialName is null)
            return (null, MissingField(fileName, entryIndex, "officialName"));

        var firstDay = GetRequiredString(element, "firstDayOfWeek", fileName, entryIndex);
        if (firstDay is null)
            return (null, MissingField(fileName, entryIndex, "firstDayOfWeek"));

        var weekendStart = GetRequiredString(element, "weekendStart", fileName, entryIndex);
        if (weekendStart is null)
            return (null, MissingField(fileName, entryIndex, "weekendStart"));

        var weekendEnd = GetRequiredString(element, "weekendEnd", fileName, entryIndex);
        if (weekendEnd is null)
            return (null, MissingField(fileName, entryIndex, "weekendEnd"));

        var measurementSystem = GetRequiredString(
            element, "measurementSystem", fileName, entryIndex);
        if (measurementSystem is null)
            return (null, MissingField(fileName, entryIndex, "measurementSystem"));

        var currenciesList = ImmutableArray.CreateBuilder<CountryCurrencyAcceptance>();
        if (element.TryGetProperty("currencies", out var currenciesElement) &&
            currenciesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var ccElement in currenciesElement.EnumerateArray())
            {
                if (ccElement.ValueKind != JsonValueKind.Object)
                    continue;

                var ccAlpha = GetOptionalString(ccElement, "iso4217AlphaCode");
                var ccLevel = GetOptionalString(ccElement, "level");
                if (ccAlpha is null || ccLevel is null)
                    continue;

                currenciesList.Add(new CountryCurrencyAcceptance(ccAlpha, ccLevel));
            }
        }

        var entry = new CountrySpec(
            Iso31661Alpha2Code: alpha2,
            Iso31661Alpha3Code: alpha3,
            Iso31661NumericCode: numericCode,
            DisplayName: displayName,
            OfficialName: officialName,
            EndonymDisplayName: GetOptionalString(element, "endonymDisplayName"),
            PhoneNumberPrefix: GetOptionalString(element, "phoneNumberPrefix"),
            PhoneNumberNationalFormat:
                GetOptionalString(element, "phoneNumberNationalFormat"),
            PhoneNumberMinDigits: GetOptionalInt(element, "phoneNumberMinDigits"),
            PhoneNumberMaxDigits: GetOptionalInt(element, "phoneNumberMaxDigits"),
            FirstDayOfWeek: firstDay,
            WeekendStart: weekendStart,
            WeekendEnd: weekendEnd,
            MeasurementSystem: measurementSystem,
            PrimaryLanguageIso6391Code: GetOptionalString(element, "primaryLanguageISO6391Code"),
            PrimaryCurrencyIso4217AlphaCode:
                GetOptionalString(element, "primaryCurrencyISO4217AlphaCode"),
            PrimaryLocaleIetfBcp47Tag: GetOptionalString(element, "primaryLocaleIETFBCP47Tag"),
            SovereignCountryIso31661Alpha2Code:
                GetOptionalString(element, "sovereignCountryISO31661Alpha2Code"),
            GeopoliticalEntityShortCodes:
                GetStringList(element, "geopoliticalEntityShortCodes"),
            SubdivisionIso31662Codes: GetStringList(element, "subdivisionISO31662Codes"),
            TimezoneIanaIdentifiers: GetStringList(element, "timezoneIanaIdentifiers"),
            LocaleIetfBcp47Tags: GetStringList(element, "localeIETFBCP47Tags"),
            SpokenLanguageIso6391Codes: GetStringList(element, "spokenLanguageISO6391Codes"),
            TerritoryIso31661Alpha2Codes: GetStringList(element, "territoryISO31661Alpha2Codes"),
            Currencies: currenciesList.ToImmutable());

        return (entry, null);
    }

    private static (SubdivisionSpec? Entry, EmitDiagnostic? Diagnostic) ParseSubdivision(
        JsonElement element, string fileName, int entryIndex)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return (null, EntryMustBeObject(fileName, entryIndex, element.ValueKind));

        var code = GetRequiredString(element, "iso31662Code", fileName, entryIndex);
        if (code is null)
            return (null, MissingField(fileName, entryIndex, "iso31662Code"));

        var shortCode = GetRequiredString(element, "shortCode", fileName, entryIndex);
        if (shortCode is null)
            return (null, MissingField(fileName, entryIndex, "shortCode"));

        var displayName = GetRequiredString(element, "displayName", fileName, entryIndex);
        if (displayName is null)
            return (null, MissingField(fileName, entryIndex, "displayName"));

        var officialName = GetRequiredString(element, "officialName", fileName, entryIndex);
        if (officialName is null)
            return (null, MissingField(fileName, entryIndex, "officialName"));

        var countryCode = GetRequiredString(
            element, "countryISO31661Alpha2Code", fileName, entryIndex);
        if (countryCode is null)
            return (null, MissingField(fileName, entryIndex, "countryISO31661Alpha2Code"));

        var entry = new SubdivisionSpec(
            Iso31662Code: code,
            ShortCode: shortCode,
            DisplayName: displayName,
            OfficialName: officialName,
            EndonymDisplayName: GetOptionalString(element, "endonymDisplayName"),
            CountryIso31661Alpha2Code: countryCode,
            ParentIso31662Code: GetOptionalString(element, "parentISO31662Code"),
            Type: GetOptionalString(element, "type"),
            Order: GetOptionalInt(element, "order"));

        return (entry, null);
    }

    private static (CurrencySpec? Entry, EmitDiagnostic? Diagnostic) ParseCurrency(
        JsonElement element, string fileName, int entryIndex)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return (null, EntryMustBeObject(fileName, entryIndex, element.ValueKind));

        var alpha = GetRequiredString(element, "iso4217AlphaCode", fileName, entryIndex);
        if (alpha is null)
            return (null, MissingField(fileName, entryIndex, "iso4217AlphaCode"));

        var displayName = GetRequiredString(element, "displayName", fileName, entryIndex);
        if (displayName is null)
            return (null, MissingField(fileName, entryIndex, "displayName"));

        var entry = new CurrencySpec(
            Iso4217AlphaCode: alpha,
            Iso4217NumericCode: GetOptionalString(element, "iso4217NumericCode"),
            DisplayName: displayName,
            DecimalPlaces: GetOptionalInt(element, "decimalPlaces") ?? 0,
            Symbol: GetOptionalString(element, "symbol"),
            IsActive: GetOptionalBool(element, "isActive") ?? false,
            IsSupported: GetOptionalBool(element, "isSupported") ?? false);

        return (entry, null);
    }

    private static (LanguageSpec? Entry, EmitDiagnostic? Diagnostic) ParseLanguage(
        JsonElement element, string fileName, int entryIndex)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return (null, EntryMustBeObject(fileName, entryIndex, element.ValueKind));

        var code = GetRequiredString(element, "iso6391Code", fileName, entryIndex);
        if (code is null)
            return (null, MissingField(fileName, entryIndex, "iso6391Code"));

        var name = GetRequiredString(element, "name", fileName, entryIndex);
        if (name is null)
            return (null, MissingField(fileName, entryIndex, "name"));

        var writingDirection = GetRequiredString(
            element, "writingDirection", fileName, entryIndex);
        if (writingDirection is null)
            return (null, MissingField(fileName, entryIndex, "writingDirection"));

        var entry = new LanguageSpec(
            Iso6391Code: code,
            Name: name,
            Endonym: GetOptionalString(element, "endonym"),
            WritingDirection: writingDirection,
            IsSupported: GetOptionalBool(element, "isSupported") ?? false,
            SpokenInCountryIso31661Alpha2Codes:
                GetStringList(element, "spokenInCountryISO31661Alpha2Codes"));

        return (entry, null);
    }

    private static (LocaleSpec? Entry, EmitDiagnostic? Diagnostic) ParseLocale(
        JsonElement element, string fileName, int entryIndex)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return (null, EntryMustBeObject(fileName, entryIndex, element.ValueKind));

        var tag = GetRequiredString(element, "ietfBcp47Tag", fileName, entryIndex);
        if (tag is null)
            return (null, MissingField(fileName, entryIndex, "ietfBcp47Tag"));

        var name = GetRequiredString(element, "name", fileName, entryIndex);
        if (name is null)
            return (null, MissingField(fileName, entryIndex, "name"));

        var languageCode = GetRequiredString(
            element, "languageISO6391Code", fileName, entryIndex);
        if (languageCode is null)
            return (null, MissingField(fileName, entryIndex, "languageISO6391Code"));

        var firstDay = GetRequiredString(element, "firstDayOfWeek", fileName, entryIndex);
        if (firstDay is null)
            return (null, MissingField(fileName, entryIndex, "firstDayOfWeek"));

        var decimalSeparator = GetRequiredString(
            element, "decimalSeparator", fileName, entryIndex);
        if (decimalSeparator is null)
            return (null, MissingField(fileName, entryIndex, "decimalSeparator"));

        // thousandsSeparator schema allows maxLength=1 with no minLength —
        // so empty string is valid. Read as optional, default to "".
        var thousandsSeparator = GetOptionalString(element, "thousandsSeparator") ?? string.Empty;

        var dateFormat = GetRequiredString(element, "dateFormatPattern", fileName, entryIndex);
        if (dateFormat is null)
            return (null, MissingField(fileName, entryIndex, "dateFormatPattern"));

        var entry = new LocaleSpec(
            IetfBcp47Tag: tag,
            Name: name,
            Endonym: GetOptionalString(element, "endonym"),
            LanguageIso6391Code: languageCode,
            CountryIso31661Alpha2Code: GetOptionalString(element, "countryISO31661Alpha2Code"),
            IsSelectable: GetOptionalBool(element, "isSelectable") ?? false,
            FirstDayOfWeek: firstDay,
            DecimalSeparator: decimalSeparator,
            ThousandsSeparator: thousandsSeparator,
            DateFormatPattern: dateFormat);

        return (entry, null);
    }

    private static (TimezoneSpec? Entry, EmitDiagnostic? Diagnostic) ParseTimezone(
        JsonElement element, string fileName, int entryIndex)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return (null, EntryMustBeObject(fileName, entryIndex, element.ValueKind));

        var iana = GetRequiredString(element, "ianaIdentifier", fileName, entryIndex);
        if (iana is null)
            return (null, MissingField(fileName, entryIndex, "ianaIdentifier"));

        var displayName = GetRequiredString(element, "displayName", fileName, entryIndex);
        if (displayName is null)
            return (null, MissingField(fileName, entryIndex, "displayName"));

        var stdOffset = GetOptionalInt(element, "currentStdOffsetMinutes");
        if (stdOffset is null)
            return (null, MissingField(fileName, entryIndex, "currentStdOffsetMinutes"));

        var entry = new TimezoneSpec(
            IanaIdentifier: iana,
            DisplayName: displayName,
            CurrentStdOffsetMinutes: stdOffset.Value,
            CurrentDstOffsetMinutes: GetOptionalInt(element, "currentDstOffsetMinutes"),
            CurrentStdAbbrev: GetOptionalString(element, "currentStdAbbrev") ?? string.Empty,
            CurrentDstAbbrev: GetOptionalString(element, "currentDstAbbrev"),
            CountryIso31661Alpha2Code: GetOptionalString(element, "countryISO31661Alpha2Code"),
            CoApplicableCountryIso31661Alpha2Codes:
                GetStringList(element, "coApplicableCountryISO31661Alpha2Codes"),
            Aliases: GetStringList(element, "aliases"));

        return (entry, null);
    }

    private static (GeopoliticalEntitySpec? Entry, EmitDiagnostic? Diagnostic)
        ParseGeopoliticalEntity(JsonElement element, string fileName, int entryIndex)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return (null, EntryMustBeObject(fileName, entryIndex, element.ValueKind));

        var shortCode = GetRequiredString(element, "shortCode", fileName, entryIndex);
        if (shortCode is null)
            return (null, MissingField(fileName, entryIndex, "shortCode"));

        var name = GetRequiredString(element, "name", fileName, entryIndex);
        if (name is null)
            return (null, MissingField(fileName, entryIndex, "name"));

        var type = GetRequiredString(element, "type", fileName, entryIndex);
        if (type is null)
            return (null, MissingField(fileName, entryIndex, "type"));

        var entry = new GeopoliticalEntitySpec(
            ShortCode: shortCode,
            Name: name,
            Type: type,
            CountryIso31661Alpha2Codes: GetStringList(element, "countryISO31661Alpha2Codes"));

        return (entry, null);
    }

    private static string? GetRequiredString(
        JsonElement element, string propertyName, string fileName, int entryIndex)
    {
        _ = fileName;
        _ = entryIndex;
        if (!element.TryGetProperty(propertyName, out var prop) ||
            prop.ValueKind != JsonValueKind.String)
            return null;

        var value = prop.GetString();
        return value.Falsey() ? null : value;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return null;

        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }

    private static int? GetOptionalInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return null;

        if (prop.ValueKind != JsonValueKind.Number)
            return null;

        return prop.TryGetInt32(out var value) ? value : null;
    }

    private static bool? GetOptionalBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return null;

        if (prop.ValueKind == JsonValueKind.True) return true;
        if (prop.ValueKind == JsonValueKind.False) return false;
        return null;
    }

    private static IReadOnlyList<string> GetStringList(
        JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) ||
            prop.ValueKind != JsonValueKind.Array)
            return ImmutableArray<string>.Empty;

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                continue;

            var value = item.GetString();
            if (value.Truthy())
                builder.Add(value!);
        }

        return builder.ToImmutable();
    }

    private static EmitDiagnostic EntryMustBeObject(
        string fileName, int entryIndex, JsonValueKind actual) =>
        EmitDiagnostics.MalformedSpec(
            fileName, $"entries[{entryIndex}] must be a JSON object, got {actual}");

    private static EmitDiagnostic MissingField(
        string fileName, int entryIndex, string fieldName) =>
        EmitDiagnostics.MalformedSpec(
            fileName, $"entries[{entryIndex}] missing required string '{fieldName}'");
}
