// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "iana-tzdb";
const SOURCE_LICENSE = "Public Domain (IANA Time Zone Database)";

const URL_ZONE1970 = "https://data.iana.org/time-zones/data/zone1970.tab";
const URL_BACKWARD = "https://data.iana.org/time-zones/data/backward";

/**
 * One row from IANA's `zone1970.tab`. Format (tab-separated):
 *   country_codes\tcoords\tzone_name[\tcomments]
 *
 * Examples:
 *   AD\t+4230+00131\tEurope/Andorra
 *   AE,OM,RE,SC,TF\t+2518+05518\tAsia/Dubai\tCrozet
 *
 * - country_codes: comma-separated ISO 3166-1 alpha-2 codes. The FIRST entry is the
 *   primary country per IANA convention; remaining entries are co-applicable countries
 *   (territories sharing the zone). E.g., Asia/Dubai is primarily UAE (AE) but also
 *   covers Oman (OM), Réunion (RE), Seychelles (SC), French Southern Territories (TF).
 * - coords: ISO 6709 lat/lon format (e.g., "+2518+05518" = 25°18'N, 55°18'E)
 * - zone_name: IANA identifier (e.g., "Asia/Dubai")
 * - comments: optional human disambiguator
 */
export interface ZoneTabEntry {
  countryCodes: string[]; // primary first
  coordinates: string; // ISO 6709 raw
  zoneName: string;
  comments: string | null;
}

/**
 * One alias mapping from IANA's `backward` file. Format:
 *   Link\tcanonical_name\talias_name[\t#optional comment]
 *
 * Examples:
 *   Link\tAustralia/Sydney\tAustralia/ACT\t#= Australia/Canberra
 *   Link\tEurope/London\tGB
 *   Link\tAmerica/New_York\tUS/Eastern
 *
 * `aliases` are old/deprecated zone names that should resolve to `canonical`.
 */
export interface BackwardAlias {
  canonical: string;
  alias: string;
}

export interface IanaTzdbFetchResult extends Pick<
  CachedFetch,
  "provenance" | "fromCache"
> {
  /**
   * Parsed entries from zone1970.tab (canonical zones with primary country FK +
   * co-applicable countries).
   */
  zones: ZoneTabEntry[];
  /** Parsed `Link` entries from `backward` (alias → canonical mappings). */
  aliases: BackwardAlias[];
  /** Map canonical zone id → list of aliases pointing to it (inverted from `aliases`). */
  aliasesByCanonical: Map<string, string[]>;
}

export async function fetchIanaTzdb(options?: {
  ttlHours?: number;
}): Promise<IanaTzdbFetchResult> {
  const [zoneFile, backwardFile] = await Promise.all([
    fetchAndCache({
      source: SOURCE_NAME,
      url: URL_ZONE1970,
      license: SOURCE_LICENSE,
      cacheKey: "zone1970.tab",
      ttlHours: options?.ttlHours,
    }),
    fetchAndCache({
      source: SOURCE_NAME,
      url: URL_BACKWARD,
      license: SOURCE_LICENSE,
      cacheKey: "backward",
      ttlHours: options?.ttlHours,
    }),
  ]);

  const zones = parseZone1970Tab(zoneFile.body.toString("utf8"));
  const aliases = parseBackward(backwardFile.body.toString("utf8"));

  const aliasesByCanonical = new Map<string, string[]>();
  for (const { canonical, alias } of aliases) {
    const list = aliasesByCanonical.get(canonical) ?? [];
    list.push(alias);
    aliasesByCanonical.set(canonical, list);
  }
  for (const list of aliasesByCanonical.values()) list.sort();

  // Use zone1970 as the dominant provenance (backward shares the same source). Both URLs
  // captured below for the spec's `sources[]` block by the caller pulling from .cache/.
  return {
    zones,
    aliases,
    aliasesByCanonical,
    provenance: zoneFile.provenance,
    fromCache: zoneFile.fromCache && backwardFile.fromCache,
  };
}

export function parseZone1970Tab(text: string): ZoneTabEntry[] {
  const entries: ZoneTabEntry[] = [];
  for (const rawLine of text.split("\n")) {
    const line = rawLine.trim();
    if (!line || line.startsWith("#")) continue;
    const fields = line.split("\t");
    const countryField = fields[0];
    const coordsField = fields[1];
    const zoneField = fields[2];
    if (!countryField || !coordsField || !zoneField) continue;
    const countryCodes = countryField
      .split(",")
      .map((c) => c.trim().toUpperCase())
      .filter(Boolean);
    if (countryCodes.length === 0) continue;
    entries.push({
      countryCodes,
      coordinates: coordsField,
      zoneName: zoneField,
      comments: fields[3]?.trim() || null,
    });
  }
  return entries;
}

export function parseBackward(text: string): BackwardAlias[] {
  const entries: BackwardAlias[] = [];
  for (const rawLine of text.split("\n")) {
    const line = rawLine.trim();
    if (!line.startsWith("Link")) continue;
    // Strip trailing comments: "Link\tcanonical\talias\t#..." -> drop "#..." portion
    const noComment = line.split("#")[0]?.trimEnd();
    if (!noComment) continue;
    const fields = noComment.split(/\s+/);
    // Expected: ["Link", canonical, alias]
    if (fields.length < 3) continue;
    const canonical = fields[1];
    const alias = fields[2];
    if (!canonical || !alias) continue;
    entries.push({ canonical, alias });
  }
  return entries;
}

if (
  process.argv[1]?.endsWith("iana-tzdb.ts") ||
  process.argv[1]?.endsWith("iana-tzdb.js")
) {
  const result = await fetchIanaTzdb();
  const sample = result.zones.slice(0, 3);
  const usEastern = result.aliasesByCanonical.get("America/New_York") ?? [];
  console.log(
    JSON.stringify(
      {
        fromCache: result.fromCache,
        zoneCount: result.zones.length,
        aliasCount: result.aliases.length,
        sampleZones: sample,
        americaNewYorkAliases: usEastern,
        europeLondonAliases:
          result.aliasesByCanonical.get("Europe/London") ?? [],
      },
      null,
      2,
    ),
  );
}
