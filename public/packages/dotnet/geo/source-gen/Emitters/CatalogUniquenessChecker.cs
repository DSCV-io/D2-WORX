// -----------------------------------------------------------------------
// <copyright file="CatalogUniquenessChecker.cs" company="DCSV">
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
/// Enforces catalog-uniqueness as a build-time safety gate: every catalog
/// must have unique normalized names across all matchable name fields per
/// entity. The check normalizes each name field (NFD + strip Mn + invariant
/// casefold + collapse whitespace) and asserts no two entities in the same
/// catalog share the same normalized form. Surfaces
/// <see cref="DiagnosticIds.DuplicateNormalizedName"/> on collisions; build
/// fails on the diagnostic. Eliminates the "first match wins" determinism
/// risk in <c>IGeoNameResolver</c> at the source.
/// </summary>
internal static class CatalogUniquenessChecker
{
    /// <summary>
    /// Runs the uniqueness check across every loaded catalog. Returns one
    /// diagnostic per (catalog, normalized-name) collision.
    /// </summary>
    /// <param name="context">The aggregate spec context.</param>
    /// <returns>Collision diagnostics; empty when every catalog is clean.</returns>
    public static ImmutableArray<EmitDiagnostic> Check(GeoSpecContext context)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();

        if (context.Countries is { } countries)
        {
            CheckCatalog(
                specName: "countries.spec.json",
                entries: ExtractCountryNames(countries.Entries),
                diagnostics: diagnostics);
        }

        if (context.Subdivisions is { } subdivisions)
        {
            // Per-country bucketed check — "Georgia" is both US-GA and the
            // country GE, "Northern Province" is a legitimate name in several
            // countries. Resolver scope is mandatory per-country (see
            // IGeoNameResolver.TryResolveSubdivisionByName), so collisions are
            // only meaningful within the same parent-country bucket.
            CheckSubdivisionsPerCountry(subdivisions.Entries, diagnostics);
        }

        // The name-resolver covers Country + Subdivision only — the other
        // catalogs are addressable solely by their typed code, so duplicate
        // display names within them carry no resolver-determinism risk.
        // Running the uniqueness check only on resolver-bearing catalogs
        // also avoids false-triggering on legitimate cross-language locale-
        // name overlaps (e.g. "Afar" as Locale on multiple regional
        // variants).
        _ = context.Currencies;
        _ = context.Languages;
        _ = context.Locales;
        _ = context.Timezones;
        _ = context.GeopoliticalEntities;

        return diagnostics.ToImmutable();
    }

    private static void CheckCatalog(
        string specName,
        IEnumerable<(string EntityId, IEnumerable<string> Names)> entries,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        var normalizedToEntities =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var (entityId, names) in entries)
        {
            if (entityId.Falsey())
                continue;

            // Deduplicate per-entity (some entities have the same name in
            // multiple fields — DisplayName == OfficialName is common).
            var perEntitySeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in names)
            {
                if (name.Falsey())
                    continue;

                var normalized = Normalize(name);
                if (normalized.Falsey())
                    continue;

                if (!perEntitySeen.Add(normalized))
                    continue;

                if (!normalizedToEntities.TryGetValue(normalized, out var owners))
                {
                    owners = new List<string>();
                    normalizedToEntities[normalized] = owners;
                }

                owners.Add(entityId);
            }
        }

        foreach (var kvp in normalizedToEntities)
        {
            if (kvp.Value.Count < 2)
                continue;

            // Stable-sort the colliding entity ids for deterministic
            // diagnostic output across runs.
            kvp.Value.Sort(StringComparer.Ordinal);
            diagnostics.Add(EmitDiagnostics.DuplicateNormalizedName(
                specName, kvp.Key, string.Join(", ", kvp.Value)));
        }
    }

    private static IEnumerable<(string EntityId, IEnumerable<string> Names)> ExtractCountryNames(
        IReadOnlyList<CountrySpec> entries)
    {
        foreach (var entry in entries)
        {
            yield return (entry.Iso31661Alpha2Code, NamesOf(entry));
        }
    }

    private static IEnumerable<string> NamesOf(CountrySpec entry)
    {
        // Display-form names only — codes (alpha-2 / alpha-3) are addressable
        // by their typed identifiers, never typed free-form into a resolver
        // query.
        yield return entry.DisplayName;
        yield return entry.OfficialName;
        if (entry.EndonymDisplayName.Truthy())
            yield return entry.EndonymDisplayName!;
    }

    private static void CheckSubdivisionsPerCountry(
        IReadOnlyList<SubdivisionSpec> entries,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        // Bucket per parent country, then run the same collision detection
        // against each country's subdivision set.
        var byCountry = new Dictionary<string, List<SubdivisionSpec>>(
            StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (entry.CountryIso31661Alpha2Code.Falsey())
                continue;

            if (!byCountry.TryGetValue(entry.CountryIso31661Alpha2Code, out var bucket))
            {
                bucket = new List<SubdivisionSpec>();
                byCountry[entry.CountryIso31661Alpha2Code] = bucket;
            }

            bucket.Add(entry);
        }

        foreach (var kvp in byCountry)
        {
            CheckCatalog(
                specName: $"subdivisions.spec.json [country {kvp.Key}]",
                entries: ProjectSubdivisionNames(kvp.Value),
                diagnostics: diagnostics);
        }
    }

    private static IEnumerable<(string EntityId, IEnumerable<string> Names)>
        ProjectSubdivisionNames(IReadOnlyList<SubdivisionSpec> entries)
    {
        foreach (var entry in entries)
        {
            yield return (entry.Iso31662Code, NamesOf(entry));
        }
    }

    private static IEnumerable<string> NamesOf(SubdivisionSpec entry)
    {
        // Display-form names only — ShortCode + Iso31662Code are addressable
        // by their typed identifiers, never typed free-form into a resolver
        // query.
        yield return entry.DisplayName;
        yield return entry.OfficialName;
        if (entry.EndonymDisplayName.Truthy())
            yield return entry.EndonymDisplayName!;
    }

    private static string Normalize(string input)
    {
        if (input.Falsey())
            return string.Empty;

        var decomposed = input.Normalize(NormalizationForm.FormD);

        var stripped = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                stripped.Append(ch);
        }

        var lowered = CultureInfo.InvariantCulture.TextInfo.ToLower(stripped.ToString());

        // Collapse whitespace + trim.
        var trimmed = lowered.AsSpan().Trim();
        if (trimmed.IsEmpty)
            return string.Empty;

        var collapsed = new StringBuilder(trimmed.Length);
        var previousWasSpace = false;
        foreach (var ch in trimmed)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasSpace)
                {
                    collapsed.Append(' ');
                    previousWasSpace = true;
                }
            }
            else
            {
                collapsed.Append(ch);
                previousWasSpace = false;
            }
        }

        return collapsed.ToString();
    }
}
