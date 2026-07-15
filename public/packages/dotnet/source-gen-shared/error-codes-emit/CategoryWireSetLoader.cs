// -----------------------------------------------------------------------
// <copyright file="CategoryWireSetLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.SourceGen;

using System;
using System.Collections.Immutable;
using System.Text.Json;

/// <summary>
/// Reads the closed <c>ErrorCategory</c> wire set from
/// <c>error-category.spec.json</c> (surfaced via <c>AdditionalFiles</c>). The
/// per-catalog engine cross-checks every code's <c>category</c> against this
/// set so a code referencing an unknown category fails with the catalog's
/// unknown-category diagnostic instead of silently passing. The accepted set is
/// thereby spec-derived (exactly the categories declared in
/// <c>error-category.spec.json</c>) rather than a hand-maintained subset. If the
/// spec is absent or malformed the set is empty and the membership check
/// degrades to a no-op — the same degradation the en-US.json TK cross-check
/// uses, so a missing AdditionalFile never produces a false positive.
/// </summary>
internal static class CategoryWireSetLoader
{
    private const string _CATEGORIES_KEY = "categories";
    private const string _WIRE_KEY = "wire";

    /// <summary>
    /// Parses the error-category spec content into the closed set of category
    /// wire strings. Returns an empty set if the spec is malformed or missing
    /// the <c>categories</c> array (the membership check then degrades to a
    /// no-op, leaving the existing per-spec validation in place).
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
            // membership check then becomes a no-op (no false unknown-category
            // diagnostics).
            return ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);
        }

        return builder.ToImmutable();
    }
}
