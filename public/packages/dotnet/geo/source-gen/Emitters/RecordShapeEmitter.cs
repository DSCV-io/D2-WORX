// -----------------------------------------------------------------------
// <copyright file="RecordShapeEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Emitters;

using System.Collections.Immutable;
using System.Text;
using DcsvIo.D2.Geo.SourceGen.Spec;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Emits a single denormalized record per geo catalog: Country,
/// Subdivision, Currency, Language, Locale, Timezone, GeopoliticalEntity.
/// Identifier types carry the <c>*Code</c> suffix (CountryCode,
/// SubdivisionCode, …); the bare singular name is the record. Nav
/// properties reference the same single record type recursively — the
/// natural shape for SSR + BFF one-level navigation. Universal dual
/// representation: every relationship carries BOTH a typed code field
/// (O(1) <c>Contains</c> via <c>FrozenSet</c> / <c>ReadonlySet</c>) AND
/// a nav-record field (ordered iteration). PK field names describe what
/// the value IS (<c>Iso31661Alpha2Code</c>, <c>IetfBcp47Tag</c>,
/// <c>IanaName</c>); FK fields prefix with the relationship semantics
/// (<c>Primary</c>, <c>Sovereign</c>, <c>Territory</c>, <c>Member</c>,
/// <c>SpokenIn</c>, <c>AcceptedIn</c>, <c>CoApplicable</c>).
/// </summary>
/// <remarks>
/// Nav properties on every record use <c>get; internal set;</c> with
/// sensible defaults (<c>null</c> for nullable single primaries; empty
/// frozen sets / empty arrays for collections) — the friend assembly
/// <c>DcsvIo.D2.Geo.Default</c> mutates them in the wire-nav step of the
/// two-pass populate pattern. Scalar required fields stay
/// <c>required init</c>. From any assembly other than
/// <c>DcsvIo.D2.Geo.Default</c>, nav properties are compile-time
/// unwritable so record immutability holds.
/// </remarks>
internal static class RecordShapeEmitter
{
    private const string _NAMESPACE = EmitterHelpers.AbstractionsNamespace;

    /// <summary>
    /// Emits the eight record-shape files (one per catalog plus the
    /// supporting <c>CountryCurrencyAcceptance</c> record) plus the two
    /// catalog-shared fixed enums (<c>GeoDayOfWeek</c>,
    /// <c>MeasurementSystem</c>).
    /// </summary>
    /// <param name="context">The aggregate spec context (unused — the
    /// record SHAPES are spec-shape-only, independent of per-entry data;
    /// the per-instance data emission lives in the data emitters under
    /// <c>DcsvIo.D2.Geo.Default</c>).</param>
    /// <returns>The per-record emit results.</returns>
    public static ImmutableArray<EmitResult> EmitAll(GeoSpecContext context)
    {
        _ = context;

        var results = ImmutableArray.CreateBuilder<EmitResult>();
        results.Add(EmitCountryCurrencyAcceptance());
        results.Add(EmitDayOfWeekEnum());
        results.Add(EmitMeasurementSystemEnum());

        results.Add(EmitCountry());
        results.Add(EmitSubdivision());
        results.Add(EmitCurrency());
        results.Add(EmitLanguage());
        results.Add(EmitLocale());
        results.Add(EmitTimezone());
        results.Add(EmitGeopoliticalEntity());

        return results.ToImmutable();
    }

    private static EmitResult EmitCountryCurrencyAcceptance()
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Per-country currency acceptance binding — pairs a typed");
        sb.AppendLine(
            "/// <see cref=\"CurrencyCode\"/> identifier with the acceptance");
        sb.AppendLine(
            "/// level (LegalTender / WidelyAccepted / Tourist) AND the resolved");
        sb.AppendLine(
            "/// <see cref=\"Currency\"/> nav record (wired in the wire-nav step");
        sb.AppendLine("/// of the two-pass populate pattern).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public sealed record CountryCurrencyAcceptance");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>ISO 4217 alpha identifier (e.g. <c>CurrencyCode.USD</c>).</summary>");
        sb.AppendLine("    public required CurrencyCode Iso4217AlphaCode { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Acceptance classification — legal tender / widely / "
            + "tourist.</summary>");
        sb.AppendLine("    public required CurrencyAcceptanceLevel Level { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Resolved currency nav record. Wired by the friend "
            + "assembly's wire-nav step.</summary>");
        sb.AppendLine("    public Currency? Currency { get; internal set; }");
        sb.AppendLine("}");

