// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import type { CountryCode } from "@d2/geo-abstractions";
import { describe, expect, it } from "vitest";

import { CountryLookup } from "../../src/countries.js";
import {
  tryResolveCountryByName,
  tryResolveSubdivisionByName,
} from "../../src/name-resolution/default-geo-name-resolver.js";

interface CountryCase {
  readonly input: string;
  readonly expectedIso31661Alpha2Code: string | null;
  readonly comment: string;
}

interface SubdivisionCase {
  readonly input: string;
  readonly parentCountryIso31661Alpha2Code: string;
  readonly expectedIso31662Code: string | null;
  readonly comment: string;
}

interface ConfusablesFixture {
  readonly countryCases: readonly CountryCase[];
  readonly subdivisionCases: readonly SubdivisionCase[];
}

function loadFixture(): ConfusablesFixture {
  const here = dirname(fileURLToPath(import.meta.url));
  // From server/shared/typescript/geo-default/tests/name-resolution/
  // up to repo root is 6 levels.
  const repoRoot = join(here, "..", "..", "..", "..", "..", "..");
  const path = join(
    repoRoot,
    "contracts",
    "geo",
    "fixtures",
    "confusables.fixture.json",
  );
  const raw = readFileSync(path, "utf8");
  return JSON.parse(raw) as ConfusablesFixture;
}

describe("confusables fixture parity (TS resolver ↔ .NET resolver)", () => {
  const fixture = loadFixture();

  describe("country cases", () => {
    for (const row of fixture.countryCases) {
      it(`${row.comment}: '${row.input}' → ${row.expectedIso31661Alpha2Code ?? "notFound"}`, () => {
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
      it(`${row.comment}: '${row.input}' in ${row.parentCountryIso31661Alpha2Code} → ${row.expectedIso31662Code ?? "notFound"}`, () => {
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
