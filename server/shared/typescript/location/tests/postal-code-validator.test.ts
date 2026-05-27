// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { CountryCode as CountryCodeConst } from "@d2/geo-abstractions";
import type { CountryCode } from "@d2/geo-abstractions";
import { describe, expect, it } from "vitest";

import { defaultPostalCodeValidator } from "../src/index.js";

const CA: CountryCode = CountryCodeConst.CA as CountryCode;
const v = defaultPostalCodeValidator();

describe("DefaultPostalCodeValidator", () => {
  describe("Happy path — global formats", () => {
    it.each([
      "90210",
      "90210-1234",
      "A1A 1A1",
      "SW1A 1AA",
      "100-0001",
      "2000",
      "10115",
    ])("%s → Ok", (code) => {
      const r = v.validate(code);
      expect(r.success).toBe(true);
      expect(r.data).toBe(code);
    });
  });

  describe("Garbage — undefined / empty / whitespace / emoji / symbols", () => {
    it.each([undefined, "", "   ", "\t\n\r"])(
      "%j → ValidationFailed",
      (code) => {
        const r = v.validate(code as string | undefined);
        expect(r.success).toBe(false);
        expect(r.messages[0]!.key).toBe("geo_validation_postal_code_invalid");
      },
    );

    it("emoji rejected", () => {
      expect(v.validate("💩90210").success).toBe(false);
    });

    it("special chars only rejected", () => {
      expect(v.validate("!@#$%").success).toBe(false);
    });
  });

  describe("Length boundaries", () => {
    it("too short (AB) → fail", () => {
      expect(v.validate("AB").success).toBe(false);
    });

    it("too long (11 chars) → fail", () => {
      expect(v.validate("12345678901").success).toBe(false);
    });

    it("min length 3 → Ok", () => {
      expect(v.validate("ABC").success).toBe(true);
    });

    it("max length 10 → Ok", () => {
      expect(v.validate("1234567890").success).toBe(true);
    });
  });

  describe("Leading/trailing hyphens vs whitespace", () => {
    it.each(["-12345", "12345-"])("hyphen-bounded %s → fail", (code) => {
      expect(v.validate(code).success).toBe(false);
    });

    it.each([" 12345", "12345 "])("space-bounded %s → Ok (trimmed)", (code) => {
      expect(v.validate(code).success).toBe(true);
    });
  });

  describe("Country argument ignored by default impl", () => {
    it("US 5-digit numeric → Ok even with CountryCode.CA", () => {
      expect(v.validate("12345", CA).success).toBe(true);
    });

    it("countryCode undefined → Ok", () => {
      expect(v.validate("90210", undefined).success).toBe(true);
    });
  });

  describe("Adversarial — injection / oversized", () => {
    it("CRLF injected → regex rejects", () => {
      expect(v.validate("90210\r\nINJECTED").success).toBe(false);
    });

    it("NUL injected → regex rejects", () => {
      expect(v.validate("90210\0INJECTED").success).toBe(false);
    });

    it("all-spaces → ValidationFailed", () => {
      expect(v.validate("     ").success).toBe(false);
    });

    it("10k-char input does not hang", () => {
      const r = v.validate("A".repeat(10_000));
      expect(r.success).toBe(false);
    });
  });
});
