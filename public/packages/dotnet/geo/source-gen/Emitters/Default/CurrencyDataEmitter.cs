// -----------------------------------------------------------------------
// <copyright file="CurrencyDataEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Emitters.Default;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using DcsvIo.D2.Geo.SourceGen.Spec;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Emits the per-currency DATA — single shape per entity + cycle-resolution
/// via friend-assembly <c>internal set</c> + two-pass populate. Output is a
/// single file (<c>CurrencyLookup.g.cs</c>) carrying a static
/// <c>Currencies</c> accessor + a static <c>CurrencyLookup</c> class with
/// the FrozenDictionary indexes and the <c>WireNav()</c> wire-nav method.
/// </summary>
/// <remarks>
/// First pass (static ctor) materializes every <c>Currency</c> record with
/// scalar required-init fields populated; nav-rep
/// <c>AcceptedInCountries</c> + code-rep <c>AcceptedInCountryIso31661Alpha2Codes</c>
/// start empty. Wire-nav step walks the country catalog and populates
/// BOTH reps via the friend-assembly <c>internal set</c> accessors.
/// </remarks>
internal static class CurrencyDataEmitter
{
    private const string _NAMESPACE = DefaultEmitterHelpers.DefaultNamespace;

    /// <summary>
    /// Emits the single <c>CurrencyLookup.g.cs</c> file. Empty when the
    /// spec context lacks a currencies catalog.
    /// </summary>
    /// <param name="context">The aggregate spec context.</param>
    /// <returns>The emit result.</returns>
    public static ImmutableArray<EmitResult> EmitAll(GeoSpecContext context)
    {
        if (context.Currencies is not { } currenciesEnv)
            return ImmutableArray<EmitResult>.Empty;

        var entries = SortByAlpha3(currenciesEnv.Entries);

        var sb = new StringBuilder();
        DefaultEmitterHelpers.AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine($"using {DefaultEmitterHelpers.AbstractionsNamespace};");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();

        // -------- Currencies data accessor --------
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Per-currency <see cref=\"Currency\"/> accessors keyed by ISO 4217");
        sb.AppendLine(
            "/// alpha-3 (e.g. <c>Currencies.USD</c>). Each accessor reads through");
        sb.AppendLine(
            "/// <see cref=\"CurrencyLookup.ByCode\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class Currencies");
        sb.AppendLine("{");
        var emittedAlphas = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.Iso4217AlphaCode.Falsey() ||
                !DefaultEmitterHelpers.IsValidIdentifier(entry.Iso4217AlphaCode))
                continue;

            if (!emittedAlphas.Add(entry.Iso4217AlphaCode))
                continue;

            var xmlDoc = DefaultEmitterHelpers.EscapeXmlDoc(entry.DisplayName);
            sb.AppendLine(
                $"    /// <summary>{xmlDoc} ({entry.Iso4217AlphaCode}).</summary>");
            sb.AppendLine(
                $"    public static Currency {entry.Iso4217AlphaCode} => "
                + $"CurrencyLookup.ByCode[CurrencyCode.{entry.Iso4217AlphaCode}];");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // -------- Lookup with first pass + wire-nav --------
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// O(1) lookup tables over the currency catalog. First pass (static");
        sb.AppendLine(
            "/// ctor) materializes every <see cref=\"Currency\"/> record with");
        sb.AppendLine(
            "/// scalar required-init fields populated; wire-nav step");
        sb.AppendLine(
            "/// (<see cref=\"WireNav\"/>) walks the country catalog and populates");
        sb.AppendLine(
            "/// both the typed code set (<c>AcceptedInCountryIso31661Alpha2Codes</c>)");
        sb.AppendLine(
            "/// and the record list (<c>AcceptedInCountries</c>) via the");
        sb.AppendLine("/// friend-assembly <c>internal set</c> accessors.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class CurrencyLookup");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>Currency records indexed by "
            + "<see cref=\"CurrencyCode\"/> enum.</summary>");
        sb.AppendLine(
            "    public static readonly FrozenDictionary<CurrencyCode, Currency> ByCode;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Currency records indexed by ISO 4217 alpha-3 string.</summary>");
        sb.AppendLine(
            "    public static readonly FrozenDictionary<string, Currency> ByIso4217Alpha;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>All Currency records in spec order.</summary>");
        sb.AppendLine(
            "    public static readonly IReadOnlyList<Currency> All;");
        sb.AppendLine();

        sb.AppendLine("    static CurrencyLookup()");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        // First pass: construct every Currency record "
            + "(reverse navs empty until wire-nav).");
        sb.AppendLine("        var byCode = new Dictionary<CurrencyCode, Currency>();");
        emittedAlphas.Clear();
        foreach (var entry in entries)
        {
            if (entry.Iso4217AlphaCode.Falsey() ||
                !DefaultEmitterHelpers.IsValidIdentifier(entry.Iso4217AlphaCode))
                continue;

            if (!emittedAlphas.Add(entry.Iso4217AlphaCode))
                continue;

            sb.AppendLine(
                $"        byCode[CurrencyCode.{entry.Iso4217AlphaCode}] = "
                + CurrencyRecordLiteral(entry) + ";");
        }

        sb.AppendLine();
        sb.AppendLine("        ByCode = byCode.ToFrozenDictionary();");
        sb.AppendLine();
        sb.AppendLine(
            "        var byAlpha = new Dictionary<string, Currency>("
            + "System.StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine("        foreach (var kvp in byCode)");
        sb.AppendLine("            byAlpha[kvp.Value.Iso4217AlphaCode.ToString()] = kvp.Value;");
        sb.AppendLine();
        sb.AppendLine(
            "        ByIso4217Alpha = byAlpha.ToFrozenDictionary("
            + "System.StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine();
        sb.AppendLine("        var all = new Currency[]");
        sb.AppendLine("        {");
        emittedAlphas.Clear();
        foreach (var entry in entries)
        {
            if (entry.Iso4217AlphaCode.Falsey() ||
                !DefaultEmitterHelpers.IsValidIdentifier(entry.Iso4217AlphaCode))
                continue;

            if (!emittedAlphas.Add(entry.Iso4217AlphaCode))
                continue;

            sb.AppendLine($"            byCode[CurrencyCode.{entry.Iso4217AlphaCode}],");
        }

        sb.AppendLine("        };");
        sb.AppendLine("        All = all;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // WireNav — accumulate per-currency accepted-in-countries by walking
        // Country.Currencies (built in CountryLookup first pass).
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Wire-nav step of the two-pass populate pattern. Invoked exactly");
        sb.AppendLine(
            "    /// once by <c>GeoDataInitializer</c> after every catalog's first-pass");
        sb.AppendLine(
            "    /// static ctor has run. Walks the country catalog and populates");
        sb.AppendLine(
            "    /// each currency's <c>AcceptedInCountryIso31661Alpha2Codes</c> set");
        sb.AppendLine(
            "    /// + <c>AcceptedInCountries</c> nav list via the friend-assembly");
        sb.AppendLine("    /// <c>internal set</c> accessors.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static void WireNav()");
        sb.AppendLine("    {");
        sb.AppendLine("        var grouped = new Dictionary<CurrencyCode, List<Country>>();");
        sb.AppendLine("        foreach (var country in CountryLookup.All)");
        sb.AppendLine("        {");
        sb.AppendLine("            foreach (var cc in country.Currencies)");
        sb.AppendLine("            {");
        sb.AppendLine(
            "                if (!grouped.TryGetValue("
            + "cc.Iso4217AlphaCode, out var list))");
        sb.AppendLine("                {");
        sb.AppendLine("                    list = new List<Country>();");
        sb.AppendLine("                    grouped[cc.Iso4217AlphaCode] = list;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                if (!list.Contains(country))");
        sb.AppendLine("                    list.Add(country);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        foreach (var kvp in grouped)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (!ByCode.TryGetValue(kvp.Key, out var currency))");
        sb.AppendLine("                continue;");
        sb.AppendLine();
        sb.AppendLine("            currency.AcceptedInCountries = kvp.Value.ToArray();");
        sb.AppendLine();
        sb.AppendLine("            var codeSet = new HashSet<CountryCode>();");
        sb.AppendLine("            foreach (var c in kvp.Value)");
        sb.AppendLine("                codeSet.Add(c.Iso31661Alpha2Code);");
        sb.AppendLine();
        sb.AppendLine(
            "            currency.AcceptedInCountryIso31661Alpha2Codes = "
            + "codeSet.ToFrozenSet();");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return ImmutableArray.Create(new EmitResult(
            HintName: "CurrencyLookup.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty));
    }

    private static string CurrencyRecordLiteral(CurrencySpec entry)
    {
        var numeric = DefaultEmitterHelpers.EscapeStringLiteral(
            entry.Iso4217NumericCode ?? string.Empty);
        var displayName = DefaultEmitterHelpers.EscapeStringLiteral(entry.DisplayName);
        var symbol = DefaultEmitterHelpers.EscapeStringLiteral(entry.Symbol ?? string.Empty);

        var sb = new StringBuilder();
        sb.Append("new Currency { ");
        sb.Append($"Iso4217AlphaCode = CurrencyCode.{entry.Iso4217AlphaCode}, ");
        sb.Append($"Iso4217NumericCode = \"{numeric}\", ");
        sb.Append($"DisplayName = \"{displayName}\", ");
        sb.Append($"OfficialName = \"{displayName}\", ");
        sb.Append($"DecimalPlaces = {entry.DecimalPlaces}, ");
        sb.Append($"Symbol = \"{symbol}\", ");
        sb.Append($"IsSupported = {(entry.IsSupported ? "true" : "false")}");
        sb.Append(" }");
        return sb.ToString();
    }

    private static IReadOnlyList<CurrencySpec> SortByAlpha3(IEnumerable<CurrencySpec> entries)
    {
        var list = new List<CurrencySpec>(entries);
        list.Sort(static (a, b) => string.CompareOrdinal(a.Iso4217AlphaCode, b.Iso4217AlphaCode));
        return list;
    }
}
