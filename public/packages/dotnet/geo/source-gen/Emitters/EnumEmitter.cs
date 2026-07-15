// -----------------------------------------------------------------------
// <copyright file="EnumEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Emitters;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using DcsvIo.D2.Geo.SourceGen.Spec;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Emits the closed-set <c>enum</c> types for catalogs that carry a small
/// closed identifier vocabulary. Naming convention: the identifier enum
/// carries the <c>*Code</c> suffix; the bare singular name (Country,
/// Currency, Language, GeopoliticalEntity) is reserved for the spec-derived
/// data record. This mirrors the open-set wrapper-struct catalogs
/// (SubdivisionCode / LocaleCode / TimezoneCode) so the identifier-type
/// concept is symmetric across enum-backed + struct-backed catalogs.
/// <list type="bullet">
///   <item><c>CountryCode</c> (backing = ISO 3166-1 numeric).</item>
///   <item><c>CurrencyCode</c> (backing = ISO 4217 numeric).</item>
///   <item><c>LanguageCode</c> (sequential 1..N).</item>
///   <item><c>GeopoliticalEntityCode</c> (sequential 1..N).</item>
///   <item>Fixed-vocabulary enums — <c>GeopoliticalEntityType</c> (exact
///     stable integer assignments preserved across the wire and across
///     .NET / TS), <c>WritingDirection</c>, <c>DateFormatPattern</c>,
///     <c>CurrencyAcceptanceLevel</c>.</item>
/// </list>
/// Each emitted enum carries
/// <c>[JsonConverter(typeof(JsonStringEnumConverter))]</c> so the wire
/// format is the canonical alpha identifier (e.g. <c>"US"</c>, <c>"USD"</c>,
/// <c>"en"</c>) — never the numeric backing field. Per the strict wire-code
/// policy there is NO <c>Unknown</c> sentinel — unknown wire codes throw
/// <see cref="System.Text.Json.JsonException"/> at deserialization (caller
/// boundary maps to 400 ValidationFailed).
/// </summary>
internal static class EnumEmitter
{
    private const string _NAMESPACE = EmitterHelpers.AbstractionsNamespace;

    /// <summary>
    /// Emits every enum + supporting closed-vocabulary enum used by the
    /// abstractions assembly. Returns one <see cref="EmitResult"/> per type
    /// so the dispatcher can write each into its own <c>.g.cs</c> hint name.
    /// </summary>
    /// <param name="context">The aggregate spec context.</param>
    /// <returns>The per-type emit results.</returns>
    public static ImmutableArray<EmitResult> EmitAll(GeoSpecContext context)
    {
        var results = ImmutableArray.CreateBuilder<EmitResult>();

        results.Add(EmitFixedVocabularyEnums());
        results.Add(EmitGeopoliticalEntityType());

        if (context.Countries is { } countries)
            results.Add(EmitCountry(countries.Entries));

        if (context.Currencies is { } currencies)
            results.Add(EmitCurrency(currencies.Entries));

        if (context.Languages is { } languages)
            results.Add(EmitLanguage(languages.Entries));

        if (context.GeopoliticalEntities is { } gpes)
            results.Add(EmitGeopoliticalEntityEnum(gpes.Entries));

        return results.ToImmutable();
    }

