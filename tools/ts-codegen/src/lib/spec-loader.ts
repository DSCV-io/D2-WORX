// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";

import { diagError, type EmitDiagnostic } from "./diagnostics.js";

/**
 * Result of `loadSpec` — either the parsed payload OR a diagnostic
 * describing why the spec couldn't be loaded. Mirrors .NET `LoadResult`.
 */
export interface SpecLoadResult<T> {
  readonly spec?: T;
  readonly diagnostics: readonly EmitDiagnostic[];
}

/**
 * Load and JSON-parse a spec file. Returns the parsed object (the caller
 * does its own per-spec validation) OR a malformed-spec diagnostic.
 */
export function loadSpec<T>(
  path: string,
  malformedDiagId: string,
): SpecLoadResult<T> {
  let raw: string;
  try {
    raw = readFileSync(path, "utf8");
  } catch (e) {
    return {
      diagnostics: [
        diagError(
          malformedDiagId,
          `failed to read spec: ${(e as Error).message}`,
          path,
        ),
      ],
    };
  }
  try {
    const parsed = JSON.parse(raw) as T;
    return { spec: parsed, diagnostics: [] };
  } catch (e) {
    return {
      diagnostics: [
        diagError(
          malformedDiagId,
          `spec JSON parse failed: ${(e as Error).message}`,
          path,
        ),
      ],
    };
  }
}
