// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { CountryCode, type SubdivisionCode } from "@d2/geo-abstractions";
import { describe, expect, it } from "vitest";

import { SubdivisionLookup } from "../../src/subdivisions.js";

describe("SubdivisionLookup", () => {
  it("byCode cardinality is at least 3000", () => {
    expect(Object.keys(SubdivisionLookup.byCode).length).toBeGreaterThanOrEqual(
      3000,
    );
  });

  it("all length matches byCode key count", () => {
    expect(SubdivisionLookup.all.length).toBe(
      Object.keys(SubdivisionLookup.byCode).length,
    );
  });

  it("byCountry covers US with at least 50 entries", () => {
    expect(
      (SubdivisionLookup.byCountry[CountryCode.US] ?? []).length,
    ).toBeGreaterThanOrEqual(50);
  });

  it("byCountry covers CA with at least 10 entries", () => {
    expect(
      (SubdivisionLookup.byCountry[CountryCode.CA] ?? []).length,
    ).toBeGreaterThanOrEqual(10);
  });

  // §1.2 category: Domain-specific — per-VALUE pins.
  it("US-NY fields pinned", () => {
    const ny = SubdivisionLookup.byCode["US-NY" as SubdivisionCode];
    expect(ny).toBeDefined();
    expect(ny!.iso31662Code).toBe("US-NY");
    expect(ny!.shortCode).toBe("NY");
    expect(ny!.displayName).toBe("New York");
    expect(ny!.countryIso31661Alpha2Code).toBe(CountryCode.US);
    expect(ny!.country).toBeDefined();
    expect(ny!.country!.iso31661Alpha2Code).toBe(CountryCode.US);
    expect(ny!.parentSubdivisionIso31662Code).toBeUndefined();
    expect(ny!.parentSubdivision).toBeUndefined();
  });

  it("US-CA fields pinned", () => {
    const ca = SubdivisionLookup.byCode["US-CA" as SubdivisionCode];
    expect(ca).toBeDefined();
    expect(ca!.iso31662Code).toBe("US-CA");
    expect(ca!.shortCode).toBe("CA");
    expect(ca!.displayName).toBe("California");
    expect(ca!.countryIso31661Alpha2Code).toBe(CountryCode.US);
  });

  it("US-TX fields pinned", () => {
    const tx = SubdivisionLookup.byCode["US-TX" as SubdivisionCode];
    expect(tx!.shortCode).toBe("TX");
    expect(tx!.displayName).toBe("Texas");
  });

  it("CA-ON Ontario pinned", () => {
    const on = SubdivisionLookup.byCode["CA-ON" as SubdivisionCode];
    expect(on!.displayName).toBe("Ontario");
    expect(on!.countryIso31661Alpha2Code).toBe(CountryCode.CA);
  });

  it("FR-IDF Île-de-France pinned", () => {
    const idf = SubdivisionLookup.byCode["FR-IDF" as SubdivisionCode];
    expect(idf!.countryIso31661Alpha2Code).toBe(CountryCode.FR);
    expect(idf!.displayName).toBeDefined();
  });

  // §1.2 category: Cross-field — Country backref symmetry.
  it("every subdivision: country nav matches countryIso31661Alpha2Code", () => {
    for (const sub of SubdivisionLookup.all) {
      expect(sub.country).toBeDefined();
      expect(sub.country!.iso31661Alpha2Code).toBe(
        sub.countryIso31661Alpha2Code,
      );
    }
  });

  it("when parentSubdivisionIso31662Code is set, the nav resolves to same code", () => {
    for (const sub of SubdivisionLookup.all) {
      if (sub.parentSubdivisionIso31662Code !== undefined) {
        expect(sub.parentSubdivision).toBeDefined();
        expect(sub.parentSubdivision!.iso31662Code).toBe(
          sub.parentSubdivisionIso31662Code,
        );
      }
    }
  });

  it("AQ (Antarctica) has no entry in byCountry index", () => {
    expect(SubdivisionLookup.byCountry[CountryCode.AQ]).toBeUndefined();
  });

  it("every subdivision: shortCode + type non-empty", () => {
    for (const sub of SubdivisionLookup.all) {
      expect(sub.shortCode).toBeDefined();
      expect(sub.type).toBeDefined();
    }
  });
});
