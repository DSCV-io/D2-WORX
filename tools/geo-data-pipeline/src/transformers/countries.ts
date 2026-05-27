// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { CountryCodesRow } from "../fetchers/datasets-country-codes.js";

/**
 * Partial Country spec entry produced from a single source.
 * The full Country record has more fields (endonyms, phone format, CLDR enrichments,
 * M:M lists, etc.) that come from other sources. This shape captures what
 * `datasets/country-codes` can populate; remaining fields are filled by other
 * transformers and merged by the spec-writer's merge pass.
 */
export interface CountryPartialFromDatasets {
  iso31661Alpha2Code: string;
  iso31661Alpha3Code: string | null;
  iso31661NumericCode: string | null;
  displayName: string;
  officialName: string;
  sovereignCountryISO31661Alpha2Code: string | null;
  phoneNumberPrefix: string | null;
  primaryCurrencyISO4217AlphaCode: string | null;
  primaryLanguageISO6391Code: string | null;
  // Provenance — which source(s) each field originated from. Helps merging.
  _provenance: {
    source: "datasets-country-codes";
    extractedAt: string;
  };
}

const COUNTRY_CODE_LENGTH = 2;
const SOVEREIGNTY_PATTERNS: Array<[RegExp, "code-tail" | "literal"]> = [
  [/^Part of ([A-Z]{2})$/, "code-tail"],
  [/^Territory of ([A-Z]{2})$/, "code-tail"],
  [/^Territories of ([A-Z]{2})$/, "code-tail"],
  [/^Commonwealth of ([A-Z]{2})$/, "code-tail"],
  [/^Crown dependency of ([A-Z]{2})$/, "code-tail"],
  [/^Associated with ([A-Z]{2})$/, "code-tail"],
];

/**
 * Maps a datasets-country-codes row to a partial Country spec entry.
 * Returns null when the row lacks the minimum required identity (ISO 3166-1 Alpha-2).
 */
export function transformCountryRow(
  row: CountryCodesRow,
): CountryPartialFromDatasets | null {
  const alpha2 = row["ISO3166-1-Alpha-2"]?.trim();
  if (!alpha2 || alpha2.length !== COUNTRY_CODE_LENGTH) {
    return null;
  }
  const displayName =
    row["CLDR display name"]?.trim() ?? row["official_name_en"]?.trim();
  const officialName =
    row["official_name_en"]?.trim() ?? row["CLDR display name"]?.trim();
  if (!displayName || !officialName) {
    return null;
  }

  return {
    iso31661Alpha2Code: alpha2.toUpperCase(),
    iso31661Alpha3Code: row["ISO3166-1-Alpha-3"]?.trim().toUpperCase() ?? null,
    iso31661NumericCode: padNumericCode(
      row["ISO3166-1-numeric"]?.trim() ?? null,
    ),
    displayName,
    officialName,
    sovereignCountryISO31661Alpha2Code: deriveSovereign(
      row["is_independent"]?.trim() ?? null,
    ),
    phoneNumberPrefix: cleanPhonePrefix(row["Dial"]?.trim() ?? null),
    primaryCurrencyISO4217AlphaCode: cleanCurrencyCode(
      row["ISO4217-currency_alphabetic_code"],
    ),
    primaryLanguageISO6391Code: derivePrimaryLanguage(
      row["Languages"]?.trim() ?? null,
    ),
    _provenance: {
      source: "datasets-country-codes",
      extractedAt: new Date().toISOString(),
    },
  };
}

/**
 * Derives `SovereignCountryISO31661Alpha2Code` from the dataset's `is_independent` text.
 * Recognized patterns include "Part of XX", "Territory of XX", "Commonwealth of XX",
 * "Crown dependency of XX", "Associated with XX" (where XX is an ISO 3166-1 alpha-2).
 * Unknown patterns and explicit edge cases ("In contention", "International") return null;
 * consuming code should treat null as "sovereign or undetermined."
 */
export function deriveSovereign(isIndependent: string | null): string | null {
  if (!isIndependent || isIndependent === "Yes") return null;
  for (const [pattern] of SOVEREIGNTY_PATTERNS) {
    const match = isIndependent.match(pattern);
    if (match?.[1]) return match[1].toUpperCase();
  }
  return null;
}

/**
 * The dataset stores numeric codes without leading zeros (e.g., "4" for Afghanistan).
 * ISO 3166-1 numeric codes are canonically 3-digit zero-padded ("004").
 */
export function padNumericCode(raw: string | null): string | null {
  if (!raw) return null;
  if (!/^\d{1,3}$/.test(raw)) return null;
  return raw.padStart(3, "0");
}

/**
 * Dial codes in the dataset are sometimes "1", "1-787", "44", "972" etc.
 * For PrimaryCurrencyISO4217 + libphonenumber consumption we only need the country code
 * portion (the digits before any hyphen). Multi-prefix countries like the US/Canada NANP
 * share "1"; subdivisions like "1-787" (Puerto Rico) get the area code stripped.
 */
export function cleanPhonePrefix(raw: string | null): string | null {
  if (!raw) return null;
  const trimmed = raw.split(/[-,]/)[0]?.trim();
  if (!trimmed) return null;
  if (!/^\d{1,4}$/.test(trimmed)) return null;
  return trimmed;
}

/**
 * Some rows have comma-separated currency codes in `ISO4217-currency_alphabetic_code`
 * (a CSV format violation upstream — should be one code per field). Examples seen:
 *   UY: "UYU,UYW" (UYW = Uruguayan Peso en Unidades Indexadas — inflation-adjusted variant)
 *   VE: "VES,VED" (VED = Bolívar Digital — transitional digital currency)
 * The PRIMARY currency is the first listed; the variant is secondary. Take the first
 * 3-letter code matching ISO 4217 alpha format.
 */
export function cleanCurrencyCode(
  raw: string | null | undefined,
): string | null {
  if (!raw) return null;
  const first = raw.split(",")[0]?.trim().toUpperCase();
  if (!first) return null;
  if (!/^[A-Z]{3}$/.test(first)) return null;
  return first;
}

/**
 * `Languages` is a comma-separated list of BCP-47-ish tags ordered by prevalence (e.g.,
 * "en-CA,fr-CA"). Primary = first entry's ISO 639-1 part. Subsequent entries inform the
 * Country.Locales M:M, which is the responsibility of a separate transformer that joins
 * against the supported-locales overlay.
 */
export function derivePrimaryLanguage(languages: string | null): string | null {
  if (!languages) return null;
  const first = languages.split(",")[0]?.trim();
  if (!first) return null;
  const langPart = first.split("-")[0]?.toLowerCase();
  if (!langPart || !/^[a-z]{2,3}$/.test(langPart)) return null;
  return langPart;
}
