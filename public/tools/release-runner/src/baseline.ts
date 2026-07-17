// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Baseline ref resolution — pure, side-effect-free helper.
//
// Extracted from cli.ts so tests can import and exercise the real function
// without triggering cli.ts's module-level side effects (argv capture,
// git calls, process.exit).

import { truthy } from "@dcsv-io/d2-utilities";

// ---------------------------------------------------------------------------
// resolveBaseline
// ---------------------------------------------------------------------------

/**
 * Returns the resolved integration baseline branch ref, or `undefined` when
 * neither the `--against` argument nor the `D2_RELEASE_BASELINE` environment
 * variable is set. The caller is responsible for failing loudly on `undefined`.
 *
 * Resolution order: arg > env > undefined.
 * Empty strings are treated as absent.
 */
export function resolveBaseline(
  arg: string | undefined,
  env: string | undefined,
): string | undefined {
  if (truthy(arg)) return arg;
  if (truthy(env)) return env;

  return undefined;
}
