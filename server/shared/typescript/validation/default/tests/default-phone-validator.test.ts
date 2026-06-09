// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { CountryCode } from "@d2/geo-abstractions";
import { TK } from "@d2/i18n-keys";
import { describe, expect, it } from "vitest";

import { DefaultPhoneValidator } from "../src/default-phone-validator.js";

const validator = new DefaultPhoneValidator();
const US = "US" as CountryCode;

describe("DefaultPhoneValidator — adversarial + property", () => {
  it("rejects undefined input (falsey path)", () => {
    const result = validator.validate(undefined);
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe("VALIDATION_FAILED");
    expect(result.inputErrors[0]?.field).toBe("phone");
    expect(result.inputErrors[0]?.errors[0]?.key).toBe(
      TK.common.validation.PHONE_INVALID.key,
    );
  });

  it("rejects empty string", () => {
    expect(validator.validate("", US).success).toBe(false);
  });

  it("rejects whitespace-only input", () => {
    expect(validator.validate("   ", US).success).toBe(false);
  });

  it("rejects a national-format number with no default region", () => {
    expect(validator.validate("02079460958").success).toBe(false);
  });

  it("rejects non-numeric garbage", () => {
    // NOT a vanity-letter number — libphonenumber-csharp maps letters to
    // digits (a cross-runtime divergence), so a "CALL"-style input is avoided.
    expect(validator.validate("not-a-phone", US).success).toBe(false);
  });

  it("rejects an absurdly long digit run", () => {
    expect(validator.validate("9".repeat(40), US).success).toBe(false);
  });

  it("normalizes a valid national number to E.164", () => {
    const result = validator.validate("(202) 555-0143", US);
    expect(result.success).toBe(true);
    expect(result.data).toBe("+12025550143");
  });

  it("is idempotent — re-validating an E.164 result returns it unchanged", () => {
    const first = validator.validate("(202) 555-0143", US);
    expect(first.success).toBe(true);
    // E.164 form carries the country, so no default region is needed.
    const second = validator.validate(first.data!);
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
