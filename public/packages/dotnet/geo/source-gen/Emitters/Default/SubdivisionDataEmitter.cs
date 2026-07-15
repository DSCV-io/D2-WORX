// -----------------------------------------------------------------------
// <copyright file="SubdivisionDataEmitter.cs" company="DCSV">
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
/// Emits the per-subdivision DATA in two files:
/// <list type="bullet">
///   <item><description>
///     <c>SubdivisionLookup.g.cs</c> — first pass constructs every
///     <c>Subdivision</c> record with the parent <c>Country</c> nav set to
///     <c>null</c>; wire-nav step mutates each record's <c>Country</c> +
///     <c>ParentSubdivision</c> navs via the friend-assembly
///     <c>internal set</c> accessor. The lookup carries
///     <c>FrozenDictionary&lt;SubdivisionCode, Subdivision&gt; ByCode</c>
///     plus a per-country index
///     <c>FrozenDictionary&lt;CountryCode, IReadOnlyList&lt;Subdivision&gt;&gt; ByCountry</c>
///     that <c>CountryLookup.WireNav</c> consumes.
///   </description></item>
///   <item><description>
///     <c>SubdivisionsNested.g.cs</c> — <c>Subdivisions.US.NY</c> nested
///     static-class hierarchy where every leaf is a
///     <c>SubdivisionCode</c> constant. Codes that start with a digit get
///     a leading underscore.
///   </description></item>
/// </list>
/// </summary>
internal static class SubdivisionDataEmitter
{
    private const string _NAMESPACE = DefaultEmitterHelpers.DefaultNamespace;

    /// <summary>
    /// Emits both subdivision data files. Returns an empty array when the
    /// spec context lacks a subdivisions catalog.
    /// </summary>
    /// <param name="context">The aggregate spec context.</param>
    /// <returns>The lookup + nested emit results.</returns>
    public static ImmutableArray<EmitResult> EmitAll(GeoSpecContext context)
    {
        if (context.Subdivisions is not { } subsEnv)
            return ImmutableArray<EmitResult>.Empty;

        // Build the valid-country member set so we can defensively skip
        // subdivisions referencing countries that aren't in the catalog.
        var validCountryMembers = new HashSet<string>(System.StringComparer.Ordinal);
        if (context.Countries is { } countriesEnv)
        {
            foreach (var country in countriesEnv.Entries)
            {
                if (country.Iso31661Alpha2Code.Truthy()
                    && DefaultEmitterHelpers.IsValidIdentifier(country.Iso31661Alpha2Code))
                    validCountryMembers.Add(country.Iso31661Alpha2Code);
            }
        }

        var filtered = new List<SubdivisionSpec>();
        foreach (var s in subsEnv.Entries)
        {
            if (s.CountryIso31661Alpha2Code.Falsey()
                || !validCountryMembers.Contains(s.CountryIso31661Alpha2Code))
                continue;

            filtered.Add(s);
        }

        var entries = SortByCode(filtered);

        // Build the set of subdivision-codes we actually emitted so we can
        // skip ParentSubdivisionIso31662Code refs that point at filtered-out
        // entries.
        var validSubdivisionCodes = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var s in entries)
            validSubdivisionCodes.Add(s.Iso31662Code);

