// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { falsey } from "./falsey.js";

/**
 * Reads an indexed env-var array of the form
 * `PREFIX__0=a, PREFIX__1=b, ...` and returns the values in index order.
 * Matches .NET `IConfiguration` array binding semantics so .NET and Node
 * services can read identically-shaped configuration. Stops at the first
 * gap (first missing index), so sparse arrays collapse to the dense prefix.
 */
export function parseEnvArray(
  prefix: string,
  env: Readonly<Record<string, string | undefined>>,
): string[] {
  if (falsey(prefix))
    throw new RangeError("parseEnvArray: prefix must be non-empty");
  const out: string[] = [];
  for (let i = 0; ; i++) {
    const key = `${prefix}__${i}`;
    const v = env[key];
    if (v === undefined) break;
    out.push(v);
  }
  return out;
}
