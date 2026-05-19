// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { mkdir, readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import {
  loadSubdivisions,
  type CountryOrderReport,
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
  fieldCoverage: Record<string, { populated: number; total: number; pct: string }>;
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

export async function buildSubdivisionsSpec(): Promise<SubdivisionsSpec> {
  const primaryLanguageMap = await loadCountriesPrimaryLanguageMap();
  const loaded = await loadSubdivisions(primaryLanguageMap);

  const total = loaded.entries.length;
  const coverage = {
    iso31662Code: total,
    shortCode: total,
    countryISO31661Alpha2Code: total,
    displayName: total,
    type: total,
    parentISO31662Code: loaded.entries.filter((e) => e.parentISO31662Code !== null).length,
    endonymDisplayName: loaded.entries.filter((e) => e.endonymDisplayName !== null).length,
    localizedDisplayNames_en: loaded.entries.filter((e) => e.localizedDisplayNames["en"]).length,
    localizedDisplayNames_ja: loaded.entries.filter((e) => e.localizedDisplayNames["ja"]).length,
    localizedDisplayNames_zh: loaded.entries.filter((e) => e.localizedDisplayNames["zh"]).length,
  };
  const fieldCoverage: Record<string, { populated: number; total: number; pct: string }> = {};
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
  console.error(`  order breakdown: 1st=${firstOrder}, 2nd=${secondOrder}, 3+=${thirdPlusOrder}`);
  console.error(`  countries with 2nd+ order data: ${countriesWithSecondPlusOrder}`);
  console.error(`  debian-only entries: ${loaded.debianOnly}, CLDR-only: ${loaded.cldrOnly}`);
  for (const [field, stats] of Object.entries(fieldCoverage)) {
    console.error(`  ${field}: ${stats.populated}/${stats.total} (${stats.pct})`);
  }

  return {
    $schema: "./subdivisions.schema.json",
    $note:
      "PIPELINE-RAW spec — produced by tools/geo-data-pipeline. Not directly consumed by " +
      "codegen / D2.Shared.Geo.Default. A clean/transform pass to the sibling " +
      "contracts/geo/subdivisions.spec.json (one level up) is a separate step. Sources: " +
      "debian/iso-codes iso_3166-2.json (LGPL — authoritative ISO 3166-2 hierarchy: " +
      "code/type/parent/order) joined with CLDR cldr-subdivisions-full (Unicode-3.0 — English " +
      "displayName authority) + Wikidata SPARQL (CC0 — endonyms + localized names across 89 " +
      "languages with 93-97% per-language coverage). The catalog ships ALL orders (1st " +
      "through 3rd+); consumers filter on the `order` field. Country.Subdivisions M:M nav " +
      "back-fill happens in a separate cross-catalog merge pass by the Tier 2 builder.",
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
    entries: loaded.entries,
  };
}

if (
  process.argv[1]?.endsWith("write-subdivisions.ts") ||
  process.argv[1]?.endsWith("write-subdivisions.js")
) {
  const spec = await buildSubdivisionsSpec();
  await mkdir(dirname(SPEC_OUTPUT_PATH), { recursive: true });
  await writeSpecJson(SPEC_OUTPUT_PATH, spec);
  console.error(`[write] ${SPEC_OUTPUT_PATH} (${spec.entries.length} entries)`);
}
