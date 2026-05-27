// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Value objects.
export type { Coordinates } from "./value-objects/coordinates.js";
export {
  createCoordinates,
  coordinatesFromGeohash,
  coordinatesFromPlusCode,
} from "./value-objects/coordinates.js";
export type { StreetAddress } from "./value-objects/street-address.js";
export {
  createStreetAddress,
  normalizeForHash,
} from "./value-objects/street-address.js";
export type { AdminLocation } from "./value-objects/admin-location.js";
export { createAdminLocation } from "./value-objects/admin-location.js";

// Composer.
export { composeLocationHash } from "./compose-location-hash.js";

// Postal-code validator.
export type { IPostalCodeValidator } from "./postal-code-validator.js";
export { defaultPostalCodeValidator } from "./postal-code-validator.js";

// Encoder utilities (exposed for cross-language parity tests + advanced
// consumers needing direct geohash / plus-code arithmetic without going
// through the full Coordinates pipeline).
export {
  encodeGeohash,
  decodeGeohash,
  truncateOrPadGeohash,
  isValidGeohash,
} from "./encoding/geohash-encoder.js";
export {
  encodePlusCode,
  decodePlusCode,
  isValidPlusCode,
} from "./encoding/pluscode-encoder.js";
