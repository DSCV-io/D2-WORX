// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Phone number formatting helpers.
 *
 * Storage format (Geo + Auth): digits-only E.164, e.g. "13213214321".
 * Display format: friendly per-region formatting, e.g. "+1 (321) 321-4321".
 */

/** Strip all non-digit characters. Returns the storage format Geo expects. */
export function phoneToDigits(raw: string): string {
  return raw.replace(/\D/g, "");
}

/**
 * Friendly display for stored digits-only value.
 *
 * North America (11 digits starting with 1): +1 (XXX) XXX-XXXX
 * Other lengths: simple "+{digits}" prefix
 */
export function formatPhoneForDisplay(digits: string): string {
  if (!digits) return "";
  // North America: 1XXXXXXXXXX → +1 (XXX) XXX-XXXX
  if (digits.length === 11 && digits.startsWith("1")) {
    return `+1 (${digits.slice(1, 4)}) ${digits.slice(4, 7)}-${digits.slice(7)}`;
  }
  return `+${digits}`;
}

/** Validate phone format — digits-only, 7-15 digits (matches Geo + Auth). */
export function isValidPhoneFormat(digits: string): boolean {
  return /^\d{7,15}$/.test(digits);
}
