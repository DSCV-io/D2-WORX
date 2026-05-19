// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import Papa from "papaparse";
import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "datasets-country-codes";
const SOURCE_URL =
  "https://raw.githubusercontent.com/datasets/country-codes/main/data/country-codes.csv";
const SOURCE_LICENSE = "PDDL-1.0 (Open Knowledge Foundation Public Domain Dedication)";
const CACHE_KEY = "country-codes.csv";

/**
 * One row of the datasets/country-codes CSV — ~50 columns documenting a country.
 * We type the columns we actually consume; the rest stay accessible via index signature.
 */
export interface CountryCodesRow {
  "ISO3166-1-Alpha-2": string | null;
  "ISO3166-1-Alpha-3": string | null;
  "ISO3166-1-numeric": string | null;
  "official_name_en": string | null;
  "official_name_fr": string | null;
  "official_name_es": string | null;
  "official_name_ar": string | null;
  "official_name_ru": string | null;
  "official_name_zh": string | null;
  "CLDR display name": string | null;
  "Capital": string | null;
  "Continent": string | null;
  "Dial": string | null;
  "ISO4217-currency_alphabetic_code": string | null;
  "ISO4217-currency_numeric_code": string | null;
  "ISO4217-currency_name": string | null;
  "ISO4217-currency_minor_unit": string | null;
  "Languages": string | null;
  "Region Name": string | null;
  "Region Code": string | null;
  "Sub-region Name": string | null;
  "Sub-region Code": string | null;
  "Intermediate Region Name": string | null;
  "Intermediate Region Code": string | null;
  "M49": string | null;
  "TLD": string | null;
  "Geoname ID": string | null;
  "ISO4217-currency_country_name": string | null;
  "is_independent": string | null;
  [column: string]: string | null;
}

export interface CountryCodesFetchResult extends Pick<CachedFetch, "provenance" | "fromCache"> {
  rows: CountryCodesRow[];
  columnCount: number;
}

export async function fetchDatasetsCountryCodes(options?: {
  ttlHours?: number;
}): Promise<CountryCodesFetchResult> {
  const fetched = await fetchAndCache({
    source: SOURCE_NAME,
    url: SOURCE_URL,
    license: SOURCE_LICENSE,
    cacheKey: CACHE_KEY,
    ttlHours: options?.ttlHours,
  });

  const csvText = fetched.body.toString("utf8");
  const parsed = Papa.parse<Record<string, string>>(csvText, {
    header: true,
    skipEmptyLines: true,
    transform: (value: string) => (value === "" ? "" : value),
  });

  if (parsed.errors.length > 0) {
    const significantErrors = parsed.errors.filter(
      (e) =>
        e.type !== "FieldMismatch" ||
        (e.code !== "TooFewFields" && e.code !== "TooManyFields"),
    );
    if (significantErrors.length > 0) {
      console.error(
        `[fetch] CSV parse errors (${significantErrors.length}):`,
        significantErrors.slice(0, 3),
      );
    }
  }

  const rows: CountryCodesRow[] = parsed.data.map((raw) => {
    const cleaned: Record<string, string | null> = {};
    for (const [key, value] of Object.entries(raw)) {
      cleaned[key] = value === "" ? null : value;
    }
    return cleaned as CountryCodesRow;
  });

  return {
    rows,
    columnCount: parsed.meta.fields?.length ?? 0,
    provenance: fetched.provenance,
    fromCache: fetched.fromCache,
  };
}

/**
 * Spike entry point — invoke from CLI to verify fetch + parse end-to-end.
 */
if (import.meta.url === `file://${process.argv[1]?.replaceAll("\\", "/")}`) {
  const result = await fetchDatasetsCountryCodes();
  const sample = result.rows[0];
  console.log(
    JSON.stringify(
      {
        fromCache: result.fromCache,
        rowCount: result.rows.length,
        columnCount: result.columnCount,
        provenance: result.provenance,
        firstRowAlpha2: sample?.["ISO3166-1-Alpha-2"],
        firstRowName: sample?.["CLDR display name"],
        sampleColumns: sample
          ? {
              alpha2: sample["ISO3166-1-Alpha-2"],
              alpha3: sample["ISO3166-1-Alpha-3"],
              numeric: sample["ISO3166-1-numeric"],
              displayName: sample["CLDR display name"],
              officialEn: sample["official_name_en"],
              dial: sample["Dial"],
              currency: sample["ISO4217-currency_alphabetic_code"],
              region: sample["Region Name"],
              subRegion: sample["Sub-region Name"],
              continent: sample["Continent"],
            }
          : null,
      },
      null,
      2,
    ),
  );
}
