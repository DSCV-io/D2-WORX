// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  LOCALE_CODE_SET,
  SUBDIVISION_CODE_SET,
  TIMEZONE_CODE_SET,
} from "@d2/geo-abstractions";

import { canonicalize, loadFixture } from "../src/index.js";

interface WrapperStructsFixture {
  readonly SubdivisionCode: readonly string[];
  readonly LocaleCode: readonly string[];
  readonly TimezoneCode: readonly string[];
}

/**
 * Map fixture-key (the .NET wrapper-struct type name) → the corresponding
 * TS-side closed-set export. The wrapper-struct catalogs are the cross-
 * language wire contract: a string in one runtime's set MUST be in the
 * other's, else a value rejected by one runtime will be accepted by the
 * other (silent drift).
 */
const tsWrapperCatalog: Readonly<Record<string, ReadonlySet<string>>> = {
  SubdivisionCode: SUBDIVISION_CODE_SET,
  LocaleCode: LOCALE_CODE_SET,
  TimezoneCode: TIMEZONE_CODE_SET,
};

describe("geo wrapper-structs parity (.NET catalog ↔ TS catalog)", () => {
  const fixture = loadFixture<WrapperStructsFixture>("geo", "wrapper-structs");
  const fixtureData = fixture.data;

  for (const wrapperName of Object.keys(tsWrapperCatalog).sort()) {
    describe(`wrapper struct ${wrapperName}`, () => {
      const fixtureArray = (
        fixtureData as unknown as Readonly<Record<string, readonly string[]>>
      )[wrapperName]!;
      const tsSet = tsWrapperCatalog[wrapperName]!;

      // Canonicalize for sorted-array comparison — the fixture is already
      // ordinal-sorted by the .NET emitter; the TS-side ReadonlySet has
      // no defined iteration order so we sort before comparing.
      const fixtureSorted = [...fixtureArray].sort();
      const tsSorted = [...tsSet].sort();

      it("has identical cardinality", () => {
        expect(tsSorted.length).toBe(fixtureSorted.length);
      });

      it("has identical membership (sorted-array equality)", () => {
        expect(tsSorted).toEqual(fixtureSorted);
      });

      it("canonical sorted arrays are byte-equal", () => {
        expect(canonicalize(tsSorted)).toEqual(canonicalize(fixtureSorted));
      });

      // Per-VALUE pins: spot-check the boundary entries + a small sample
      // through the middle so a drifted entry names itself. The full set
      // is pinned by the membership assertion above; these are additional
      // failure-message-rich anchors. Picks first 5, last 5, plus every
      // ceil(len/10)th entry.
      const sampleIndices = new Set<number>();
      const len = fixtureSorted.length;
      for (let i = 0; i < Math.min(5, len); i++) sampleIndices.add(i);
      for (let i = Math.max(0, len - 5); i < len; i++) sampleIndices.add(i);
      const stride = Math.max(1, Math.ceil(len / 10));
      for (let i = 0; i < len; i += stride) sampleIndices.add(i);

      for (const idx of [...sampleIndices].sort((a, b) => a - b)) {
        const code = fixtureSorted[idx]!;
        it(`code ${code} (index ${idx}) is present in TS-side set`, () => {
          expect(tsSet.has(code)).toBe(true);
        });
      }
    });
  }
});
