// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { mkdir, readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import {
  loadSubdivisions,
  type CldrZombieWarning,
  type CountryOrderReport,
  type MissingWikidataEnRow,
  type PerCountryDivergenceRow,
  type SubdivisionPartial,
} from "../transformers/subdivisions.js";
import { REPO_ROOT_PATH } from "../util/cache.js";
import { writeSpecJson } from "../util/json-encoding.js";

const SPEC_OUTPUT_PATH = resolve(
  REPO_ROOT_PATH,
  "contracts",
  "geo",
  "src-data",
  "subdivisions.spec.json",
);
const COUNTRIES_SPEC_PATH = resolve(
  REPO_ROOT_PATH,
  "contracts",
  "geo",
  "src-data",
  "countries.spec.json",
);
const LOGS_DIR = resolve(REPO_ROOT_PATH, "tools", "geo-data-pipeline", "logs");
const MISSING_WIKIDATA_EN_LOG_PATH = resolve(
  LOGS_DIR,
  "missing-wikidata-en.json",
);

interface CountriesSpecMinimal {
  entries: Array<{
    iso31661Alpha2Code: string;
    primaryLanguageISO6391Code: string | null;
  }>;
}

interface SubdivisionsSpec {
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
  orderBreakdown: {
    total: number;
    firstOrder: number;
    secondOrder: number;
    thirdPlusOrder: number;
    countriesWithSecondPlusOrder: number;
  };
  /**
   * Sorted by total entry count desc; the top of the list is useful for
   * "who has hierarchy data" review.
   */
  countryOrderReports: CountryOrderReport[];
  debianVsCldrDrift: { debianOnly: number; cldrOnly: number };
  wikidataFills: { endonymFills: number; localizedNameAdds: number };
  /**
   * Codes CLDR ships that Debian no longer considers current. Each row triggers
   * a D2GEO011 warning at transform time + is logged here for operator review.
   */
  cldrZombieWarnings: CldrZombieWarning[];
  /**
   * Debian-present codes where Wikidata lacks an `en` label and we fell back
   * to Debian `name`. Per-row triage signal — operator reviews
   * `tools/geo-data-pipeline/logs/missing-wikidata-en.json` after each
   * refresh and adds an overlay if a Debian fallback is awkward.
   */
  missingWikidataEnCount: number;
  /**
   * Per-country count of subdivisions where Wikidata.en disagrees with the
   * Debian `name` field. Informational — gauges data drift between sources.
   */
  perCountryDivergence: PerCountryDivergenceRow[];
  entries: SubdivisionPartial[];
}

async function loadCountriesPrimaryLanguageMap(): Promise<Map<string, string>> {
  const text = await readFile(COUNTRIES_SPEC_PATH, "utf8");
  const spec = JSON.parse(text) as CountriesSpecMinimal;
  const map = new Map<string, string>();
  for (const c of spec.entries) {
    if (c.primaryLanguageISO6391Code) {
      map.set(c.iso31661Alpha2Code, c.primaryLanguageISO6391Code);
    }
  }
  return map;
}

export interface BuildSubdivisionsResult {
  spec: SubdivisionsSpec;
  /**
   * Triage rows for codes where Wikidata.en was missing and the pipeline fell
   * back to Debian. Written to `logs/missing-wikidata-en.json` by the CLI
   * entrypoint — operator reviews after each refresh.
   */
  missingWikidataEnRows: MissingWikidataEnRow[];
}

