// -----------------------------------------------------------------------
// <copyright file="TimezoneSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Spec;

using System.Collections.Generic;

/// <summary>
/// One entry parsed from <c>timezones.spec.json</c>. Offsets are sampled
/// from Node ICU <c>Intl.DateTimeFormat</c> at January 15 (STD) and
/// July 15 (DST) per UTC year — emitters bake the values into the
/// generated abstractions.
/// </summary>
/// <param name="IanaIdentifier">
/// IANA tzdb zone identifier (e.g. <c>"America/New_York"</c>).
/// </param>
/// <param name="DisplayName">English display name.</param>
/// <param name="CurrentStdOffsetMinutes">
/// Standard-time offset from UTC in minutes (range -720..840 covers
/// UTC-12 through UTC+14).
/// </param>
/// <param name="CurrentDstOffsetMinutes">
/// Daylight-saving-time offset from UTC in minutes; null for zones that
/// do not observe DST.
/// </param>
/// <param name="CurrentStdAbbrev">Standard-time abbreviation (e.g. <c>"EST"</c>).</param>
/// <param name="CurrentDstAbbrev">
/// Daylight-saving abbreviation (e.g. <c>"EDT"</c>); null when the zone
/// does not observe DST.
/// </param>
/// <param name="CountryIso31661Alpha2Code">
/// Primary owning country alpha-2 code; null for <c>Etc/</c> pseudo-zones.
/// </param>
/// <param name="CoApplicableCountryIso31661Alpha2Codes">
/// Other countries that share the zone (alpha-2 codes).
/// </param>
/// <param name="Aliases">
/// Deprecated / linked IANA identifiers that resolve to this zone.
/// </param>
internal sealed record TimezoneSpec(
    string IanaIdentifier,
    string DisplayName,
    int CurrentStdOffsetMinutes,
    int? CurrentDstOffsetMinutes,
    string CurrentStdAbbrev,
    string? CurrentDstAbbrev,
    string? CountryIso31661Alpha2Code,
    IReadOnlyList<string> CoApplicableCountryIso31661Alpha2Codes,
    IReadOnlyList<string> Aliases);