        return ImmutableArray.Create(
            EmitLookup(entries, validSubdivisionCodes),
            EmitNested(entries));
    }

    private static EmitResult EmitLookup(
        IReadOnlyList<SubdivisionSpec> entries,
        HashSet<string> validSubdivisionCodes)
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
            "/// O(1) lookup tables over the subdivision catalog. First pass");
        sb.AppendLine(
            "/// (static ctor) constructs every <see cref=\"Subdivision\"/> record");
        sb.AppendLine(
            "/// with <c>Country</c> + <c>ParentSubdivision</c> nav refs set to");
        sb.AppendLine(
            "/// <c>null</c>; wire-nav step (<see cref=\"WireNav\"/>) mutates");
        sb.AppendLine(
            "/// each record's nav refs via friend-assembly <c>internal set</c>");
        sb.AppendLine(
            "/// visibility. The <see cref=\"ByCountry\"/> per-country index is");
        sb.AppendLine(
            "/// consumed by <c>CountryLookup.WireNav</c> to populate");
        sb.AppendLine("/// <c>Country.Subdivisions</c>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class SubdivisionLookup");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>Subdivision records indexed by typed "
            + "<see cref=\"SubdivisionCode\"/>.</summary>");
        sb.AppendLine(
            "    public static readonly FrozenDictionary<SubdivisionCode, Subdivision> ByCode;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Subdivision records grouped by parent "
            + "<see cref=\"CountryCode\"/>.</summary>");
        sb.AppendLine(
            "    public static readonly "
            + "FrozenDictionary<CountryCode, IReadOnlyList<Subdivision>> ByCountry;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>All Subdivision records in spec order "
            + "(alpha-sorted by code).</summary>");
        sb.AppendLine(
            "    public static readonly IReadOnlyList<Subdivision> All;");
        sb.AppendLine();

        // -------- First-pass static ctor --------
        sb.AppendLine("    static SubdivisionLookup()");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        // First pass: construct every Subdivision record "
            + "(nav refs null until wire-nav).");
        sb.AppendLine("        var byCode = new Dictionary<SubdivisionCode, Subdivision>();");
        foreach (var entry in entries)
        {
            var code = DefaultEmitterHelpers.EscapeStringLiteral(entry.Iso31662Code);
            sb.AppendLine(
                $"        byCode[SubdivisionCode.FromString(\"{code}\")] = "
                + SubdivisionRecordLiteral(entry, validSubdivisionCodes) + ";");
        }

        sb.AppendLine();
        sb.AppendLine("        ByCode = byCode.ToFrozenDictionary();");
        sb.AppendLine();
        sb.AppendLine("        var all = new Subdivision[]");
        sb.AppendLine("        {");
        foreach (var entry in entries)
        {
            var code = DefaultEmitterHelpers.EscapeStringLiteral(entry.Iso31662Code);
            sb.AppendLine(
                $"            byCode[SubdivisionCode.FromString(\"{code}\")],");
        }

        sb.AppendLine("        };");
        sb.AppendLine("        All = all;");
        sb.AppendLine();

        // Per-country index — group by typed CountryCode.
        sb.AppendLine(
            "        var byCountryMutable = "
            + "new Dictionary<CountryCode, List<Subdivision>>();");
        sb.AppendLine("        foreach (var sub in all)");
        sb.AppendLine("        {");
        sb.AppendLine("            var cc = sub.CountryIso31661Alpha2Code;");
        sb.AppendLine("            if (!byCountryMutable.TryGetValue(cc, out var list))");
        sb.AppendLine("            {");
        sb.AppendLine("                list = new List<Subdivision>();");
        sb.AppendLine("                byCountryMutable[cc] = list;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            list.Add(sub);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine(
            "        var byCountry = "
            + "new Dictionary<CountryCode, IReadOnlyList<Subdivision>>();");
        sb.AppendLine("        foreach (var kvp in byCountryMutable)");
        sb.AppendLine("            byCountry[kvp.Key] = kvp.Value.ToArray();");
        sb.AppendLine();
        sb.AppendLine("        ByCountry = byCountry.ToFrozenDictionary();");
        sb.AppendLine("    }");
        sb.AppendLine();

        // -------- WireNav --------
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Wire-nav step of the two-pass populate pattern. Invoked exactly");
        sb.AppendLine(
            "    /// once by <c>GeoDataInitializer</c> after every catalog's first-pass");
        sb.AppendLine(
            "    /// static ctor has run. Mutates each subdivision's <c>Country</c> +");
        sb.AppendLine(
            "    /// <c>ParentSubdivision</c> navs via the friend-assembly");
        sb.AppendLine("    /// <c>internal set</c> accessors.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static void WireNav()");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var sub in All)");
        sb.AppendLine("        {");
        sb.AppendLine(
            "            if (CountryLookup.ByCode.TryGetValue("
            + "sub.CountryIso31661Alpha2Code, out var country))");
        sb.AppendLine("                sub.Country = country;");
        sb.AppendLine();
        sb.AppendLine("            if (sub.ParentSubdivisionIso31662Code is { } parentCode");
        sb.AppendLine("                && ByCode.TryGetValue(parentCode, out var parent))");
        sb.AppendLine("            {");
        sb.AppendLine("                sub.ParentSubdivision = parent;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new EmitResult(
            HintName: "SubdivisionLookup.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static string SubdivisionRecordLiteral(
        SubdivisionSpec entry,
        HashSet<string> validSubdivisionCodes)
    {
        var endonymDisplay = entry.EndonymDisplayName ?? entry.DisplayName;
        var endonymOfficial = entry.EndonymDisplayName ?? entry.OfficialName;

        var code = DefaultEmitterHelpers.EscapeStringLiteral(entry.Iso31662Code);
        var shortCode = DefaultEmitterHelpers.EscapeStringLiteral(entry.ShortCode);
        var displayName = DefaultEmitterHelpers.EscapeStringLiteral(entry.DisplayName);
        var officialName = DefaultEmitterHelpers.EscapeStringLiteral(entry.OfficialName);
        var endonymDisplayLit = DefaultEmitterHelpers.EscapeStringLiteral(endonymDisplay);
        var endonymOfficialLit = DefaultEmitterHelpers.EscapeStringLiteral(endonymOfficial);
        var typeLit = DefaultEmitterHelpers.EscapeStringLiteral(entry.Type ?? string.Empty);

        var sb = new StringBuilder();
        sb.Append("new Subdivision { ");
        sb.Append($"Iso31662Code = SubdivisionCode.FromString(\"{code}\"), ");
        sb.Append($"ShortCode = \"{shortCode}\", ");
        sb.Append($"DisplayName = \"{displayName}\", ");
        sb.Append($"OfficialName = \"{officialName}\", ");
        sb.Append($"EndonymDisplayName = \"{endonymDisplayLit}\", ");
        sb.Append($"EndonymOfficialName = \"{endonymOfficialLit}\", ");
        sb.Append($"CountryIso31661Alpha2Code = CountryCode.{entry.CountryIso31661Alpha2Code}, ");
        if (entry.ParentIso31662Code.Truthy()
            && validSubdivisionCodes.Contains(entry.ParentIso31662Code!))
        {
            var parent = DefaultEmitterHelpers.EscapeStringLiteral(entry.ParentIso31662Code!);
            sb.Append(
                $"ParentSubdivisionIso31662Code = SubdivisionCode.FromString(\"{parent}\"), ");
        }
        else
        {
            sb.Append("ParentSubdivisionIso31662Code = null, ");
        }

        sb.Append($"Type = \"{typeLit}\"");
        sb.Append(" }");
        return sb.ToString();
    }

    private static EmitResult EmitNested(IReadOnlyList<SubdivisionSpec> entries)
    {
        // Group entries by country alpha-2 — the nested class layer.
        var grouped = new SortedDictionary<string, List<SubdivisionSpec>>(
            System.StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var country = entry.CountryIso31661Alpha2Code;
            if (country.Falsey())
                continue;

            if (!grouped.TryGetValue(country, out var list))
            {
                list = new List<SubdivisionSpec>();
                grouped[country] = list;
            }

            list.Add(entry);
        }

        var sb = new StringBuilder();
        DefaultEmitterHelpers.AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine($"using {DefaultEmitterHelpers.AbstractionsNamespace};");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Strongly-typed nested-class hierarchy over the subdivision catalog —");
        sb.AppendLine(
            "/// <c>Subdivisions.US.NY</c> returns a <see cref=\"SubdivisionCode\"/>.");
        sb.AppendLine(
            "/// Short codes that start with a digit (e.g. <c>\"02\"</c>) carry a");
        sb.AppendLine(
            "/// leading underscore (<c>Subdivisions.AD._02</c>) to satisfy C#'s");
        sb.AppendLine("/// identifier rules; the underlying wrapped string is unchanged.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class Subdivisions");
        sb.AppendLine("{");

        foreach (var kvp in grouped)
        {
            var country = kvp.Key;
            sb.AppendLine($"    /// <summary>Subdivisions of {country}.</summary>");
            sb.AppendLine($"    public static class {country}");
            sb.AppendLine("    {");
            foreach (var entry in kvp.Value)
            {
                var memberName = DefaultEmitterHelpers.CoerceToIdentifier(entry.ShortCode);
                if (memberName is null)
                    continue;

                // C# disallows a member with the same name as its enclosing type
                // (CS0542) — e.g. `BZ-BZ` would produce `Subdivisions.BZ.BZ`.
                // Prefix with `_` (`Subdivisions.BZ._BZ`); the underlying wrapped
                // string is unchanged.
                if (string.Equals(memberName, country, System.StringComparison.Ordinal))
                    memberName = "_" + memberName;

                var xmlDoc = DefaultEmitterHelpers.EscapeXmlDoc(entry.DisplayName);
                var code = DefaultEmitterHelpers.EscapeStringLiteral(entry.Iso31662Code);
                sb.AppendLine(
                    $"        /// <summary>{xmlDoc} ({entry.Iso31662Code}).</summary>");
                sb.AppendLine(
                    $"        public static readonly SubdivisionCode {memberName} = "
                    + $"SubdivisionCode.FromString(\"{code}\");");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return new EmitResult(
            HintName: "SubdivisionsNested.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static IReadOnlyList<SubdivisionSpec> SortByCode(IEnumerable<SubdivisionSpec> entries)
    {
        var list = new List<SubdivisionSpec>(entries);
        list.Sort(static (a, b) => string.CompareOrdinal(a.Iso31662Code, b.Iso31662Code));
        return list;
    }
}
