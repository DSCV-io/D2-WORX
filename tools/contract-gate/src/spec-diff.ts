// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Spec/i18n arm — JSON-diff engine for array-of-objects catalogs.
//
// Algorithm (keyed-set diff):
//   beforeById = index before[arrayProp] by element[idField]
//   afterById  = index after[arrayProp]  by element[idField]
//   for each id in beforeById:
//     absent in after         → FINDING (removed entry)  // deprecated-or-not, removal fails
//     type/value changed      → FINDING (retyped value)
//   added ids (not in before) → PASS (additive)
//   reorder                   → PASS (identity is idField, not array index)
//
// Deprecation rule:
//   Adding "deprecated": true to a surviving entry → PASS (additive marker).
//   Deleting a deprecated entry → FINDING (same rule as non-deprecated).
//
// Telemetry nested catalogs (meters → instruments → tags → values) are handled
// by the NestedCatalogIdentity descriptor and recursive diffing.

import type {
  CatalogIdentity,
  FlatCatalogIdentity,
  MultiCatalogIdentity,
  NestedCatalogIdentity,
} from "./catalog-identity.js";
import type { BreakingFinding } from "./breaking-finding.js";

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** A simple bag of JSON-compatible values. */
type JsonObject = Record<string, unknown>;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function isObject(v: unknown): v is JsonObject {
  return v !== null && typeof v === "object" && !Array.isArray(v);
}

function isStringArray(v: unknown): v is string[] {
  return Array.isArray(v) && v.every((x) => typeof x === "string");
}

/**
 * Index an array of objects by a given identity field.
 * Throws when the array or its items are malformed, or when duplicate IDs
 * are detected (fail-loud — a catalog with duplicate IDs is invalid).
 */
function indexById(
  items: unknown[],
  idField: string,
  context: string,
): Map<string, JsonObject> {
  const map = new Map<string, JsonObject>();

  for (const item of items) {
    if (!isObject(item)) {
      throw new Error(
        `[spec-diff] ${context}: catalog entry is not an object: ${JSON.stringify(item)}`,
      );
    }

    const id = item[idField];

    if (typeof id !== "string" || id.length === 0) {
      throw new Error(
        `[spec-diff] ${context}: catalog entry missing string identity field '${idField}': ${JSON.stringify(item)}`,
      );
    }

    if (map.has(id)) {
      throw new Error(
        `[spec-diff] ${context}: duplicate identity '${id}' — catalog IDs must be unique`,
      );
    }

    map.set(id, item);
  }

  return map;
}

/**
 * Compare two JSON values for type-or-value change.
 * An existing field that changes type OR value (on a primitive) is a break.
 * A field ADDED to an existing entry is NOT a break (additive).
 */
function fieldHasChanged(before: unknown, after: unknown): boolean {
  const typeB = typeof before;
  const typeA = typeof after;

  if (typeB !== typeA) return true;

  if (typeB === "string" || typeB === "number" || typeB === "boolean") {
    return before !== after;
  }

  // For arrays and objects: a type-change is already caught above;
  // deeper structural changes within a nested entry that aren't covered
  // by idField-keyed recursion are flagged as a break if the serialized
  // form changes. This is conservative (over-flagging is safer than
  // under-flagging for a gate — a force valve clears it).
  return JSON.stringify(before) !== JSON.stringify(after);
}

// ---------------------------------------------------------------------------
// Flat catalog diff
// ---------------------------------------------------------------------------

/**
 * Diff a flat array-of-objects catalog against its baseline.
 *
 * @param before - Parsed baseline catalog document.
 * @param after  - Parsed proposed catalog document.
 * @param identity - The identity descriptor for this catalog.
 * @param filePath - Source file path for finding messages.
 * @returns Array of breaking findings (empty = no breaks).
 */
