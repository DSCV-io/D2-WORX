// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { falsey } from "@d2/utilities";

import { diagError, DiagnosticIds } from "../lib/diagnostics.js";
import type { EmitDiagnostic } from "../lib/diagnostics.js";

/**
 * Enforces the subdivision-vocabulary discipline — the ISO 3166-2 concept is
 * consistently referred to as `subdivision` across spec field identifiers;
 * the words `region`, `state`, and `province` are forbidden at identifier
 * position. The guard inspects field NAMES (identifiers) — field VALUES are
 * exempt (display strings on `Subdivision.type` like `"State"` / `"Province"`
 * / `"Parish"` remain legal user-facing labels).
 *
 * Mirrors .NET `D2.Shared.Geo.SourceGen.VocabularyGuard.Validate` shape +
 * tokens byte-for-byte.
 */

/** The forbidden identifier tokens (case-insensitive). */
export const FORBIDDEN_IDENTIFIERS: readonly string[] = [
  "region",
  "state",
  "province",
];

/**
 * Walks `fieldNames` and surfaces a diagnostic for any identifier whose
 * lowercased form contains a forbidden token. Empty / whitespace-only field
 * names are skipped (degenerate spec metadata produces no false positives).
 */
export function validateVocabulary(
  specName: string,
  fieldNames: readonly (string | undefined)[],
): readonly EmitDiagnostic[] {
  const diagnostics: EmitDiagnostic[] = [];
  for (const fieldName of fieldNames) {
    if (falsey(fieldName)) continue;
    // ! is safe because falsey() returned false.
    const lowered = fieldName!.toLowerCase();
    for (const forbidden of FORBIDDEN_IDENTIFIERS) {
      if (lowered.includes(forbidden)) {
        diagnostics.push(
          diagError(
            DiagnosticIds.GEO_VOCABULARY_VIOLATION,
            `${specName}: field '${fieldName}' uses forbidden token '${forbidden}' ` +
              `— the ISO 3166-2 concept is consistently 'subdivision'`,
          ),
        );
        break;
      }
    }
  }
  return diagnostics;
}
