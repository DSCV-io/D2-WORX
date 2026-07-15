// -----------------------------------------------------------------------
// <copyright file="SubdivisionSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Spec;

/// <summary>
/// One entry parsed from <c>subdivisions.spec.json</c>. The vocabulary
/// discipline is enforced: identifiers always say <c>subdivision</c>
/// (never <c>region</c> / <c>state</c> / <c>province</c>); the
/// <see cref="Type"/> value is a free-form display label that may legally
/// say <c>"Province"</c> / <c>"State"</c> / etc. as a user-facing label.
/// </summary>
/// <param name="Iso31662Code">
/// ISO 3166-2 code (e.g. <c>"US-CA"</c>).
/// </param>
/// <param name="ShortCode">
/// Country-local short identifier (the segment after the hyphen).
/// </param>
/// <param name="DisplayName">English display name.</param>
/// <param name="OfficialName">Long-form official name.</param>
/// <param name="EndonymDisplayName">Native-script name; null when unknown.</param>
/// <param name="CountryIso31661Alpha2Code">Owning country alpha-2 code.</param>
/// <param name="ParentIso31662Code">
/// Parent subdivision's ISO 3166-2 code when nested; null at the top
/// administrative level.
/// </param>
/// <param name="Type">
/// Display label for the subdivision kind (<c>"State"</c>, <c>"Parish"</c>,
/// <c>"District"</c>, etc.); null when unspecified.
/// </param>
/// <param name="Order">
/// Hierarchy depth (1..4); null when the spec omits explicit nesting.
/// </param>
internal sealed record SubdivisionSpec(
    string Iso31662Code,
    string ShortCode,
    string DisplayName,
    string OfficialName,
    string? EndonymDisplayName,
    string CountryIso31661Alpha2Code,
    string? ParentIso31662Code,
    string? Type,
    int? Order);
