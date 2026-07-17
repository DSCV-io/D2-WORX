// -----------------------------------------------------------------------
// <copyright file="RecordShapeEmitterSnapshotTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.SourceGen.Emitters;

using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Geo.SourceGen.Emitters.Default;
using DcsvIo.D2.Geo.SourceGen.Spec;
using Xunit;

/// <summary>
/// Shape coverage for the per-catalog DATA emitters under
/// <c>DcsvIo.D2.Geo.SourceGen.Emitters.Default</c>. Each test invokes
/// the emitter against a small synthetic <see cref="GeoSpecContext"/>
/// and asserts canonical shape markers in the emitted source string
/// — single source file per catalog, namespace declaration,
/// public lookup class, dictionary initialization, wire-nav method.
/// String-equality "key markers present" snapshots are used in place
/// of a snapshot-engine because the emitted output is large (~600KB
/// for the full catalog) and the per-row content is already pinned by
/// the lookup-tests under <c>Default/Lookups/</c>.
/// </summary>
public sealed class RecordShapeEmitterSnapshotTests
{
    private static readonly SpecMetadata sr_metadata = new(
        CatalogVersion: "0.0.1-test",
        GeneratedAt: "2026-01-01T00:00:00Z",
        LastEditedAt: null,
        IsGenerated: true,
        Source: "synthetic-test");

    // §1.2 category: Domain-specific — Country emitter shape.
    [Fact]
    public void CountryDataEmitter_TwoEntries_EmitsLookupAndAccessor()
    {
        var spec = BuildContext(countries: BuildCountryEnvelope(["US", "CA"]));

        var results = CountryDataEmitter.EmitAll(spec);

        results.Should().ContainSingle();
        var src = results[0].GeneratedSource;
        src.Should().Contain("namespace DcsvIo.D2.Geo.Default");
        src.Should().Contain("public static class Countries");
        src.Should().Contain("public static class CountryLookup");
        src.Should().Contain(
            "public static readonly FrozenDictionary<CountryCode, Country> ByCode");
        src.Should().Contain("internal static void WireNav()");

        // Both seeded countries must appear as accessors.
        src.Should().Contain("public static Country US");
        src.Should().Contain("public static Country CA");
    }

    [Fact]
    public void CountryDataEmitter_EmptyContext_ReturnsEmpty()
    {
        var spec = BuildContext(countries: null);
        var results = CountryDataEmitter.EmitAll(spec);
        results.Should().BeEmpty();
    }

    // §1.2 category: Domain-specific — Subdivision emitter shape.
    [Fact]
    public void SubdivisionDataEmitter_OneSubdivision_EmitsLookupAndByCountryIndex()
    {
        var spec = BuildContext(
            countries: BuildCountryEnvelope(["US"]),
            subdivisions: BuildSubdivisionEnvelope([("US-CA", "CA", "US", "California")]));

        var results = SubdivisionDataEmitter.EmitAll(spec);

        results.Should().NotBeEmpty();
        var combined = string.Join("\n", results.Select(r => r.GeneratedSource));
        combined.Should().Contain("public static class SubdivisionLookup");
        combined.Should().Contain("ByCode");
        combined.Should().Contain("ByCountry");
        combined.Should().Contain("US-CA");
    }

    // §1.2 category: Domain-specific — Currency emitter shape.
    [Fact]
    public void CurrencyDataEmitter_OneCurrency_EmitsLookupAndAccessor()
    {
        var spec = BuildContext(currencies: BuildCurrencyEnvelope([("USD", "840")]));

        var results = CurrencyDataEmitter.EmitAll(spec);

        results.Should().ContainSingle();
        var src = results[0].GeneratedSource;
        src.Should().Contain("public static class Currencies");
        src.Should().Contain("public static class CurrencyLookup");
        src.Should().Contain("public static Currency USD");
        src.Should().Contain("internal static void WireNav()");
    }

