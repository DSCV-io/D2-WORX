// -----------------------------------------------------------------------
// <copyright file="LocaleDataEmitter.cs" company="DCSV">
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
/// Emits the per-locale DATA — single shape per entity + cycle-resolution
/// via friend-assembly <c>internal set</c> + two-pass populate. Output:
/// <list type="bullet">
///   <item><description>
///     <c>LocaleLookup.g.cs</c> — first pass constructs every
///     <c>Locale</c> record with scalars and code-rep fields populated;
///     wire-nav step mutates each record's <c>Language</c> + <c>Country</c>
///     nav refs via friend-assembly <c>internal set</c> accessors.
///   </description></item>
///   <item><description>
///     <c>LocalesNested.g.cs</c> — <c>Locales.en.US</c> nested static-class
///     hierarchy. Skips 1-segment language-only tags and tags that are
///     strict prefixes of deeper siblings. Both remain accessible via the
///     flat lookup.
///   </description></item>
/// </list>
/// </summary>
internal static class LocaleDataEmitter
{
    private const string _NAMESPACE = DefaultEmitterHelpers.DefaultNamespace;

    /// <summary>
    /// Emits both locale data files. Empty when the spec context lacks a
    /// locales catalog.
    /// </summary>
    /// <param name="context">The aggregate spec context.</param>
    /// <returns>The lookup + nested emit results.</returns>
    public static ImmutableArray<EmitResult> EmitAll(GeoSpecContext context)
    {
        if (context.Locales is not { } localesEnv)
            return ImmutableArray<EmitResult>.Empty;

        var entries = SortByTag(localesEnv.Entries);

        // Build the valid-LanguageCode member set (PascalCased). 162 locales
        // reference ISO 639-2 / 639-3 codes outside the LanguageCode enum;
        // their Language nav stays null. See KNOWN_WARNINGS.md "Language
        // enum scope" for the limitation.
        var validLanguageMembers = new HashSet<string>(System.StringComparer.Ordinal);
        if (context.Languages is { } langsEnv)
        {
            foreach (var lang in langsEnv.Entries)
            {
                var m = PascalCaseLanguageMember(lang.Iso6391Code);
                if (m is not null)
                    validLanguageMembers.Add(m);
            }
        }

        // Build the valid-CountryCode member set so we can defensively skip
        // locales referencing countries not in the catalog.
        var validCountryMembers = new HashSet<string>(System.StringComparer.Ordinal);
        if (context.Countries is { } countriesEnv)
        {
            foreach (var c in countriesEnv.Entries)
            {
                if (c.Iso31661Alpha2Code.Truthy()
                    && DefaultEmitterHelpers.IsValidIdentifier(c.Iso31661Alpha2Code))
                    validCountryMembers.Add(c.Iso31661Alpha2Code);
            }
        }

        return ImmutableArray.Create(
            EmitLookup(entries, validLanguageMembers, validCountryMembers),
            EmitNested(entries));
    }

    private static EmitResult EmitLookup(
        IReadOnlyList<LocaleSpec> entries,
        HashSet<string> validLanguageMembers,
        HashSet<string> validCountryMembers)
    {
        var sb = new StringBuilder();
        DefaultEmitterHelpers.AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine($"using {DefaultEmitterHelpers.AbstractionsNamespace};");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// O(1) lookup tables over the locale catalog. First pass (static");
        sb.AppendLine(
            "/// ctor) constructs every <see cref=\"Locale\"/> record with scalars");
        sb.AppendLine(
            "/// and code-rep fields populated and nav refs set to <c>null</c>;");
        sb.AppendLine(
            "/// wire-nav step (<see cref=\"WireNav\"/>) mutates each record's");
        sb.AppendLine(
            "/// <c>Language</c> + <c>Country</c> nav refs via friend-assembly");
        sb.AppendLine("/// <c>internal set</c> visibility.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class LocaleLookup");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>Locale records indexed by typed "
            + "<see cref=\"LocaleCode\"/>.</summary>");
        sb.AppendLine(
            "    public static readonly FrozenDictionary<LocaleCode, Locale> ByCode;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>All Locale records in spec order "
            + "(alpha-sorted by tag).</summary>");
        sb.AppendLine(
            "    public static readonly IReadOnlyList<Locale> All;");
        sb.AppendLine();

