// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  CountryCode,
  GeopoliticalEntityCode,
  GeopoliticalEntityType,
} from "@d2/geo-abstractions";
import { describe, expect, it } from "vitest";

import {
  GeopoliticalEntities,
  GeopoliticalEntityLookup,
} from "../../src/geopolitical-entities.js";

describe("GeopoliticalEntityLookup", () => {
  it("byCode entry for every GeopoliticalEntityCode enum member", () => {
    for (const code of Object.values(GeopoliticalEntityCode)) {
      const record = GeopoliticalEntityLookup.byCode[code];
      expect(record, `missing record for ${code}`).toBeDefined();
      expect(record!.shortCode).toBe(code);
    }
  });

  it("byCode cardinality is at least 50", () => {
    expect(
      Object.keys(GeopoliticalEntityLookup.byCode).length,
    ).toBeGreaterThanOrEqual(50);
  });

  // §1.2 category: Domain-specific — per-VALUE pins.
  it("EU fields pinned: FR + DE members, displayName non-empty", () => {
    const eu = GeopoliticalEntities.EU;
    expect(eu.shortCode).toBe(GeopoliticalEntityCode.EU);
    expect(eu.displayName.length).toBeGreaterThan(0);

    // EU continent / continental-Europe member set (per current spec
    // hand-rolled catalog) — pin lower bound that captures core members.
    expect(eu.memberCountryIso31661Alpha2Codes.size).toBeGreaterThanOrEqual(27);
    expect(
      eu.memberCountryIso31661Alpha2Codes.has(CountryCode.FR as CountryCode),
    ).toBe(true);
    expect(
      eu.memberCountryIso31661Alpha2Codes.has(CountryCode.DE as CountryCode),
    ).toBe(true);
  });

  it("EU members nav wired", () => {
    const eu = GeopoliticalEntities.EU;
    expect(
      eu.memberCountries.some((c) => c.iso31661Alpha2Code === CountryCode.FR),
    ).toBe(true);
    expect(eu.memberCountries.length).toBe(
      eu.memberCountryIso31661Alpha2Codes.size,
    );
  });

  it("NATO contains US + GB", () => {
    const nato = GeopoliticalEntities.NATO;
    expect(
      nato.memberCountryIso31661Alpha2Codes.has(CountryCode.US as CountryCode),
    ).toBe(true);
    expect(
      nato.memberCountryIso31661Alpha2Codes.has(CountryCode.GB as CountryCode),
    ).toBe(true);
  });

  it("OECD contains US + JP", () => {
    const oecd = GeopoliticalEntities.OECD;
    expect(
      oecd.memberCountryIso31661Alpha2Codes.has(CountryCode.US as CountryCode),
    ).toBe(true);
    expect(
      oecd.memberCountryIso31661Alpha2Codes.has(CountryCode.JP as CountryCode),
    ).toBe(true);
  });

  it("UN has 193 member states", () => {
    expect(GeopoliticalEntities.UN.memberCountryIso31661Alpha2Codes.size).toBe(
      193,
    );
  });

  // §1.2 category: Cross-field — code+nav agreement.
  it("every entity: nav count matches code set size", () => {
    for (const entity of GeopoliticalEntityLookup.all) {
      expect(entity.memberCountries.length).toBe(
        entity.memberCountryIso31661Alpha2Codes.size,
      );
    }
  });

  it("every entity: type is a valid enum member", () => {
    const validTypes = new Set<number>(Object.values(GeopoliticalEntityType));
    for (const entity of GeopoliticalEntityLookup.all)
      expect(validTypes.has(entity.type)).toBe(true);
  });
});