    // §1.2 category: Domain-specific — Language emitter shape.
    [Fact]
    public void LanguageDataEmitter_OneLanguage_EmitsLookupAndAccessor()
    {
        var spec = BuildContext(languages: BuildLanguageEnvelope([("en", "English", "LTR")]));

        var results = LanguageDataEmitter.EmitAll(spec);

        results.Should().ContainSingle();
        var src = results[0].GeneratedSource;
        src.Should().Contain("public static class Languages");
        src.Should().Contain("public static class LanguageLookup");
        src.Should().Contain("internal static void WireNav()");
    }

    // §1.2 category: Domain-specific — Locale emitter shape.
    [Fact]
    public void LocaleDataEmitter_OneLocale_EmitsLookupAndNestedAccessor()
    {
        var spec = BuildContext(
            countries: BuildCountryEnvelope(["US"]),
            languages: BuildLanguageEnvelope([("en", "English", "LTR")]),
            locales: BuildLocaleEnvelope([("en-US", "en", "US")]));

        var results = LocaleDataEmitter.EmitAll(spec);

        results.Should().NotBeEmpty();
        var combined = string.Join("\n", results.Select(r => r.GeneratedSource));
        combined.Should().Contain("public static class LocaleLookup");
        combined.Should().Contain("en-US");
    }

    // §1.2 category: Domain-specific — Timezone emitter shape.
    [Fact]
    public void TimezoneDataEmitter_OneZone_EmitsLookupAndAccessor()
    {
        var spec = BuildContext(
            countries: BuildCountryEnvelope(["US"]),
            timezones: BuildTimezoneEnvelope([("America/New_York", "US", -300)]));

        var results = TimezoneDataEmitter.EmitAll(spec);

        results.Should().NotBeEmpty();
        var combined = string.Join("\n", results.Select(r => r.GeneratedSource));
        combined.Should().Contain("public static class TimezoneLookup");
        combined.Should().Contain("America/New_York");
    }

    // §1.2 category: Domain-specific — GeopoliticalEntity emitter shape.
    [Fact]
    public void GeopoliticalEntityDataEmitter_OneEntity_EmitsLookupAndMembership()
    {
        var spec = BuildContext(
            countries: BuildCountryEnvelope(["US"]),
            geopoliticalEntities: BuildGpeEnvelope(
                [("UN", "United Nations", "International", ["US"])]));

        var results = GeopoliticalEntityDataEmitter.EmitAll(spec);

        results.Should().ContainSingle();
        var src = results[0].GeneratedSource;
        src.Should().Contain("public static class GeopoliticalEntities");
        src.Should().Contain("public static class GeopoliticalEntityLookup");
        src.Should().Contain("UN");
        src.Should().Contain("internal static void WireNav()");
    }

    // §1.2 category: State-lifecycle — GeoDataInitializer emitter shape.
    [Fact]
    public void GeoDataInitializerEmitter_EmptyContext_EmitsCoordinatorWithGuardFlag()
    {
        var spec = BuildContext();

        var results = GeoDataInitializerEmitter.EmitAll(spec);

        results.Should().ContainSingle();
        var src = results[0].GeneratedSource;

        // Coordinator class + guard flag + module-initializer attribute.
        src.Should().Contain("internal static class GeoDataInitializer");
        src.Should().Contain("private static bool s_initialized;");
        src.Should().Contain("[ModuleInitializer]");
        src.Should().Contain("internal static void Initialize()");
        src.Should().Contain("if (s_initialized)");
        src.Should().Contain("s_initialized = true;");

        // First-pass static-ctor runs for every per-catalog Lookup type.
        src.Should().Contain(
            "RuntimeHelpers.RunClassConstructor(typeof(CountryLookup).TypeHandle);");
        src.Should().Contain(
            "RuntimeHelpers.RunClassConstructor(typeof(SubdivisionLookup).TypeHandle);");

        // Wire-nav step in dependency order.
        src.Should().Contain("SubdivisionLookup.WireNav();");
        src.Should().Contain("CountryLookup.WireNav();");
        src.Should().Contain("GeopoliticalEntityLookup.WireNav();");
    }

