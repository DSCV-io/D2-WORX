// -----------------------------------------------------------------------
// <copyright file="ParentCountryTableEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Emitters;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using DcsvIo.D2.Geo.SourceGen.Spec;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Emits the static lookup table backing
/// <c>SubdivisionCode.ParentCountry</c>. Pre-computed from the
/// subdivisions spec: each ISO 3166-2 code maps to its parent
/// <see cref="Spec.CountrySpec.Iso31661Alpha2Code"/> via the spec's
/// <c>countryISO31661Alpha2Code</c> back-reference. The lookup ships as
/// a sealed internal static class so the cross-cutting derivation lives
/// in one well-known place; the wrapper struct just dispatches via
/// <c>SubdivisionParentCountryLookup.GetParent</c>.
/// </summary>
internal static class ParentCountryTableEmitter
{
    private const string _NAMESPACE = EmitterHelpers.AbstractionsNamespace;

    /// <summary>
    /// Emits the parent-country lookup. Returns a singleton result; an
    /// absent <c>subdivisions.spec.json</c> produces an empty table that
    /// throws on every lookup (no silent fallback).
    /// </summary>
    /// <param name="context">The aggregate spec context.</param>
    /// <returns>The single emit result.</returns>
    public static ImmutableArray<EmitResult> EmitAll(GeoSpecContext context)
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// O(1) lookup from <see cref=\"SubdivisionCode\"/> string value to its");
        sb.AppendLine(
            "/// owning <see cref=\"Country\"/>. Pre-computed at codegen time from");
        sb.AppendLine(
            "/// <c>subdivisions.spec.json</c> — every subdivision's parent country");
        sb.AppendLine(
            "/// is derived once at codegen time. Consumed exclusively by");
        sb.AppendLine(
            "/// <see cref=\"SubdivisionCode.ParentCountry\"/>; not part of the public API.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static class SubdivisionParentCountryLookup");
        sb.AppendLine("{");
        sb.AppendLine(
            "    private static readonly FrozenDictionary<string, CountryCode> "
            + "sr_parents =");
        sb.AppendLine("        new Dictionary<string, CountryCode>(StringComparer.Ordinal)");
        sb.AppendLine("        {");

        if (context.Subdivisions is { } subs)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var sorted = new List<SubdivisionSpec>(subs.Entries);
            sorted.Sort(static (a, b) => string.CompareOrdinal(
                a.Iso31662Code, b.Iso31662Code));

            foreach (var entry in sorted)
            {
                if (entry.Iso31662Code.Falsey() ||
                    entry.CountryIso31661Alpha2Code.Falsey())
                    continue;

                if (!seen.Add(entry.Iso31662Code))
                    continue;

                sb.AppendLine(
                    $"            [\"{EscapeStringLiteral(entry.Iso31662Code)}\"] = "
                    + $"CountryCode.{entry.CountryIso31661Alpha2Code},");
            }
        }

        sb.AppendLine("        }.ToFrozenDictionary(StringComparer.Ordinal);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Resolves the parent <see cref=\"CountryCode\"/> for the supplied");
        sb.AppendLine(
            "    /// <paramref name=\"subdivisionCode\"/>. Throws");
        sb.AppendLine(
            "    /// <see cref=\"InvalidOperationException\"/> when the code is not present");
        sb.AppendLine("    /// in the catalog — fail-loud is intentional (every codegen-issued");
        sb.AppendLine("    /// SubdivisionCode must be in the table; absence is a bug, not a");
        sb.AppendLine("    /// user-input case).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"subdivisionCode\">The raw subdivision code.</param>");
        sb.AppendLine("    /// <returns>The owning country.</returns>");
        sb.AppendLine("    public static CountryCode GetParent(string subdivisionCode)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (subdivisionCode is null)");
        sb.AppendLine(
            "            throw new ArgumentNullException(nameof(subdivisionCode));");
        sb.AppendLine();
        sb.AppendLine("        if (!sr_parents.TryGetValue(subdivisionCode, out var country))");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new InvalidOperationException(");
        sb.AppendLine(
            "                $\"Subdivision code '{subdivisionCode}' is not present "
            + "in the geo catalog.\");");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return country;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return ImmutableArray.Create(new EmitResult(
            HintName: "SubdivisionParentCountryLookup.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty));
    }

    private static void AppendFileHeader(StringBuilder sb) =>
        EmitterHelpers.AppendFileHeader(sb);

    private static string EscapeStringLiteral(string value) =>
        EmitterHelpers.EscapeStringLiteral(value);
}