export function diffFlatCatalog(
  before: unknown,
  after: unknown,
  identity: FlatCatalogIdentity,
  filePath: string,
): BreakingFinding[] {
  if (!isObject(before)) {
    throw new Error(`[spec-diff] ${filePath}: baseline is not a JSON object`);
  }

  if (!isObject(after)) {
    throw new Error(`[spec-diff] ${filePath}: proposed is not a JSON object`);
  }

  const { arrayProp, idField } = identity;

  const beforeArr = before[arrayProp];
  const afterArr = after[arrayProp];

  if (!Array.isArray(beforeArr)) {
    // Catalog not present in the before doc — treat as fully additive.
    return [];
  }

  if (!Array.isArray(afterArr)) {
    throw new Error(
      `[spec-diff] ${filePath}: expected array at '${arrayProp}' in proposed document but got ${typeof afterArr}`,
    );
  }

  const context = `${filePath}[${arrayProp}]`;
  const beforeById = indexById(beforeArr, idField, `${context}(before)`);
  const afterById = indexById(afterArr, idField, `${context}(after)`);

  const findings: BreakingFinding[] = [];

  for (const [id, beforeEntry] of beforeById) {
    const afterEntry = afterById.get(id);

    if (afterEntry === undefined) {
      // Entry was removed — breaking regardless of deprecated status.
      const wasDeprecated = beforeEntry["deprecated"] === true;
      const deprecatedNote = wasDeprecated
        ? "\n  Even deprecated entries must not be deleted — deprecate-not-delete: keep the entry, mark deprecated: true.\n  To remove it anyway (forced break): pull the force valve (WIRE-BREAKING: / BREAKING CHANGE: commit footer) + bump MAJOR + add CHANGELOG entry."
        : '\n  Spec entries are immutable-once-published, additive-only, deprecate-not-delete:\n    • To retire it safely: keep the entry and set "deprecated": true; the generated [Obsolete]/@deprecated push callers off it.\n    • To delete it anyway (forced break, atomic-deploy only): pull the force valve —\n      add a commit footer  WIRE-BREAKING: <reason>  OR  BREAKING CHANGE: <reason>\n      AND bump the package semver MAJOR + add the CHANGELOG.md breaking entry.';

      findings.push({
        arm: "spec",
        severity: "ERROR",
        file: filePath,
        message:
          `✗ BREAKING: ${filePath}\n` +
          `  Entry '${id}' was removed (present on baseline, absent in proposed).${deprecatedNote}\n\n` +
          `  Gate FAILED — entry removed without force valve.`,
      });

      continue;
    }

    // Entry present in both — check for type/value changes on existing fields.
    for (const [key, beforeVal] of Object.entries(beforeEntry)) {
      if (key === "deprecated") continue; // deprecated flag flipping true is always pass

      const afterVal = afterEntry[key];

      if (afterVal === undefined) {
        // A field present in baseline was removed from the proposed entry.
        findings.push({
          arm: "spec",
          severity: "ERROR",
          file: filePath,
          message:
            `✗ BREAKING: ${filePath}\n` +
            `  Entry '${id}': field '${key}' was removed from the entry (field-level break).\n` +
            `  Existing entry fields are immutable once published.\n\n` +
            `  Gate FAILED — field removed without force valve.`,
        });

        continue;
      }

      if (fieldHasChanged(beforeVal, afterVal)) {
        findings.push({
          arm: "spec",
          severity: "ERROR",
          file: filePath,
          message:
            `✗ BREAKING: ${filePath}\n` +
            `  Entry '${id}': field '${key}' changed value (${JSON.stringify(beforeVal)} → ${JSON.stringify(afterVal)}).\n` +
            `  Published entry fields are immutable — changing a value is a breaking change.\n\n` +
            `  Gate FAILED — field value changed without force valve.`,
        });
      }
    }
  }

  return findings;
}

// ---------------------------------------------------------------------------
// Nested catalog diff (telemetry: meters → instruments → tags → values)
// ---------------------------------------------------------------------------

