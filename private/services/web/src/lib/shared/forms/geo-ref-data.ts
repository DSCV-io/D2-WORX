// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * GeoRefData → UI option array transforms.
 *
 * Converts catalog record arrays into combobox-friendly option arrays
 * with display labels, flag paths, phone prefixes, etc.
 */
import type { Country, Subdivision } from "@dcsv-io/d2-geo-abstractions";
import { getCountryCallingCode } from "./phone-format.js";

/**
 * Popular countries shown first in dropdowns (order preserved).
 * Mirrors DeCAF's top-countries list, converted to ISO alpha-2.
 */
const POPULAR_COUNTRIES: readonly string[] = [
  "US", // United States
  "CA", // Canada
  "MX", // Mexico
  "GB", // United Kingdom
  "DE", // Germany
  "FR", // France
  "IT", // Italy
  "ES", // Spain
  "UA", // Ukraine
  "PL", // Poland
  "NL", // Netherlands
  "AU", // Australia
  "NZ", // New Zealand
  "JP", // Japan
  "KR", // South Korea
  "CN", // China
  "HK", // Hong Kong
  "TW", // Taiwan
  "BR", // Brazil
  "CO", // Colombia
  "AR", // Argentina
  "IL", // Israel
  "SA", // Saudi Arabia
  "AE", // United Arab Emirates
] as const;

export interface CountryOption {
  /** ISO 3166-1 alpha-2 code (e.g. "US"). */
  value: string;
  /** Display name (e.g. "United States"). */
  label: string;
  /** Path to flag SVG (e.g. "/flags/4x3/us.svg"). */
  flag: string;
  /** International calling code (e.g. "+1"). */
  phonePrefix: string;
  /** National phone number format hint (e.g. "XXX-XXX-XXXX"). */
  phoneFormat: string;
  /** ISO 3166-2 codes for this country's subdivisions. */
  subdivisionCodes: string[];
}

export interface SubdivisionOption {
  /** ISO 3166-2 code (e.g. "US-CA"). */
  value: string;
  /** Display name (e.g. "California"). */
  label: string;
}

/**
 * Convert a countries array to a sorted option array.
 * Popular countries appear first (in POPULAR_COUNTRIES order),
 * then remaining countries sorted alphabetically by display name.
 */
export function countriesToOptions(countries: readonly Country[]): CountryOption[] {
  // Drop entries missing the iso code or display name — without either,
  // the option has nothing to render. Treat empty strings as absent.
  const all = countries.flatMap((c): CountryOption[] => {
    if (!c.iso31661Alpha2Code || !c.displayName) return [];
    return [
      {
        value: c.iso31661Alpha2Code,
        label: c.displayName,
        flag: `/flags/4x3/${c.iso31661Alpha2Code.toLowerCase()}.svg`,
        phonePrefix: c.phoneNumberPrefix
          ? `+${c.phoneNumberPrefix}`
          : getCountryCallingCode(c.iso31661Alpha2Code),
        phoneFormat: c.phoneNumberNationalFormat,
        subdivisionCodes: [...c.subdivisionIso31662Codes],
      },
    ];
  });

  const popularSet = new Set(POPULAR_COUNTRIES);
  const byCode = new Map(all.map((o) => [o.value, o]));

  const popular: CountryOption[] = [];
  for (const code of POPULAR_COUNTRIES) {
    const opt = byCode.get(code);
    if (opt) popular.push(opt);
  }

  const rest = all
    .filter((o) => !popularSet.has(o.value))
    .sort((a, b) => a.label.localeCompare(b.label));

  return [...popular, ...rest];
}

/**
 * Filter subdivisions for a specific country.
 * Returns a sorted option array.
 */
export function subdivisionsForCountry(
  subdivisions: readonly Subdivision[],
  countryIso2: string,
): SubdivisionOption[] {
  return subdivisions
    .filter((s) => s.countryIso31661Alpha2Code === countryIso2)
    .flatMap((s): SubdivisionOption[] => {
      if (!s.iso31662Code || !s.displayName) return [];
      return [{ value: s.iso31662Code, label: s.displayName }];
    })
    .sort((a, b) => a.label.localeCompare(b.label));
}

/**
 * Build the set of country codes that have subdivisions.
 * Used to gate state/province validation in contact schemas.
 */
export function buildCountriesWithSubdivisions(
  subdivisionsByCountry: Record<string, SubdivisionOption[]>,
): Set<string> {
  return new Set(Object.keys(subdivisionsByCountry));
}
