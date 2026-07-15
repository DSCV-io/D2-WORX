// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { RawTerritory } from "../fetchers/libphonenumber-metadata.js";

export interface CountryPhoneMetadata {
  /**
   * ITU-T E.164 country code (without leading +). Same as datasets/country-codes
   * Dial field; libphonenumber confirms.
   */
  phoneNumberPrefix: string;
  /** Best display format template using $1/$2/$3 placeholders, e.g. "($1) $2-$3" for US. */
  phoneNumberNationalFormat: string | null;
  /**
   * Minimum digits allowed for a national number (across all type sections —
   * fixedLine/mobile/etc.).
   */
  phoneNumberMinDigits: number | null;
  /** Maximum digits allowed. */
  phoneNumberMaxDigits: number | null;
  /**
   * International dialing prefix (e.g. "011" for US, "00" for most of EU).
   * Useful for parsing inbound user input.
   */
  internationalPrefix: string | null;
  /** National prefix typically stripped before dialing (e.g. "1" for US, "0" for UK). */
  nationalPrefix: string | null;
}

/**
 * Extracts CountryPhoneMetadata from a parsed libphonenumber territory.
 *
 * Format selection: the LAST numberFormat in `<availableFormats>` is empirically the
 * most-general one (specific patterns appear first per leadingDigits restriction; the
 * general pattern lands last). If no formats exist (rare territories), returns null.
 *
 * Length range parsing handles:
 *   "10" → [10]
 *   "9,10" → [9, 10]
 *   "[8-17]" → [8..17]
 *   "[7-9],10" → [7, 8, 9, 10]
 *   "[4-8]" (localOnly) — IGNORED; we only use national lengths
 */
export function transformPhoneMetadata(
  territory: RawTerritory,
): CountryPhoneMetadata | null {
  const { attributes, formats, perTypePossibleLengths } = territory;
  if (!attributes.countryCode) return null;

  const allLengths = new Set<number>();
  for (const lens of perTypePossibleLengths) {
    if (!lens.national) continue;
    for (const len of parseNationalLengths(lens.national)) {
      allLengths.add(len);
    }
  }
  const sortedLengths = [...allLengths].sort((a, b) => a - b);
  const minDigits = sortedLengths[0] ?? null;
  const maxDigits = sortedLengths[sortedLengths.length - 1] ?? null;

  // Pick the LAST format as the canonical display format; libphonenumber orders
  // specific (leadingDigits-restricted) patterns first and the general pattern last.
  const lastFormat = formats[formats.length - 1];
  const phoneNumberNationalFormat = lastFormat?.format ?? null;

  return {
    phoneNumberPrefix: attributes.countryCode,
    phoneNumberNationalFormat,
    phoneNumberMinDigits: minDigits,
    phoneNumberMaxDigits: maxDigits,
    internationalPrefix: attributes.internationalPrefix ?? null,
    nationalPrefix: attributes.nationalPrefix ?? null,
  };
}

/**
 * Parses libphonenumber's `national` length expression into a list of integers.
 * Examples: "10" → [10]; "9,10" → [9,10]; "[8-17]" → [8..17]; "[7-9],10" → [7,8,9,10].
 */
export function parseNationalLengths(expression: string): number[] {
  const result: number[] = [];
  const trimmed = expression.replace(/\s/g, "");
  if (!trimmed) return result;

  const segments = trimmed.split(",");
  for (const segment of segments) {
    const rangeMatch = segment.match(/^\[(\d+)-(\d+)\]$/);
    if (rangeMatch && rangeMatch[1] && rangeMatch[2]) {
      const lo = Number.parseInt(rangeMatch[1], 10);
      const hi = Number.parseInt(rangeMatch[2], 10);
      if (Number.isFinite(lo) && Number.isFinite(hi) && lo <= hi) {
        for (let n = lo; n <= hi; n++) result.push(n);
      }
      continue;
    }
    const singleNumber = Number.parseInt(segment, 10);
    if (Number.isFinite(singleNumber)) result.push(singleNumber);
  }
  return result;
}
