// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { mkdir, readFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fetchIanaTzdb } from "../fetchers/iana-tzdb.js";
import {
  reconcileIanaWithIcu,
  transformTimezone,
  type TimezonePartial,
} from "../transformers/timezones.js";
import { REPO_ROOT_PATH } from "../util/cache.js";
import { writeSpecJson } from "../util/json-encoding.js";

const SPEC_OUTPUT_PATH = resolve(
  REPO_ROOT_PATH,
  "contracts",
  "geo",
  "src-data",
  "timezones.spec.json",
);
const CACHE_DIR = resolve(
  REPO_ROOT_PATH,
  "tools",
  "geo-data-pipeline",
  ".cache",
  "iana-tzdb",
);

interface TimezonesSpec {
  $schema: string;
  $note: string;
  catalogVersion: string;
  generatedAt: string;
  sources: Array<{
    name: string;
    url: string;
    license: string;
    fetchedAt: string;
    sha256: string;
  }>;
  fieldCoverage: Record<
    string,
    { populated: number; total: number; pct: string }
  >;
  /** ICU<->IANA reconciliation diagnostics — for operator visibility during refresh. */
  reconciliation: {
    ianaOnlyZones: string[];
    icuOnlyZones: string[];
  };
  entries: TimezonePartial[];
}

export async function buildTimezonesSpec(): Promise<TimezonesSpec> {
  console.error(`[fetch] IANA tzdb (zone1970.tab + backward)`);
  const iana = await fetchIanaTzdb();

  // Load both provenance files (zone1970 + backward) for the spec sources block
  const backwardProvenance = JSON.parse(
    await readFile(join(CACHE_DIR, "backward.provenance.json"), "utf8"),
  ) as typeof iana.provenance;

  const sources = [
    {
      name: iana.provenance.source,
      url: iana.provenance.url,
      license: iana.provenance.license,
      fetchedAt: iana.provenance.fetchedAt,
      sha256: iana.provenance.sha256,
    },
    {
      name: backwardProvenance.source,
      url: backwardProvenance.url,
      license: backwardProvenance.license,
      fetchedAt: backwardProvenance.fetchedAt,
      sha256: backwardProvenance.sha256,
    },
  ];

  const entries: TimezonePartial[] = [];
  let skipped = 0;
  for (const zone of iana.zones) {
    const partial = transformTimezone(zone, iana.aliasesByCanonical);
    if (partial) entries.push(partial);
    else skipped++;
  }

  entries.sort((a, b) => a.ianaIdentifier.localeCompare(b.ianaIdentifier));

  const reconciliation = reconcileIanaWithIcu(iana);

  // Field coverage report
  const total = entries.length;
  const coverage = {
    ianaIdentifier: countNonNull(entries, (e) => e.ianaIdentifier),
    displayName: countNonNull(entries, (e) => e.displayName),
    currentStdOffsetMinutes: total, // always populated
    currentDstOffsetMinutes: entries.filter(
      (e) => e.currentDstOffsetMinutes !== null,
    ).length,
    currentStdAbbrev: countNonNull(entries, (e) => e.currentStdAbbrev),
    currentDstAbbrev: entries.filter((e) => e.currentDstAbbrev !== null).length,
    countryISO31661Alpha2Code: countNonNull(
      entries,
      (e) => e.countryISO31661Alpha2Code,
    ),
    aliases: entries.filter((e) => e.aliases.length > 0).length,
    coApplicableCountries: entries.filter(
      (e) => e.coApplicableCountryISO31661Alpha2Codes.length > 0,
    ).length,
  };
  const fieldCoverage: Record<
    string,
    { populated: number; total: number; pct: string }
  > = {};
  for (const [field, populated] of Object.entries(coverage)) {
    fieldCoverage[field] = {
      populated,
      total,
      pct: `${((populated / total) * 100).toFixed(1)}%`,
    };
  }

  console.error(
    `[transform] timezones: ${entries.length} entries (${skipped} rows skipped)`,
  );
  for (const [field, stats] of Object.entries(fieldCoverage)) {
    console.error(
      `  ${field}: ${stats.populated}/${stats.total} (${stats.pct})`,
    );
  }
  console.error(
    `[reconcile] IANA-only zones: ${reconciliation.ianaOnlyZones.length}; ` +
      `ICU-only zones: ${reconciliation.icuOnlyZones.length}`,
  );
  if (reconciliation.ianaOnlyZones.length > 0) {
    console.error(
      `  IANA-only sample: ${reconciliation.ianaOnlyZones.slice(0, 5).join(", ")}`,
    );
  }
  if (reconciliation.icuOnlyZones.length > 0) {
    console.error(
      `  ICU-only sample: ${reconciliation.icuOnlyZones.slice(0, 5).join(", ")}`,
    );
  }

  return {
    $schema: "./timezones.schema.json",
    $note:
      "PIPELINE-RAW spec — produced by tools/geo-data-pipeline. Not directly consumed by " +
      "codegen / DcsvIo.D2.Geo.Default. A clean/transform pass to the sibling " +
      "contracts/geo/timezones.spec.json (one level up) is a separate step. Sources: " +
      "IANA zone1970.tab + backward (public domain — canonical zones, primary/co-applicable " +
      "country FKs, aliases, ISO 6709 coordinates) + Node's built-in ICU Intl.DateTimeFormat " +
      "(current STD/DST offsets + abbreviations sampled at 2026-01-15 and 2026-07-15). " +
      "Missing: LocalizedDisplayNames (CLDR cldr-dates-full/main/{locale}/timeZoneNames.json " +
      "follow-up pass), Selectable (Tier 2 curated subset), Deprecation.",
    catalogVersion: "0.0.1",
    generatedAt: new Date().toISOString(),
    sources,
    fieldCoverage,
    reconciliation,
    entries,
  };
}

function countNonNull<T>(
  items: readonly T[],
  pick: (item: T) => string | null,
): number {
  let n = 0;
  for (const item of items) if (pick(item)) n++;
  return n;
}

if (
  process.argv[1]?.endsWith("write-timezones.ts") ||
  process.argv[1]?.endsWith("write-timezones.js")
) {
  const spec = await buildTimezonesSpec();
  await mkdir(dirname(SPEC_OUTPUT_PATH), { recursive: true });
  await writeSpecJson(SPEC_OUTPUT_PATH, spec);
  console.error(`[write] ${SPEC_OUTPUT_PATH} (${spec.entries.length} entries)`);
}
