// -----------------------------------------------------------------------
// <copyright file="GeopoliticalEntitySpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Spec;

using System.Collections.Generic;

/// <summary>
/// One entry parsed from <c>geopolitical-entities.spec.json</c>. The
/// catalog is hand-rolled (no upstream source) and lives alongside the
/// pipeline-derived specs in <c>contracts/geo/</c>. The forward M:M edge
/// is <see cref="CountryIso31661Alpha2Codes"/>; the inverse-nav
/// (<c>Country.geopoliticalEntityShortCodes</c>) is computed at
/// Tier-2 build time.
/// </summary>
/// <param name="ShortCode">
/// Identifier — typically the well-known abbreviation (<c>"NATO"</c>,
/// <c>"EU"</c>, <c>"ASEAN"</c>, etc.) or an ISO continent two-letter
/// code (<c>"AF"</c>, <c>"AN"</c>, <c>"AS"</c>, ...).
/// </param>
/// <param name="Name">English display name.</param>
/// <param name="Type">
/// One of the 23 <c>GeopoliticalEntityType</c> enum values
/// (<c>"Continent"</c>, <c>"MilitaryAlliance"</c>,
/// <c>"FreeTradeAgreement"</c>, etc.).
/// </param>
/// <param name="CountryIso31661Alpha2Codes">
/// Member country alpha-2 codes. May reference disputed entities (e.g.
/// <c>"XK"</c> Kosovo) not present in <c>countries.spec.json</c>;
/// known-orphan exemptions are documented in the parity tests.
/// </param>
internal sealed record GeopoliticalEntitySpec(
    string ShortCode,
    string Name,
    string Type,
    IReadOnlyList<string> CountryIso31661Alpha2Codes);
