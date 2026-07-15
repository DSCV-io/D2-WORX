// -----------------------------------------------------------------------
// <copyright file="CategorySpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Registry.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;

/// <summary>
/// Reads the closed <c>ErrorCategory</c> wire set from
/// <c>error-category.spec.json</c> (surfaced via <c>AdditionalFiles</c>). The
/// registry generator cross-checks every code's <c>category</c> against this
/// set so a code referencing an unknown category fails with a clear diagnostic
/// (<c>D2ERC007</c>) instead of emitting a reference to a non-existent enum
/// member.
/// </summary>
internal static class CategorySpecLoader
{
    private const string _CATEGORIES_KEY = "categories";
    private const string _WIRE_KEY = "wire";

    /// <summary>
    /// Parses the error-category spec content into the closed set of category
    /// wire strings. Returns an empty set if the spec is malformed (the
    /// membership check then degrades to a no-op, leaving the existing
    /// per-spec validation in place).
    /// </summary>
    /// <param name="json">Raw JSON content of <c>error-category.spec.json</c>.</param>
    /// <returns>The set of category wire strings (ordinal comparison).</returns>
    public static ImmutableHashSet<string> LoadWireSet(string json)
    {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return builder.ToImmutable();

            if (!root.TryGetProperty(_CATEGORIES_KEY, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
                return builder.ToImmutable();

            foreach (var element in arr.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    continue;
                if (!element.TryGetProperty(_WIRE_KEY, out var wireEl) ||
                    wireEl.ValueKind != JsonValueKind.String)
                    continue;
                var wire = wireEl.GetString();
                if (wire is not null)
                    builder.Add(wire);
            }
        }
        catch (JsonException)
        {
            // A malformed category spec leaves the wire set empty; the
            // membership check below becomes a no-op (no false D2ERC007s).
            return ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Cross-checks every registry entry's <c>category</c> against the closed
    /// wire set. Returns one <c>D2ERC007</c> diagnostic per entry whose
    /// category is not a member. If <paramref name="wireSet"/> is empty the
    /// check is skipped (the spec was absent or malformed — the existing
    /// per-spec validation already covers that case).
    /// </summary>
    /// <param name="entries">All aggregated registry entries.</param>
    /// <param name="wireSet">The closed set of category wire strings.</param>
    /// <returns>The membership diagnostics (empty when all categories are known).</returns>
    public static ImmutableArray<DcsvIo.D2.SourceGen.EmitDiagnostic> Check(
        IReadOnlyList<RegistrySpecEntry> entries,
        ImmutableHashSet<string> wireSet)
    {
        if (wireSet.Count == 0)
            return ImmutableArray<DcsvIo.D2.SourceGen.EmitDiagnostic>.Empty;

        var diagnostics = ImmutableArray.CreateBuilder<DcsvIo.D2.SourceGen.EmitDiagnostic>();
        foreach (var entry in entries)
        {
            if (!wireSet.Contains(entry.Category))
            {
                diagnostics.Add(RegistryDiagnostics.UnknownCategory(
                    entry.Code, entry.SpecFileName, entry.Category));
            }
        }

        return diagnostics.ToImmutable();
    }
}