    [Fact]
    public void GeoDataInitializerEmitter_IdenticalInputs_ProduceIdenticalSource()
    {
        // Determinism — re-runs with the same input produce the same output
        // so the incremental-generator cache stays warm.
        var spec = BuildContext();

        var first = GeoDataInitializerEmitter.EmitAll(spec);
        var second = GeoDataInitializerEmitter.EmitAll(spec);

        Normalize(first[0].GeneratedSource).Should().Be(Normalize(second[0].GeneratedSource));
    }

    [Fact]
    public void CountryDataEmitter_IdenticalInputs_ProduceIdenticalSource()
    {
        var spec = BuildContext(countries: BuildCountryEnvelope(["US", "CA"]));

        var first = CountryDataEmitter.EmitAll(spec);
        var second = CountryDataEmitter.EmitAll(spec);

        Normalize(first[0].GeneratedSource).Should()
            .Be(Normalize(second[0].GeneratedSource));
    }

    // ----------------------------------------------------------------------
    // Synthetic-spec builders — minimal shapes that exercise the emitter
    // path end-to-end without requiring the real ~250-country catalog.
    // ----------------------------------------------------------------------

    private static GeoSpecContext BuildContext(
        SpecEnvelope<CountrySpec>? countries = null,
        SpecEnvelope<SubdivisionSpec>? subdivisions = null,
        SpecEnvelope<CurrencySpec>? currencies = null,
        SpecEnvelope<LanguageSpec>? languages = null,
        SpecEnvelope<LocaleSpec>? locales = null,
        SpecEnvelope<TimezoneSpec>? timezones = null,
        SpecEnvelope<GeopoliticalEntitySpec>? geopoliticalEntities = null) =>
        new(
            Countries: countries,
            Subdivisions: subdivisions,
            Currencies: currencies,
            Languages: languages,
            Locales: locales,
            Timezones: timezones,
            GeopoliticalEntities: geopoliticalEntities);

    private static SpecEnvelope<CountrySpec> BuildCountryEnvelope(IReadOnlyList<string> alpha2)
    {
        var entries = new List<CountrySpec>();
        foreach (var code in alpha2)
        {
            entries.Add(new CountrySpec(
                Iso31661Alpha2Code: code,
                Iso31661Alpha3Code: code + "X",
                Iso31661NumericCode: "000",
                DisplayName: code + " Display",
                OfficialName: code + " Official",
                EndonymDisplayName: null,
                PhoneNumberPrefix: "1",
                PhoneNumberNationalFormat: "$1",
                PhoneNumberMinDigits: 7,
                PhoneNumberMaxDigits: 10,
                FirstDayOfWeek: "Monday",
                WeekendStart: "Saturday",
                WeekendEnd: "Sunday",
                MeasurementSystem: "Metric",
                PrimaryLanguageIso6391Code: null,
                PrimaryCurrencyIso4217AlphaCode: null,
                PrimaryLocaleIetfBcp47Tag: null,
                SovereignCountryIso31661Alpha2Code: null,
                GeopoliticalEntityShortCodes: System.Array.Empty<string>(),
                SubdivisionIso31662Codes: System.Array.Empty<string>(),
                TimezoneIanaIdentifiers: System.Array.Empty<string>(),
                LocaleIetfBcp47Tags: System.Array.Empty<string>(),
                SpokenLanguageIso6391Codes: System.Array.Empty<string>(),
                TerritoryIso31661Alpha2Codes: System.Array.Empty<string>(),
                Currencies: System.Array.Empty<CountryCurrencyAcceptance>()));
        }

        return new SpecEnvelope<CountrySpec>(sr_metadata, entries);
    }

    private static SpecEnvelope<SubdivisionSpec> BuildSubdivisionEnvelope(
        IReadOnlyList<(string Iso31662, string Short, string Country, string Display)> rows)
    {
        var entries = new List<SubdivisionSpec>();
        foreach (var (iso, shortCode, country, display) in rows)
        {
            entries.Add(new SubdivisionSpec(
                Iso31662Code: iso,
                ShortCode: shortCode,
                DisplayName: display,
                OfficialName: display,
                EndonymDisplayName: null,
                CountryIso31661Alpha2Code: country,
                ParentIso31662Code: null,
                Type: "State",
                Order: 0));
        }

        return new SpecEnvelope<SubdivisionSpec>(sr_metadata, entries);
    }

