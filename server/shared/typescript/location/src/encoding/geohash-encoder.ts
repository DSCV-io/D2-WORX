// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Internal geohash encoder / decoder using the Niemeyer base-32 alphabet.
 * TypeScript port of `D2.Shared.Location.Encoding.GeohashEncoder` —
 * bit-interleaves longitude (even bits) and latitude (odd bits), then
 * maps 5-bit chunks to base-32. Algorithm: Niemeyer 2008
 * (https://en.wikipedia.org/wiki/Geohash). Alphabet
 * `0123456789bcdefghjkmnpqrstuvwxyz` (excludes `a`, `i`, `l`, `o`).
 */

const ALPHABET = "0123456789bcdefghjkmnpqrstuvwxyz";

// Lookup table char-code → 5-bit value (-1 for invalid).
const LOOKUP: number[] = new Array(128).fill(-1);
for (let i = 0; i < ALPHABET.length; i++) {
  LOOKUP[ALPHABET.charCodeAt(i)] = i;
}

/**
 * Encodes a coordinate pair as a geohash string of the requested precision.
 */
export function encodeGeohash(
  latitude: number,
  longitude: number,
  precision = 10,
): string {
  const chars: string[] = new Array(precision);
  let latMin = -90.0;
  let latMax = 90.0;
  let lonMin = -180.0;
  let lonMax = 180.0;

  let bits = 0;
  let isLon = true;
  let bitIdx = 0;
  let charIdx = 0;

  while (charIdx < precision) {
    let mid: number;
    if (isLon) {
      mid = (lonMin + lonMax) / 2.0;
      if (longitude >= mid) {
        bits = (bits << 1) | 1;
        lonMin = mid;
      } else {
        bits <<= 1;
        lonMax = mid;
      }
    } else {
      mid = (latMin + latMax) / 2.0;
      if (latitude >= mid) {
        bits = (bits << 1) | 1;
        latMin = mid;
      } else {
        bits <<= 1;
        latMax = mid;
      }
    }

    isLon = !isLon;
    bitIdx++;

    if (bitIdx === 5) {
      chars[charIdx++] = ALPHABET[bits]!;
      bits = 0;
      bitIdx = 0;
    }
  }

  return chars.join("");
}

/**
 * Decodes a geohash string to bounding-box center + half-span error.
 */
export function decodeGeohash(geohash: string): {
  latitude: number;
  longitude: number;
  latError: number;
  lonError: number;
} {
  let latMin = -90.0;
  let latMax = 90.0;
  let lonMin = -180.0;
  let lonMax = 180.0;

  let isLon = true;

  for (let i = 0; i < geohash.length; i++) {
    const code = geohash.charCodeAt(i);
    const val = code < 128 ? LOOKUP[code]! : -1;

    for (let bit = 4; bit >= 0; bit--) {
      const bitVal = (val >> bit) & 1;
      let mid: number;

      if (isLon) {
        mid = (lonMin + lonMax) / 2.0;
        if (bitVal === 1) lonMin = mid;
        else lonMax = mid;
      } else {
        mid = (latMin + latMax) / 2.0;
        if (bitVal === 1) latMin = mid;
        else latMax = mid;
      }

      isLon = !isLon;
    }
  }

  return {
    latitude: (latMin + latMax) / 2.0,
    longitude: (lonMin + lonMax) / 2.0,
    latError: (latMax - latMin) / 2.0,
    lonError: (lonMax - lonMin) / 2.0,
  };
}

/**
 * Truncates or pads (via decode + re-encode at target precision) a geohash
 * string to exactly the requested precision.
 */
export function truncateOrPadGeohash(geohash: string, precision = 10): string {
  if (geohash.length === precision) return geohash;
  if (geohash.length > precision) return geohash.slice(0, precision);
  const { latitude, longitude } = decodeGeohash(geohash);
  return encodeGeohash(latitude, longitude, precision);
}

/**
 * Returns true when every character belongs to the geohash base-32 alphabet
 * and the length is between 1 and 12.
 */
export function isValidGeohash(value: string): boolean {
  if (value.length < 1 || value.length > 12) return false;
  for (let i = 0; i < value.length; i++) {
    const code = value.charCodeAt(i);
    if (code >= 128 || LOOKUP[code]! < 0) return false;
  }
  return true;
}
