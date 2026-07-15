// -----------------------------------------------------------------------
// <copyright file="VocabularyGuard.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Enforces the subdivision vocabulary discipline — the ISO 3166-2 concept
/// is consistently referred to as <c>subdivision</c> across spec field
/// identifiers; the words <c>region</c>, <c>state</c>, and <c>province</c>
/// are forbidden at identifier position.
/// </summary>
/// <remarks>
/// The guard inspects field NAMES (identifiers). Field VALUES are exempt —
/// display strings on <c>Subdivision.Type</c> like <c>"State"</c> /
/// <c>"Province"</c> / <c>"Parish"</c> remain legal user-facing labels.
/// </remarks>
internal static class VocabularyGuard
{
    /// <summary>The forbidden identifier tokens (case-insensitive).</summary>
    public static readonly ImmutableArray<string> ForbiddenIdentifiers =
        ImmutableArray.Create("region", "state", "province");

    /// <summary>
    /// Walks <paramref name="fieldNames"/> and surfaces a diagnostic for any
    /// identifier whose lowercased form contains a forbidden token. Empty /
    /// whitespace / null field names are skipped (degenerate spec metadata
    /// produces no false positives).
    /// </summary>
    /// <param name="specName">The spec file name (used in diagnostic args).</param>
    /// <param name="fieldNames">The collected identifier names from the spec.</param>
    /// <returns>
    /// The diagnostics surfaced (one per forbidden identifier encountered);
    /// empty when the field set is clean.
    /// </returns>
    public static ImmutableArray<EmitDiagnostic> Validate(
        string specName,
        IEnumerable<string?> fieldNames)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        foreach (var fieldName in fieldNames)
        {
            if (fieldName.Falsey())
                continue;

            // ! is safe because Falsey() returned false above.
            var lowered = fieldName!.ToLowerInvariant();
            foreach (var forbidden in ForbiddenIdentifiers)
            {
                if (lowered.IndexOf(forbidden, StringComparison.Ordinal) >= 0)
                {
                    diagnostics.Add(EmitDiagnostics.VocabularyViolation(specName, fieldName));
                    break;
                }
            }
        }

        return diagnostics.ToImmutable();
    }
}
