// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { mkdir } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import {
  fetchCldrCurrencies,
  type CldrCurrencyEntry,
} from "../fetchers/cldr-currencies.js";
import {
  fetchCldrSupplemental,
  type CurrencyDataPayload,
} from "../fetchers/cldr-supplemental.js";
import { fetchDatasetsCurrencyCodes } from "../fetchers/datasets-currency-codes.js";
import {
  buildCurrencyEntries,
  type CurrencyPartial,
} from "../transformers/currencies.js";
import { REPO_ROOT_PATH } from "../util/cache.js";
import { writeSpecJson } from "../util/json-encoding.js";
import {
  getEndonymLanguageList,
  loadCountryPrimaryLanguageMap,
} from "../util/endonym-languages.js";

const SPEC_OUTPUT_PATH = resolve(
  REPO_ROOT_PATH,
  "public",
  "contracts",
  "geo",
  "src-data",
  "currencies.spec.json",
);

interface CurrenciesSpec {
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
  /** Counts split active vs retired so operators see the breakdown at a glance. */
  activityBreakdown: { active: number; retired: number };
  entries: CurrencyPartial[];
}

export async function buildCurrenciesSpec(): Promise<CurrenciesSpec> {
  // Layer A.1 — datasets/currency-codes (PDDL) — per-country canonical alpha3/numeric/decimals
  console.error(`[fetch] datasets/currency-codes`);
  const datasets = await fetchDatasetsCurrencyCodes();

  // Layer A.2 — CLDR currencyData.json — temporal usage history + fractions
  console.error(`[fetch] CLDR currencyData supplemental`);
  const cldrCurrencyData =
    await fetchCldrSupplemental<CurrencyDataPayload>("currencyData");

  // Layer A.3 — countries.spec.json (already on disk) for the country→primary-language map.
  // Cross-spec dependency; acceptable since both live in the same pipeline output dir.
  console.error(`[load] countries.spec.json for country→primary-language map`);
  const countryToPrimaryLang = await loadCountryPrimaryLanguageMap().catch(
    (err: unknown) => {
      throw new Error(
        `currencies spec needs countries.spec.json on disk for country→primary-language map; ` +
          `run \`pnpm write:countries\` first, then \`pnpm write:currencies\`. ` +
          `Cause: ${String(err)}`,
      );
    },
  );

  // Layer A.4 — CLDR cldr-numbers-full per-locale currencies files. We fetch the union of
  // (11 supported locales) ∪ (every primary language of every country in the catalog) so
  // endonym derivation works for currencies whose home country speaks Arabic/Hindi/Thai/etc.
  // Some CLDR locales lack a currencies.json file (smaller locales) — gracefully skip 404s.
  const allEndonymLanguages = await getEndonymLanguageList();
  console.error(
    `[fetch] CLDR per-locale currencies (${allEndonymLanguages.length} languages)`,
  );
  const cldrLocaleCurrencies = new Map<
    string,
    Map<string, CldrCurrencyEntry>
  >();
  const cldrLocaleProvenances: Array<{
    name: string;
    url: string;
    license: string;
    fetchedAt: string;
    sha256: string;
  }> = [];
  let localeSkipped = 0;
  for (const locale of allEndonymLanguages) {
    try {
      const r = await fetchCldrCurrencies(locale);
      cldrLocaleCurrencies.set(locale, r.byCurrencyCode);
      cldrLocaleProvenances.push({
        name: r.provenance.source,
        url: r.provenance.url,
        license: r.provenance.license,
        fetchedAt: r.provenance.fetchedAt,
        sha256: r.provenance.sha256,
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      if (message.includes(" 404 ") || message.includes("Not Found")) {
        // Locale has no currencies.json in cldr-numbers-full — silently skip.
        // Common for smaller / less-localized CLDR locales.
        localeSkipped++;
      } else {
        console.error(
          `  [warn] cldr-numbers-full currencies.json fetch failed for ${locale}: ${message}`,
        );
        throw err;
      }
    }
  }
  if (localeSkipped > 0) {
    console.error(
      `  [skip] ${localeSkipped} locales without cldr-numbers-full currencies.json`,
    );
  }

  // Provenance assembly
  const sources: CurrenciesSpec["sources"] = [
    {
      name: datasets.provenance.source,
      url: datasets.provenance.url,
      license: datasets.provenance.license,
      fetchedAt: datasets.provenance.fetchedAt,
      sha256: datasets.provenance.sha256,
    },
    {
      name: cldrCurrencyData.provenance.source,
      url: cldrCurrencyData.provenance.url,
      license: cldrCurrencyData.provenance.license,
      fetchedAt: cldrCurrencyData.provenance.fetchedAt,
      sha256: cldrCurrencyData.provenance.sha256,
    },
    ...cldrLocaleProvenances,
  ];

  // Build via transformer
  const entries = buildCurrencyEntries({
    datasetsRows: datasets.rows,
    cldrRegion: cldrCurrencyData.payload.supplemental.currencyData.region,
    cldrFractions:
      cldrCurrencyData.payload.supplemental.currencyData.fractions ?? {},
    cldrLocaleCurrencies,
    countryToPrimaryLang,
  });

  // Activity breakdown
  const activityBreakdown = {
    active: entries.filter((e) => e.isActive).length,
    retired: entries.filter((e) => !e.isActive).length,
  };

  // Field coverage report
  const total = entries.length;
  const coverage = {
    iso4217AlphaCode: countNonNull(entries, (e) => e.iso4217AlphaCode),
    iso4217NumericCode: countNonNull(entries, (e) => e.iso4217NumericCode),
    displayName: countNonNull(entries, (e) => e.displayName),
    symbol: countNonNull(entries, (e) => e.symbol),
    endonymDisplayName: countNonNull(entries, (e) => e.endonymDisplayName),
    localizedDisplayNames_en: countNonNull(
      entries,
      (e) => e.localizedDisplayNames["en"] ?? null,
    ),
    localizedDisplayNames_ja: countNonNull(
      entries,
      (e) => e.localizedDisplayNames["ja"] ?? null,
    ),
    localizedDisplayNames_zh: countNonNull(
      entries,
      (e) => e.localizedDisplayNames["zh"] ?? null,
    ),
    localizedDisplayNames_ar: countNonNull(
      entries,
      (e) => e.localizedDisplayNames["ar"] ?? null,
    ),
    usageHistory: entries.filter((e) => e.usageHistory.length > 0).length,
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
    `[transform] currencies: ${entries.length} entries ` +
      `(${activityBreakdown.active} active, ${activityBreakdown.retired} retired)`,
  );
  for (const [field, stats] of Object.entries(fieldCoverage)) {
    console.error(
      `  ${field}: ${stats.populated}/${stats.total} (${stats.pct})`,
    );
  }

  return {
    $schema: "./currencies.schema.json",
    $note:
      "PIPELINE-RAW spec — produced by tools/geo-data-pipeline. Not directly consumed by " +
      "codegen / DcsvIo.D2.Geo.Default. A clean/transform pass to the sibling " +
      "public/contracts/geo/currencies.spec.json (one level up) is a separate step. Sources: " +
      "datasets/currency-codes (PDDL — ISO 4217 alpha3/numeric/decimals, active+retired) + " +
      "CLDR cldr-core/supplemental/currencyData.json (Unicode-3.0 — temporal per-country " +
      "usage history INVERTED into Currency.usageHistory + fractions/decimal-places) + " +
      "CLDR cldr-numbers-full/{locale}/currencies.json (Unicode-3.0 — English + ~95 " +
      "localized display names spanning all supported locales + every primary-language " +
      "of every country in the catalog, so endonym derivation works for currencies whose " +
      "home country speaks Arabic/Hindi/Thai/etc + narrow symbol). Catalog includes both " +
      "active AND retired currencies — better to include and let consumers filter than risk " +
      "incorrectly " +
      "excluding something that may need to be active later. usageHistory entries may " +
      "reference ISO 3166-3 transitional country codes (SU, YU, CS, DD) for dissolved " +
      "entities — the Tier 2 GeopoliticalEntity layer is where these get mapped. " +
      "Endonym derivation uses primary issuing country's primary language; multi-issuer " +
      "currencies like EUR may yield endonym=null when no single home country can be picked.",
    catalogVersion: "0.0.1",
    generatedAt: new Date().toISOString(),
    sources,
    fieldCoverage,
    activityBreakdown,
    entries,
  };
}

function countNonNull<T>(
  items: readonly T[],
  pick: (item: T) => string | null,
): number {
  let n = 0;
  for (const item of items) if (pick(item) !== null) n++;
  return n;
}

if (
  process.argv[1]?.endsWith("write-currencies.ts") ||
  process.argv[1]?.endsWith("write-currencies.js")
) {
  const spec = await buildCurrenciesSpec();
  await mkdir(dirname(SPEC_OUTPUT_PATH), { recursive: true });
  await writeSpecJson(SPEC_OUTPUT_PATH, spec);
  console.error(`[write] ${SPEC_OUTPUT_PATH} (${spec.entries.length} entries)`);
}
