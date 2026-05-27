// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { LanguageCodeRow } from "../fetchers/datasets-language-codes.js";
import {
  deriveWritingDirection,
  type LanguageDataPayload,
  type ScriptMetadataPayload,
} from "../fetchers/cldr-script-language-data.js";

/**
 * Partial Language spec entry produced by merging:
 *   - datasets/language-codes (PDDL) — primary alpha2 + English name catalog
 *   - Wikidata SPARQL (CC0) — endonyms via P218
 *   - CLDR scriptMetadata.json + languageData.json (Unicode-3.0) — writing direction
 *   - CLDR cldr-localenames-full/{locale}/languages.json (Unicode-3.0) — localized display names
 *
 * Mirrors the Language entity shape. `IsSupported` is a Tier 2 field (derived from the
 * selectable-locale set) and not part of this pipeline-raw shape; consumers apply it
 * downstream via the SUPPORTED_LANGUAGE_CODES list.
 */
export interface LanguagePartial {
  /** ISO 639-1 two-letter code, lowercase. Primary key. */
  iso6391Code: string;
  /** English display name from datasets/language-codes (PDDL). */
  displayName: string;
  /** Native-language name from Wikidata P218. Null when no endonym available. */
  endonymDisplayName: string | null;
  /** "LTR" or "RTL" derived from CLDR script→rtl chain. Null when no script mapping. */
  writingDirection: "LTR" | "RTL" | null;
  /**
   * Primary ISO 15924 script code from CLDR languageData. Informational; pairs with
   * writingDirection.
   */
  primaryScriptISO15924Code: string | null;
  /**
   * Localized display names across the 11 supported languages (parallel to
   * Country.LocalizedDisplayNames).
   */
  localizedDisplayNames: Record<string, string>;
  _provenance: {
    sources: string[];
    extractedAt: string;
  };
}

/**
 * Build a LanguagePartial for a single datasets row, given the auxiliary lookup maps.
 * Returns null when the row lacks a valid 2-letter alpha2 code.
 */
export function transformLanguageRow(
  row: LanguageCodeRow,
  ctx: {
    wikidataEndonyms: Map<string, string>;
    scriptMetadata: ScriptMetadataPayload["scriptMetadata"];
    languageData: LanguageDataPayload["supplemental"]["languageData"];
    cldrLanguageNamesByLocale: Map<string, Map<string, string>>;
  },
): LanguagePartial | null {
  const iso = row.alpha2.toLowerCase();
  if (iso.length !== 2 || !/^[a-z]{2}$/.test(iso)) return null;

  const endonym = ctx.wikidataEndonyms.get(iso) ?? null;
  const direction = deriveWritingDirection(
    iso,
    ctx.scriptMetadata,
    ctx.languageData,
  );
  const primaryScript = ctx.languageData[iso]?._scripts?.[0] ?? null;

  const localizedDisplayNames: Record<string, string> = {};
  for (const [locale, names] of ctx.cldrLanguageNamesByLocale.entries()) {
    const name = names.get(iso);
    if (name) localizedDisplayNames[locale] = name;
  }

  const sources: string[] = ["datasets/language-codes"];
  if (endonym) sources.push("wikidata-sparql");
  if (direction) sources.push("cldr-script-language-data");
  if (Object.keys(localizedDisplayNames).length > 0)
    sources.push("cldr-localenames-full");

  return {
    iso6391Code: iso,
    displayName: row.english,
    endonymDisplayName: endonym,
    writingDirection: direction,
    primaryScriptISO15924Code: primaryScript,
    localizedDisplayNames,
    _provenance: {
      sources,
      extractedAt: new Date().toISOString(),
    },
  };
}
