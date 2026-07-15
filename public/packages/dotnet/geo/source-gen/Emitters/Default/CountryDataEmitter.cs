// -----------------------------------------------------------------------
// <copyright file="CountryDataEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Emitters.Default;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using DcsvIo.D2.Geo.SourceGen.Spec;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Emits the per-country DATA — single shape per entity + cycle-resolution
/// via friend-assembly <c>internal set</c> + two-pass populate. Output is a
/// single file (<c>CountryLookup.g.cs</c>) carrying:
/// <list type="bullet">
///   <item><description>
///     Static <c>Countries</c> accessor (<c>Countries.US</c> → <c>Country</c>).
///   </description></item>
///   <item><description>
///     Static <c>CountryLookup</c> class with the FrozenDictionary indexes.
///   </description></item>
///   <item><description>
///     <c>WireNav()</c> wire-nav method invoked by the coordinator.
///   </description></item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// First pass lives in <c>CountryLookup</c>'s static constructor. Every
/// <c>Country</c> record is materialized with scalar required-init fields
/// AND every code-rep field populated from spec data (typed codes, no
/// runtime resolution required). Nav-rep fields (PrimaryLanguage /
/// PrimaryCurrency / PrimaryLocale / SovereignCountry / Locales /
/// Territories / GeopoliticalEntities / Subdivisions) start at
/// <c>null</c> or <c>[]</c>.
/// </para>
/// <para>
/// Second pass lives in <c>internal static void WireNav()</c> which the
/// <c>GeoDataInitializer</c> module initializer invokes after every
/// catalog's first-pass static ctor has run. WireNav() walks the spec
/// entries again and mutates the nav-rep properties via the friend-assembly
/// <c>internal set</c> accessors. The code reps are already valid from the
/// first pass — no second-pass code-rep mutation is needed.
/// </para>
/// <para>
/// Three sovereign-but-uninhabited territories (AQ Antarctica, BV Bouvet
/// Island, HM Heard &amp; McDonald) carry <c>null</c> in the spec for
/// <c>primaryLanguageISO6391Code</c> / <c>primaryCurrencyISO4217AlphaCode</c>
/// / <c>primaryLocaleIETFBCP47Tag</c>. The nullable single-primary nav
/// properties on the <c>Country</c> record stay <c>null</c> for these
/// three countries — every other country resolves through wire-nav.
/// </para>
/// </remarks>
internal static class CountryDataEmitter
{
    private const string _NAMESPACE = DefaultEmitterHelpers.DefaultNamespace;