    private static EmitResult EmitFixedVocabularyEnums()
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Writing direction enumeration carried by every");
        sb.AppendLine("/// <c>Language</c> record. Drives RTL UI container flips.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[JsonConverter(typeof(JsonStringEnumConverter))]");
        sb.AppendLine("public enum WritingDirection : byte");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Left-to-right text flow (en, fr, de, ...).</summary>");
        sb.AppendLine("    LTR = 0,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Right-to-left text flow (ar, he, fa, ur).</summary>");
        sb.AppendLine("    RTL = 1,");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Date-component ordering for the human-readable form of a date.");
        sb.AppendLine("/// Carried on every <c>Locale</c> record.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[JsonConverter(typeof(JsonStringEnumConverter))]");
        sb.AppendLine("public enum DateFormatPattern : byte");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>Year-Month-Day (ISO; ja-JP, ko-KR, hu, zh).</summary>");
        sb.AppendLine("    YMD = 0,");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Day-Month-Year (most of the world: en-GB, de, fr, ...).</summary>");
        sb.AppendLine("    DMY = 1,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Month-Day-Year (en-US).</summary>");
        sb.AppendLine("    MDY = 2,");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Classification for entries in <c>CountryCurrencyAcceptance</c>");
        sb.AppendLine("/// — codifies the de-facto-over-politically-correct rule: enumerate");
        sb.AppendLine("/// every currency genuinely accepted in commerce (legal tender, widely");
        sb.AppendLine("/// accepted, tourist-zone), not just the sovereign currency on paper.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[JsonConverter(typeof(JsonStringEnumConverter))]");
        sb.AppendLine("public enum CurrencyAcceptanceLevel : byte");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>Official sovereign currency. Most countries have "
            + "exactly one.</summary>");
        sb.AppendLine("    LegalTender = 0,");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>De-facto commercial acceptance — ubiquitous in trade "
            + "though not legally required.</summary>");
        sb.AppendLine("    WidelyAccepted = 1,");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Narrow tourist-zone / specialty acceptance — present "
            + "in tourism economies but not broadly usable.</summary>");
        sb.AppendLine("    Tourist = 2,");
        sb.AppendLine("}");

        return new EmitResult(
            HintName: "FixedVocabularyEnums.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static EmitResult EmitGeopoliticalEntityType()
    {
        // 23 values across 4 region-tagged categories with EXACT, stable
        // integer assignments preserved across the wire and across .NET / TS.
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// 23-value type classification for <c>GeopoliticalEntity</c>. Exact");
        sb.AppendLine(
            "/// integer assignments preserved across all four region-tagged");
        sb.AppendLine(
            "/// categories — stable across the wire and across .NET / TS for");
        sb.AppendLine("/// cross-runtime round-tripping.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[JsonConverter(typeof(JsonStringEnumConverter))]");
        sb.AppendLine("public enum GeopoliticalEntityType : byte");
        sb.AppendLine("{");
        sb.AppendLine("    // --- General Geopolitical (3) ---");
        sb.AppendLine("    /// <summary>Continental landmass (e.g. AF, AS, EU).</summary>");
        sb.AppendLine("    Continent = 0,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Sub-continental region (e.g. CAS, EAS, SEA).</summary>");
        sb.AppendLine("    SubContinent = 1,");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Geopolitical region (e.g. LATAM, MENA, NORD).</summary>");
        sb.AppendLine("    GeopoliticalRegion = 2,");
        sb.AppendLine();
        sb.AppendLine("    // --- Economic (8) ---");
        sb.AppendLine(
            "    /// <summary>Free trade agreement (e.g. USMCA, CPTPP, RCEP).</summary>");
        sb.AppendLine("    FreeTradeAgreement = 10,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Customs union (e.g. EUCU, SACU).</summary>");
        sb.AppendLine("    CustomsUnion = 11,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Common market (e.g. EEA, MERCOSUR).</summary>");
        sb.AppendLine("    CommonMarket = 12,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Economic union (e.g. EAEU).</summary>");
        sb.AppendLine("    EconomicUnion = 13,");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Monetary union (e.g. EZ, ECCU, WAEMU, CEMAC).</summary>");
        sb.AppendLine("    MonetaryUnion = 14,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Bilateral investment treaty.</summary>");
        sb.AppendLine("    BilateralInvestmentTreaty = 15,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Development agreement.</summary>");
        sb.AppendLine("    DevelopmentAgreement = 16,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Resource-sharing agreement.</summary>");
        sb.AppendLine("    ResourceSharingAgreement = 17,");
        sb.AppendLine();
        sb.AppendLine("    // --- Political (6) ---");
        sb.AppendLine("    /// <summary>Political union (e.g. EUR — European Union).</summary>");
        sb.AppendLine("    PoliticalUnion = 20,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Human-rights agreement.</summary>");
        sb.AppendLine("    HumanRightsAgreement = 21,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Environmental agreement.</summary>");
        sb.AppendLine("    EnvironmentalAgreement = 22,");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Governance &amp; cooperation agreement (e.g. UN, G7, "
            + "OECD).</summary>");
        sb.AppendLine("    GovernanceAndCooperationAgreement = 23,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Peace treaty.</summary>");
        sb.AppendLine("    PeaceTreaty = 24,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Democracy-promotion agreement.</summary>");
        sb.AppendLine("    DemocracyPromotionAgreement = 25,");
        sb.AppendLine();
        sb.AppendLine("    // --- Military (6) ---");
        sb.AppendLine(
            "    /// <summary>Military alliance (e.g. NATO, CSTO, ANZUS).</summary>");
        sb.AppendLine("    MilitaryAlliance = 30,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Arms-control agreement.</summary>");
        sb.AppendLine("    ArmsControlAgreement = 31,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Status-of-forces agreement.</summary>");
        sb.AppendLine("    StatusOfForcesAgreement = 32,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Peacekeeping agreement.</summary>");
        sb.AppendLine("    PeacekeepingAgreement = 33,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Security-cooperation agreement.</summary>");
        sb.AppendLine("    SecurityCooperationAgreement = 34,");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Non-aggression pact.</summary>");
        sb.AppendLine("    NonAggressionPact = 35,");
        sb.AppendLine("}");

        return new EmitResult(
            HintName: "GeopoliticalEntityType.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static EmitResult EmitCountry(IReadOnlyList<CountrySpec> entries)
    {
        // Backing = ISO 3166-1 numeric (ushort).
        // Member name = ISO 3166-1 alpha-2 code. Alpha-2 is already a valid
        // C# identifier (two ASCII uppercase letters).
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// ISO 3166-1 alpha-2 country identifier. Backed by the ISO 3166-1 numeric");
        sb.AppendLine(
            "/// code (ushort). The wire form is the alpha-2 string (e.g. <c>\"US\"</c>),");
        sb.AppendLine(
            "/// enforced by the embedded <c>JsonStringEnumConverter</c>. Unknown wire codes");
        sb.AppendLine(
            "/// throw <see cref=\"System.Text.Json.JsonException\"/> — caller boundary maps");
        sb.AppendLine("/// to 400 ValidationFailed (strict deserialization policy).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[JsonConverter(typeof(JsonStringEnumConverter))]");
        sb.AppendLine("public enum CountryCode : ushort");
        sb.AppendLine("{");

        var emittedNumerics = new HashSet<ushort>();
        var sorted = SortByAlpha2(entries);
        var index = 0;
        foreach (var entry in sorted)
        {
            if (!ushort.TryParse(
                    entry.Iso31661NumericCode,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var numeric))
            {
                // Fall back to sequential offset when numeric is unparseable;
                // this is defensive — every Tier-2 entry should carry a valid
                // ISO numeric.
                numeric = (ushort)(60000 + index);
            }

            // ISO 3166-1 reserves a few numerics for transitional codes (e.g.,
            // exceptional reservations). De-dup if collision (shouldn't happen
            // in well-formed catalog).
            while (!emittedNumerics.Add(numeric))
                numeric = (ushort)(numeric + 1);

            sb.AppendLine(
                $"    /// <summary>{EscapeXmlDoc(entry.DisplayName)} "
                + $"({entry.Iso31661Alpha2Code}).</summary>");
            sb.AppendLine($"    {entry.Iso31661Alpha2Code} = {numeric},");
            sb.AppendLine();
            index++;
        }

        sb.AppendLine("}");

        return new EmitResult(
            HintName: "CountryCode.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static EmitResult EmitCurrency(IReadOnlyList<CurrencySpec> entries)
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
            "/// ISO 4217 alpha currency identifier. Backed by the ISO 4217 numeric code");
        sb.AppendLine(
            "/// (ushort). Wire form is the alpha-3 string (e.g. <c>\"USD\"</c>). Unknown");
        sb.AppendLine(
            "/// wire codes throw <see cref=\"System.Text.Json.JsonException\"/> at the");
        sb.AppendLine(
            "/// deserialization boundary (strict deserialization policy).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[JsonConverter(typeof(JsonStringEnumConverter))]");
        sb.AppendLine("public enum CurrencyCode : ushort");
        sb.AppendLine("{");

        var emittedNumerics = new HashSet<ushort>();
        var emittedAlphas = new HashSet<string>(StringComparer.Ordinal);
        var sorted = SortByAlpha3(entries);
        var index = 0;
        foreach (var entry in sorted)
        {
            // Some historical currencies share ISO 4217 numeric codes (the
            // numeric is reused after retirement). De-dup defensively.
            ushort numeric;
            if (!entry.Iso4217NumericCode.Falsey() &&
                ushort.TryParse(
                    entry.Iso4217NumericCode,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out numeric))
            {
                while (!emittedNumerics.Add(numeric))
                    numeric = (ushort)(numeric + 1);
            }
            else
            {
                numeric = (ushort)(60000 + index);
                while (!emittedNumerics.Add(numeric))
                    numeric = (ushort)(numeric + 1);
            }

            var alpha = entry.Iso4217AlphaCode;
            if (alpha.Falsey() || !IsValidIdentifier(alpha))
            {
                // skip currencies whose alpha code is not a valid C# identifier.
                index++;
                continue;
            }

            if (!emittedAlphas.Add(alpha))
            {
                // duplicate alpha — skip the second occurrence.
                index++;
                continue;
            }

            sb.AppendLine(
                $"    /// <summary>{EscapeXmlDoc(entry.DisplayName)} "
                + $"({alpha}).</summary>");
            sb.AppendLine($"    {alpha} = {numeric},");
            sb.AppendLine();
            index++;
        }

        sb.AppendLine("}");

        return new EmitResult(
            HintName: "CurrencyCode.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static EmitResult EmitLanguage(IReadOnlyList<LanguageSpec> entries)
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
            "/// ISO 639-1 alpha-2 language identifier. Members named with the");
        sb.AppendLine(
            "/// PascalCase form of the alpha-2 code (<c>En</c>, <c>Fr</c>, etc. —");
        sb.AppendLine(
            "/// alpha-2 codes are two lowercase ASCII letters, capitalized here to");
        sb.AppendLine(
            "/// match C# member-name conventions). Backing field assigned sequentially");
        sb.AppendLine(
            "/// — no canonical numeric assignment exists in ISO 639-1. Unknown wire");
        sb.AppendLine(
            "/// codes throw <see cref=\"System.Text.Json.JsonException\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine(
            "/// Each member carries <c>[JsonStringEnumMemberName(\"&lt;code&gt;\")]</c>");
        sb.AppendLine(
            "/// so the JSON wire form is the canonical lowercase ISO 639-1 code");
        sb.AppendLine(
            "/// (e.g. <c>\"en\"</c>) rather than the PascalCased C# member name");
        sb.AppendLine(
            "/// (<c>\"En\"</c>). <c>JsonStringEnumConverter</c> honors");
        sb.AppendLine(
            "/// <c>[JsonStringEnumMemberName]</c> (the .NET 9+ attribute) on both");
        sb.AppendLine(
            "/// serialize and deserialize — keeps wire-form parity with the TS-side");
        sb.AppendLine(
            "/// <c>LanguageCode</c> const-object (which uses lowercase string values).");
        sb.AppendLine(
            "/// LanguageCode is the ONLY geo enum needing this attribute — CountryCode /");
        sb.AppendLine(
            "/// CurrencyCode / GeopoliticalEntityCode / GeopoliticalEntityType / etc. all use");
        sb.AppendLine(
            "/// member names whose canonical wire form already matches the C# casing.");
        sb.AppendLine("/// </remarks>");
        sb.AppendLine("[JsonConverter(typeof(JsonStringEnumConverter))]");
        sb.AppendLine("public enum LanguageCode : ushort");
        sb.AppendLine("{");

        var emittedMembers = new HashSet<string>(StringComparer.Ordinal);
        ushort sequential = 1;
        foreach (var entry in SortByCode(entries))
        {
            var memberName = ToPascalCase(entry.Iso6391Code);
            if (memberName.Falsey() || !IsValidIdentifier(memberName))
                continue;

            if (!emittedMembers.Add(memberName))
                continue;

            // [JsonStringEnumMemberName] is the .NET 9+ attribute that
            // JsonStringEnumConverter honors for a custom wire name — NOT
            // [EnumMember] (DataContract), which System.Text.Json ignores.
            // Wire form = the ISO 639-1 lowercase code (matches TS-side).
            var wireValue = entry.Iso6391Code.ToLowerInvariant();
            sb.AppendLine(
                $"    /// <summary>{EscapeXmlDoc(entry.Name)} "
                + $"({entry.Iso6391Code}).</summary>");
            sb.AppendLine($"    [JsonStringEnumMemberName(\"{wireValue}\")]");
            sb.AppendLine($"    {memberName} = {sequential},");
            sb.AppendLine();
            sequential = (ushort)(sequential + 1);
        }

        sb.AppendLine("}");

        return new EmitResult(
            HintName: "LanguageCode.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static EmitResult EmitGeopoliticalEntityEnum(
        IReadOnlyList<GeopoliticalEntitySpec> entries)
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
            "/// Identifier for a supranational geopolitical grouping (continents,");
        sb.AppendLine(
            "/// trade blocs, political unions, military alliances, etc.). Members");
        sb.AppendLine(
            "/// named with the well-known abbreviation (<c>EU</c>, <c>NATO</c>,");
        sb.AppendLine(
            "/// <c>USMCA</c>, <c>BRICS</c>) — the short code from the catalog. Backing");
        sb.AppendLine(
            "/// field assigned sequentially. Unknown wire codes throw");
        sb.AppendLine(
            "/// <see cref=\"System.Text.Json.JsonException\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[JsonConverter(typeof(JsonStringEnumConverter))]");
        sb.AppendLine("public enum GeopoliticalEntityCode : ushort");
        sb.AppendLine("{");

        var emittedMembers = new HashSet<string>(StringComparer.Ordinal);
        ushort sequential = 1;
        foreach (var entry in SortByShortCode(entries))
        {
            var memberName = entry.ShortCode;
            if (memberName.Falsey() || !IsValidIdentifier(memberName))
                continue;

            if (!emittedMembers.Add(memberName))
                continue;

            sb.AppendLine($"    /// <summary>{EscapeXmlDoc(entry.Name)} ({memberName}).</summary>");
            sb.AppendLine($"    {memberName} = {sequential},");
            sb.AppendLine();
            sequential = (ushort)(sequential + 1);
        }

        sb.AppendLine("}");

        return new EmitResult(
            HintName: "GeopoliticalEntityCode.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static IEnumerable<CountrySpec> SortByAlpha2(IEnumerable<CountrySpec> entries)
    {
        var list = new List<CountrySpec>(entries);
        list.Sort(static (a, b) => string.CompareOrdinal(
            a.Iso31661Alpha2Code, b.Iso31661Alpha2Code));
        return list;
    }

    private static IEnumerable<CurrencySpec> SortByAlpha3(IEnumerable<CurrencySpec> entries)
    {
        var list = new List<CurrencySpec>(entries);
        list.Sort(static (a, b) => string.CompareOrdinal(
            a.Iso4217AlphaCode, b.Iso4217AlphaCode));
        return list;
    }

    private static IEnumerable<LanguageSpec> SortByCode(IEnumerable<LanguageSpec> entries)
    {
        var list = new List<LanguageSpec>(entries);
        list.Sort(static (a, b) => string.CompareOrdinal(a.Iso6391Code, b.Iso6391Code));
        return list;
    }

    private static IEnumerable<GeopoliticalEntitySpec> SortByShortCode(
        IEnumerable<GeopoliticalEntitySpec> entries)
    {
        var list = new List<GeopoliticalEntitySpec>(entries);
        list.Sort(static (a, b) => string.CompareOrdinal(a.ShortCode, b.ShortCode));
        return list;
    }

    private static string ToPascalCase(string value)
    {
        if (value.Falsey())
            return string.Empty;

        // ISO 639-1 codes are two lowercase ASCII letters — uppercase the first
        // and leave the rest lowercase.
        var first = char.ToUpperInvariant(value[0]);
        if (value.Length == 1)
            return first.ToString();

        return first + value.Substring(1).ToLowerInvariant();
    }

    private static bool IsValidIdentifier(string candidate)
    {
        if (candidate.Falsey())
            return false;

        // First char must be a letter or underscore.
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

    private static void AppendFileHeader(StringBuilder sb) =>
        EmitterHelpers.AppendFileHeader(sb);

    private static string EscapeXmlDoc(string value) => EmitterHelpers.EscapeXmlDoc(value);
}
