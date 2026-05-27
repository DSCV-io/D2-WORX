// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type {
  IanaTzdbFetchResult,
  ZoneTabEntry,
} from "../fetchers/iana-tzdb.js";

/**
 * Partial Timezone spec entry produced from IANA tzdb tab files + Node's built-in
 * `Intl.DateTimeFormat` for current offset/abbreviation extraction. The full Timezone
 * entity also carries `LocalizedDisplayNames` (CLDR cldr-dates-full/main/{locale}/
 * timeZoneNames.json) + `Selectable` (Tier 2 curated subset) + `Deprecation` — those
 * layer in via additional passes.
 */
export interface TimezonePartial {
  /** IANA canonical identifier, e.g. "America/Edmonton". PK. */
  ianaIdentifier: string;
  /**
   * Friendly English display name derived from id by splitting on `/` and replacing `_` with space.
   */
  displayName: string;
  /** Current standard-time UTC offset in minutes. Sampled via Intl.DateTimeFormat. */
  currentStdOffsetMinutes: number;
  /** Current daylight-saving UTC offset in minutes. Null when this zone observes no DST. */
  currentDstOffsetMinutes: number | null;
  /**
   * Current standard-time abbreviation, e.g. "EST" or "GMT+04". Sampled via Intl.DateTimeFormat.
   */
  currentStdAbbrev: string;
  /** Current daylight-saving abbreviation, e.g. "EDT". Null when this zone observes no DST. */
  currentDstAbbrev: string | null;
  /** Primary ISO 3166-1 alpha-2 country code (first entry in zone1970.tab's country list). */
  countryISO31661Alpha2Code: string;
  /** Co-applicable country codes after the primary (territories sharing the zone). */
  coApplicableCountryISO31661Alpha2Codes: string[];
  /** Deprecated/legacy IANA ids that resolve to this canonical (from `backward` file). */
  aliases: string[];
  /** ISO 6709 lat/lon, raw from zone1970.tab (e.g. "+2518+05518"). */
  iso6709Coordinates: string;
  /** Optional human comment from zone1970.tab (e.g. "Crozet" for Asia/Dubai). */
  comments: string | null;
}

/**
 * Sample dates used to detect DST + extract current offsets. Northern hemisphere zones
 * STD in January (winter), DST in July (summer); southern hemisphere flipped. Sampling
 * at both points lets the transformer pick min/max offset to identify STD vs DST.
 *
 * Year chosen as "current-ish" — refresh script picks the latest IANA tzdata release; the
 * offsets we report reflect what's in effect during THIS calendar year, not historical.
 */
const SAMPLE_DATE_WINTER_NH = new Date(Date.UTC(2026, 0, 15, 12, 0, 0)); // 2026-01-15T12:00:00Z
const SAMPLE_DATE_SUMMER_NH = new Date(Date.UTC(2026, 6, 15, 12, 0, 0)); // 2026-07-15T12:00:00Z

const PRETTIFY_REPLACEMENTS: Array<[string, string]> = [
  ["_", " "],
  ["/", " — "],
];

export function transformTimezone(
  zoneEntry: ZoneTabEntry,
  aliasesByCanonical: Map<string, string[]>,
): TimezonePartial | null {
  const id = zoneEntry.zoneName;
  const primaryCountry = zoneEntry.countryCodes[0];
  if (!primaryCountry) return null;

  const sampleA = sampleOffset(id, SAMPLE_DATE_WINTER_NH);
  const sampleB = sampleOffset(id, SAMPLE_DATE_SUMMER_NH);
  if (!sampleA || !sampleB) return null;

  // STD = smaller (more west / less time-shift); DST = larger (one hour east per DST shift).
  // Equal => no DST (or year-round same offset).
  const sortedByOffset = [sampleA, sampleB].sort(
    (a, b) => a.offsetMinutes - b.offsetMinutes,
  );
  const stdSample = sortedByOffset[0];
  const dstSample = sortedByOffset[1];
  // Both samples guaranteed non-null since we early-returned above
  if (!stdSample || !dstSample) return null;
  const observesDst = stdSample.offsetMinutes !== dstSample.offsetMinutes;

  return {
    ianaIdentifier: id,
    displayName: prettifyZoneId(id),
    currentStdOffsetMinutes: stdSample.offsetMinutes,
    currentDstOffsetMinutes: observesDst ? dstSample.offsetMinutes : null,
    currentStdAbbrev: stdSample.abbrev,
    currentDstAbbrev: observesDst ? dstSample.abbrev : null,
    countryISO31661Alpha2Code: primaryCountry,
    coApplicableCountryISO31661Alpha2Codes: zoneEntry.countryCodes.slice(1),
    aliases: aliasesByCanonical.get(id) ?? [],
    iso6709Coordinates: zoneEntry.coordinates,
    comments: zoneEntry.comments,
  };
}

