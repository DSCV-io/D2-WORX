// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  CountryCode as CountryCodeConst,
  asSubdivisionCode,
} from "@d2/geo-abstractions";
import type { CountryCode, SubdivisionCode } from "@d2/geo-abstractions";
import { describe, expect, it } from "vitest";

import {
  createAdminLocation,
  defaultPostalCodeValidator,
} from "../src/index.js";

const US: CountryCode = CountryCodeConst.US as CountryCode;
const CA: CountryCode = CountryCodeConst.CA as CountryCode;
const US_NY: SubdivisionCode = asSubdivisionCode("US-NY");

describe("AdminLocation", () => {
  describe("Happy path", () => {
    it("country + matching subdivision → Ok", () => {
      const r = createAdminLocation(US, US_NY);
      expect(r.success).toBe(true);
      expect(r.data!.countryIso31661Alpha2Code).toBe(US);
      expect(r.data!.subdivisionIso31662Code).toBe(US_NY);
    });
  });

  describe("Coherence cases (Amendment 50)", () => {
    it("all-null → ValidationFailed (admin_empty_record)", () => {
      const r = createAdminLocation();
      expect(r.success).toBe(false);
      expect(r.messages[0]!.key).toBe("geo_validation_admin_empty_record");
    });

    it("all-whitespace fields → ValidationFailed (degenerate, admin_empty_record)", () => {
      const r = createAdminLocation(undefined, undefined, "   ", "   ");
      expect(r.success).toBe(false);
      expect(r.messages[0]!.key).toBe("geo_validation_admin_empty_record");
    });

    it("country-only → Ok", () => {
      const r = createAdminLocation(US);
      expect(r.success).toBe(true);
      expect(r.data!.countryIso31661Alpha2Code).toBe(US);
      expect(r.data!.subdivisionIso31662Code).toBeUndefined();
    });

    it("subdivision-only → Ok with auto-populated country", () => {
      const r = createAdminLocation(undefined, US_NY);
      expect(r.success).toBe(true);
      expect(r.data!.countryIso31661Alpha2Code).toBe(US);
      expect(r.data!.subdivisionIso31662Code).toBe(US_NY);
    });

    it("country + mismatched subdivision → ValidationFailed", () => {
      const r = createAdminLocation(CA, US_NY);
      expect(r.success).toBe(false);
      expect(r.messages[0]!.key).toBe(
        "geo_validation_admin_country_subdivision_mismatch",
      );
    });

    it("city-only → Ok (non-degenerate)", () => {
      const r = createAdminLocation(undefined, undefined, "Brooklyn");
      expect(r.success).toBe(true);
      expect(r.data!.city).toBe("Brooklyn");
    });

    it("postal-only (no validator) → Ok", () => {
      const r = createAdminLocation(undefined, undefined, undefined, "90210");
      expect(r.success).toBe(true);
      expect(r.data!.postalCode).toBe("90210");
    });
  });

  describe("City normalization", () => {
    it("whitespace collapsed", () => {
      const r = createAdminLocation(undefined, undefined, "  New    York  ");
      expect(r.data!.city).toBe("New York");
    });

    it("empty city stored as undefined", () => {
      const r = createAdminLocation(US, undefined, "");
      expect(r.success).toBe(true);
      expect(r.data!.city).toBeUndefined();
    });
  });

  describe("Postal-code validator integration", () => {
    it("validator failure propagates", () => {
      const r = createAdminLocation(
        US,
        undefined,
        undefined,
        "AB",
        defaultPostalCodeValidator(),
      );
      expect(r.success).toBe(false);
      expect(r.messages[0]!.key).toBe("geo_validation_postal_code_invalid");
    });

    it("validator success → AdminLocation Ok", () => {
      const r = createAdminLocation(
        US,
        undefined,
        undefined,
        "90210",
        defaultPostalCodeValidator(),
      );
      expect(r.success).toBe(true);
      expect(r.data!.postalCode).toBe("90210");
    });

    it("null postal + validator supplied → no validation performed", () => {
      const r = createAdminLocation(
        US,
        undefined,
        undefined,
        undefined,
        defaultPostalCodeValidator(),
      );
      expect(r.success).toBe(true);
      expect(r.data!.postalCode).toBeUndefined();
    });
  });

  describe("HashId invariants", () => {
    it("starts with v1.", () => {
      expect(
        createAdminLocation(US, undefined, "Brooklyn").data!.hashId,
      ).toMatch(/^v1\./);
    });

    it("length 67", () => {
      expect(createAdminLocation(US).data!.hashId).toHaveLength(67);
    });

    it("deterministic across calls", () => {
      const r1 = createAdminLocation(US, undefined, "Brooklyn", "11201");
      const r2 = createAdminLocation(US, undefined, "Brooklyn", "11201");
      expect(r1.data!.hashId).toBe(r2.data!.hashId);
    });

    it("differs by country", () => {
      const r1 = createAdminLocation(US);
      const r2 = createAdminLocation(CA);
      expect(r1.data!.hashId).not.toBe(r2.data!.hashId);
    });

    it("subdivision-only auto-pop equals explicit matching", () => {
      const r1 = createAdminLocation(undefined, US_NY);
      const r2 = createAdminLocation(US, US_NY);
      expect(r1.data!.hashId).toBe(r2.data!.hashId);
    });
  });

  describe("Adversarial — CRLF / injection in free-text", () => {
    it("city with CRLF → stripped from stored", () => {
      const r = createAdminLocation(US, undefined, "Brooklyn\r\nINJECTED");
      expect(r.success).toBe(true);
      expect(r.data!.city).not.toContain("\r");
      expect(r.data!.city).not.toContain("\n");
    });

    it("postal with CRLF → validator rejects", () => {
      const r = createAdminLocation(
        US,
        undefined,
        undefined,
        "90210\r\nINJECTED",
        defaultPostalCodeValidator(),
      );
      expect(r.success).toBe(false);
    });
  });
});
