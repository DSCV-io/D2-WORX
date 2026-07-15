// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { CountryCode, type TimezoneCode } from "@dcsv-io/d2-geo-abstractions";
import { describe, expect, it } from "vitest";

import { TimezoneLookup } from "../../src/timezones.js";

describe("TimezoneLookup", () => {
  it("byCode cardinality is at least 250", () => {
    expect(Object.keys(TimezoneLookup.byCode).length).toBeGreaterThanOrEqual(
      250,
    );
  });

  it("all length matches byCode key count", () => {
    expect(TimezoneLookup.all.length).toBe(
      Object.keys(TimezoneLookup.byCode).length,
    );
  });

  // §1.2 category: Domain-specific — per-VALUE pins.
  it("America/New_York fields pinned", () => {
    const ny = TimezoneLookup.byCode["America/New_York" as TimezoneCode];
    expect(ny).toBeDefined();
    expect(ny!.ianaName).toBe("America/New_York");
    expect(ny!.currentStdOffsetMinutes).toBe(-300);
    expect(ny!.currentStdAbbrev).toBe("EST");
    expect(ny!.primaryCountryIso31661Alpha2Code).toBe(CountryCode.US);
    expect(ny!.primaryCountry!.iso31661Alpha2Code).toBe(CountryCode.US);
  });

  it("Europe/London nav wired", () => {
    const london = TimezoneLookup.byCode["Europe/London" as TimezoneCode];
    expect(london!.primaryCountryIso31661Alpha2Code).toBe(CountryCode.GB);
    expect(london!.primaryCountry!.iso31661Alpha2Code).toBe(CountryCode.GB);
  });

  it("Asia/Tokyo nav wired + no DST", () => {
    const tokyo = TimezoneLookup.byCode["Asia/Tokyo" as TimezoneCode];
    expect(tokyo!.primaryCountryIso31661Alpha2Code).toBe(CountryCode.JP);
    expect(tokyo!.currentStdOffsetMinutes).toBe(540);
    expect(tokyo!.currentDstOffsetMinutes).toBeUndefined();
    expect(tokyo!.currentDstAbbrev).toBeUndefined();
  });

  it("at least one zone in the catalog has CoApplicable countries (shared-zone scenario)", () => {
    // Shared-zone scenario — at least one IANA zone in the catalog
    // has a non-empty coApplicableCountryIso31661Alpha2Codes set
    // (Europe shares the same zone across multiple countries).
    const sharedZones = TimezoneLookup.all.filter(
      (tz) => tz.coApplicableCountryIso31661Alpha2Codes.size > 0,
    );
    expect(sharedZones.length).toBeGreaterThan(0);
  });

  // §1.2 category: Cross-field — code+nav agreement.
  it("every tz: primaryCountry nav matches code when set", () => {
    for (const tz of TimezoneLookup.all) {
      if (tz.primaryCountry !== undefined)
        expect(tz.primaryCountryIso31661Alpha2Code).toBe(
          tz.primaryCountry.iso31661Alpha2Code,
        );
    }
  });

  it("every tz: coApplicableCountries count matches code set size", () => {
    for (const tz of TimezoneLookup.all) {
      expect(tz.coApplicableCountries.length).toBe(
        tz.coApplicableCountryIso31661Alpha2Codes.size,
      );
    }
  });

  it("every tz: localizedDisplayNames object is present", () => {
    // The localizedDisplayNames dictionary is per-zone optional in the
    // current catalog; pin only that the field exists for every record.
    for (const tz of TimezoneLookup.all)
      expect(tz.localizedDisplayNames).toBeDefined();
  });
});
