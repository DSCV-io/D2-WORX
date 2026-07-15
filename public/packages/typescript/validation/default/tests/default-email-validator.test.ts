// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { TK } from "@d2/i18n-keys";
import { describe, expect, it } from "vitest";

import {
  DefaultEmailValidator,
  EMAIL_PATTERN,
} from "../src/default-email-validator.js";

const validator = new DefaultEmailValidator();

describe("DefaultEmailValidator — adversarial + property", () => {
  it("rejects undefined input (falsey path)", () => {
    const result = validator.validate(undefined);
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe("VALIDATION_FAILED");
    expect(result.inputErrors[0]?.field).toBe("email");
    expect(result.inputErrors[0]?.errors[0]?.key).toBe(
      TK.common.validation.EMAIL_INVALID.key,
    );
  });

  it("rejects empty string", () => {
    expect(validator.validate("").success).toBe(false);
  });

  it("rejects whitespace-only input", () => {
    expect(validator.validate("   ").success).toBe(false);
  });

  it.each([
    "userexample.com",
    "a@@b.com",
    "a b@c.com",
    "a@b",
    "a@b.com.",
    "münchen@example.com",
    "user@exämple.com",
  ])("rejects malformed address %s", (bad) => {
    expect(validator.validate(bad).success).toBe(false);
  });

  it.each([".a@b.com", "a.@b.com", "a..b@c.com"])(
    // The shared structural pattern permits dots in any LOCAL-part position
    // (RFC 5321/5322 practical limits, not a full dot-atom validator). Both
    // runtimes accept these — pinned so a stricter regex edit is a conscious
    // cross-language decision, not an accident.
    "accepts local-part dot-positioning %s",
    (addr) => {
      expect(validator.validate(addr).success).toBe(true);
    },
  );

  it("normalizes (trim + lowercase) on success", () => {
    const result = validator.validate("  User@Example.COM  ");
    expect(result.success).toBe(true);
    expect(result.data).toBe("user@example.com");
  });

  it("is idempotent — validating a normalized value returns it unchanged", () => {
    const first = validator.validate("  User@Example.COM  ");
    expect(first.success).toBe(true);
    const second = validator.validate(first.data!);
    expect(second.success).toBe(true);
    expect(second.data).toBe(first.data);
  });

  it("returns quickly on a pathological long input (ReDoS guard)", () => {
    // A long run of local-part chars with a single '@' and no valid domain —
    // the kind of input a backtracking-vulnerable regex would hang on. The
    // anchored, bounded pattern must short-circuit.
    const pathological = "a".repeat(50_000) + "@";
    const start = performance.now();
    const result = validator.validate(pathological);
    const elapsedMs = performance.now() - start;
    expect(result.success).toBe(false);
    // 500 ms ceiling — a 10x margin over the ~50 ms intent. A backtracking
    // regex would blow past this on a 50k-char input; the bounded pattern
    // short-circuits well under it.
    expect(elapsedMs).toBeLessThan(500);
  });

  it("exposes the cross-language EMAIL_PATTERN literal", () => {
    // Pin the exact literal. The contract-tests parity suite asserts this is
    // byte-identical with the .NET `EMAIL_PATTERN`; pinning it here too means
    // any local edit fails fast without waiting on the cross-runtime suite.
    // long regex literal — cannot wrap
    expect(EMAIL_PATTERN).toBe(
      "^(?=.{1,254}$)[A-Z0-9._%+\\-]{1,64}@[A-Z0-9](?:[A-Z0-9\\-]{0,61}[A-Z0-9])?(?:\\.[A-Z0-9](?:[A-Z0-9\\-]{0,61}[A-Z0-9])?)+$",
    );
  });
});
