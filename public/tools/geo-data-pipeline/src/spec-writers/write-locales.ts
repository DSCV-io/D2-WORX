// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { mkdir } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fetchCldrAvailableLocales } from "../fetchers/cldr-available-locales.js";
import {
  fetchCldrDates,
  type DateFormatPattern,
} from "../fetchers/cldr-dates.js";
import {
  deriveDefaultRegionTag,
  fetchCldrLikelySubtags,
} from "../fetchers/cldr-likely-subtags.js";
import { fetchCldrNumbers } from "../fetchers/cldr-numbers.js";
import {
  transformLocaleTag,
  type LocaleCldrFormattingData,
  type LocalePartial,
} from "../transformers/locales.js";
import type { FetchProvenance } from "../util/cache.js";
import { REPO_ROOT_PATH } from "../util/cache.js";
import { writeSpecJson } from "../util/json-encoding.js";

const SPEC_OUTPUT_PATH = resolve(
  REPO_ROOT_PATH,
  "public",
  "contracts",
  "geo",
  "src-data",
  "locales.spec.json",
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

interface LocalesSpec {
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
  /** Stats describing locale tag shape distribution. */
  tagShape: {
    bareLanguage: number;
    languageRegion: number;
    languageScriptRegion: number;
    languageScript: number;
    other: number;
  };
  /**
   * Count of lang-Region tags derived via CLDR likelySubtags
   * (NOT in availableLocales.json directly).
   */
  derivedDefaultRegionTags: number;
  entries: LocalePartial[];
}

export async function buildLocalesSpec(): Promise<LocalesSpec> {
  // Layer A.1 — CLDR availableLocales.json (authoritative tag catalog)
  console.error(`[fetch] CLDR availableLocales.json`);
  const cldr = await fetchCldrAvailableLocales();

  // Layer A.2 — CLDR likelySubtags.json (to derive default-region tags like "en-US"
  // that availableLocales.json deliberately omits as redundant with bare "en")
  console.error(`[fetch] CLDR likelySubtags.json`);
  const likely = await fetchCldrLikelySubtags();

  const sources: LocalesSpec["sources"] = [
    {
      name: cldr.provenance.source,
      url: cldr.provenance.url,
      license: cldr.provenance.license,
      fetchedAt: cldr.provenance.fetchedAt,
      sha256: cldr.provenance.sha256,
    },
    {
      name: likely.provenance.source,
      url: likely.provenance.url,
      license: likely.provenance.license,
      fetchedAt: likely.provenance.fetchedAt,
      sha256: likely.provenance.sha256,
    },
    {
      name: "node-icu/Intl.DisplayNames",
      url: "https://nodejs.org/docs/latest/api/intl.html",
      license: "Unicode-3.0 (ICU embedded in Node)",
      fetchedAt: new Date().toISOString(),
      sha256: `node-${process.versions.node}`,
    },
  ];

  // Assemble candidate tags: CLDR full set + derived default-region tags
  //   - For bare-language tags ("en", "ja", "zh"): derive the default region
  //     ("en-US", "ja-JP", "zh-CN").
  //   - For lang-Script tags ("zh-Hans", "zh-Hant", "sr-Cyrl", "sr-Latn"): derive the
  //     default region in the script context ("zh-Hans-CN", "zh-Hant-TW", "sr-Cyrl-RS",
  //     "sr-Latn-RS"). CLDR's likelySubtags keys these by lang-Script directly; the
  //     expanded form is lang-Script-Region. We preserve the script subtag when deriving.
  const candidateTags = new Set<string>(cldr.fullTags);
  let derivedCount = 0;
  for (const tag of cldr.fullTags) {
    const parts = tag.split("-");
    if (parts.length === 1) {
      // Bare-language tag — derive lang-Region default (script dropped)
      const defaultRegion = deriveDefaultRegionTag(tag, likely.bySourceTag);
      if (defaultRegion && !candidateTags.has(defaultRegion)) {
        candidateTags.add(defaultRegion);
        derivedCount++;
      }
    } else if (
      parts.length === 2 &&
      parts[1] &&
      /^[A-Z][a-z]{3}$/.test(parts[1])
    ) {
      // Lang-Script tag (e.g., "zh-Hans") — derive lang-Script-Region by looking up the
      // lang-Script form directly in likelySubtags, then reconstructing with script preserved.
      const expanded = likely.bySourceTag.get(tag);
      if (expanded) {
        const expParts = expanded.split("-");
        if (
          expParts.length === 3 &&
          expParts[0] &&
          expParts[1] &&
          expParts[2]
        ) {
          const derivedTag = `${expParts[0]}-${expParts[1]}-${expParts[2]}`;
          if (!candidateTags.has(derivedTag)) {
            candidateTags.add(derivedTag);
            derivedCount++;
          }
        }
      }
    }
  }
  console.error(
    `  [derive] +${derivedCount} default-region tags from likelySubtags`,
  );

  // Layer A.3 — CLDR numbers + dates per-locale fetches via locale-inheritance fallback chain.
  // For each candidate tag we walk: <tag> → drop last subtag → ... → "en" (root fallback).
  // Cache results keyed by tag so sibling lookups are free. Provenance is collected for each
  // distinct successful upstream fetch.
  console.error(
    `[fetch] CLDR per-locale numbers + dates (with inheritance fallback)`,
  );
  const formattingByTag = new Map<string, LocaleCldrFormattingData>();
  const numbersProvenance = new Map<string, FetchProvenance>();
  const datesProvenance = new Map<string, FetchProvenance>();
  for (const tag of candidateTags) {
    const resolved = await resolveLocaleFormatting(
      tag,
      numbersProvenance,
      datesProvenance,
    );
    if (resolved) formattingByTag.set(tag, resolved);
  }
  for (const prov of numbersProvenance.values()) {
    sources.push({
      name: prov.source,
      url: prov.url,
      license: prov.license,
      fetchedAt: prov.fetchedAt,
      sha256: prov.sha256,
    });
  }
  for (const prov of datesProvenance.values()) {
    sources.push({
      name: prov.source,
      url: prov.url,
      license: prov.license,
      fetchedAt: prov.fetchedAt,
      sha256: prov.sha256,
    });
  }
  console.error(
    `  [cldr] numbers distinct locales fetched: ${numbersProvenance.size}; ` +
      `dates: ${datesProvenance.size}`,
  );

  // Merge pass — transform each tag through ICU + resolved CLDR formatting
  const entries: LocalePartial[] = [];
  let skipped = 0;
  let skippedNoFormatting = 0;
  for (const tag of candidateTags) {
    const formatting = formattingByTag.get(tag);
    if (!formatting) {
      // CLDR has no usable data for this tag (even after fallback to en) — skip.
      // Very rare; happens only when "en" itself fails (network error path).
      skippedNoFormatting++;
      continue;
    }
    const partial = transformLocaleTag(tag, {
      supportedLocaleCodes: SUPPORTED_LANGUAGE_CODES,
      formatting,
    });
    if (partial) entries.push(partial);
    else skipped++;
  }
  if (skippedNoFormatting > 0) {
    console.error(
      `  [skip] ${skippedNoFormatting} tags missing CLDR formatting data`,
    );
  }
  entries.sort((a, b) => a.ietfBcp47Tag.localeCompare(b.ietfBcp47Tag));

  // Tag-shape distribution stats
  const tagShape = {
    bareLanguage: 0,
    languageRegion: 0,
    languageScriptRegion: 0,
    languageScript: 0,
    other: 0,
  };
  for (const e of entries) {
    if (!e.scriptSubtag && !e.regionSubtag) tagShape.bareLanguage++;
    else if (!e.scriptSubtag && e.regionSubtag) tagShape.languageRegion++;
    else if (e.scriptSubtag && e.regionSubtag) tagShape.languageScriptRegion++;
    else if (e.scriptSubtag && !e.regionSubtag) tagShape.languageScript++;
    else tagShape.other++;
  }

  // Field coverage report
  const total = entries.length;
  const coverage = {
    ietfBcp47Tag: countNonNull(entries, (e) => e.ietfBcp47Tag),
    languageSubtag: countNonNull(entries, (e) => e.languageSubtag),
    scriptSubtag: countNonNull(entries, (e) => e.scriptSubtag),
    regionSubtag: countNonNull(entries, (e) => e.regionSubtag),
    displayName: countNonNull(entries, (e) => e.displayName),
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
    decimalSeparator: countNonNull(entries, (e) => e.decimalSeparator || null),
    thousandsSeparator: countNonNull(
      entries,
      (e) => e.thousandsSeparator || null,
    ),
    dateFormatPattern: countNonNull(entries, (e) => e.dateFormatPattern),
    cldrDataSourceLocale_exact: entries.filter(
      (e) => e.cldrDataSourceLocale === e.ietfBcp47Tag,
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
    `[transform] locales: ${entries.length} entries (${skipped} tags skipped)`,
  );
  console.error(`  tag shapes:`);
  for (const [shape, count] of Object.entries(tagShape)) {
    console.error(`    ${shape}: ${count}`);
  }
  for (const [field, stats] of Object.entries(fieldCoverage)) {
    console.error(
      `  ${field}: ${stats.populated}/${stats.total} (${stats.pct})`,
    );
  }

  return {
    $schema: "./locales.schema.json",
    $note:
      "PIPELINE-RAW spec — produced by tools/geo-data-pipeline. Not directly consumed by " +
      "codegen / DcsvIo.D2.Geo.Default. A clean/transform pass to the sibling " +
      "public/contracts/geo/locales.spec.json (one level up) is a separate step. Sources: " +
      "CLDR cldr-core/availableLocales.json (Unicode-3.0 — `full` set) + CLDR " +
      "supplemental/likelySubtags.json (Unicode-3.0 — used to derive default-region tags " +
      "like 'en-US' / 'pt-BR' / 'zh-Hans-CN' / 'ja-JP' that availableLocales.json " +
      "deliberately omits as redundant with their bare-language forms) + Node's built-in " +
      "ICU Intl.Locale (structural decomposition into language/script/region subtags) + " +
      "Node's Intl.DisplayNames (English display name + endonym in own language + localized " +
      "display names across the 11 supported locales) + CLDR cldr-numbers-full/{locale}/" +
      "numbers.json (Unicode-3.0 — decimal + thousands separators via CLDR locale-inheritance " +
      "fallback chain) + CLDR cldr-dates-full/{locale}/ca-gregorian.json (Unicode-3.0 — " +
      "dateFormatPattern enum DMY/MDY/YMD derived from short date pattern via same fallback " +
      "chain). `cldrDataSourceLocale` field carries the actual fallback tag CLDR resolved to " +
      "(equals ietfBcp47Tag when locale has a direct file). Missing: IsSelectable (Tier 2 " +
      "curated subset — typically the 11 lang-only tags + their default lang-region forms), " +
      "Deprecation, denormalized region-derived fields (firstDayOfWeek / weekendStart / " +
      "weekendEnd / measurementSystem) which live on Country in src-data and get denormalized " +
      "onto Locale in the Tier 2 clean-pass. UN M49 numeric regions (e.g., 'en-001' = World " +
      "English) surface in regionSubtag as their numeric string.",
    catalogVersion: "0.0.1",
    generatedAt: new Date().toISOString(),
    sources,
    fieldCoverage,
    tagShape,
    derivedDefaultRegionTags: derivedCount,
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

/**
 * Walks the CLDR locale-inheritance chain for a tag, trying numbers.json + ca-gregorian.json
 * at each step. Returns the first successful pair as a LocaleCldrFormattingData. Caches each
 * upstream fetch's provenance into the caller-provided maps so the writer can dedupe sources.
 *
 * Chain: `<tag>` → drop last subtag → ... → `"en"` (root fallback).
 * Example: "zh-Hans-CN" → "zh-Hans" → "zh" → "en"
 * Example: "en-CA" → "en"
 *
 * Within-process per-source caches (resolvedNumbersBy / resolvedDatesBy) avoid re-resolving
 * the same chain for sibling locales — first "zh-Hans-CN" might walk to "zh" for numbers; a
 * subsequent "zh-Hans-MO" reuses the cached "zh" payload directly.
 */
const resolvedNumbersBy = new Map<
  string,
  { decimal: string; thousands: string; provenance: FetchProvenance }
>();
const resolvedDatesBy = new Map<
  string,
  { pattern: DateFormatPattern; provenance: FetchProvenance }
>();

async function resolveLocaleFormatting(
  tag: string,
  numbersProvenance: Map<string, FetchProvenance>,
  datesProvenance: Map<string, FetchProvenance>,
): Promise<LocaleCldrFormattingData | null> {
  const chain = buildFallbackChain(tag);

  let numbersResult: {
    decimal: string;
    thousands: string;
    sourceTag: string;
  } | null = null;
  for (const candidate of chain) {
    if (resolvedNumbersBy.has(candidate)) {
      const cached = resolvedNumbersBy.get(candidate)!;
      numbersProvenance.set(candidate, cached.provenance);
      numbersResult = {
        decimal: cached.decimal,
        thousands: cached.thousands,
        sourceTag: candidate,
      };
      break;
    }
    try {
      const r = await fetchCldrNumbers(candidate);
      resolvedNumbersBy.set(candidate, {
        decimal: r.decimalSeparator,
        thousands: r.thousandsSeparator,
        provenance: r.provenance,
      });
      numbersProvenance.set(candidate, r.provenance);
      numbersResult = {
        decimal: r.decimalSeparator,
        thousands: r.thousandsSeparator,
        sourceTag: candidate,
      };
      break;
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      if (message.includes(" 404 ") || message.includes("Not Found")) {
        // Expected — try next tag in the CLDR locale-inheritance chain.
        continue;
      }
      console.error(
        `  [warn] cldr-numbers-full/${candidate}/numbers.json fetch failed: ${message}`,
      );
      throw err;
    }
  }
  if (!numbersResult) return null;

  let datesResult: { pattern: DateFormatPattern; sourceTag: string } | null =
    null;
  for (const candidate of chain) {
    if (resolvedDatesBy.has(candidate)) {
      const cached = resolvedDatesBy.get(candidate)!;
      datesProvenance.set(candidate, cached.provenance);
      datesResult = { pattern: cached.pattern, sourceTag: candidate };
      break;
    }
    try {
      const r = await fetchCldrDates(candidate);
      resolvedDatesBy.set(candidate, {
        pattern: r.dateFormatPattern,
        provenance: r.provenance,
      });
      datesProvenance.set(candidate, r.provenance);
      datesResult = { pattern: r.dateFormatPattern, sourceTag: candidate };
      break;
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      if (message.includes(" 404 ") || message.includes("Not Found")) {
        // Expected — try next tag in the CLDR locale-inheritance chain.
        continue;
      }
      console.error(
        `  [warn] cldr-dates-full/${candidate}/ca-gregorian.json fetch failed: ${message}`,
      );
      throw err;
    }
  }
  if (!datesResult) return null;

  return {
    decimalSeparator: numbersResult.decimal,
    thousandsSeparator: numbersResult.thousands,
    dateFormatPattern: datesResult.pattern,
    // When numbers and dates resolved from different fallback tags, surface the LESS-specific
    // one (the parent further up the chain) so operators see worst-case fallback distance.
    cldrDataSourceLocale: pickLessSpecific(
      numbersResult.sourceTag,
      datesResult.sourceTag,
      chain,
    ),
  };
}

function buildFallbackChain(tag: string): string[] {
  const parts = tag.split("-");
  const chain: string[] = [];
  for (let i = parts.length; i >= 1; i--) {
    chain.push(parts.slice(0, i).join("-"));
  }
  // Root fallback — always end at "en" if not already present
  if (chain[chain.length - 1] !== "en") chain.push("en");
  return chain;
}

function pickLessSpecific(
  a: string,
  b: string,
  chain: readonly string[],
): string {
  // The one appearing LATER in the chain is the LESS-specific (more-fallback) one.
  const aIdx = chain.indexOf(a);
  const bIdx = chain.indexOf(b);
  return aIdx >= bIdx ? a : b;
}

if (
  process.argv[1]?.endsWith("write-locales.ts") ||
  process.argv[1]?.endsWith("write-locales.js")
) {
  const spec = await buildLocalesSpec();
  await mkdir(dirname(SPEC_OUTPUT_PATH), { recursive: true });
  await writeSpecJson(SPEC_OUTPUT_PATH, spec);
  console.error(`[write] ${SPEC_OUTPUT_PATH} (${spec.entries.length} entries)`);
}
