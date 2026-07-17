// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { CountryCode } from "@dcsv-io/d2-geo-abstractions";
import {
  DefaultEmailValidator,
  DefaultPhoneValidator,
  DefaultPostalCodeValidator,
  EMAIL_PATTERN,
} from "@dcsv-io/d2-validation";
import { describe, expect, it } from "vitest";

import { loadContractFixture } from "../src/index.js";

/**
 * The exact email-validation pattern literal. BOTH runtimes assert against
 * this same string (the .NET test holds the identical literal), so a drift
 * on EITHER side fails its respective test. Kept here verbatim — do NOT
 * import it from the package, because the point is to pin the package's
 * runtime value AGAINST an out-of-band copy.
 */
// long regex literal — cannot wrap
const EXPECTED_EMAIL_PATTERN =
  "^(?=.{1,254}$)[A-Z0-9._%+\\-]{1,64}@[A-Z0-9](?:[A-Z0-9\\-]{0,61}[A-Z0-9])?(?:\\.[A-Z0-9](?:[A-Z0-9\\-]{0,61}[A-Z0-9])?)+$";

/**
 * One corpus row. Mirrors the hand-authored
 * `contracts/validation/fixtures/<validator>.json` shape. An input is
 * EITHER a literal `input` string OR synthesized from `inputKind`.
 */
interface ValidationRow {
  readonly name: string;
  readonly input?: string;
  readonly inputKind?: "null" | "whitespace" | "oversized";
  readonly char?: string;
  readonly inputRepeat?: number;
  readonly suffix?: string;
  readonly country?: string;
  readonly valid: boolean;
  readonly normalized?: string;
  readonly errorKey?: string;
}

interface ValidationFixture {
  readonly version: string;
  readonly validator: string;
  readonly rows: readonly ValidationRow[];
}

/**
 * Synthesize the validator INPUT for a row. Both runtimes implement this
 * identically: `inputKind` "null" => empty string, "whitespace" => a few
 * spaces, "oversized" => `char.repeat(inputRepeat)` (+ optional `suffix`).
 * A literal `input` is returned verbatim. The result is typed
 * `string | undefined` so the "null" case can exercise the empty path —
 * the validators treat empty / whitespace / undefined identically (falsey).
 */
function synthInput(row: ValidationRow): string | undefined {
  if (row.inputKind === undefined) return row.input;
  switch (row.inputKind) {
    case "null":
      return "";
    case "whitespace":
      return "   ";
    case "oversized": {
      const char = row.char ?? "a";
      const repeat = row.inputRepeat ?? 1;
      return char.repeat(repeat) + (row.suffix ?? "");
    }
  }
}

/** Map a corpus country string to the `CountryCode` brand (or undefined). */
function toCountry(country: string | undefined): CountryCode | undefined {
  return country as CountryCode | undefined;
}

/** Flatten a result's input-error message keys for `.toContain` assertions. */
function errorKeys(
  inputErrors: ReadonlyArray<{
    readonly errors: ReadonlyArray<{ readonly key: string }>;
  }>,
): string[] {
  return inputErrors.flatMap((e) => e.errors.map((m) => m.key));
}

function loadCorpus(name: string): ValidationFixture {
  const fixture = loadContractFixture<ValidationFixture>("validation", name);
  if (fixture.rows.length === 0) {
    throw new Error(
      `Validation corpus '${name}' loaded with zero rows — check the ` +
        "contracts/validation/fixtures path and JSON shape.",
    );
  }
  return fixture;
}

describe("validation parity — email (contracts/validation/fixtures/email.json)", () => {
  const corpus = loadCorpus("email");
  const validator = new DefaultEmailValidator();

  for (const row of corpus.rows) {
    it(row.name, () => {
      const input = synthInput(row);
      const result = validator.validate(input);

      if (row.valid) {
        expect(result.success, row.name).toBe(true);
        expect(result.data, row.name).toBe(row.normalized);
      } else {
        expect(result.success, row.name).toBe(false);
        expect(result.errorCode, row.name).toBe("VALIDATION_FAILED");
        expect(errorKeys(result.inputErrors), row.name).toContain(row.errorKey);
      }
    });
  }

  it("EMAIL_PATTERN runtime value matches the cross-language literal", () => {
    expect(EMAIL_PATTERN).toBe(EXPECTED_EMAIL_PATTERN);
  });
});

describe("validation parity — phone (contracts/validation/fixtures/phone.json)", () => {
  const corpus = loadCorpus("phone");
  const validator = new DefaultPhoneValidator();

  for (const row of corpus.rows) {
    it(row.name, () => {
      const input = synthInput(row);
      const result = validator.validate(input, toCountry(row.country));

      if (row.valid) {
        expect(result.success, row.name).toBe(true);
        expect(result.data, row.name).toBe(row.normalized);
      } else {
        expect(result.success, row.name).toBe(false);
        expect(result.errorCode, row.name).toBe("VALIDATION_FAILED");
        expect(errorKeys(result.inputErrors), row.name).toContain(row.errorKey);
      }
    });
  }
});

describe("validation parity — postcode (contracts/validation/fixtures/postcode.json)", () => {
  const corpus = loadCorpus("postcode");
  const validator = new DefaultPostalCodeValidator();

  for (const row of corpus.rows) {
    it(row.name, () => {
      const input = synthInput(row);
      const result = validator.validate(input, toCountry(row.country));

      if (row.valid) {
        expect(result.success, row.name).toBe(true);
        expect(result.data, row.name).toBe(row.normalized);
      } else {
        expect(result.success, row.name).toBe(false);
        expect(result.errorCode, row.name).toBe("VALIDATION_FAILED");
        expect(errorKeys(result.inputErrors), row.name).toContain(row.errorKey);
      }
    });
  }
});