    /// <summary>
    /// Emits a single file (<c>CountryLookup.g.cs</c>) carrying the
    /// <c>Countries</c> accessor + <c>CountryLookup</c> dictionaries +
    /// <c>WireNav()</c> wire-nav method.
    /// </summary>
    /// <param name="context">The aggregate spec context.</param>
    /// <returns>The single emit result.</returns>
    public static ImmutableArray<EmitResult> EmitAll(GeoSpecContext context)
    {
        if (context.Countries is not { } countriesEnv)
            return ImmutableArray<EmitResult>.Empty;

        var entries = SortByAlpha2(countriesEnv.Entries);

        // Pre-compute valid LanguageCode members (PascalCased ISO 639-1) so we
        // can defensively skip Country.PrimaryLanguage references that aren't
        // in the LanguageCode enum (8 countries reference 639-3 codes like
        // `fil`, `niu` not present in the ISO 639-1 enum).
        var validLanguageMembers = new HashSet<string>(StringComparer.Ordinal);
        if (context.Languages is { } langsEnv)
        {
            foreach (var lang in langsEnv.Entries)
            {
                var m = ToLanguageMember(lang.Iso6391Code);
                if (m is not null)
                    validLanguageMembers.Add(m);
            }
        }

        // Pre-compute valid CurrencyCode members so we can defensively skip
        // unknown alpha-3 references.
        var validCurrencyMembers = new HashSet<string>(StringComparer.Ordinal);
        if (context.Currencies is { } currenciesEnv)
        {
            foreach (var cur in currenciesEnv.Entries)
            {
                if (cur.Iso4217AlphaCode.Truthy()
                    && DefaultEmitterHelpers.IsValidIdentifier(cur.Iso4217AlphaCode))
                    validCurrencyMembers.Add(cur.Iso4217AlphaCode);
            }
        }

        // Pre-compute valid GeopoliticalEntityCode members so we can skip nav
        // refs to entities whose ShortCode isn't a valid C# identifier (e.g.
        // punctuation-bearing legacy codes).
        var validGpeMembers = new HashSet<string>(StringComparer.Ordinal);
        if (context.GeopoliticalEntities is { } gpesEnv)
        {
            foreach (var gpe in gpesEnv.Entries)
            {
                if (gpe.ShortCode.Truthy()
                    && DefaultEmitterHelpers.IsValidIdentifier(gpe.ShortCode))
                    validGpeMembers.Add(gpe.ShortCode);
            }
        }

        var sb = new StringBuilder();
        DefaultEmitterHelpers.AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using DcsvIo.D2.Utilities.Extensions;");
        sb.AppendLine($"using {DefaultEmitterHelpers.AbstractionsNamespace};");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();

        // -------- Countries data accessor (read-through to CountryLookup) --------
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Per-country <see cref=\"Country\"/> accessors keyed by ISO 3166-1");
        sb.AppendLine(
            "/// alpha-2 (e.g. <c>Countries.US</c>). Each accessor reads through");
        sb.AppendLine(
            "/// <see cref=\"CountryLookup.ByCode\"/> so consumers always observe a");
        sb.AppendLine(
            "/// fully-materialized record. Nav refs (PrimaryLanguage, Subdivisions,");
        sb.AppendLine(
            "/// Territories, ...) are populated in the wire-nav step by the");
        sb.AppendLine(
            "/// <c>GeoDataInitializer</c> module initializer after every catalog's");
        sb.AppendLine("/// first pass has run.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class Countries");
        sb.AppendLine("{");
        foreach (var entry in entries)
        {
            sb.AppendLine(
                $"    /// <summary>{DefaultEmitterHelpers.EscapeXmlDoc(entry.DisplayName)} "
                + $"({entry.Iso31661Alpha2Code}).</summary>");
            sb.AppendLine(
                $"    public static Country {entry.Iso31661Alpha2Code} => "
                + $"CountryLookup.ByCode[CountryCode.{entry.Iso31661Alpha2Code}];");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // -------- Lookup with first pass (static ctor) + wire-nav --------
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// O(1) lookup tables over the country catalog. First pass (this");
        sb.AppendLine(
            "/// class's static constructor) materializes every <see cref=\"Country\"/>");
        sb.AppendLine(
            "/// record with scalar fields AND every code-rep field populated;");
        sb.AppendLine(
            "/// the wire-nav step (<see cref=\"WireNav\"/>) mutates the recorded");
        sb.AppendLine(
            "/// nav-rep properties via friend-assembly <c>internal set</c> visibility.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class CountryLookup");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>Country records indexed by "
            + "<see cref=\"CountryCode\"/> enum.</summary>");
        sb.AppendLine(
            "    public static readonly FrozenDictionary<CountryCode, Country> ByCode;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Country records indexed by ISO 3166-1 alpha-2 string.</summary>");
        sb.AppendLine(
            "    public static readonly FrozenDictionary<string, Country> ByIso31661Alpha2;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Country records indexed by ISO 3166-1 alpha-3 string.</summary>");
        sb.AppendLine(
            "    public static readonly FrozenDictionary<string, Country> ByIso31661Alpha3;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>All Country records in spec order (alpha-2 sorted).</summary>");
        sb.AppendLine(
            "    public static readonly IReadOnlyList<Country> All;");
        sb.AppendLine();

