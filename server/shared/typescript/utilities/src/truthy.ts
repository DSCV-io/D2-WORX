// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { falsey } from "./falsey.js";

/**
 * Inverse of `falsey` — returns true when the value is non-null, non-empty,
 * and (for strings) contains at least one non-whitespace character.
 */
export function truthy(value: unknown): boolean {
  return !falsey(value);
}
