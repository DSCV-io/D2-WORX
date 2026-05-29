// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import type { CountryCode } from "@d2/geo-abstractions";
import { describe, expect, it } from "vitest";

import { CountryLookup } from "../../src/countries.js";
import {
  tryResolveCountryByName,
  tryResolveSubdivisionByName,
} from "../../src/name-resolution/default-geo-name-resolver.js";

/**
 * Wire-shape carve-out per rules.md §6.15: these interfaces mirror the
 * literal JSON shape of `contracts/geo/fixtures/confusables.fixture.json`.
 * The fixture file uses JSON `null` as the "expected NOT FOUND" sentinel
 * (the .NET parity test on the same fixture branches on the same `null`).
 * The `| null` here mirrors the literal wire encoding to keep cross-
 * language fixture-consumer parity.
 */
interface CountryCase {
  readonly input: string;
  /** Wire-shape: `null` denotes "expected NOT FOUND"; see interface JSDoc. */
  readonly expectedIso31661Alpha2Code: string | null;
  readonly comment: string;
}

interface SubdivisionCase {
  readonly input: string;
  readonly parentCountryIso31661Alpha2Code: string;
  /** Wire-shape: `null` denotes "expected NOT FOUND"; see interface JSDoc. */
  readonly expectedIso31662Code: string | null;
  readonly comment: string;
}

interface ConfusablesFixture {
  readonly countryCases: readonly CountryCase[];
  readonly subdivisionCases: readonly SubdivisionCase[];
}

function loadFixture(): ConfusablesFixture {
  // Walk up from this test file looking for the repo root marker
  // (a directory containing contracts/geo/).
  let dir = dirname(fileURLToPath(import.meta.url));
  for (let i = 0; i < 12; i++) {
    const candidate = join(
      dir,
      "contracts",
      "geo",
      "fixtures",
      "confusables.fixture.json",
    );
    try {
      const raw = readFileSync(candidate, "utf8");
      return JSON.parse(raw) as ConfusablesFixture;
    } catch {
      // not here; walk up
    }
    const parent = resolve(dir, "..");
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error(
    "could not locate contracts/geo/fixtures/confusables.fixture.json",
  );
}

describe("confusables fixture parity (TS resolver ↔ .NET resolver)", () => {
  const fixture = loadFixture();

  describe("country cases", () => {
    for (const row of fixture.countryCases) {
      const expected = row.expectedIso31661Alpha2Code ?? "notFound";
      const caseName = `${row.comment}: '${row.input}' → ${expected}`;
      it(caseName, () => {
        const result = tryResolveCountryByName(row.input);
        if (row.expectedIso31661Alpha2Code === null) {
          expect(result.success).toBe(false);
        } else {
          expect(result.success).toBe(true);
          expect(result.data?.iso31661Alpha2Code).toBe(
            row.expectedIso31661Alpha2Code,
          );
        }
      });
    }
  });

  describe("subdivision cases", () => {
    for (const row of fixture.subdivisionCases) {
      const expected = row.expectedIso31662Code ?? "notFound";
      const label =
        `${row.comment}: '${row.input}' in ` +
        `${row.parentCountryIso31661Alpha2Code} → ${expected}`;
      it(label, () => {
        const parentCode = row.parentCountryIso31661Alpha2Code as CountryCode;
        const parent = CountryLookup.byCode[parentCode];
        const result = tryResolveSubdivisionByName(row.input, parent!);
        if (row.expectedIso31662Code === null) {
          expect(result.success).toBe(false);
        } else {
          expect(result.success).toBe(true);
          expect(result.data?.iso31662Code).toBe(row.expectedIso31662Code);
        }
      });
    }
  });
});
