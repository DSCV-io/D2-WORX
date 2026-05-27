// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { ALL_SCOPES, Scopes } from "@d2/auth-abstractions";

import { canonicalize, loadFixture } from "../src/index.js";

interface PathToWireMap {
  readonly [pathName: string]: string;
}

/**
 * Recursively walk a nested tree of `{ segment: string | nested }` and
 * collect every string leaf. Returns the wire values (NOT the path
 * keys) — the .NET emitter keys its fixture by PascalCase nested-class
 * path (`Anon.Public.Health`) while the TS tree keys with lowercase
 * segments (`anon.public.health`); only the wire values are language-
 * neutral, so the leaves-only comparison is the canonical parity check.
 */
function collectLeaves(node: unknown): string[] {
  if (typeof node === "string") return [node];
  if (node !== null && typeof node === "object") {
    const out: string[] = [];
    for (const v of Object.values(node as Record<string, unknown>))
      out.push(...collectLeaves(v));

    return out;
  }

  return [];
}

describe("auth-scopes parity (.NET catalog ↔ TS catalog)", () => {
  describe("Scopes ↔ auth-scopes/scopes.json", () => {
    const fixture = loadFixture<PathToWireMap>("auth-scopes", "scopes");
    const fixtureMap = fixture.data;
    const fixturePaths = Object.keys(fixtureMap).sort();
    const fixtureWireValues = fixturePaths.map((k) => fixtureMap[k]!).sort();

    const tsTreeLeaves = collectLeaves(Scopes).sort();
    const tsAllScopesSorted = [...ALL_SCOPES].sort();

    it("Scopes tree flattens to identical wire-value set", () => {
      expect(tsTreeLeaves).toEqual(fixtureWireValues);
    });

    it("ALL_SCOPES matches fixture wire-value set", () => {
      expect(tsAllScopesSorted).toEqual(fixtureWireValues);
    });

    it("ALL_SCOPES equals flattened Scopes tree (internal consistency)", () => {
      expect(tsAllScopesSorted).toEqual(tsTreeLeaves);
    });

    // Per-VALUE pin: every fixture entry asserted individually so a
    // failure message names the specific drifted scope path.
    for (const path of fixturePaths) {
      const wireValue = fixtureMap[path]!;
      it(`scope ${path} (${wireValue}) is present in ALL_SCOPES`, () => {
        expect(ALL_SCOPES).toContain(wireValue);
      });

      it(`scope ${path} (${wireValue}) is present in flattened Scopes tree`, () => {
        expect(tsTreeLeaves).toContain(wireValue);
      });
    }

    it("canonical wire-value lists are byte-equal", () => {
      expect(canonicalize(tsAllScopesSorted)).toEqual(
        canonicalize(fixtureWireValues),
      );
    });
  });
});
