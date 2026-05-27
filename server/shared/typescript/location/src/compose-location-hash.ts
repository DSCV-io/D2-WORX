// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { sha256Hex } from "./internal/sha256-hex.js";
import type { AdminLocation } from "./value-objects/admin-location.js";
import type { Coordinates } from "./value-objects/coordinates.js";
import type { StreetAddress } from "./value-objects/street-address.js";

/**
 * Composes a single hash identifier from up to three location
 * components. Returns `undefined` when ALL three inputs are undefined /
 * null (location is absent — not an error). Otherwise returns
 * `"v1." + sha256(c.hashId | s.hashId | a.hashId)`; missing slots
 * contribute `""` (positional, never collapsed). Inner component
 * `"v1."` prefixes ARE included in the outer hash input.
 *
 * Returns `string | undefined` rather than `D2Result<string>` —
 * documented §17 carve-out: the operation cannot fail (inputs are
 * already-validated value objects or null/undefined), and the all-null
 * case is a legitimate non-error state.
 */
export function composeLocationHash(
  coordinates?: Coordinates | undefined,
  streetAddress?: StreetAddress | undefined,
  adminLocation?: AdminLocation | undefined,
): string | undefined {
  if (
    coordinates === undefined &&
    streetAddress === undefined &&
    adminLocation === undefined
  ) {
    return undefined;
  }

  const input =
    (coordinates?.hashId ?? "") +
    "|" +
    (streetAddress?.hashId ?? "") +
    "|" +
    (adminLocation?.hashId ?? "");

  return "v1." + sha256Hex(input);
}
