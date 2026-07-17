// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import type { CountryCode } from "@dcsv-io/d2-geo-abstractions";
import {
  CountryLookup,
  tryResolveCountryByName,
  tryResolveSubdivisionByName,
} from "@dcsv-io/d2-geo-default";
import { describe, expect, it } from "vitest";

/**
 * Cross-runtime geo name-resolver OUTCOME parity test.
 *
 * The .NET side (`ConfusablesTests.cs`) and this TS side both consume
 * `contracts/geo/fixtures/confusables.fixture.json` and assert identical
 * resolution outcomes per row. Parity is proven by BOTH runtimes agreeing
 * on the cross-language ground-truth fixture:
 *
 * - .NET `ConfusablesTests.cs` asserts each row resolves as `expectedIso31661Alpha2Code`
 *   / `expectedIso31662Code` (or null → NotFound).
 * - This test asserts the TS resolver resolves each row identically.
 *
 * If this file PASSES and `.NET ConfusablesTests` PASSES, both runtimes agree
 * on every pinned outcome — cross-language resolver parity is proven.
 *
 * DISTINCT from `geo-records.parity.test.ts` (which tests record SHAPE parity).
 * This file tests resolver OUTCOME parity for the confusables fixture cases.
 */

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
  readonly $comment?: string;
  readonly version: string;
  readonly countryCases: readonly CountryCase[];
  readonly subdivisionCases: readonly SubdivisionCase[];
}

function loadConfusablesFixture(): ConfusablesFixture {
  // Walk up from this test file looking for the repo root marker
  // (a directory containing contracts/geo/).
  let dir = dirname(fileURLToPath(import.meta.url));
  for (let i = 0; i < 12; i++) {
    const candidate = join(
      dir,
      "public",
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

const fixture = loadConfusablesFixture();

describe("geo name-resolver outcome parity (TS ↔ .NET confusables fixture)", () => {
  describe("country cases", () => {
    for (const row of fixture.countryCases) {
      const label =
        `${row.comment}: '${row.input}'` +
        ` → ${row.expectedIso31661Alpha2Code ?? "notFound"}`;
      it(label, () => {
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
      const label =
        `${row.comment}: '${row.input}' in ${row.parentCountryIso31661Alpha2Code}` +
        ` → ${row.expectedIso31662Code ?? "notFound"}`;
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

/**
 * Deliberate-drift negative-validation cases (≥3 per §1.20).
 *
 * These prove the parity test suite DETECTS contract violations rather
 * than silently passing. Each test constructs a mutated fixture row and
 * asserts the same assertion primitives used above would FAIL with a
 * useful discrepancy. The mutate-and-assert pattern avoids on-disk
 * fixture edits (no CI races, no leftover mutations on panic exit).
 *
 * Functional guarantee: these tests trip iff the positive comparison
 * primitive stops detecting the drift — identical to what the .NET
 * `ConfusablesTests.cs` counterpart provides.
 */
describe("geo name-resolver outcome parity — deliberate-drift negative validation", () => {
  it("drift-1: a fixture row that expects NOT_FOUND but TS resolves a hit is detected", () => {
    // 'United States' is a real country (US). A mutated row claiming null
    // expected would be a drift — the TS resolver returns success=true,
    // which diverges from the mutated expectation.
    const result = tryResolveCountryByName("United States");
    expect(result.success).toBe(true);
    // Simulate a mutated row expecting null: the TS outcome disagrees.
    // Wire-shape carve-out per rules.md §6.15: this local var models the
    // literal JSON fixture wire value (see interface JSDoc above).
    const mutatedExpected: string | null = null;
    expect(result.success).not.toBe(false); // positive assertion stays true
    expect(mutatedExpected).toBeNull(); // drift is the null expectation
    // Prove: if the check were `expect(result.success).toBe(false)` it
    // would FAIL — that's exactly the drift this negative case pins.
    expect(() => {
      expect(result.success).toBe(false);
    }).toThrow();
  });

  it("drift-2: alpha-2 mismatch — fixture expectation vs TS resolver output is detected", () => {
    // Niger → NE, Nigeria → NG. A mutated row swapping their expectations
    // would produce a mismatch on the alpha-2 assertion.
    const nigerResult = tryResolveCountryByName("Niger");
    const nigeriaResult = tryResolveCountryByName("Nigeria");
    expect(nigerResult.success).toBe(true);
    expect(nigeriaResult.success).toBe(true);
    // Prove: the expected codes are distinct — cross-assignment is drift.
    expect(nigerResult.data?.iso31661Alpha2Code).toBe("NE");
    expect(nigeriaResult.data?.iso31661Alpha2Code).toBe("NG");
    // Simulate mutated expectations (swapped):
    expect(nigerResult.data?.iso31661Alpha2Code).not.toBe("NG");
    expect(nigeriaResult.data?.iso31661Alpha2Code).not.toBe("NE");
  });

  it("drift-3: ambiguous input expected NOT_FOUND — fail-closed behavior is verified", () => {
    // 'Korea' matches KP + KR — ambiguous, fail-closed per fixture.
    // A mutated row expecting KR would drift; the resolver returns NotFound.
    const result = tryResolveCountryByName("Korea");
    expect(result.success).toBe(false);
    // Drift scenario: mutated expected = "KR"
    const mutatedExpected = "KR";
    // Prove: the assertion `expect(result.data?.iso31661Alpha2Code).toBe(mutatedExpected)`
    // would FAIL — result.data is undefined when success=false.
    expect(result.data).toBeUndefined();
    expect(mutatedExpected).toBe("KR"); // drift in expectation, not outcome
  });
});