        // -------- First-pass static ctor --------
        sb.AppendLine("    static CountryLookup()");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        // First pass: construct every Country record "
            + "with default/empty nav values.");
        sb.AppendLine("        var byCode = new Dictionary<CountryCode, Country>();");
        sb.AppendLine();

        foreach (var entry in entries)
        {
            EmitCountryRecordConstruction(
                sb, entry, validCurrencyMembers, validLanguageMembers, validGpeMembers);
        }

        sb.AppendLine();
        sb.AppendLine("        ByCode = byCode.ToFrozenDictionary();");
        sb.AppendLine();
        sb.AppendLine(
            "        var byAlpha2 = new Dictionary<string, Country>("
            + "System.StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine(
            "        var byAlpha3 = new Dictionary<string, Country>("
            + "System.StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine("        foreach (var kvp in byCode)");
        sb.AppendLine("        {");
        sb.AppendLine("            byAlpha2[kvp.Value.Iso31661Alpha2Code.ToString()] = kvp.Value;");
        sb.AppendLine("            if (kvp.Value.Iso31661Alpha3Code.Truthy())");
        sb.AppendLine("                byAlpha3[kvp.Value.Iso31661Alpha3Code] = kvp.Value;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine(
            "        ByIso31661Alpha2 = byAlpha2.ToFrozenDictionary("
            + "System.StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine(
            "        ByIso31661Alpha3 = byAlpha3.ToFrozenDictionary("
            + "System.StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine();
        sb.AppendLine("        var all = new Country[]");
        sb.AppendLine("        {");
        foreach (var entry in entries)
            sb.AppendLine($"            byCode[CountryCode.{entry.Iso31661Alpha2Code}],");

        sb.AppendLine("        };");
        sb.AppendLine("        All = all;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // -------- WireNav --------
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Wire-nav step of the two-pass populate pattern. Invoked exactly");
        sb.AppendLine(
            "    /// once by <c>GeoDataInitializer</c> after every catalog's first-pass");
        sb.AppendLine(
            "    /// static ctor has run. Mutates the recorded <see cref=\"Country\"/>");
        sb.AppendLine(
            "    /// nav properties (PrimaryLanguage, PrimaryCurrency, PrimaryLocale,");
        sb.AppendLine(
            "    /// SovereignCountry, Locales, Territories, GeopoliticalEntities,");
        sb.AppendLine(
            "    /// Subdivisions, SubdivisionIso31662Codes) via the friend-assembly");
        sb.AppendLine("    /// <c>internal set</c> accessors.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static void WireNav()");
        sb.AppendLine("    {");
        foreach (var entry in entries)
            EmitCountryNavWire(sb, entry, validLanguageMembers, validGpeMembers);

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return ImmutableArray.Create(new EmitResult(
            HintName: "CountryLookup.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty));
    }

    private static string? ToLanguageMember(string? code)
    {
        if (code.Falsey())
            return null;

        var c = code!;
        var first = char.ToUpperInvariant(c[0]);
        var member = c.Length == 1 ? first.ToString() : first + c.Substring(1).ToLowerInvariant();
        return DefaultEmitterHelpers.IsValidIdentifier(member) ? member : null;
    }

    private static void EmitCountryRecordConstruction(
        StringBuilder sb,
        CountrySpec entry,
        HashSet<string> validCurrencyMembers,
        HashSet<string> validLanguageMembers,
        HashSet<string> validGpeMembers)
    {
        var c = entry.Iso31661Alpha2Code;
        var alpha3 = DefaultEmitterHelpers.EscapeStringLiteral(entry.Iso31661Alpha3Code);
        var numeric = DefaultEmitterHelpers.EscapeStringLiteral(entry.Iso31661NumericCode);
        var displayName = DefaultEmitterHelpers.EscapeStringLiteral(entry.DisplayName);
        var officialName = DefaultEmitterHelpers.EscapeStringLiteral(entry.OfficialName);
        var endonymDisplay = DefaultEmitterHelpers.EscapeStringLiteral(
            entry.EndonymDisplayName ?? entry.DisplayName);
        var endonymOfficial = DefaultEmitterHelpers.EscapeStringLiteral(
            entry.EndonymDisplayName ?? entry.OfficialName);
        var phonePrefix = DefaultEmitterHelpers.EscapeStringLiteral(
            entry.PhoneNumberPrefix ?? string.Empty);
        var phoneNationalFormat = DefaultEmitterHelpers.EscapeStringLiteral(
            entry.PhoneNumberNationalFormat ?? string.Empty);
        var phoneMinDigits = DefaultEmitterHelpers.IntLiteralOrNull(entry.PhoneNumberMinDigits);
        var firstDay = DefaultEmitterHelpers.MapDayOfWeek(entry.FirstDayOfWeek);
        var weekendStart = DefaultEmitterHelpers.MapDayOfWeek(entry.WeekendStart);
        var weekendEnd = DefaultEmitterHelpers.MapDayOfWeek(entry.WeekendEnd);
        var measurementSystem = DefaultEmitterHelpers.MapMeasurementSystem(entry.MeasurementSystem);

        sb.AppendLine($"        byCode[CountryCode.{c}] = new Country");
        sb.AppendLine("        {");
        sb.AppendLine($"            Iso31661Alpha2Code = CountryCode.{c},");
        sb.AppendLine($"            Iso31661Alpha3Code = \"{alpha3}\",");
        sb.AppendLine($"            Iso31661NumericCode = \"{numeric}\",");
        sb.AppendLine($"            DisplayName = \"{displayName}\",");
        sb.AppendLine($"            OfficialName = \"{officialName}\",");
        sb.AppendLine($"            EndonymDisplayName = \"{endonymDisplay}\",");
        sb.AppendLine($"            EndonymOfficialName = \"{endonymOfficial}\",");
        sb.AppendLine($"            PhoneNumberPrefix = \"{phonePrefix}\",");
        sb.AppendLine($"            PhoneNumberNationalFormat = \"{phoneNationalFormat}\",");
        sb.AppendLine($"            PhoneNumberMinDigits = {phoneMinDigits},");
        sb.AppendLine($"            PhoneNumberMaxDigits = {entry.PhoneNumberMaxDigits ?? 0},");
        sb.AppendLine($"            FirstDayOfWeek = {firstDay},");
        sb.AppendLine($"            WeekendStart = {weekendStart},");
        sb.AppendLine($"            WeekendEnd = {weekendEnd},");
        sb.AppendLine($"            MeasurementSystem = {measurementSystem},");

        // ---- Primary single FK code reps (init) ----
        var primaryLangMember = ToLanguageMember(entry.PrimaryLanguageIso6391Code);
        if (primaryLangMember is not null && validLanguageMembers.Contains(primaryLangMember))
        {
            sb.AppendLine(
                $"            PrimaryLanguageIso6391Code = "
                + $"LanguageCode.{primaryLangMember},");
        }
        else
        {
            sb.AppendLine("            PrimaryLanguageIso6391Code = null,");
        }

        if (entry.PrimaryCurrencyIso4217AlphaCode.Truthy()
            && validCurrencyMembers.Contains(entry.PrimaryCurrencyIso4217AlphaCode!))
        {
            sb.AppendLine(
                $"            PrimaryCurrencyIso4217AlphaCode = "
                + $"CurrencyCode.{entry.PrimaryCurrencyIso4217AlphaCode},");
        }
        else
        {
            sb.AppendLine("            PrimaryCurrencyIso4217AlphaCode = null,");
        }

        if (entry.PrimaryLocaleIetfBcp47Tag.Truthy())
        {
            var primaryLocale = DefaultEmitterHelpers.EscapeStringLiteral(
                entry.PrimaryLocaleIetfBcp47Tag!);
            sb.AppendLine(
                $"            PrimaryLocaleIetfBcp47Tag = "
                + $"LocaleCode.FromString(\"{primaryLocale}\"),");
        }
        else
        {
            sb.AppendLine("            PrimaryLocaleIetfBcp47Tag = null,");
        }

        if (entry.SovereignCountryIso31661Alpha2Code.Truthy()
            && DefaultEmitterHelpers.IsValidIdentifier(entry.SovereignCountryIso31661Alpha2Code!))
        {
            sb.AppendLine(
                $"            SovereignCountryIso31661Alpha2Code = "
                + $"CountryCode.{entry.SovereignCountryIso31661Alpha2Code},");
        }
        else
        {
            sb.AppendLine("            SovereignCountryIso31661Alpha2Code = null,");
        }

        // ---- Set FK code reps (init) ----
        EmitTypedCountrySet(sb, "TerritoryIso31661Alpha2Codes", entry.TerritoryIso31661Alpha2Codes);
        EmitTypedLocaleSet(sb, "LocaleIetfBcp47Tags", entry.LocaleIetfBcp47Tags);
        EmitTypedGpeSet(
            sb,
            "GeopoliticalEntityShortCodes",
            entry.GeopoliticalEntityShortCodes,
            validGpeMembers);
        EmitTypedCurrencySet(
            sb, "CurrencyIso4217AlphaCodes", entry.Currencies, validCurrencyMembers);

        // ---- Nested currency-acceptance list (required init) ----
        sb.AppendLine("            Currencies = new CountryCurrencyAcceptance[]");
        sb.AppendLine("            {");
        foreach (var cc in entry.Currencies)
        {
            if (cc.Iso4217AlphaCode.Falsey())
                continue;

            if (!validCurrencyMembers.Contains(cc.Iso4217AlphaCode))
                continue;

            sb.AppendLine("                new CountryCurrencyAcceptance");
            sb.AppendLine("                {");
            sb.AppendLine(
                $"                    Iso4217AlphaCode = "
                + $"CurrencyCode.{cc.Iso4217AlphaCode},");
            var level = DefaultEmitterHelpers.MapCurrencyAcceptanceLevel(cc.Level);
            sb.AppendLine($"                    Level = {level},");
            sb.AppendLine("                },");
        }

        sb.AppendLine("            },");

        sb.AppendLine("        };");
    }

    private static void EmitTypedCountrySet(
        StringBuilder sb,
        string fieldName,
        IReadOnlyList<string> codes)
    {
        var members = new List<string>();
        foreach (var raw in codes)
        {
            if (raw.Falsey() || !DefaultEmitterHelpers.IsValidIdentifier(raw))
                continue;

            members.Add(raw);
        }

        if (members.Count == 0)
        {
            sb.AppendLine($"            {fieldName} = FrozenSet<CountryCode>.Empty,");
            return;
        }

        sb.AppendLine($"            {fieldName} = new HashSet<CountryCode>");
        sb.AppendLine("            {");
        foreach (var m in members)
            sb.AppendLine($"                CountryCode.{m},");

        sb.AppendLine("            }.ToFrozenSet(),");
    }

    private static void EmitTypedLocaleSet(
        StringBuilder sb,
        string fieldName,
        IReadOnlyList<string> tags)
    {
        var present = new List<string>();
        foreach (var t in tags)
        {
            if (t.Falsey())
                continue;

            present.Add(t);
        }

        if (present.Count == 0)
        {
            sb.AppendLine($"            {fieldName} = FrozenSet<LocaleCode>.Empty,");
            return;
        }

        sb.AppendLine($"            {fieldName} = new HashSet<LocaleCode>");
        sb.AppendLine("            {");
        foreach (var t in present)
        {
            var tag = DefaultEmitterHelpers.EscapeStringLiteral(t);
            sb.AppendLine($"                LocaleCode.FromString(\"{tag}\"),");
        }

        sb.AppendLine("            }.ToFrozenSet(),");
    }

    private static void EmitTypedGpeSet(
        StringBuilder sb,
        string fieldName,
        IReadOnlyList<string> codes,
        HashSet<string> validGpeMembers)
    {
        var members = new List<string>();
        foreach (var g in codes)
        {
            if (g.Falsey() || !validGpeMembers.Contains(g))
                continue;

            members.Add(g);
        }

        if (members.Count == 0)
        {
            sb.AppendLine($"            {fieldName} = FrozenSet<GeopoliticalEntityCode>.Empty,");
            return;
        }

        sb.AppendLine($"            {fieldName} = new HashSet<GeopoliticalEntityCode>");
        sb.AppendLine("            {");
        foreach (var g in members)
            sb.AppendLine($"                GeopoliticalEntityCode.{g},");

        sb.AppendLine("            }.ToFrozenSet(),");
    }

    private static void EmitTypedCurrencySet(
        StringBuilder sb,
        string fieldName,
        IReadOnlyList<CountryCurrencyAcceptance> acceptance,
        HashSet<string> validCurrencyMembers)
    {
        var members = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cc in acceptance)
        {
            if (cc.Iso4217AlphaCode.Falsey() || !validCurrencyMembers.Contains(cc.Iso4217AlphaCode))
                continue;

            if (seen.Add(cc.Iso4217AlphaCode))
                members.Add(cc.Iso4217AlphaCode);
        }

        if (members.Count == 0)
        {
            sb.AppendLine($"            {fieldName} = FrozenSet<CurrencyCode>.Empty,");
            return;
        }

        sb.AppendLine($"            {fieldName} = new HashSet<CurrencyCode>");
        sb.AppendLine("            {");
        foreach (var m in members)
            sb.AppendLine($"                CurrencyCode.{m},");

        sb.AppendLine("            }.ToFrozenSet(),");
    }

    private static void EmitCountryNavWire(
        StringBuilder sb,
        CountrySpec entry,
        HashSet<string> validLanguageMembers,
        HashSet<string> validGpeMembers)
    {
        var c = entry.Iso31661Alpha2Code;
        sb.AppendLine($"        // {c}");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            var rec = ByCode[CountryCode.{c}];");

        // PrimaryLanguage nav.
        var primaryLangMember = ToLanguageMember(entry.PrimaryLanguageIso6391Code);
        if (primaryLangMember is not null && validLanguageMembers.Contains(primaryLangMember))
        {
            sb.AppendLine(
                $"            rec.PrimaryLanguage = "
                + $"LanguageLookup.ByCode[LanguageCode.{primaryLangMember}];");
        }

        // PrimaryCurrency nav.
        if (entry.PrimaryCurrencyIso4217AlphaCode.Truthy()
            && DefaultEmitterHelpers.IsValidIdentifier(entry.PrimaryCurrencyIso4217AlphaCode!))
        {
            sb.AppendLine(
                $"            rec.PrimaryCurrency = "
                + $"CurrencyLookup.ByCode[CurrencyCode.{entry.PrimaryCurrencyIso4217AlphaCode}];");
        }

        // PrimaryLocale nav. Direct indexer — fail-loud on missing locale ref;
        // SpecLoader's MissingLocaleReference (D2GEO011) validation guarantees
        // every country.primaryLocaleIETFBCP47Tag resolves at build time.
        if (entry.PrimaryLocaleIetfBcp47Tag.Truthy())
        {
            var primaryLocale = DefaultEmitterHelpers.EscapeStringLiteral(
                entry.PrimaryLocaleIetfBcp47Tag!);
            sb.AppendLine(
                $"            rec.PrimaryLocale = "
                + $"LocaleLookup.ByCode[LocaleCode.FromString(\"{primaryLocale}\")];");
        }

        // SovereignCountry nav.
        if (entry.SovereignCountryIso31661Alpha2Code.Truthy()
            && DefaultEmitterHelpers.IsValidIdentifier(entry.SovereignCountryIso31661Alpha2Code!))
        {
            sb.AppendLine(
                $"            rec.SovereignCountry = "
                + $"ByCode[CountryCode.{entry.SovereignCountryIso31661Alpha2Code}];");
        }

        // Locales nav (list, materialized from LocaleIetfBcp47Tags set).
        var localeTags = new List<string>();
        foreach (var tag in entry.LocaleIetfBcp47Tags)
        {
            if (tag.Falsey())
                continue;

            localeTags.Add(tag);
        }

        if (localeTags.Count > 0)
        {
            // Direct indexer per tag — fail-loud on missing locale ref;
            // SpecLoader's MissingLocaleReference (D2GEO011) validation
            // guarantees every country.localeIETFBCP47Tags[] entry resolves
            // at build time.
            sb.AppendLine("            rec.Locales = new Locale[]");
            sb.AppendLine("            {");
            foreach (var tag in localeTags)
            {
                var escaped = DefaultEmitterHelpers.EscapeStringLiteral(tag);
                sb.AppendLine(
                    $"                LocaleLookup.ByCode[LocaleCode.FromString(\"{escaped}\")],");
            }

            sb.AppendLine("            };");
        }

        // Territories nav (list).
        var territoryCodes = new List<string>();
        foreach (var t in entry.TerritoryIso31661Alpha2Codes)
        {
            if (t.Falsey() || !DefaultEmitterHelpers.IsValidIdentifier(t))
                continue;

            territoryCodes.Add(t);
        }

        if (territoryCodes.Count > 0)
        {
            sb.AppendLine("            rec.Territories = new Country[]");
            sb.AppendLine("            {");
            foreach (var t in territoryCodes)
                sb.AppendLine($"                ByCode[CountryCode.{t}],");

            sb.AppendLine("            };");
        }

        // GeopoliticalEntities nav (list).
        var gpes = new List<string>();
        foreach (var gpe in entry.GeopoliticalEntityShortCodes)
        {
            if (gpe.Falsey() || !validGpeMembers.Contains(gpe))
                continue;

            gpes.Add(gpe);
        }

        if (gpes.Count > 0)
        {
            sb.AppendLine("            rec.GeopoliticalEntities = new GeopoliticalEntity[]");
            sb.AppendLine("            {");
            foreach (var gpe in gpes)
            {
                sb.AppendLine(
                    $"                GeopoliticalEntityLookup.ByCode"
                    + $"[GeopoliticalEntityCode.{gpe}],");
            }

            sb.AppendLine("            };");
        }

        // Subdivisions nav (list) + SubdivisionIso31662Codes set — derived from
        // SubdivisionLookup.ByCountry.
        sb.AppendLine(
            $"            if (SubdivisionLookup.ByCountry.TryGetValue("
            + $"CountryCode.{c}, out var subs))");
        sb.AppendLine("            {");
        sb.AppendLine("                rec.Subdivisions = subs;");
        sb.AppendLine("                var codeSet = new HashSet<SubdivisionCode>();");
        sb.AppendLine("                foreach (var sub in subs)");
        sb.AppendLine("                    codeSet.Add(sub.Iso31662Code);");
        sb.AppendLine();
        sb.AppendLine("                rec.SubdivisionIso31662Codes = codeSet.ToFrozenSet();");
        sb.AppendLine("            }");

        // Wire nested Currency nav on each acceptance entry.
        sb.AppendLine("            foreach (var cc in rec.Currencies)");
        sb.AppendLine("            {");
        sb.AppendLine(
            "                if (CurrencyLookup.ByCode.TryGetValue("
            + "cc.Iso4217AlphaCode, out var cur))");
        sb.AppendLine("                    cc.Currency = cur;");
        sb.AppendLine("            }");

        sb.AppendLine("        }");
    }

    private static IReadOnlyList<CountrySpec> SortByAlpha2(IEnumerable<CountrySpec> entries)
    {
        var list = new List<CountrySpec>(entries);
        list.Sort(static (a, b) => string.CompareOrdinal(
            a.Iso31661Alpha2Code, b.Iso31661Alpha2Code));
        return list;
    }
}