/**
 * Replace `_` with space, split path segments with em-dash separator.
 * Examples:
 *   "America/New_York" → "America — New York"
 *   "Asia/Kuala_Lumpur" → "Asia — Kuala Lumpur"
 *   "America/Argentina/Buenos_Aires" → "America — Argentina — Buenos Aires"
 *   "UTC" → "UTC"
 */
export function prettifyZoneId(id: string): string {
  let result = id;
  for (const [from, to] of PRETTIFY_REPLACEMENTS) {
    result = result.split(from).join(to);
  }
  return result;
}

interface OffsetSample {
  /** UTC offset in minutes (e.g. -300 for EST, -240 for EDT, +330 for IST). */
  offsetMinutes: number;
  /**
   * Short timezone abbreviation (e.g. "EST", "EDT", "IST"). Falls back to GMT-style string when
   * no abbrev.
   */
  abbrev: string;
}

export function sampleOffset(
  timeZone: string,
  when: Date,
): OffsetSample | null {
  try {
    const longOffsetParts = new Intl.DateTimeFormat("en-US", {
      timeZone,
      timeZoneName: "longOffset",
    }).formatToParts(when);
    const shortNameParts = new Intl.DateTimeFormat("en-US", {
      timeZone,
      timeZoneName: "short",
    }).formatToParts(when);

    const offsetRaw = longOffsetParts.find(
      (p) => p.type === "timeZoneName",
    )?.value;
    const abbrev = shortNameParts.find((p) => p.type === "timeZoneName")?.value;
    if (!offsetRaw || !abbrev) return null;

    const offsetMinutes = parseGmtOffsetToMinutes(offsetRaw);
    if (offsetMinutes === null) return null;

    return { offsetMinutes, abbrev };
  } catch {
    return null;
  }
}

/**
 * Parses Intl.DateTimeFormat `longOffset` output to total minutes.
 * Inputs: "GMT" → 0; "GMT-05:00" → -300; "GMT+05:30" → +330; "GMT+13:00" → +780.
 * Edge: "GMT-04" (no minutes) → -240. "UTC" → 0.
 */
export function parseGmtOffsetToMinutes(raw: string): number | null {
  if (raw === "GMT" || raw === "UTC") return 0;
  const match = raw.match(/^GMT([+-])(\d{1,2})(?::(\d{2}))?$/);
  if (!match) return null;
  const sign = match[1] === "-" ? -1 : 1;
  const hours = Number.parseInt(match[2] ?? "0", 10);
  const minutes = Number.parseInt(match[3] ?? "0", 10);
  if (!Number.isFinite(hours) || !Number.isFinite(minutes)) return null;
  return sign * (hours * 60 + minutes);
}

/**
 * Cross-checks the IANA zone list against Node's bundled ICU tzdb. Surfaces:
 *   - IANA zones Node's ICU doesn't know about (rare; usually means brand-new zones)
 *   - Node ICU zones NOT in IANA's tab files (typically aliases or obsolete ids)
 *
 * Used by the spec writer as a sanity-check before emit.
 */
export function reconcileIanaWithIcu(ianaResult: IanaTzdbFetchResult): {
  ianaOnlyZones: string[];
  icuOnlyZones: string[];
} {
  const ianaIds = new Set(ianaResult.zones.map((z) => z.zoneName));
  const icuIds = new Set(Intl.supportedValuesOf("timeZone"));

  const ianaOnlyZones: string[] = [];
  for (const id of ianaIds) if (!icuIds.has(id)) ianaOnlyZones.push(id);

  const icuOnlyZones: string[] = [];
  for (const id of icuIds) if (!ianaIds.has(id)) icuOnlyZones.push(id);

  return {
    ianaOnlyZones: ianaOnlyZones.sort(),
    icuOnlyZones: icuOnlyZones.sort(),
  };
}
