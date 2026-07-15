// -----------------------------------------------------------------------
// <copyright file="LanguageDataEmitter.cs" company="DCSV">
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
/// Emits the per-language DATA — single shape per entity + cycle-resolution
/// via friend-assembly <c>internal set</c> + two-pass populate. Output is a
/// single file (<c>LanguageLookup.g.cs</c>) carrying a static
/// <c>Languages</c> accessor + a static <c>LanguageLookup</c> class with
/// the FrozenDictionary indexes and the <c>WireNav()</c> wire-nav method.
/// </summary>
/// <remarks>
/// First pass (static ctor) materializes every <c>Language</c> record with
/// scalar required-init fields populated; reverse-nav reps
/// (<c>SpokenInCountries</c> + <c>SpokenInCountryIso31661Alpha2Codes</c> +
/// <c>Locales</c> + <c>LocaleIetfBcp47Tags</c>) start empty. Wire-nav step
/// walks the country + locale catalogs and populates both reps via the
/// friend-assembly <c>internal set</c> accessors.
/// </remarks>
internal static class LanguageDataEmitter
{
    private const string _NAMESPACE = DefaultEmitterHelpers.DefaultNamespace;

    /// <summary>
    /// Emits the single <c>LanguageLookup.g.cs</c> file. Empty when the
    /// spec context lacks a languages catalog.
    /// </summary>
    /// <param name="context">The aggregate spec context.</param>
    /// <returns>The emit result.</returns>
    public static ImmutableArray<EmitResult> EmitAll(GeoSpecContext context)
    {
        if (context.Languages is not { } languagesEnv)
            return ImmutableArray<EmitResult>.Empty;

        var entries = SortByCode(languagesEnv.Entries);

        var sb = new StringBuilder();
        DefaultEmitterHelpers.AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine($"using {DefaultEmitterHelpers.AbstractionsNamespace};");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();

        // -------- Languages data accessor --------
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Per-language <see cref=\"Language\"/> accessors keyed by PascalCased");
        sb.AppendLine(
            "/// ISO 639-1 (e.g. <c>Languages.En</c>) — matches the");
        sb.AppendLine(
            "/// <see cref=\"LanguageCode\"/> enum member casing.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class Languages");
        sb.AppendLine("{");
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var member = PascalCaseMember(entry.Iso6391Code);
            if (member is null || !seen.Add(member))
                continue;

