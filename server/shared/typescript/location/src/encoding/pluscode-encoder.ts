// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Internal Open Location Code (OLC / plus-code) encoder / decoder.
 * TypeScript port of `D2.Shared.Location.Encoding.PlusCodeEncoder` —
 * the canonical Google OLC spec algorithm. Alphabet
 * `23456789CFGHJMPQRVWX` (20 chars). 10-significant-digit code →
 * 11-character string: 8 pair digits + `+` + 2 grid digits.
 */

const ALPHABET = "23456789CFGHJMPQRVWX";
const ENCODING_BASE = 20;
const SEPARATOR_POSITION = 8;
const FULL_PAIRS = 4;
const GRID_COLUMNS = 4;
const GRID_ROWS = 5;
const LAT_MAX = 90.0;
const LON_MAX = 180.0;
const SEPARATOR = "+";
const PADDING = "0";

const PAIR_LAT_SIZE = 180.0 / Math.pow(ENCODING_BASE, FULL_PAIRS);
const PAIR_LON_SIZE = 360.0 / Math.pow(ENCODING_BASE, FULL_PAIRS);

const LOOKUP: number[] = new Array(128).fill(-1);
for (let i = 0; i < ALPHABET.length; i++) {
  LOOKUP[ALPHABET.charCodeAt(i)] = i;
}

/**
 * Encodes a coordinate pair as an OLC plus-code of the requested length.
 */
export function encodePlusCode(
  latitude: number,
  longitude: number,
  codeLength = 10,
): string {
  let lat = Math.min(latitude, LAT_MAX - 1e-9);
  let lon = longitude;
  while (lon < -LON_MAX) lon += 360.0;
  while (lon >= LON_MAX) lon -= 360.0;

  lat += LAT_MAX;
  lon += LON_MAX;

  const gridDigits = Math.max(0, codeLength - SEPARATOR_POSITION);
  const out: string[] = [];

  let latRem = lat;
  let lonRem = lon;
  let latStep = 180.0 / ENCODING_BASE;
  let lonStep = 360.0 / ENCODING_BASE;

  for (let i = 0; i < FULL_PAIRS; i++) {
    const lonDigit = Math.floor(lonRem / lonStep);
    lonRem -= lonDigit * lonStep;
    const latDigit = Math.floor(latRem / latStep);
    latRem -= latDigit * latStep;
    out.push(ALPHABET[lonDigit]!);
    out.push(ALPHABET[latDigit]!);
    lonStep /= ENCODING_BASE;
    latStep /= ENCODING_BASE;
  }

  out.push(SEPARATOR);

  let gridLatSize = PAIR_LAT_SIZE / GRID_ROWS;
  let gridLonSize = PAIR_LON_SIZE / GRID_COLUMNS;

  for (let i = 0; i < gridDigits; i++) {
    let row = Math.floor(latRem / gridLatSize);
    let col = Math.floor(lonRem / gridLonSize);
    row = Math.min(row, GRID_ROWS - 1);
    col = Math.min(col, GRID_COLUMNS - 1);
    latRem -= row * gridLatSize;
    lonRem -= col * gridLonSize;
    gridLatSize /= GRID_ROWS;
    gridLonSize /= GRID_COLUMNS;
    out.push(ALPHABET[row * GRID_COLUMNS + col]!);
  }

  return out.join("");
}

/**
 * Decodes an OLC plus-code to bounding-box center + half-span error.
 */
export function decodePlusCode(plusCode: string): {
  latitude: number;
  longitude: number;
  latError: number;
  lonError: number;
} {
  const upper = plusCode.toUpperCase();
  const sepIdx = upper.indexOf(SEPARATOR);

  const prefixRaw = sepIdx >= 0 ? upper.substring(0, sepIdx) : upper;
  // Strip trailing padding zeros from prefix.
  let prefix = prefixRaw;
  while (prefix.length > 0 && prefix.endsWith(PADDING)) {
    prefix = prefix.slice(0, -1);
  }
  const suffix =
    sepIdx >= 0 && sepIdx + 1 < upper.length ? upper.substring(sepIdx + 1) : "";

  const fullPairs = Math.floor(prefix.length / 2);

  let lat = 0.0;
  let lon = 0.0;
  let latStep = 180.0 / ENCODING_BASE;
  let lonStep = 360.0 / ENCODING_BASE;

  for (let i = 0; i < fullPairs; i++) {
    const lonDigit = LOOKUP[prefix.charCodeAt(i * 2)]!;
    const latDigit = LOOKUP[prefix.charCodeAt(i * 2 + 1)]!;
    lon += lonDigit * lonStep;
    lat += latDigit * latStep;
    lonStep /= ENCODING_BASE;
    latStep /= ENCODING_BASE;
  }

  let latError = PAIR_LAT_SIZE / 2.0;
  let lonError = PAIR_LON_SIZE / 2.0;

  let gridLatSize = PAIR_LAT_SIZE / GRID_ROWS;
  let gridLonSize = PAIR_LON_SIZE / GRID_COLUMNS;

  for (let i = 0; i < suffix.length; i++) {
    const digit = LOOKUP[suffix.charCodeAt(i)]!;
    const row = Math.floor(digit / GRID_COLUMNS);
    const col = digit % GRID_COLUMNS;
    lat += row * gridLatSize;
    lon += col * gridLonSize;
    gridLatSize /= GRID_ROWS;
    gridLonSize /= GRID_COLUMNS;
  }

  if (suffix.length > 0) {
    latError = (gridLatSize * GRID_ROWS) / 2.0;
    lonError = (gridLonSize * GRID_COLUMNS) / 2.0;
  }

  lat += latError;
  lon += lonError;
  lat -= LAT_MAX;
  lon -= LON_MAX;

  return { latitude: lat, longitude: lon, latError, lonError };
}

/**
 * Returns true when the input is a syntactically valid OLC plus-code.
 */
export function isValidPlusCode(plusCode: string | undefined): boolean {
  if (plusCode === undefined || plusCode.length === 0) return false;
  if (plusCode.trim().length === 0) return false;

  const upper = plusCode.toUpperCase();
  const sepIdx = upper.indexOf(SEPARATOR);
  if (sepIdx < 0 || upper.indexOf(SEPARATOR, sepIdx + 1) >= 0) return false;
  if (sepIdx < 2 || sepIdx > SEPARATOR_POSITION) return false;

  const prefix = upper.substring(0, sepIdx);
  let seenPad = false;
  for (let i = 0; i < prefix.length; i++) {
    const c = prefix.charAt(i);
    if (c === PADDING) {
      seenPad = true;
      continue;
    }
    if (seenPad) return false;
    const code = prefix.charCodeAt(i);
    if (code >= 128 || LOOKUP[code]! < 0) return false;
  }

  const suffix = upper.substring(sepIdx + 1);
  if (suffix.length === 0) return false;
  for (let i = 0; i < suffix.length; i++) {
    const code = suffix.charCodeAt(i);
    if (code >= 128 || LOOKUP[code]! < 0) return false;
  }

  let sigPrefix = prefix;
  while (sigPrefix.length > 0 && sigPrefix.endsWith(PADDING)) {
    sigPrefix = sigPrefix.slice(0, -1);
  }
  const totalSig = sigPrefix.length + suffix.length;
  return totalSig >= 2;
}
