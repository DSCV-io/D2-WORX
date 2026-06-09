// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  ALL_ERROR_CATEGORIES,
  ErrorCategoryWire,
  type ErrorCategory,
} from "@d2/error-category";
import { canonicalize, loadFixture } from "../src/index.js";

// ---------------------------------------------------------------------------
// Cross-runtime parity guard — the relocated ErrorCategory enum.
//
// The .NET side emits `contract-tests/fixtures/error-category/mapping.json`
// (via `ErrorCategoryFixtureEmitter`) — a PascalCase member name → snake_case
// wire string map reflected off the .NET `ErrorCategory` enum. The TS side
// exposes the same map via `ErrorCategoryWire` from `@d2/error-category`.
//
// PARITY AXES:
//   member set      : fixture keys (PascalCase members) === ErrorCategoryWire keys
//   wire strings    : fixture[member] === ErrorCategoryWire[member]
//   union closure   : every wire string is one of the nine ALL_ERROR_CATEGORIES
// ---------------------------------------------------------------------------

interface CategoryMap {
  readonly [member: string]: string;
}

describe("error-category parity (.NET ErrorCategory ↔ TS ErrorCategory)", () => {
  const fixture = loadFixture<CategoryMap>("error-category", "mapping");
  const fixtureMap = fixture.data;
  const fixtureKeys = Object.keys(fixtureMap).sort();
  const tsMap = ErrorCategoryWire as Readonly<Record<string, string>>;
  const tsKeys = Object.keys(tsMap).sort();

  it("has identical PascalCase member membership", () => {
    expect(tsKeys).toEqual(fixtureKeys);
  });

  it("has exactly nine members", () => {
    expect(fixtureKeys).toHaveLength(9);
    expect(tsKeys).toHaveLength(9);
  });

  for (const member of fixtureKeys) {
    it(`member ${member} has identical wire string`, () => {
      expect(tsMap[member]).toBe(fixtureMap[member]);
    });
  }

  it("canonical maps are byte-equal", () => {
    const tsAsMap: Record<string, string> = {};
    for (const k of tsKeys) tsAsMap[k] = tsMap[k]!;
    expect(canonicalize(tsAsMap)).toEqual(canonicalize(fixtureMap));
  });

  it("every fixture wire string is a member of the closed union", () => {
    const wireSet = new Set<string>(ALL_ERROR_CATEGORIES);
    const bad = fixtureKeys
      .filter((m) => !wireSet.has(fixtureMap[m]!))
      .map((m) => `${m}: "${fixtureMap[m]}"`);
    expect(bad).toEqual([]);
  });

  it("ALL_ERROR_CATEGORIES equals the fixture wire-string set", () => {
    const fixtureWires = fixtureKeys.map((m) => fixtureMap[m]!).sort();
    const tsWires: ErrorCategory[] = [...ALL_ERROR_CATEGORIES].sort();
    expect(tsWires).toEqual(fixtureWires);
  });
});