/**
 * Diff a nested catalog (e.g. the telemetry catalog: meters → instruments →
 * tags → values). Recursively applies the keyed-set diff at each nesting level.
 */
export function diffNestedCatalog(
  before: unknown,
  after: unknown,
  identity: NestedCatalogIdentity,
  filePath: string,
): BreakingFinding[] {
  if (!isObject(before)) {
    throw new Error(`[spec-diff] ${filePath}: baseline is not a JSON object`);
  }

  if (!isObject(after)) {
    throw new Error(`[spec-diff] ${filePath}: proposed is not a JSON object`);
  }

  const findings: BreakingFinding[] = [];
  const { arrayProp, idField, nested } = identity;

  const beforeArr = before[arrayProp];
  const afterArr = after[arrayProp];

  if (!Array.isArray(beforeArr)) return [];
  if (!Array.isArray(afterArr)) {
    throw new Error(
      `[spec-diff] ${filePath}: expected array at '${arrayProp}' in proposed`,
    );
  }

  const context = `${filePath}[${arrayProp}]`;
  const beforeById = indexById(beforeArr, idField, `${context}(before)`);
  const afterById = indexById(afterArr, idField, `${context}(after)`);

  for (const [id, beforeEntry] of beforeById) {
    const afterEntry = afterById.get(id);

    if (afterEntry === undefined) {
      findings.push({
        arm: "spec",
        severity: "ERROR",
        file: filePath,
        message:
          `✗ BREAKING: ${filePath}\n` +
          `  Top-level entry '${id}' (in '${arrayProp}') was removed.\n` +
          `  Gate FAILED — entry removed without force valve.`,
      });

      continue;
    }

    // Recurse into the nested level.
    const nestedBeforeArr = beforeEntry[nested.arrayProp];
    const nestedAfterArr = afterEntry[nested.arrayProp];

    if (!Array.isArray(nestedBeforeArr)) continue;
    if (!Array.isArray(nestedAfterArr)) {
      throw new Error(
        `[spec-diff] ${filePath}: expected nested array '${nested.arrayProp}' in '${id}' proposed entry`,
      );
    }

    const nestedContext = `${context}[${id}][${nested.arrayProp}]`;
    const nestedBefore = indexById(
      nestedBeforeArr,
      nested.idField,
      `${nestedContext}(before)`,
    );
    const nestedAfter = indexById(
      nestedAfterArr,
      nested.idField,
      `${nestedContext}(after)`,
    );

    for (const [nestedId, nestedBeforeEntry] of nestedBefore) {
      const nestedAfterEntry = nestedAfter.get(nestedId);

      if (nestedAfterEntry === undefined) {
        findings.push({
          arm: "spec",
          severity: "ERROR",
          file: filePath,
          message:
            `✗ BREAKING: ${filePath}\n` +
            `  Entry '${id}' → '${nestedId}' (in '${nested.arrayProp}') was removed.\n` +
            `  Gate FAILED — nested entry removed without force valve.`,
        });

        continue;
      }

      // Check for value changes on basic fields.
      for (const [key, beforeVal] of Object.entries(nestedBeforeEntry)) {
        if (key === nested.arrayProp) continue; // handled by deeper recursion
        if (key === nested.nested?.arrayProp) continue;

        const afterVal = nestedAfterEntry[key];

        if (afterVal !== undefined && fieldHasChanged(beforeVal, afterVal)) {
          findings.push({
            arm: "spec",
            severity: "ERROR",
            file: filePath,
            message:
              `✗ BREAKING: ${filePath}\n` +
              `  Entry '${id}' → '${nestedId}': field '${key}' changed (${JSON.stringify(beforeVal)} → ${JSON.stringify(afterVal)}).\n` +
              `  Gate FAILED — field value changed without force valve.`,
          });
        }
      }

      // Recurse into tags level if present.
      const deepNested = nested.nested;

      if (deepNested === undefined) continue;

      const tagsBeforeArr = nestedBeforeEntry[deepNested.arrayProp];
      const tagsAfterArr = nestedAfterEntry[deepNested.arrayProp];

      if (!Array.isArray(tagsBeforeArr)) continue;
      if (!Array.isArray(tagsAfterArr)) {
        throw new Error(
          `[spec-diff] ${filePath}: expected tags array '${deepNested.arrayProp}' in '${nestedId}'`,
        );
      }

      const tagsBefore = indexById(
        tagsBeforeArr,
        deepNested.idField,
        `tags(before)`,
      );
      const tagsAfter = indexById(
        tagsAfterArr,
        deepNested.idField,
        `tags(after)`,
      );

      for (const [tagId, tagBeforeEntry] of tagsBefore) {
        const tagAfterEntry = tagsAfter.get(tagId);

        if (tagAfterEntry === undefined) {
          findings.push({
            arm: "spec",
            severity: "ERROR",
            file: filePath,
            message:
              `✗ BREAKING: ${filePath}\n` +
              `  Entry '${id}' → '${nestedId}' → tag '${tagId}' was removed.\n` +
              `  Gate FAILED — tag removed without force valve.`,
          });

          continue;
        }

        // Check values array (flat string array) if present.
        const valProp = deepNested.valuesArrayProp;

        if (valProp !== undefined) {
          const beforeVals = tagBeforeEntry[valProp];
          const afterVals = tagAfterEntry[valProp];

          if (isStringArray(beforeVals) && isStringArray(afterVals)) {
            const afterValSet = new Set(afterVals);

            for (const val of beforeVals) {
              if (!afterValSet.has(val)) {
                findings.push({
                  arm: "spec",
                  severity: "ERROR",
                  file: filePath,
                  message:
                    `✗ BREAKING: ${filePath}\n` +
                    `  Entry '${id}' → '${nestedId}' → tag '${tagId}': value '${val}' was removed from '${valProp}'.\n` +
                    `  Gate FAILED — tag value removed without force valve.`,
                });
              }
            }
          }
        }
      }
    }
  }

  return findings;
}