export async function buildSubdivisionsSpec(): Promise<BuildSubdivisionsResult> {
  const primaryLanguageMap = await loadCountriesPrimaryLanguageMap();
  const loaded = await loadSubdivisions(primaryLanguageMap);

  const total = loaded.entries.length;
  const coverage = {
    iso31662Code: total,
    shortCode: total,
    countryISO31661Alpha2Code: total,
    displayName: total,
    type: total,
    parentISO31662Code: loaded.entries.filter(
      (e) => e.parentISO31662Code !== null,
    ).length,
    endonymDisplayName: loaded.entries.filter(
      (e) => e.endonymDisplayName !== null,
    ).length,
    localizedDisplayNames_en: loaded.entries.filter(
      (e) => e.localizedDisplayNames["en"],
    ).length,
    localizedDisplayNames_ja: loaded.entries.filter(
      (e) => e.localizedDisplayNames["ja"],
    ).length,
    localizedDisplayNames_zh: loaded.entries.filter(
      (e) => e.localizedDisplayNames["zh"],
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
      pct: total === 0 ? "0.0%" : `${((populated / total) * 100).toFixed(1)}%`,
    };
  }

  const firstOrder = loaded.entries.filter((e) => e.order === 1).length;
  const secondOrder = loaded.entries.filter((e) => e.order === 2).length;
  const thirdPlusOrder = loaded.entries.filter((e) => e.order >= 3).length;
  const countriesWithSecondPlusOrder = loaded.countryOrderReports.filter(
    (r) => r.secondOrder + r.thirdPlusOrder > 0,
  ).length;

  console.error(`[transform] subdivisions: ${total} entries`);
  console.error(
    `  order breakdown: 1st=${firstOrder}, 2nd=${secondOrder}, 3+=${thirdPlusOrder}`,
  );
  console.error(
    `  countries with 2nd+ order data: ${countriesWithSecondPlusOrder}`,
  );
  console.error(
    `  debian-only entries: ${loaded.debianOnly}, CLDR-only: ${loaded.cldrOnly}`,
  );
  for (const [field, stats] of Object.entries(fieldCoverage)) {
    console.error(
      `  ${field}: ${stats.populated}/${stats.total} (${stats.pct})`,
    );
  }

  const spec: SubdivisionsSpec = {
    $schema: "./subdivisions.schema.json",
    $note:
      "PIPELINE-RAW spec — produced by tools/geo-data-pipeline. Not directly consumed by " +
      "codegen / D2.Shared.Geo.Default. A clean/transform pass to the sibling " +
      "contracts/geo/subdivisions.spec.json (one level up) is a separate step. Sources: " +
      "debian/iso-codes iso_3166-2.json (LGPL — authoritative ISO 3166-2 current-codes list + " +
      "hierarchy: code/type/parent/order) joined with Wikidata SPARQL (CC0 — " +
      "PRIMARY displayName/officialName authority via the .en label; tracks Wikipedia 1:1 + " +
      "current ISO reassignments; falls back to debian/iso-codes' `name` field on the ~140 " +
      "codes Wikidata lacks .en for) + CLDR cldr-subdivisions-full (Unicode-3.0 — secondary " +
      "seed for the 11 supported locale labels, NOT used for English displayName as it's stale " +
      "for many post-2020 ISO reassignments). CLDR-only codes filtered as D2GEO011 zombies " +
      "when debian/iso-codes no longer lists them. The catalog ships ALL orders (1st through " +
      "3rd+); consumers filter on the `order` field. Country.Subdivisions M:M nav back-fill " +
      "happens in a separate cross-catalog merge pass by the Tier 2 builder.",
    catalogVersion: "0.0.1",
    generatedAt: new Date().toISOString(),
    sources: loaded.provenance.map((p) => ({
      name: p.source,
      url: p.url,
      license: p.license,
      fetchedAt: p.fetchedAt,
      sha256: p.sha256,
    })),
    fieldCoverage,
    orderBreakdown: {
      total,
      firstOrder,
      secondOrder,
      thirdPlusOrder,
      countriesWithSecondPlusOrder,
    },
    countryOrderReports: loaded.countryOrderReports,
    debianVsCldrDrift: {
      debianOnly: loaded.debianOnly,
      cldrOnly: loaded.cldrOnly,
    },
    wikidataFills: {
      endonymFills: loaded.wikidataEndonymFills,
      localizedNameAdds: loaded.wikidataLocalizedAdds,
    },
    cldrZombieWarnings: loaded.cldrZombieWarnings,
    missingWikidataEnCount: loaded.missingWikidataEn.length,
    perCountryDivergence: loaded.perCountryDivergence,
    entries: loaded.entries,
  };

  return {
    spec,
    missingWikidataEnRows: loaded.missingWikidataEn,
  };
}

/**
 * Writes the operator-triage JSON log of Debian-present codes that lack a
 * Wikidata `en` label (and therefore fell back to Debian's `name` field for
 * displayName). Lives at `tools/geo-data-pipeline/logs/missing-wikidata-en.json`
 * — gitignored as a refresh artifact. Operator reviews after each refresh and
 * decides whether to add an overlay entry.
 */
async function writeMissingWikidataEnLog(
  rows: readonly MissingWikidataEnRow[],
): Promise<void> {
  await mkdir(LOGS_DIR, { recursive: true });
  const payload = {
    $generated: true,
    $note:
      "Operator-triage log: Debian-present subdivision codes that lack a Wikidata 'en' label. " +
      "The pipeline fell back to debian/iso-codes' `name` field for displayName. Review each " +
      "entry's `debianFallbackName` — if it's awkward " +
      "(non-canonical English / odd transliteration), " +
      "consider adding an overlay entry at " +
      "contracts/geo/overlays/subdivisions.overlays.spec.json. See " +
      "contracts/geo/KNOWN_WARNINGS.md for the source-priority rationale.",
    generatedAt: new Date().toISOString(),
    rowCount: rows.length,
    rows,
  };
  await writeSpecJson(MISSING_WIKIDATA_EN_LOG_PATH, payload);
}

if (
  process.argv[1]?.endsWith("write-subdivisions.ts") ||
  process.argv[1]?.endsWith("write-subdivisions.js")
) {
  const result = await buildSubdivisionsSpec();
  await mkdir(dirname(SPEC_OUTPUT_PATH), { recursive: true });
  await writeSpecJson(SPEC_OUTPUT_PATH, result.spec);
  console.error(
    `[write] ${SPEC_OUTPUT_PATH} (${result.spec.entries.length} entries)`,
  );
  await writeMissingWikidataEnLog(result.missingWikidataEnRows);
  console.error(
    `[write] ${MISSING_WIKIDATA_EN_LOG_PATH} ` +
      `(${result.missingWikidataEnRows.length} rows — operator triage signal)`,
  );
}