            var xmlDoc = DefaultEmitterHelpers.EscapeXmlDoc(entry.Name);
            sb.AppendLine(
                $"    /// <summary>{xmlDoc} ({entry.Iso6391Code}).</summary>");
            sb.AppendLine(
                $"    public static Language {member} => "
                + $"LanguageLookup.ByCode[LanguageCode.{member}];");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // -------- Lookup with first pass + wire-nav --------
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// O(1) lookup tables over the language catalog. First pass (static");
        sb.AppendLine(
            "/// ctor) materializes every <see cref=\"Language\"/> record with");
        sb.AppendLine(
            "/// scalar required-init fields populated and reverse navs empty;");
        sb.AppendLine(
            "/// wire-nav step (<see cref=\"WireNav\"/>) walks the country + locale");
        sb.AppendLine(
            "/// catalogs and populates both the typed code sets and the record");
        sb.AppendLine(
            "/// lists via the friend-assembly <c>internal set</c> accessors.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class LanguageLookup");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>Language records indexed by "
            + "<see cref=\"LanguageCode\"/> enum.</summary>");
        sb.AppendLine(
            "    public static readonly FrozenDictionary<LanguageCode, Language> ByCode;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Language records indexed by ISO 639-1 lowercase string.</summary>");
        sb.AppendLine(
            "    public static readonly FrozenDictionary<string, Language> ByIso6391;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>All Language records in spec order.</summary>");
        sb.AppendLine(
            "    public static readonly IReadOnlyList<Language> All;");
        sb.AppendLine();

        sb.AppendLine("    static LanguageLookup()");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        // First pass: construct every Language record "
            + "(reverse navs empty until wire-nav).");
        sb.AppendLine("        var byCode = new Dictionary<LanguageCode, Language>();");
        seen.Clear();
        foreach (var entry in entries)
        {
            var member = PascalCaseMember(entry.Iso6391Code);
            if (member is null || !seen.Add(member))
                continue;

            sb.AppendLine(
                $"        byCode[LanguageCode.{member}] = "
                + LanguageRecordLiteral(entry, member) + ";");
        }

        sb.AppendLine();
        sb.AppendLine("        ByCode = byCode.ToFrozenDictionary();");
        sb.AppendLine();
        sb.AppendLine(
            "        var byIso = new Dictionary<string, Language>("
            + "System.StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine("        foreach (var kvp in byCode)");
        sb.AppendLine(
            "            byIso[kvp.Value.Iso6391Code.ToString().ToLowerInvariant()] = "
            + "kvp.Value;");
        sb.AppendLine();
        sb.AppendLine(
            "        ByIso6391 = byIso.ToFrozenDictionary("
            + "System.StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine();
        sb.AppendLine("        var all = new Language[]");
        sb.AppendLine("        {");
        seen.Clear();
        foreach (var entry in entries)
        {
            var member = PascalCaseMember(entry.Iso6391Code);
            if (member is null || !seen.Add(member))
                continue;

            sb.AppendLine($"            byCode[LanguageCode.{member}],");
        }

        sb.AppendLine("        };");
        sb.AppendLine("        All = all;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // WireNav.
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Wire-nav step of the two-pass populate pattern. Invoked exactly");
        sb.AppendLine(
            "    /// once by <c>GeoDataInitializer</c> after every catalog's first-pass");
        sb.AppendLine(
            "    /// static ctor has run. Populates each language's");
        sb.AppendLine(
            "    /// <c>SpokenInCountries</c> + <c>SpokenInCountryIso31661Alpha2Codes</c>");
        sb.AppendLine(
            "    /// (where the country's PrimaryLanguage matches) and");
        sb.AppendLine(
            "    /// <c>Locales</c> + <c>LocaleIetfBcp47Tags</c> (where the locale's");
        sb.AppendLine(
            "    /// Language matches) via the friend-assembly <c>internal set</c>");
        sb.AppendLine("    /// accessors.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static void WireNav()");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        var countriesByLanguage = "
            + "new Dictionary<LanguageCode, List<Country>>();");
        sb.AppendLine("        foreach (var country in CountryLookup.All)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (country.PrimaryLanguage is null)");
        sb.AppendLine("                continue;");
        sb.AppendLine();
        sb.AppendLine("            var code = country.PrimaryLanguage.Iso6391Code;");
        sb.AppendLine("            if (!countriesByLanguage.TryGetValue(code, out var list))");
        sb.AppendLine("            {");
        sb.AppendLine("                list = new List<Country>();");
        sb.AppendLine("                countriesByLanguage[code] = list;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            list.Add(country);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine(
            "        var localesByLanguage = "
            + "new Dictionary<LanguageCode, List<Locale>>();");
        sb.AppendLine("        foreach (var locale in LocaleLookup.All)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (locale.Language is null)");
        sb.AppendLine("                continue;");
        sb.AppendLine();
        sb.AppendLine("            var code = locale.Language.Iso6391Code;");
        sb.AppendLine("            if (!localesByLanguage.TryGetValue(code, out var list))");
        sb.AppendLine("            {");
        sb.AppendLine("                list = new List<Locale>();");
        sb.AppendLine("                localesByLanguage[code] = list;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            list.Add(locale);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        foreach (var language in All)");
        sb.AppendLine("        {");
        sb.AppendLine(
            "            if (countriesByLanguage.TryGetValue("
            + "language.Iso6391Code, out var countries))");
        sb.AppendLine("            {");
        sb.AppendLine("                language.SpokenInCountries = countries.ToArray();");
        sb.AppendLine();
        sb.AppendLine("                var countryCodeSet = new HashSet<CountryCode>();");
        sb.AppendLine("                foreach (var c in countries)");
        sb.AppendLine("                    countryCodeSet.Add(c.Iso31661Alpha2Code);");
        sb.AppendLine();
        sb.AppendLine(
            "                language.SpokenInCountryIso31661Alpha2Codes = "
            + "countryCodeSet.ToFrozenSet();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine(
            "            if (localesByLanguage.TryGetValue("
            + "language.Iso6391Code, out var locales))");
        sb.AppendLine("            {");
        sb.AppendLine("                language.Locales = locales.ToArray();");
        sb.AppendLine();
        sb.AppendLine("                var localeCodeSet = new HashSet<LocaleCode>();");
        sb.AppendLine("                foreach (var l in locales)");
        sb.AppendLine("                    localeCodeSet.Add(l.IetfBcp47Tag);");
        sb.AppendLine();
        sb.AppendLine(
            "                language.LocaleIetfBcp47Tags = "
            + "localeCodeSet.ToFrozenSet();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return ImmutableArray.Create(new EmitResult(
            HintName: "LanguageLookup.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty));
    }

    private static string LanguageRecordLiteral(LanguageSpec entry, string member)
    {
        var displayName = DefaultEmitterHelpers.EscapeStringLiteral(entry.Name);
        var endonym = DefaultEmitterHelpers.EscapeStringLiteral(entry.Endonym ?? entry.Name);
        var writingDirection = DefaultEmitterHelpers.MapWritingDirection(entry.WritingDirection);

        var sb = new StringBuilder();
        sb.Append("new Language { ");
        sb.Append($"Iso6391Code = LanguageCode.{member}, ");
        sb.Append($"DisplayName = \"{displayName}\", ");
        sb.Append($"Endonym = \"{endonym}\", ");
        sb.Append($"WritingDirection = {writingDirection}, ");
        sb.Append($"IsSupported = {(entry.IsSupported ? "true" : "false")}");
        sb.Append(" }");
        return sb.ToString();
    }

    private static string? PascalCaseMember(string? code)
    {
        if (code.Falsey())
            return null;

        var c = code!;
        var first = char.ToUpperInvariant(c[0]);
        var member = c.Length == 1 ? first.ToString() : first + c.Substring(1).ToLowerInvariant();

        return DefaultEmitterHelpers.IsValidIdentifier(member) ? member : null;
    }

    private static IReadOnlyList<LanguageSpec> SortByCode(IEnumerable<LanguageSpec> entries)
    {
        var list = new List<LanguageSpec>(entries);
        list.Sort(static (a, b) => string.CompareOrdinal(a.Iso6391Code, b.Iso6391Code));
        return list;
    }
}
