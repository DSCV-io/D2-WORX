// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { mkdir } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fetchCldrLanguages } from "../fetchers/cldr-languages.js";
import { fetchScriptAndLanguageData } from "../fetchers/cldr-script-language-data.js";
import { fetchDatasetsLanguageCodes } from "../fetchers/datasets-language-codes.js";
import { fetchWikidataLanguageEndonyms } from "../fetchers/wikidata-language-endonyms.js";
import {
  transformLanguageRow,
  type LanguagePartial,
} from "../transformers/languages.js";
import { REPO_ROOT_PATH } from "../util/cache.js";
import { writeSpecJson } from "../util/json-encoding.js";

const SPEC_OUTPUT_PATH = resolve(
  REPO_ROOT_PATH,
  "contracts",
  "geo",
  "src-data",
  "languages.spec.json",
);

/** The 11 supported languages the platform ships UI translations for. */
const SUPPORTED_LANGUAGE_CODES = [
  "en",
  "es",
  "fr",
  "de",
  "it",
  "ja",
  "nl",
  "ko",
  "zh",
  "pt",
  "pl",
] as const;

interface LanguagesSpec {
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
  entries: LanguagePartial[];
}

export async function buildLanguagesSpec(): Promise<LanguagesSpec> {
  // Layer A.1 — datasets/language-codes (CSV) — primary alpha2 + English name catalog
  console.error(`[fetch] datasets/language-codes`);
  const datasets = await fetchDatasetsLanguageCodes();

  // Layer A.2 — Wikidata SPARQL endonyms via P218
  console.error(`[fetch] Wikidata language endonyms (SPARQL P218)`);
  const wikidata = await fetchWikidataLanguageEndonyms();

  // Layer A.3 — CLDR scriptMetadata + languageData (for writing direction derivation)
  console.error(`[fetch] CLDR scriptMetadata + languageData`);
  const cldrScripts = await fetchScriptAndLanguageData();

  // Layer A.4 — CLDR cldr-localenames-full/{locale}/languages.json
  //   (one fetch per supported locale)
  console.error(
    `[fetch] CLDR per-locale language names (11 supported languages)`,
  );
  const cldrLanguageNamesByLocale = new Map<string, Map<string, string>>();
  const cldrLanguageNamesProvenances: Array<{
    name: string;
    url: string;
    license: string;
    fetchedAt: string;
    sha256: string;
  }> = [];
  for (const locale of SUPPORTED_LANGUAGE_CODES) {
    const r = await fetchCldrLanguages(locale);
    cldrLanguageNamesByLocale.set(locale, r.namesByLangCode);
    cldrLanguageNamesProvenances.push({
      name: `${r.provenance.source}`,
      url: r.provenance.url,
      license: r.provenance.license,
      fetchedAt: r.provenance.fetchedAt,
      sha256: r.provenance.sha256,
    });
  }

  // Provenance assembly — one row per upstream pull
  const sources: LanguagesSpec["sources"] = [
    {
      name: datasets.provenance.source,
      url: datasets.provenance.url,
      license: datasets.provenance.license,
      fetchedAt: datasets.provenance.fetchedAt,
      sha256: datasets.provenance.sha256,
    },
    {
      name: wikidata.fetch.provenance.source,
      url: wikidata.fetch.provenance.url,
      license: wikidata.fetch.provenance.license,
      fetchedAt: wikidata.fetch.provenance.fetchedAt,
      sha256: wikidata.fetch.provenance.sha256,
    },
    {
      name: cldrScripts.scriptMetadataProvenance.source,
      url: cldrScripts.scriptMetadataProvenance.url,
      license: cldrScripts.scriptMetadataProvenance.license,
      fetchedAt: cldrScripts.scriptMetadataProvenance.fetchedAt,
      sha256: cldrScripts.scriptMetadataProvenance.sha256,
    },
    {
      name: cldrScripts.languageDataProvenance.source,
      url: cldrScripts.languageDataProvenance.url,
      license: cldrScripts.languageDataProvenance.license,
      fetchedAt: cldrScripts.languageDataProvenance.fetchedAt,
      sha256: cldrScripts.languageDataProvenance.sha256,
    },
    ...cldrLanguageNamesProvenances,
  ];

  // Merge pass
  const entries: LanguagePartial[] = [];
  let skipped = 0;
  for (const row of datasets.rows) {
    const partial = transformLanguageRow(row, {
      wikidataEndonyms: wikidata.byCode,
      scriptMetadata: cldrScripts.scriptMetadata,
      languageData: cldrScripts.languageData,
      cldrLanguageNamesByLocale,
    });
    if (partial) entries.push(partial);
    else skipped++;
  }
  entries.sort((a, b) => a.iso6391Code.localeCompare(b.iso6391Code));

  // Field coverage report
  const total = entries.length;
  const coverage = {
    iso6391Code: countNonNull(entries, (e) => e.iso6391Code),
    displayName: countNonNull(entries, (e) => e.displayName),
    endonymDisplayName: countNonNull(entries, (e) => e.endonymDisplayName),
    writingDirection: countNonNull(entries, (e) => e.writingDirection),
    primaryScriptISO15924Code: countNonNull(
      entries,
      (e) => e.primaryScriptISO15924Code,
    ),
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
    `[transform] languages: ${entries.length} entries (${skipped} rows skipped)`,
  );
  for (const [field, stats] of Object.entries(fieldCoverage)) {
    console.error(
      `  ${field}: ${stats.populated}/${stats.total} (${stats.pct})`,
    );
  }
  const endonymCount = entries.filter((e) => e.endonymDisplayName).length;
  console.error(
    `[wikidata] endonyms found for ${endonymCount}/${total} languages`,
  );

  return {
    $schema: "./languages.schema.json",
    $note:
      "PIPELINE-RAW spec — produced by tools/geo-data-pipeline. Not directly consumed by " +
      "codegen / D2.Shared.Geo.Default. A clean/transform pass to the sibling " +
      "contracts/geo/languages.spec.json (one level up) is a separate step. Sources: " +
      "datasets/language-codes (PDDL — alpha2 + English name) + Wikidata SPARQL (CC0 — " +
      "endonyms via P218 in own language) + CLDR cldr-core scriptMetadata + languageData " +
      "(Unicode-3.0 — writing direction via lang→primary script→rtl chain) + CLDR " +
      "cldr-localenames-full/{locale}/languages.json (Unicode-3.0 — localized display " +
      "names across the 11 supported locales). Missing: IsSupported (Tier 2 curated " +
      "subset = the 11 supported langs), Deprecation. ISO 639-3 codes (3-letter) are " +
      "deliberately excluded — the catalog ships ISO 639-1 only.",
    catalogVersion: "0.0.1",
    generatedAt: new Date().toISOString(),
    sources,
    fieldCoverage,
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
  process.argv[1]?.endsWith("write-languages.ts") ||
  process.argv[1]?.endsWith("write-languages.js")
) {
  const spec = await buildLanguagesSpec();
  await mkdir(dirname(SPEC_OUTPUT_PATH), { recursive: true });
  await writeSpecJson(SPEC_OUTPUT_PATH, spec);
  console.error(`[write] ${SPEC_OUTPUT_PATH} (${spec.entries.length} entries)`);
}