// ---------------------------------------------------------------------------
// Unified catalog diff entry point
// ---------------------------------------------------------------------------

/**
 * Diff each part of a multi-catalog document (a spec file with multiple
 * independently-gated sibling arrays at the document root).
 */
function diffMultiCatalog(
  before: unknown,
  after: unknown,
  identity: MultiCatalogIdentity,
  filePath: string,
): BreakingFinding[] {
  const findings: BreakingFinding[] = [];

  for (const part of identity.parts) {
    if (part.kind === "flat") {
      findings.push(...diffFlatCatalog(before, after, part, filePath));
    } else {
      findings.push(...diffNestedCatalog(before, after, part, filePath));
    }
  }

  return findings;
}

/**
 * Diff a spec catalog (flat, nested, or multi) against its baseline.
 *
 * @param before - Parsed baseline catalog document (or undefined when the
 *   catalog did not exist on the baseline — treated as fully additive).
 * @param after  - Parsed proposed catalog document.
 * @param identity - The catalog identity descriptor.
 * @param filePath - Source file path for finding messages.
 * @returns Array of breaking findings.
 */
export function diffCatalog(
  before: unknown,
  after: unknown,
  identity: CatalogIdentity,
  filePath: string,
): BreakingFinding[] {
  if (identity.kind === "exempt") {
    // Exempt catalog — no findings, no diffing.
    return [];
  }

  // Before is undefined/null → file is new at HEAD → fully additive → pass.
  if (before === undefined || before === null) return [];

  if (identity.kind === "flat") {
    return diffFlatCatalog(before, after, identity, filePath);
  }

  if (identity.kind === "multi") {
    return diffMultiCatalog(before, after, identity, filePath);
  }

  return diffNestedCatalog(before, after, identity, filePath);
}