        sb.AppendLine("    static LocaleLookup()");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        // First pass: construct every Locale record "
            + "(nav refs null until wire-nav).");
        sb.AppendLine("        var byCode = new Dictionary<LocaleCode, Locale>();");
        foreach (var entry in entries)
        {
            var tag = DefaultEmitterHelpers.EscapeStringLiteral(entry.IetfBcp47Tag);
            sb.AppendLine(
                $"        byCode[LocaleCode.FromString(\"{tag}\")] = "
                + LocaleRecordLiteral(entry, validLanguageMembers, validCountryMembers) + ";");
        }

        sb.AppendLine();
        sb.AppendLine("        ByCode = byCode.ToFrozenDictionary();");
        sb.AppendLine();
        sb.AppendLine("        var all = new Locale[]");
        sb.AppendLine("        {");
        foreach (var entry in entries)
        {
            var tag = DefaultEmitterHelpers.EscapeStringLiteral(entry.IetfBcp47Tag);
            sb.AppendLine(
                $"            byCode[LocaleCode.FromString(\"{tag}\")],");
        }

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
            "    /// static ctor has run. Mutates each locale's <c>Language</c> +");
        sb.AppendLine(
            "    /// <c>Country</c> nav refs via the friend-assembly");
        sb.AppendLine("    /// <c>internal set</c> accessors.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static void WireNav()");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var locale in All)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (locale.LanguageIso6391Code is { } lc");
        sb.AppendLine("                && LanguageLookup.ByCode.TryGetValue(lc, out var lang))");
        sb.AppendLine("            {");
        sb.AppendLine("                locale.Language = lang;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (locale.CountryIso31661Alpha2Code is { } cc");
        sb.AppendLine("                && CountryLookup.ByCode.TryGetValue(cc, out var country))");
        sb.AppendLine("            {");
        sb.AppendLine("                locale.Country = country;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new EmitResult(
            HintName: "LocaleLookup.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static string LocaleRecordLiteral(
        LocaleSpec entry,
        HashSet<string> validLanguageMembers,
        HashSet<string> validCountryMembers)
    {
        var tag = DefaultEmitterHelpers.EscapeStringLiteral(entry.IetfBcp47Tag);
        var displayName = DefaultEmitterHelpers.EscapeStringLiteral(entry.Name);
        var endonym = DefaultEmitterHelpers.EscapeStringLiteral(entry.Endonym ?? entry.Name);
        var firstDay = DefaultEmitterHelpers.MapDayOfWeek(entry.FirstDayOfWeek);
        var decimalSep = DefaultEmitterHelpers.EscapeStringLiteral(entry.DecimalSeparator);
        var thousandsSep = DefaultEmitterHelpers.EscapeStringLiteral(entry.ThousandsSeparator);
        var dateFormat = DefaultEmitterHelpers.MapDateFormatPattern(entry.DateFormatPattern);

        var sb = new StringBuilder();
        sb.Append("new Locale { ");
        sb.Append($"IetfBcp47Tag = LocaleCode.FromString(\"{tag}\"), ");
        sb.Append($"DisplayName = \"{displayName}\", ");
        sb.Append($"Endonym = \"{endonym}\", ");

        var langMember = PascalCaseLanguageMember(entry.LanguageIso6391Code);
        if (langMember is not null && validLanguageMembers.Contains(langMember))
            sb.Append($"LanguageIso6391Code = LanguageCode.{langMember}, ");
        else
            sb.Append("LanguageIso6391Code = null, ");

        if (entry.CountryIso31661Alpha2Code.Truthy()
            && validCountryMembers.Contains(entry.CountryIso31661Alpha2Code!))
        {
            sb.Append(
                $"CountryIso31661Alpha2Code = CountryCode.{entry.CountryIso31661Alpha2Code}, ");
        }
        else
        {
            sb.Append("CountryIso31661Alpha2Code = null, ");
        }

        sb.Append($"IsSelectable = {(entry.IsSelectable ? "true" : "false")}, ");
        sb.Append($"FirstDayOfWeek = {firstDay}, ");
        sb.Append($"DecimalSeparator = \"{decimalSep}\", ");
        sb.Append($"ThousandsSeparator = \"{thousandsSep}\", ");
        sb.Append($"DateFormatPattern = {dateFormat}");
        sb.Append(" }");
        return sb.ToString();
    }

    private static EmitResult EmitNested(IReadOnlyList<LocaleSpec> entries)
    {
        // Build a trie of locale-tag segments, then walk to emit nested classes.
        var root = new TrieNode();
        var prefixes = new HashSet<string>(System.StringComparer.Ordinal);

        // First pass — record which prefixes are non-leaf (have children deeper
        // than themselves) so we can skip them as leaf members later.
        foreach (var entry in entries)
        {
            var segs = entry.IetfBcp47Tag.Split('-');

            // Every proper prefix is a non-leaf path.
            for (var i = 1; i < segs.Length; i++)
                prefixes.Add(string.Join("-", segs, 0, i));
        }

        // Second pass — insert leaf entries into the trie, skipping 1-seg tags
        // and any multi-seg tag whose tag is itself a recorded prefix of a
        // deeper sibling.
        foreach (var entry in entries)
        {
            var tag = entry.IetfBcp47Tag;
            var segs = tag.Split('-');
            if (segs.Length < 2)
                continue; // skip language-only

            if (prefixes.Contains(tag))
                continue; // this tag is itself a parent of deeper tags

            var node = root;
            for (var i = 0; i < segs.Length; i++)
            {
                var seg = segs[i];
                if (!node.Children.TryGetValue(seg, out var child))
                {
                    child = new TrieNode { Segment = seg };
                    node.Children[seg] = child;
                }

                node = child;

                if (i == segs.Length - 1)
                    node.LeafTag = tag;
            }
        }

        var sb = new StringBuilder();
        DefaultEmitterHelpers.AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("// CS8981: lowercase-only type names like 'en' / 'fr' / 'fy' are warned");
        sb.AppendLine("// against by Roslyn (may collide with future reserved language keywords),");
        sb.AppendLine("// but the nested locale hierarchy intentionally mirrors the IETF BCP 47");
        sb.AppendLine("// lowercase language subtag for cross-runtime parity with the TS-side");
        sb.AppendLine("// `Locales.en.US` const-object. The risk is theoretical (no current /");
        sb.AppendLine("// proposed C# keyword overlaps a real ISO 639-1 / 639-2 code); suppress.");
        sb.AppendLine("#pragma warning disable CS8981");
        sb.AppendLine();
        sb.AppendLine($"using {DefaultEmitterHelpers.AbstractionsNamespace};");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Strongly-typed nested-class hierarchy over the locale catalog —");
        sb.AppendLine(
            "/// <c>Locales.en.US</c> returns a <c>LocaleCode</c>. 1-segment");
        sb.AppendLine(
            "/// language-only tags (e.g. <c>\"en\"</c>) + tags that are strict prefixes");
        sb.AppendLine(
            "/// of deeper sibling tags (e.g. <c>\"az-Arab\"</c> when <c>\"az-Arab-IQ\"</c>");
        sb.AppendLine(
            "/// exists) are skipped here to avoid member/class collisions; both");
        sb.AppendLine(
            "/// remain accessible via <see cref=\"LocaleLookup.ByCode\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class Locales");
        sb.AppendLine("{");

        foreach (var kvp in OrderedChildren(root))
            EmitTrieNode(sb, kvp.Value, indent: 1);

        sb.AppendLine("}");

        return new EmitResult(
            HintName: "LocalesNested.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static void EmitTrieNode(StringBuilder sb, TrieNode node, int indent)
    {
        var pad = new string(' ', indent * 4);
        var memberName = DefaultEmitterHelpers.CoerceToIdentifier(node.Segment ?? string.Empty);
        if (memberName is null)
            return;

        if (node.Children.Count == 0 && node.LeafTag is not null)
        {
            sb.AppendLine(
                $"{pad}/// <summary>Locale <c>{node.LeafTag}</c>.</summary>");
            var leafTag = DefaultEmitterHelpers.EscapeStringLiteral(node.LeafTag);
            sb.AppendLine(
                $"{pad}public static readonly LocaleCode {memberName} = "
                + $"LocaleCode.FromString(\"{leafTag}\");");
        }
        else
        {
            sb.AppendLine(
                $"{pad}/// <summary>Locale subtree under <c>{node.Segment}</c>.</summary>");
            sb.AppendLine($"{pad}public static class {memberName}");
            sb.AppendLine($"{pad}{{");
            foreach (var kvp in OrderedChildren(node))
                EmitTrieNode(sb, kvp.Value, indent + 1);

            sb.AppendLine($"{pad}}}");
        }
    }

    private static IEnumerable<KeyValuePair<string, TrieNode>> OrderedChildren(TrieNode node)
    {
        var list = new List<KeyValuePair<string, TrieNode>>(node.Children);
        list.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
        return list;
    }

    private static string? PascalCaseLanguageMember(string? code)
    {
        if (code.Falsey())
            return null;

        var c = code!;
        var first = char.ToUpperInvariant(c[0]);
        var member = c.Length == 1 ? first.ToString() : first + c.Substring(1).ToLowerInvariant();
        return DefaultEmitterHelpers.IsValidIdentifier(member) ? member : null;
    }

    private static IReadOnlyList<LocaleSpec> SortByTag(IEnumerable<LocaleSpec> entries)
    {
        var list = new List<LocaleSpec>(entries);
        list.Sort(static (a, b) => string.CompareOrdinal(a.IetfBcp47Tag, b.IetfBcp47Tag));
        return list;
    }

    private sealed class TrieNode
    {
        public string? Segment { get; set; }

        public string? LeafTag { get; set; }

        public Dictionary<string, TrieNode> Children { get; } =
            new Dictionary<string, TrieNode>(System.StringComparer.Ordinal);
    }
}
