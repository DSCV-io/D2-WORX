// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { falsey } from "@d2/utilities";

/**
 * Naming-convention-driven classification of geo spec fields into one of four
 * categories — primitive, foreign-key code, M:M list of codes, or
 * (effectively-)primitive when no rule applies. Mirrors the .NET
 * `D2.Shared.Geo.SourceGen.FkDetector.Classify` rule set byte-for-byte so
 * cross-runtime emitters classify identical field names identically.
 *
 * Rules:
 * 1. Explicit `fkTo` annotation (when supplied) overrides the naming
 *    convention and produces `ForeignKeySingle`.
 * 2. Names ending in a list suffix (`Codes` / `Tags` / `Identifiers` plural
 *    forms) → `ForeignKeyList`.
 * 3. Names ending in a single suffix (`Iso31661Alpha2Code`, `ShortCode`,
 *    `IetfBcp47Tag`, `IanaIdentifier`, …) → `ForeignKeySingle`.
 * 4. Otherwise → `Primitive`.
 *
 * List suffixes are checked BEFORE single suffixes — every list suffix is a
 * strict superset of the single form (e.g. `Codes` contains `Code`).
 */
export const FieldClassification = {
  Primitive: "Primitive",
  ForeignKeySingle: "ForeignKeySingle",
  ForeignKeyList: "ForeignKeyList",
} as const;

export type FieldClassification =
  (typeof FieldClassification)[keyof typeof FieldClassification];

const _SINGLE_SUFFIXES: readonly string[] = [
  "Iso31661Alpha2Code",
  "Iso31661Alpha3Code",
  "Iso31661NumericCode",
  "Iso4217AlphaCode",
  "Iso4217NumericCode",
  "Iso6391Code",
  "IetfBcp47Tag",
  "IanaIdentifier",
  "ShortCode",
];

const _LIST_SUFFIXES: readonly string[] = [
  "Iso31661Alpha2Codes",
  "Iso31662Codes",
  "Iso4217AlphaCodes",
  "Iso6391Codes",
  "IetfBcp47Tags",
  "IanaIdentifiers",
  "ShortCodes",
];

/**
 * Classify a spec field name into one of `Primitive`, `ForeignKeySingle`, or
 * `ForeignKeyList`. The spec input naming convention is camelCase, but the
 * suffix-match is performed case-insensitively against the PascalCase suffix
 * tables so emitters can pass either form.
 */
export function classify(
  fieldName: string | undefined,
  fkToAnnotation?: string,
): FieldClassification {
  if (!falsey(fkToAnnotation)) return FieldClassification.ForeignKeySingle;
  if (falsey(fieldName)) return FieldClassification.Primitive;

  const lowered = fieldName!.toLowerCase();
  // List BEFORE single — see header rationale.
  for (const suffix of _LIST_SUFFIXES) {
    if (lowered.endsWith(suffix.toLowerCase()))
      return FieldClassification.ForeignKeyList;
  }
  for (const suffix of _SINGLE_SUFFIXES) {
    if (lowered.endsWith(suffix.toLowerCase()))
      return FieldClassification.ForeignKeySingle;
  }
  return FieldClassification.Primitive;
}
