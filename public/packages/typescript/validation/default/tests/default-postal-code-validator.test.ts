// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { CountryCode } from "@d2/geo-abstractions";
import { TK } from "@d2/i18n-keys";
import { describe, expect, it } from "vitest";

import { DefaultPostalCodeValidator } from "../src/default-postal-code-validator.js";

const validator = new DefaultPostalCodeValidator();
const US = "US" as CountryCode;
const CA = "CA" as CountryCode;

describe("DefaultPostalCodeValidator — adversarial + property + fail-closed", () => {
  it("rejects undefined input (falsey path)", () => {
    const result = validator.validate(undefined, US);
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe("VALIDATION_FAILED");
    expect(result.inputErrors[0]?.field).toBe("postalCode");
    expect(result.inputErrors[0]?.errors[0]?.key).toBe(
      TK.common.validation.POSTAL_CODE_INVALID.key,
    );
  });

  it("rejects empty string", () => {
    expect(validator.validate("", US).success).toBe(false);
  });

  it("rejects whitespace-only input", () => {
    expect(validator.validate("   ", US).success).toBe(false);
  });

  it("FAILS CLOSED when no country is supplied", () => {
    // A valid-looking US ZIP with no country must NOT validate — there is
    // no permissive country-agnostic fallback (parity with the .NET side).
    const result = validator.validate("90210");
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe("VALIDATION_FAILED");
  });

  it("FAILS CLOSED on an unknown country code", () => {
    const result = validator.validate("90210", "ZZ" as CountryCode);
    expect(result.success).toBe(false);
  });

  it("rejects a US ZIP that is too short", () => {
    expect(validator.validate("902", US).success).toBe(false);
  });

  it("rejects letters where digits are required (US)", () => {
    expect(validator.validate("ABCDE", US).success).toBe(false);
  });

  it("rejects a format mismatch (GB code under US country)", () => {
    expect(validator.validate("SW1A 1AA", US).success).toBe(false);
  });

  it("normalizes (trim + uppercase) on success", () => {
    const result = validator.validate("  k1a 0b1  ", CA);
    expect(result.success).toBe(true);
    expect(result.data).toBe("K1A 0B1");
  });

  it("is idempotent — re-validating a normalized value returns it unchanged", () => {
    const first = validator.validate("  k1a 0b1  ", CA);
    expect(first.success).toBe(true);
    const second = validator.validate(first.data!, CA);
    expect(second.success).toBe(true);
    expect(second.data).toBe(first.data);
  });

  it("returns quickly on a pathological long input", () => {
    const pathological = "9".repeat(50_000);
    const start = performance.now();
    const result = validator.validate(pathological, US);
    const elapsedMs = performance.now() - start;
    expect(result.success).toBe(false);
    // 500 ms ceiling — a 10x margin over the ~50 ms intent. A backtracking
    // regex would blow past this on a 50k-char input; the bounded pattern
    // short-circuits well under it.
    expect(elapsedMs).toBeLessThan(500);
  });
});
