// -----------------------------------------------------------------------
// <copyright file="SpecMetadata.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Spec;

/// <summary>
/// Common header block carried by every geo spec file. Captures the
/// catalog version + provenance fields required to emit
/// <c>GeoCatalog</c> constants. The provenance fields differ between
/// pipeline-derived specs (<c>$generated: true</c> + <c>generatedAt</c>)
/// and the hand-rolled <c>geopolitical-entities.spec.json</c>
/// (<c>$generated: false</c> + <c>lastEditedAt</c>) — both forms are
/// represented; the unused field is <c>null</c> on the other branch.
/// </summary>
/// <param name="CatalogVersion">
/// Semver string (<c>"0.1.0"</c> etc.) — surfaces as a <c>GeoCatalog</c>
/// constant on the emitted abstractions assembly.
/// </param>
/// <param name="GeneratedAt">
/// ISO-8601 timestamp for pipeline-derived specs; <c>null</c> for the
/// hand-rolled catalog.
/// </param>
/// <param name="LastEditedAt">
/// <c>YYYY-MM-DD</c> string for the hand-rolled catalog; <c>null</c> for
/// pipeline-derived specs.
/// </param>
/// <param name="IsGenerated">
/// True when <c>$generated: true</c> (pipeline-derived); false for the
/// hand-rolled catalog. Used by emitters to pick the right provenance
/// rendering.
/// </param>
/// <param name="Source">
/// Free-form provenance string (<c>"pipeline-derived"</c> or
/// <c>"manual"</c>) — recorded verbatim from the spec.
/// </param>
internal sealed record SpecMetadata(
    string CatalogVersion,
    string? GeneratedAt,
    string? LastEditedAt,
    bool IsGenerated,
    string Source);
