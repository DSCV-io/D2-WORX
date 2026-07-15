// -----------------------------------------------------------------------
// <copyright file="GeopoliticalEntityDataEmitter.cs" company="DCSV">
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
/// Emits the per-entity DATA — single shape per entity + cycle-resolution
/// via friend-assembly <c>internal set</c> + two-pass populate. Output is
/// a single file (<c>GeopoliticalEntityLookup.g.cs</c>) carrying a static
/// <c>GeopoliticalEntities</c> accessor + a static
/// <c>GeopoliticalEntityLookup</c> class with the FrozenDictionary
/// indexes and the <c>WireNav()</c> wire-nav method.
/// </summary>
/// <remarks>
/// First pass (static ctor) materializes every <c>GeopoliticalEntity</c>
/// record with scalar required-init fields populated (including the
/// typed <c>MemberCountryIso31661Alpha2Codes</c> FrozenSet);
/// <c>MemberCountries</c> nav starts empty. Wire-nav step resolves each
/// member code to its <c>Country</c> record and populates the nav list
/// via the friend-assembly <c>internal set</c> accessor.
/// </remarks>
internal static class GeopoliticalEntityDataEmitter
{
    private const string _NAMESPACE = DefaultEmitterHelpers.DefaultNamespace;

    /// <summary>
    /// Emits the single <c>GeopoliticalEntityLookup.g.cs</c> file. Empty
    /// when the spec context lacks a geopolitical-entities catalog.
    /// </summary>
    /// <param name="context">The aggregate spec context.</param>
    /// <returns>The emit result.</returns>
    public static ImmutableArray<EmitResult> EmitAll(GeoSpecContext context)
    {
        if (context.GeopoliticalEntities is not { } gpesEnv)
            return ImmutableArray<EmitResult>.Empty;

        var entries = SortByShortCode(gpesEnv.Entries);

        // Build the valid-CountryCode member set so we can defensively skip
        // member codes referencing countries not in the catalog (e.g. XK
        // Kosovo not present in countries.spec.json — known orphan).
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

        var sb = new StringBuilder();
        DefaultEmitterHelpers.AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine($"using {DefaultEmitterHelpers.AbstractionsNamespace};");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();

        // -------- GeopoliticalEntities data accessor --------
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Per-entity <see cref=\"GeopoliticalEntity\"/> accessors keyed by");
        sb.AppendLine(
            "/// short code (e.g. <c>GeopoliticalEntities.EU</c>, <c>NATO</c>).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class GeopoliticalEntities");
        sb.AppendLine("{");
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.ShortCode.Falsey() ||
                !DefaultEmitterHelpers.IsValidIdentifier(entry.ShortCode))
                continue;

            if (!seen.Add(entry.ShortCode))
                continue;

            var xmlDoc = DefaultEmitterHelpers.EscapeXmlDoc(entry.Name);
            sb.AppendLine(
                $"    /// <summary>{xmlDoc} ({entry.ShortCode}).</summary>");
            sb.AppendLine(
                $"    public static GeopoliticalEntity {entry.ShortCode} => "
                + $"GeopoliticalEntityLookup.ByCode[GeopoliticalEntityCode.{entry.ShortCode}];");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // -------- Lookup with first pass + wire-nav --------
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// O(1) lookup tables over the geopolitical-entity catalog. First");
        sb.AppendLine(
            "/// pass (static ctor) materializes every <see cref=\"GeopoliticalEntity\"/>");
        sb.AppendLine(
            "/// record with scalar required-init fields populated (including the");
        sb.AppendLine(
            "/// typed <c>MemberCountryIso31661Alpha2Codes</c> FrozenSet) and");
        sb.AppendLine(
            "/// <c>MemberCountries</c> empty; wire-nav step (<see cref=\"WireNav\"/>)");
        sb.AppendLine(
            "/// resolves each member code via <c>CountryLookup</c> and populates");
        sb.AppendLine(
            "/// <c>MemberCountries</c> via friend-assembly <c>internal set</c>");
        sb.AppendLine("/// visibility.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class GeopoliticalEntityLookup");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>Entity records indexed by "
            + "<see cref=\"GeopoliticalEntityCode\"/> enum.</summary>");
        sb.AppendLine(
            "    public static readonly "
            + "FrozenDictionary<GeopoliticalEntityCode, GeopoliticalEntity> ByCode;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>All Entity records in spec order.</summary>");
        sb.AppendLine(
            "    public static readonly IReadOnlyList<GeopoliticalEntity> All;");
        sb.AppendLine();

        sb.AppendLine("    static GeopoliticalEntityLookup()");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        // First pass: construct every record "
            + "(MemberCountries empty until wire-nav).");
        sb.AppendLine(
            "        var byCode = "
            + "new Dictionary<GeopoliticalEntityCode, GeopoliticalEntity>();");
        seen.Clear();
        foreach (var entry in entries)
        {
            if (entry.ShortCode.Falsey() ||
                !DefaultEmitterHelpers.IsValidIdentifier(entry.ShortCode))
                continue;

            if (!seen.Add(entry.ShortCode))
                continue;

            sb.AppendLine(
                $"        byCode[GeopoliticalEntityCode.{entry.ShortCode}] = "
                + GpeRecordLiteral(entry, validCountryMembers) + ";");
        }

        sb.AppendLine();
        sb.AppendLine("        ByCode = byCode.ToFrozenDictionary();");
        sb.AppendLine();
        sb.AppendLine("        var all = new GeopoliticalEntity[]");
        sb.AppendLine("        {");
        seen.Clear();
        foreach (var entry in entries)
        {
            if (entry.ShortCode.Falsey() ||
                !DefaultEmitterHelpers.IsValidIdentifier(entry.ShortCode))
                continue;

            if (!seen.Add(entry.ShortCode))
                continue;

            sb.AppendLine($"            byCode[GeopoliticalEntityCode.{entry.ShortCode}],");
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
            "    /// static ctor has run. Resolves each entity's");
        sb.AppendLine(
            "    /// <c>MemberCountryIso31661Alpha2Codes</c> via <c>CountryLookup</c>");
        sb.AppendLine(
            "    /// and populates <c>MemberCountries</c> via the friend-assembly");
        sb.AppendLine("    /// <c>internal set</c> accessor.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static void WireNav()");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var entity in All)");
        sb.AppendLine("        {");
        sb.AppendLine(
            "            var resolved = new List<Country>("
            + "entity.MemberCountryIso31661Alpha2Codes.Count);");
        sb.AppendLine("            foreach (var cc in entity.MemberCountryIso31661Alpha2Codes)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (CountryLookup.ByCode.TryGetValue(cc, out var country))");
        sb.AppendLine("                    resolved.Add(country);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            entity.MemberCountries = resolved.ToArray();");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return ImmutableArray.Create(new EmitResult(
            HintName: "GeopoliticalEntityLookup.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty));
    }

    private static string GpeRecordLiteral(
        GeopoliticalEntitySpec entry,
        HashSet<string> validCountryMembers)
    {
        var sb = new StringBuilder();
        sb.Append("new GeopoliticalEntity { ");
        sb.Append($"ShortCode = GeopoliticalEntityCode.{entry.ShortCode}, ");
        sb.Append($"DisplayName = \"{DefaultEmitterHelpers.EscapeStringLiteral(entry.Name)}\", ");
        sb.Append($"Type = {DefaultEmitterHelpers.MapGeopoliticalEntityType(entry.Type)}, ");

        var validCodes = new List<string>();
        foreach (var code in entry.CountryIso31661Alpha2Codes)
        {
            if (code.Falsey() || !validCountryMembers.Contains(code))
                continue;

            validCodes.Add(code);
        }

        if (validCodes.Count == 0)
        {
            sb.Append("MemberCountryIso31661Alpha2Codes = FrozenSet<CountryCode>.Empty ");
        }
        else
        {
            sb.Append("MemberCountryIso31661Alpha2Codes = new HashSet<CountryCode> { ");
            var first = true;
            foreach (var code in validCodes)
            {
                if (!first)
                    sb.Append(", ");

                sb.Append($"CountryCode.{code}");
                first = false;
            }

            sb.Append(" }.ToFrozenSet() ");
        }

        sb.Append(" }");
        return sb.ToString();
    }

    private static IReadOnlyList<GeopoliticalEntitySpec> SortByShortCode(
        IEnumerable<GeopoliticalEntitySpec> entries)
    {
        var list = new List<GeopoliticalEntitySpec>(entries);
        list.Sort(static (a, b) => string.CompareOrdinal(a.ShortCode, b.ShortCode));
        return list;
    }
}