    private static SpecEnvelope<CurrencySpec> BuildCurrencyEnvelope(
        IReadOnlyList<(string Alpha, string Numeric)> rows)
    {
        var entries = new List<CurrencySpec>();
        foreach (var (alpha, numeric) in rows)
        {
            entries.Add(new CurrencySpec(
                Iso4217AlphaCode: alpha,
                Iso4217NumericCode: numeric,
                DisplayName: alpha + " Display",
                DecimalPlaces: 2,
                Symbol: "$",
                IsActive: true,
                IsSupported: true));
        }

        return new SpecEnvelope<CurrencySpec>(sr_metadata, entries);
    }

    private static SpecEnvelope<LanguageSpec> BuildLanguageEnvelope(
        IReadOnlyList<(string Iso, string Name, string Direction)> rows)
    {
        var entries = new List<LanguageSpec>();
        foreach (var (iso, name, dir) in rows)
        {
            entries.Add(new LanguageSpec(
                Iso6391Code: iso,
                Name: name,
                Endonym: name,
                WritingDirection: dir,
                IsSupported: true,
                SpokenInCountryIso31661Alpha2Codes: System.Array.Empty<string>()));
        }

        return new SpecEnvelope<LanguageSpec>(sr_metadata, entries);
    }

    private static SpecEnvelope<LocaleSpec> BuildLocaleEnvelope(
        IReadOnlyList<(string Tag, string Language, string Country)> rows)
    {
        var entries = new List<LocaleSpec>();
        foreach (var (tag, lang, country) in rows)
        {
            entries.Add(new LocaleSpec(
                IetfBcp47Tag: tag,
                Name: tag,
                Endonym: tag,
                LanguageIso6391Code: lang,
                CountryIso31661Alpha2Code: country,
                IsSelectable: true,
                FirstDayOfWeek: "Sunday",
                DecimalSeparator: ".",
                ThousandsSeparator: ",",
                DateFormatPattern: "MDY"));
        }

        return new SpecEnvelope<LocaleSpec>(sr_metadata, entries);
    }

    private static SpecEnvelope<TimezoneSpec> BuildTimezoneEnvelope(
        IReadOnlyList<(string Iana, string PrimaryCountry, int StdOffset)> rows)
    {
        var entries = new List<TimezoneSpec>();
        foreach (var (iana, primary, off) in rows)
        {
            entries.Add(new TimezoneSpec(
                IanaIdentifier: iana,
                DisplayName: iana,
                CurrentStdOffsetMinutes: off,
                CurrentDstOffsetMinutes: null,
                CurrentStdAbbrev: "STD",
                CurrentDstAbbrev: null,
                CountryIso31661Alpha2Code: primary,
                CoApplicableCountryIso31661Alpha2Codes: System.Array.Empty<string>(),
                Aliases: System.Array.Empty<string>()));
        }

        return new SpecEnvelope<TimezoneSpec>(sr_metadata, entries);
    }

    private static SpecEnvelope<GeopoliticalEntitySpec> BuildGpeEnvelope(
        IReadOnlyList<(
            string ShortCode,
            string Display,
            string Type,
            IReadOnlyList<string> Members)> rows)
    {
        var entries = new List<GeopoliticalEntitySpec>();
        foreach (var (shortCode, display, type, members) in rows)
        {
            entries.Add(new GeopoliticalEntitySpec(
                ShortCode: shortCode,
                Name: display,
                Type: type,
                CountryIso31661Alpha2Codes: members));
        }

        return new SpecEnvelope<GeopoliticalEntitySpec>(sr_metadata, entries);
    }

    private static string Normalize(string s) =>
        s.Replace("\r\n", "\n").Trim();
}