        return MakeResult("CountryCurrencyAcceptance.g.cs", sb);
    }

    private static EmitResult EmitDayOfWeekEnum()
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Day-of-week enumeration used by <c>Country.FirstDayOfWeek</c>,");
        sb.AppendLine(
            "/// <c>Country.WeekendStart</c> / <c>WeekendEnd</c>, and");
        sb.AppendLine(
            "/// <c>Locale.FirstDayOfWeek</c>. Wire form is the canonical name");
        sb.AppendLine(
            "/// (<c>\"Monday\"</c>, <c>\"Tuesday\"</c>, ...). Decoupled from");
        sb.AppendLine(
            "/// <see cref=\"System.DayOfWeek\"/> so the wire format stays catalog-controlled");
        sb.AppendLine("/// and never accidentally drifts with BCL changes.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[JsonConverter(typeof(JsonStringEnumConverter))]");
        sb.AppendLine("public enum GeoDayOfWeek : byte");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Sunday.</summary>");
        sb.AppendLine("    Sunday = 0,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Monday.</summary>");
        sb.AppendLine("    Monday = 1,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Tuesday.</summary>");
        sb.AppendLine("    Tuesday = 2,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Wednesday.</summary>");
        sb.AppendLine("    Wednesday = 3,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Thursday.</summary>");
        sb.AppendLine("    Thursday = 4,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Friday.</summary>");
        sb.AppendLine("    Friday = 5,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Saturday.</summary>");
        sb.AppendLine("    Saturday = 6,");
        sb.AppendLine("}");

        return MakeResult("GeoDayOfWeek.g.cs", sb);
    }

    private static EmitResult EmitMeasurementSystemEnum()
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Measurement-system classification carried by <c>Country</c>.");
        sb.AppendLine(
            "/// UK is the Mixed odd-one-out (miles for roads but Celsius for");
        sb.AppendLine("/// weather, kg for groceries).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[JsonConverter(typeof(JsonStringEnumConverter))]");
        sb.AppendLine("public enum MeasurementSystem : byte");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Metric (most of the world).</summary>");
        sb.AppendLine("    Metric = 0,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Imperial (US: miles, °F, lbs).</summary>");
        sb.AppendLine("    Imperial = 1,");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Mixed (UK: miles for roads, Celsius for weather, kg "
            + "for groceries).</summary>");
        sb.AppendLine("    Mixed = 2,");
        sb.AppendLine("}");

        return MakeResult("MeasurementSystem.g.cs", sb);
    }

    private static EmitResult EmitCountry()
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Country record — single shape per entity. Universal dual-rep");
        sb.AppendLine(
            "/// for every relationship: typed code field (<c>FrozenSet</c>-backed");
        sb.AppendLine(
            "/// for O(1) <c>Contains</c>) AND nav record list. Primary single navs");
        sb.AppendLine(
            "/// (<c>PrimaryLanguage</c> / <c>PrimaryCurrency</c> / <c>PrimaryLocale</c>");
        sb.AppendLine(
            "/// / <c>SovereignCountry</c>) are nullable for uninhabited territories");
        sb.AppendLine(
            "/// (AQ Antarctica, BV Bouvet, HM Heard &amp; McDonald) and for");
        sb.AppendLine("/// sovereign countries lacking a parent.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public sealed record Country");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>ISO 3166-1 alpha-2 typed identifier (PK).</summary>");
        sb.AppendLine("    public required CountryCode Iso31661Alpha2Code { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>ISO 3166-1 alpha-3 string (e.g. \"USA\").</summary>");
        sb.AppendLine("    public required string Iso31661Alpha3Code { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>ISO 3166-1 three-digit numeric code.</summary>");
        sb.AppendLine("    public required string Iso31661NumericCode { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>English short display name.</summary>");
        sb.AppendLine("    public required string DisplayName { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>English official long name.</summary>");
        sb.AppendLine("    public required string OfficialName { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Native-script short name (e.g. \"日本\" for JP).</summary>");
        sb.AppendLine("    public required string EndonymDisplayName { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Native-script official name.</summary>");
        sb.AppendLine("    public required string EndonymOfficialName { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>E.164 calling code (e.g. \"1\", \"44\", \"81\").</summary>");
        sb.AppendLine("    public required string PhoneNumberPrefix { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>libphonenumber-style display template (e.g. \"$1 $2 "
            + "$3\").</summary>");
        sb.AppendLine("    public required string PhoneNumberNationalFormat { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Minimum subscriber-number digit count; null for "
            + "territories lacking subscriber data.</summary>");
        sb.AppendLine("    public int? PhoneNumberMinDigits { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Maximum subscriber-number digit count.</summary>");
        sb.AppendLine("    public required int PhoneNumberMaxDigits { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Calendar first day of the week (per CLDR).</summary>");
        sb.AppendLine("    public required GeoDayOfWeek FirstDayOfWeek { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>First weekend day (per CLDR).</summary>");
        sb.AppendLine("    public required GeoDayOfWeek WeekendStart { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Last weekend day (per CLDR).</summary>");
        sb.AppendLine("    public required GeoDayOfWeek WeekendEnd { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Measurement system in everyday use (per CLDR).</summary>");
        sb.AppendLine("    public required MeasurementSystem MeasurementSystem { get; init; }");
        sb.AppendLine();

        // ---- Single FK code reps + nav reps (primary nullable) ----
        sb.AppendLine(
            "    /// <summary>Primary majority-population language code; null for "
            + "uninhabited territories. Code rep paired with "
            + "<see cref=\"PrimaryLanguage\"/>.</summary>");
        sb.AppendLine("    public LanguageCode? PrimaryLanguageIso6391Code { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Primary majority-population language record; null for "
            + "uninhabited territories. Wired in the second pass of the two-pass "
            + "populate pattern.</summary>");
        sb.AppendLine("    public Language? PrimaryLanguage { get; internal set; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Primary currency code; null for uninhabited "
            + "territories. Code rep paired with "
            + "<see cref=\"PrimaryCurrency\"/>.</summary>");
        sb.AppendLine("    public CurrencyCode? PrimaryCurrencyIso4217AlphaCode { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Primary currency record; null for uninhabited "
            + "territories. Wired in the second pass.</summary>");
        sb.AppendLine("    public Currency? PrimaryCurrency { get; internal set; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Primary locale tag; null for uninhabited territories. "
            + "Code rep paired with <see cref=\"PrimaryLocale\"/>.</summary>");
        sb.AppendLine("    public LocaleCode? PrimaryLocaleIetfBcp47Tag { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Primary locale record; null for uninhabited "
            + "territories. Wired in the second pass.</summary>");
        sb.AppendLine("    public Locale? PrimaryLocale { get; internal set; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Owning sovereign country code for dependent "
            + "territories; null for sovereigns themselves. Code rep paired with "
            + "<see cref=\"SovereignCountry\"/>.</summary>");
        sb.AppendLine("    public CountryCode? SovereignCountryIso31661Alpha2Code { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Owning sovereign country record; null for sovereigns "
            + "themselves. Wired in the second pass.</summary>");
        sb.AppendLine("    public Country? SovereignCountry { get; internal set; }");
        sb.AppendLine();

        // ---- Set FK code reps (FrozenSet<TCode>) + nav reps (IReadOnlyList<TRecord>) ----
        sb.AppendLine(
            "    /// <summary>Owned dependent-territory country codes — O(1) "
            + "<c>Contains</c> over the set. Code rep paired with "
            + "<see cref=\"Territories\"/>.</summary>");
        sb.AppendLine(
            "    public required IReadOnlySet<CountryCode> "
            + "TerritoryIso31661Alpha2Codes { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Owned dependent-territory records. Wired in the second "
            + "pass.</summary>");
        sb.AppendLine(
            "    public IReadOnlyList<Country> Territories { get; internal set; } = [];");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Subdivision codes (states / provinces / regions) of "
            + "this country. Code rep paired with "
            + "<see cref=\"Subdivisions\"/>.</summary>");
        sb.AppendLine(
            "    public IReadOnlySet<SubdivisionCode> SubdivisionIso31662Codes "
            + "{ get; internal set; } = FrozenSet<SubdivisionCode>.Empty;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Subdivision records (states / provinces / regions) of "
            + "this country. Wired in the second pass.</summary>");
        sb.AppendLine(
            "    public IReadOnlyList<Subdivision> Subdivisions { get; internal set; } = [];");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>All locale tags used in this country. Code rep paired "
            + "with <see cref=\"Locales\"/>.</summary>");
        sb.AppendLine(
            "    public required IReadOnlySet<LocaleCode> LocaleIetfBcp47Tags { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>All locale records used in this country (full CLDR "
            + "catalog slice). Wired in the second pass.</summary>");
        sb.AppendLine(
            "    public IReadOnlyList<Locale> Locales { get; internal set; } = [];");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Geopolitical-entity short codes this country belongs "
            + "to. Code rep paired with "
            + "<see cref=\"GeopoliticalEntities\"/>.</summary>");
        sb.AppendLine(
            "    public required IReadOnlySet<GeopoliticalEntityCode> "
            + "GeopoliticalEntityShortCodes { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Geopolitical-entity records this country belongs to. "
            + "Wired in the second pass.</summary>");
        sb.AppendLine(
            "    public IReadOnlyList<GeopoliticalEntity> GeopoliticalEntities "
            + "{ get; internal set; } = [];");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Accepted currency codes (any acceptance level). Flat "
            + "code set parallel to the nested <see cref=\"Currencies\"/> "
            + "list.</summary>");
        sb.AppendLine(
            "    public required IReadOnlySet<CurrencyCode> "
            + "CurrencyIso4217AlphaCodes { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>All accepted currencies with acceptance level "
            + "(LegalTender + WidelyAccepted + Tourist).</summary>");
        sb.AppendLine(
            "    public required IReadOnlyList<CountryCurrencyAcceptance> "
            + "Currencies { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Append-only deprecation marker.</summary>");
        sb.AppendLine("    public DeprecationInfo? Deprecation { get; init; }");
        sb.AppendLine("}");

        return MakeResult("Country.g.cs", sb);
    }

    private static EmitResult EmitSubdivision()
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Subdivision record — ISO 3166-2 administrative subdivision. The");
        sb.AppendLine(
            "/// vocabulary discipline is enforced — every field name says");
        sb.AppendLine(
            "/// <c>subdivision</c> (never <c>region</c> / <c>state</c> /");
        sb.AppendLine(
            "/// <c>province</c>); display labels like <c>\"State\"</c> live on the");
        sb.AppendLine("/// <c>Type</c> field. Endonym fields drive the name-resolver's");
        sb.AppendLine("/// match against native-script names from 3rd-party text data.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public sealed record Subdivision");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>ISO 3166-2 typed subdivision identifier (PK, e.g. "
            + "\"US-CA\").</summary>");
        sb.AppendLine("    public required SubdivisionCode Iso31662Code { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Country-local short identifier (segment after the "
            + "hyphen).</summary>");
        sb.AppendLine("    public required string ShortCode { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>English display name.</summary>");
        sb.AppendLine("    public required string DisplayName { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>English official name.</summary>");
        sb.AppendLine("    public required string OfficialName { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Native-script display name (e.g. \"Bayern\" for "
            + "DE-BY).</summary>");
        sb.AppendLine("    public required string EndonymDisplayName { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Native-script official name.</summary>");
        sb.AppendLine("    public required string EndonymOfficialName { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Owning country code. Code rep paired with "
            + "<see cref=\"Country\"/>.</summary>");
        sb.AppendLine("    public required CountryCode CountryIso31661Alpha2Code { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Owning country record. Wired in the second pass of the "
            + "two-pass populate pattern.</summary>");
        sb.AppendLine("    public Country? Country { get; internal set; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Parent subdivision code when nested under another "
            + "subdivision (e.g. county under state); null at the top "
            + "administrative level. Code rep paired with "
            + "<see cref=\"ParentSubdivision\"/>.</summary>");
        sb.AppendLine("    public SubdivisionCode? ParentSubdivisionIso31662Code { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Parent subdivision record; null at the top "
            + "administrative level. Wired in the second pass.</summary>");
        sb.AppendLine("    public Subdivision? ParentSubdivision { get; internal set; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Display label (\"State\", \"Province\", \"Parish\", "
            + "...) — display data only.</summary>");
        sb.AppendLine("    public required string Type { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Append-only deprecation marker.</summary>");
        sb.AppendLine("    public DeprecationInfo? Deprecation { get; init; }");
        sb.AppendLine("}");

        return MakeResult("Subdivision.g.cs", sb);
    }

    private static EmitResult EmitCurrency()
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine(
            "/// <summary>Currency record — formatting metadata + supported flag + "
            + "reverse-nav to accepting countries.</summary>");
        sb.AppendLine("public sealed record Currency");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>Typed ISO 4217 alpha identifier (PK, e.g. "
            + "<c>CurrencyCode.USD</c>).</summary>");
        sb.AppendLine("    public required CurrencyCode Iso4217AlphaCode { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>ISO 4217 numeric string (e.g. \"840\").</summary>");
        sb.AppendLine("    public required string Iso4217NumericCode { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>English display name (e.g. \"US Dollar\").</summary>");
        sb.AppendLine("    public required string DisplayName { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>English official name.</summary>");
        sb.AppendLine("    public required string OfficialName { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Fractional-unit digit count (0 = JPY; 2 = USD; 3 = "
            + "BHD).</summary>");
        sb.AppendLine("    public required int DecimalPlaces { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Unicode symbol (e.g. \"$\", \"¥\", \"€\").</summary>");
        sb.AppendLine("    public required string Symbol { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>True when the framework treats this currency as "
            + "user-selectable.</summary>");
        sb.AppendLine("    public required bool IsSupported { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Country codes accepting this currency (any acceptance "
            + "level — LegalTender + WidelyAccepted + Tourist). Code rep paired "
            + "with <see cref=\"AcceptedInCountries\"/>.</summary>");
        sb.AppendLine(
            "    public IReadOnlySet<CountryCode> "
            + "AcceptedInCountryIso31661Alpha2Codes { get; internal set; } = "
            + "FrozenSet<CountryCode>.Empty;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Country records accepting this currency. Wired in the "
            + "second pass of the two-pass populate pattern.</summary>");
        sb.AppendLine(
            "    public IReadOnlyList<Country> AcceptedInCountries { get; internal set; } = [];");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Append-only deprecation marker.</summary>");
        sb.AppendLine("    public DeprecationInfo? Deprecation { get; init; }");
        sb.AppendLine("}");

        return MakeResult("Currency.g.cs", sb);
    }

    private static EmitResult EmitLanguage()
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine(
            "/// <summary>Language record — endonym + writing direction + supported "
            + "flag + reverse navs to countries / locales.</summary>");
        sb.AppendLine("public sealed record Language");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>Typed ISO 639-1 identifier (PK, e.g. "
            + "<c>LanguageCode.En</c>).</summary>");
        sb.AppendLine("    public required LanguageCode Iso6391Code { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>English language display name.</summary>");
        sb.AppendLine("    public required string DisplayName { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Native-script language name (e.g. \"日本語\" for "
            + "ja).</summary>");
        sb.AppendLine("    public required string Endonym { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>LTR vs RTL — drives UI container flips.</summary>");
        sb.AppendLine("    public required WritingDirection WritingDirection { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>True when the framework treats this language as "
            + "user-selectable.</summary>");
        sb.AppendLine("    public required bool IsSupported { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Country codes where this language is the "
            + "majority-population primary. Code rep paired with "
            + "<see cref=\"SpokenInCountries\"/>.</summary>");
        sb.AppendLine(
            "    public IReadOnlySet<CountryCode> "
            + "SpokenInCountryIso31661Alpha2Codes { get; internal set; } = "
            + "FrozenSet<CountryCode>.Empty;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Country records where this language is the "
            + "majority-population primary. Wired in the second pass.</summary>");
        sb.AppendLine(
            "    public IReadOnlyList<Country> SpokenInCountries { get; internal set; } = [];");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Locale tags whose backing language is this entry. Code "
            + "rep paired with <see cref=\"Locales\"/>.</summary>");
        sb.AppendLine(
            "    public IReadOnlySet<LocaleCode> LocaleIetfBcp47Tags { get; internal "
            + "set; } = FrozenSet<LocaleCode>.Empty;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Locale records whose backing language is this entry. "
            + "Wired in the second pass.</summary>");
        sb.AppendLine(
            "    public IReadOnlyList<Locale> Locales { get; internal set; } = [];");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Append-only deprecation marker.</summary>");
        sb.AppendLine("    public DeprecationInfo? Deprecation { get; init; }");
        sb.AppendLine("}");

        return MakeResult("Language.g.cs", sb);
    }

    private static EmitResult EmitLocale()
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine(
            "/// <summary>Locale record — formatting metadata + recursive "
            + "language/country nav.</summary>");
        sb.AppendLine("public sealed record Locale");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>Typed IETF BCP 47 locale identifier (PK, e.g. "
            + "<c>\"en-US\"</c>).</summary>");
        sb.AppendLine("    public required LocaleCode IetfBcp47Tag { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>English locale display name.</summary>");
        sb.AppendLine("    public required string DisplayName { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Native-script locale display name.</summary>");
        sb.AppendLine("    public required string Endonym { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Backing language code; null when locale references a "
            + "3-letter ISO 639-2/639-3 code outside the LanguageCode enum. Code "
            + "rep paired with <see cref=\"Language\"/>.</summary>");
        sb.AppendLine("    public LanguageCode? LanguageIso6391Code { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Backing language record; null when locale references a "
            + "3-letter ISO 639-2/639-3 code outside the LanguageCode enum. Wired "
            + "in the second pass.</summary>");
        sb.AppendLine("    public Language? Language { get; internal set; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Backing country code; null for language-only tags (no "
            + "region subtag). Code rep paired with "
            + "<see cref=\"Country\"/>.</summary>");
        sb.AppendLine("    public CountryCode? CountryIso31661Alpha2Code { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Backing country record; null for language-only tags. "
            + "Wired in the second pass.</summary>");
        sb.AppendLine("    public Country? Country { get; internal set; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>True when a matching contracts/messages/{tag}.json "
            + "file exists.</summary>");
        sb.AppendLine("    public required bool IsSelectable { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Calendar first day of the week per CLDR.</summary>");
        sb.AppendLine("    public required GeoDayOfWeek FirstDayOfWeek { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Single-character decimal separator (e.g. \".\" or "
            + "\",\").</summary>");
        sb.AppendLine("    public required string DecimalSeparator { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Thousands separator (zero or one char — some locales "
            + "lack one).</summary>");
        sb.AppendLine("    public required string ThousandsSeparator { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Date-component ordering for the human-readable "
            + "form.</summary>");
        sb.AppendLine("    public required DateFormatPattern DateFormatPattern { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Append-only deprecation marker.</summary>");
        sb.AppendLine("    public DeprecationInfo? Deprecation { get; init; }");
        sb.AppendLine("}");

        return MakeResult("Locale.g.cs", sb);
    }

    private static EmitResult EmitTimezone()
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine(
            "/// <summary>Timezone record — offsets + abbreviations + localized "
            + "names + aliases + country navs.</summary>");
        sb.AppendLine("public sealed record Timezone");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>Typed IANA timezone identifier (PK, e.g. "
            + "<c>\"America/New_York\"</c>).</summary>");
        sb.AppendLine("    public required TimezoneCode IanaName { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>English display name.</summary>");
        sb.AppendLine("    public required string DisplayName { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Localized display names keyed by ISO 639-1 (en, es, "
            + "fr, ... for the supported languages).</summary>");
        sb.AppendLine(
            "    public required IReadOnlyDictionary<string, string> "
            + "LocalizedDisplayNames { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Standard-time offset from UTC in minutes (e.g. -300 "
            + "for EST).</summary>");
        sb.AppendLine("    public required int CurrentStdOffsetMinutes { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Daylight-saving offset from UTC in minutes; null for "
            + "zones without DST.</summary>");
        sb.AppendLine("    public int? CurrentDstOffsetMinutes { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Standard-time abbreviation (e.g. \"EST\").</summary>");
        sb.AppendLine("    public required string CurrentStdAbbrev { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Daylight-saving abbreviation; null when no DST.</summary>");
        sb.AppendLine("    public string? CurrentDstAbbrev { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Primary owning country code; null for Etc/* "
            + "pseudo-zones. Code rep paired with "
            + "<see cref=\"PrimaryCountry\"/>.</summary>");
        sb.AppendLine("    public CountryCode? PrimaryCountryIso31661Alpha2Code { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Primary owning country record; null for Etc/* "
            + "pseudo-zones. Wired in the second pass.</summary>");
        sb.AppendLine("    public Country? PrimaryCountry { get; internal set; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Other countries sharing this IANA zone (beyond the "
            + "primary). Code rep paired with "
            + "<see cref=\"CoApplicableCountries\"/>.</summary>");
        sb.AppendLine(
            "    public required IReadOnlySet<CountryCode> "
            + "CoApplicableCountryIso31661Alpha2Codes { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Other country records sharing this IANA zone. Wired in "
            + "the second pass.</summary>");
        sb.AppendLine(
            "    public IReadOnlyList<Country> CoApplicableCountries { get; internal set; } = [];");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Curated UI subset selectability flag.</summary>");
        sb.AppendLine("    public required bool Selectable { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Deprecated / linked IANA identifiers that resolve to "
            + "this zone.</summary>");
        sb.AppendLine("    public required IReadOnlyList<string> Aliases { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Append-only deprecation marker.</summary>");
        sb.AppendLine("    public DeprecationInfo? Deprecation { get; init; }");
        sb.AppendLine("}");

        return MakeResult("Timezone.g.cs", sb);
    }

    private static EmitResult EmitGeopoliticalEntity()
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine(
            "/// <summary>GeopoliticalEntity record — member countries with O(1) "
            + "<c>Contains</c> over the typed code set.</summary>");
        sb.AppendLine("public sealed record GeopoliticalEntity");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>Typed identifier (PK, e.g. <c>\"NATO\"</c>, "
            + "<c>\"EU\"</c>).</summary>");
        sb.AppendLine("    public required GeopoliticalEntityCode ShortCode { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>English display name.</summary>");
        sb.AppendLine("    public required string DisplayName { get; init; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Type classification — drives filtering UI.</summary>");
        sb.AppendLine("    public required GeopoliticalEntityType Type { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Member country codes — O(1) <c>Contains</c> over the "
            + "set. Code rep paired with "
            + "<see cref=\"MemberCountries\"/>.</summary>");
        sb.AppendLine(
            "    public required IReadOnlySet<CountryCode> "
            + "MemberCountryIso31661Alpha2Codes { get; init; }");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Member country records. Wired in the second pass of "
            + "the two-pass populate pattern.</summary>");
        sb.AppendLine(
            "    public IReadOnlyList<Country> MemberCountries { get; internal set; } = [];");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Append-only deprecation marker.</summary>");
        sb.AppendLine("    public DeprecationInfo? Deprecation { get; init; }");
        sb.AppendLine("}");

        return MakeResult("GeopoliticalEntity.g.cs", sb);
    }

    private static EmitResult MakeResult(string hintName, StringBuilder sb)
    {
        return new EmitResult(
            HintName: hintName,
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static void AppendFileHeader(StringBuilder sb) =>
        EmitterHelpers.AppendFileHeader(sb);
}
