// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// i18n arm — flat-key-set diff over `contracts/messages/*.json` locale files.
//
// Rules:
//   Removed key     → FINDING (a published TK key is a wire-observable surface;
//                               any consumer that calls t(key) will break)
//   Added key       → PASS (additive)
//   Value change    → PASS (translation copy churns freely; the key is the
//                           wire surface, not the string content)
//   $schema key     → IGNORED (not a runtime TK key)
//   Reorder         → PASS (key-set diff, not positional)

import type { BreakingFinding } from "./breaking-finding.js";

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** Parsed flat i18n locale document (top-level key → string value). */
type LocaleDoc = Record<string, unknown>;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function isObject(v: unknown): v is LocaleDoc {
  return v !== null && typeof v === "object" && !Array.isArray(v);
}

/** Extract the set of runtime TK keys from a locale document. */
function runtimeKeys(doc: LocaleDoc): Set<string> {
  return new Set(Object.keys(doc).filter((k) => k !== "$schema"));
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Diff a single i18n locale file against its baseline.
 *
 * @param before - Parsed baseline locale document, or undefined when the file
 *   did not exist on the baseline (fully additive → no findings).
 * @param after  - Parsed proposed locale document.
 * @param filePath - Source file path for finding messages.
 * @returns Array of breaking findings (empty = no breaks).
 * @throws {Error} When `before` or `after` is not a flat object.
 */
export function diffMessageKeys(
  before: unknown,
  after: unknown,
  filePath: string,
): BreakingFinding[] {
  // New file → fully additive.
  if (before === undefined || before === null) return [];

  if (!isObject(before)) {
    throw new Error(`[i18n-diff] ${filePath}: baseline is not a JSON object`);
  }

  if (!isObject(after)) {
    throw new Error(`[i18n-diff] ${filePath}: proposed is not a JSON object`);
  }

  const beforeKeys = runtimeKeys(before);
  const afterKeys = runtimeKeys(after);

  const findings: BreakingFinding[] = [];

  for (const key of beforeKeys) {
    if (!afterKeys.has(key)) {
      findings.push({
        arm: "i18n",
        severity: "ERROR",
        file: filePath,
        message:
          `✗ BREAKING: ${filePath}\n` +
          `  Translation key '${key}' was removed (present on baseline, absent in proposed).\n` +
          `  Published TK keys are wire-observable — any consumer calling t('${key}') will break.\n` +
          `  TK keys are immutable-once-published, additive-only.\n\n` +
          `  Gate FAILED — TK key removed without force valve.`,
      });
    }
  }

  return findings;
}
