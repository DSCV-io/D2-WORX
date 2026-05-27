// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { tk } from "@d2/result";
import type { D2Result } from "@d2/result";
import { validationFailed, ok } from "@d2/result";
import { falsey } from "@d2/utilities";

import {
  decodeGeohash,
  encodeGeohash,
  isValidGeohash,
  truncateOrPadGeohash,
} from "../encoding/geohash-encoder.js";
import {
  decodePlusCode,
  encodePlusCode,
  isValidPlusCode,
} from "../encoding/pluscode-encoder.js";
import { sha256Hex } from "../internal/sha256-hex.js";

const TK_LATITUDE_RANGE = tk("geo_validation_latitude_range");
const TK_LONGITUDE_RANGE = tk("geo_validation_longitude_range");
const TK_FINITE_REQUIRED = tk("geo_validation_coordinates_finite_required");
const TK_GEOHASH_INVALID = tk("geo_validation_coordinates_geohash_invalid");
const TK_PLUSCODE_INVALID = tk("geo_validation_coordinates_pluscode_invalid");

/**
 * Immutable geographic point with three universal representations
 * (lat/lon decimal degrees, geohash-10, OLC plus-code-12) + optional
 * accuracy metadata. Mirrors .NET `D2.Shared.Location.ValueObjects.Coordinates`.
 *
 * Hash input: `geohash` (canonical 10-char) — shortest of the three
 * representations, no URL-issue characters, lexicographic prefix equals
 * spatial proximity. `hashId` = `"v1." + sha256(geohash)`.
 *
 * `accuracyMeters` is METADATA — NOT included in `hashId`.
 */
export interface Coordinates {
  readonly latitude: number;
  readonly longitude: number;
  readonly geohash: string;
  readonly plusCode: string;
  readonly accuracyMeters?: number;
  readonly hashId: string;
}

function buildFromLatLon(
  latitude: number,
  longitude: number,
  accuracyMeters: number | undefined,
): Coordinates {
  const geohash = encodeGeohash(latitude, longitude, 10);
  const { latitude: cLat, longitude: cLon } = decodeGeohash(geohash);

  // Round to F6 (~10 cm) to mirror .NET `Math.Round(..., 6, MidpointRounding.AwayFromZero)`.
  const centerLat = roundHalfAwayFromZero(cLat, 6);
  const centerLon = roundHalfAwayFromZero(cLon, 6);

  const plusCode = encodePlusCode(centerLat, centerLon, 10);
  const hashId = "v1." + sha256Hex(geohash);

  const result: Coordinates = {
    latitude: centerLat,
    longitude: centerLon,
    geohash,
    plusCode,
    hashId,
    ...(accuracyMeters !== undefined ? { accuracyMeters } : {}),
  };
  return result;
}

function roundHalfAwayFromZero(value: number, digits: number): number {
  const factor = Math.pow(10, digits);
  return value >= 0
    ? Math.floor(value * factor + 0.5) / factor
    : -Math.floor(-value * factor + 0.5) / factor;
}

/**
 * Creates a `Coordinates` from decimal-degree lat/lon values.
 */
export function createCoordinates(
  latitude: number,
  longitude: number,
  accuracyMeters?: number,
): D2Result<Coordinates> {
  if (!Number.isFinite(latitude))
    return validationFailed({ messages: [TK_FINITE_REQUIRED] });
  if (!Number.isFinite(longitude))
    return validationFailed({ messages: [TK_FINITE_REQUIRED] });
  if (latitude < -90.0 || latitude > 90.0)
    return validationFailed({ messages: [TK_LATITUDE_RANGE] });
  if (longitude < -180.0 || longitude > 180.0)
    return validationFailed({ messages: [TK_LONGITUDE_RANGE] });
  if (
    accuracyMeters !== undefined &&
    (!Number.isFinite(accuracyMeters) || accuracyMeters < 0.0)
  ) {
    return validationFailed({ messages: [TK_FINITE_REQUIRED] });
  }

  return ok<Coordinates>(buildFromLatLon(latitude, longitude, accuracyMeters));
}

/**
 * Creates a `Coordinates` from a geohash string. Strings longer than 10
 * are truncated; shorter strings are decoded → re-encoded at the canonical
 * 10-char precision.
 */
export function coordinatesFromGeohash(
  geohash: string | undefined,
  accuracyMeters?: number,
): D2Result<Coordinates> {
  if (falsey(geohash))
    return validationFailed({ messages: [TK_GEOHASH_INVALID] });
  if (!isValidGeohash(geohash!))
    return validationFailed({ messages: [TK_GEOHASH_INVALID] });
  if (
    accuracyMeters !== undefined &&
    (!Number.isFinite(accuracyMeters) || accuracyMeters < 0.0)
  ) {
    return validationFailed({ messages: [TK_FINITE_REQUIRED] });
  }

  const normalized = truncateOrPadGeohash(geohash!, 10);
  const { latitude, longitude } = decodeGeohash(normalized);
  return ok<Coordinates>(buildFromLatLon(latitude, longitude, accuracyMeters));
}

/**
 * Creates a `Coordinates` from an OLC plus-code string. The result is
 * normalized to the canonical geohash-10 cell so cross-factory inputs
 * for the same physical cell produce byte-identical `hashId` values.
 */
export function coordinatesFromPlusCode(
  plusCode: string | undefined,
  accuracyMeters?: number,
): D2Result<Coordinates> {
  if (falsey(plusCode))
    return validationFailed({ messages: [TK_PLUSCODE_INVALID] });
  if (!isValidPlusCode(plusCode!))
    return validationFailed({ messages: [TK_PLUSCODE_INVALID] });
  if (
    accuracyMeters !== undefined &&
    (!Number.isFinite(accuracyMeters) || accuracyMeters < 0.0)
  ) {
    return validationFailed({ messages: [TK_FINITE_REQUIRED] });
  }

  const { latitude, longitude } = decodePlusCode(plusCode!);
  return ok<Coordinates>(buildFromLatLon(latitude, longitude, accuracyMeters));
}
