// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, it, expect } from "vitest";
import {
  countriesToOptions,
  subdivisionsForCountry,
  buildCountriesWithSubdivisions,
} from "../geo-ref-data.js";
import type { Country, Subdivision, SubdivisionCode } from "@dcsv-io/d2-geo-abstractions";
import { DayOfWeek, MeasurementSystem } from "@dcsv-io/d2-geo-abstractions";

function makeCountry(overrides: Partial<Country> = {}): Country {
  return {
    iso31661Alpha2Code: "US" as Country["iso31661Alpha2Code"],
    iso31661Alpha3Code: "USA",
    iso31661NumericCode: "840",
    displayName: "United States",
    officialName: "United States of America",
    endonymDisplayName: "United States",
    endonymOfficialName: "United States of America",
    phoneNumberPrefix: "1",
    phoneNumberNationalFormat: "(XXX) XXX-XXXX",
    phoneNumberMaxDigits: 10,
    firstDayOfWeek: DayOfWeek.Sunday,
    weekendStart: DayOfWeek.Saturday,
    weekendEnd: DayOfWeek.Sunday,
    measurementSystem: MeasurementSystem.Imperial,
    territoryIso31661Alpha2Codes: new Set() as ReadonlySet<Country["iso31661Alpha2Code"]>,
    territories: [],
    subdivisionIso31662Codes: new Set([
      "US-CA",
      "US-NY",
    ] as SubdivisionCode[]) as ReadonlySet<SubdivisionCode>,
    subdivisions: [],
    localeIetfBcp47Tags: new Set() as ReadonlySet<Country["primaryLocaleIetfBcp47Tag"] & string>,
    locales: [],
    geopoliticalEntityShortCodes: new Set() as ReadonlySet<never>,
    geopoliticalEntities: [],
    currencyIso4217AlphaCodes: new Set() as ReadonlySet<
      Country["primaryCurrencyIso4217AlphaCode"] & string
    >,
    currencies: [],
    ...overrides,
  } as Country;
}

function makeSubdivision(overrides: Partial<Subdivision> = {}): Subdivision {
  return {
    iso31662Code: "US-CA" as SubdivisionCode,
    shortCode: "CA",
    displayName: "California",
    officialName: "State of California",
    endonymDisplayName: "California",
    endonymOfficialName: "State of California",
    countryIso31661Alpha2Code: "US" as Subdivision["countryIso31661Alpha2Code"],
    type: "State",
    ...overrides,
  } as Subdivision;
}

describe("countriesToOptions", () => {
  it("puts popular countries first, then alphabetical", () => {
    const countries: Country[] = [
      makeCountry(),
      makeCountry({
        iso31661Alpha2Code: "CA" as Country["iso31661Alpha2Code"],
        displayName: "Canada",
        phoneNumberPrefix: "1",
        subdivisionIso31662Codes: new Set([
          "CA-ON",
        ] as SubdivisionCode[]) as ReadonlySet<SubdivisionCode>,
      }),
      makeCountry({
        iso31661Alpha2Code: "AF" as Country["iso31661Alpha2Code"],
        displayName: "Afghanistan",
        phoneNumberPrefix: "93",
        subdivisionIso31662Codes: new Set() as ReadonlySet<SubdivisionCode>,
      }),
    ];

    const options = countriesToOptions(countries);

    expect(options).toHaveLength(3);
    // Popular countries first (US before CA per POPULAR_COUNTRIES order)
    expect(options[0].label).toBe("United States");
    expect(options[1].label).toBe("Canada");
    // Non-popular countries alphabetically after
    expect(options[2].label).toBe("Afghanistan");
  });

  it("includes flag path", () => {
    const options = countriesToOptions([makeCountry()]);
    expect(options[0].flag).toBe("/flags/4x3/us.svg");
  });

  it("includes phone prefix from catalog data", () => {
    const options = countriesToOptions([makeCountry()]);
    expect(options[0].phonePrefix).toBe("+1");
  });

  it("falls back to libphonenumber-js for prefix when catalog value is empty", () => {
    const options = countriesToOptions([makeCountry({ phoneNumberPrefix: "" })]);
    // Should use getCountryCallingCode fallback
    expect(options[0].phonePrefix).toBe("+1");
  });

  it("includes subdivision codes", () => {
    const options = countriesToOptions([makeCountry()]);
    expect(options[0].subdivisionCodes).toEqual(expect.arrayContaining(["US-CA", "US-NY"]));
    expect(options[0].subdivisionCodes).toHaveLength(2);
  });

  it("handles empty array", () => {
    expect(countriesToOptions([])).toEqual([]);
  });
});

describe("subdivisionsForCountry", () => {
  const subdivisions: Subdivision[] = [
    makeSubdivision(),
    makeSubdivision({
      iso31662Code: "US-NY" as SubdivisionCode,
      displayName: "New York",
      countryIso31661Alpha2Code: "US" as Subdivision["countryIso31661Alpha2Code"],
    }),
    makeSubdivision({
      iso31662Code: "CA-ON" as SubdivisionCode,
      displayName: "Ontario",
      countryIso31661Alpha2Code: "CA" as Subdivision["countryIso31661Alpha2Code"],
    }),
  ];

  it("filters subdivisions for US", () => {
    const options = subdivisionsForCountry(subdivisions, "US");
    expect(options).toHaveLength(2);
    expect(options.map((o) => o.label)).toContain("California");
    expect(options.map((o) => o.label)).toContain("New York");
  });

  it("filters subdivisions for CA", () => {
    const options = subdivisionsForCountry(subdivisions, "CA");
    expect(options).toHaveLength(1);
    expect(options[0].label).toBe("Ontario");
  });

  it("returns empty for country with no subdivisions", () => {
    const options = subdivisionsForCountry(subdivisions, "JP");
    expect(options).toHaveLength(0);
  });

  it("sorts alphabetically", () => {
    const options = subdivisionsForCountry(subdivisions, "US");
    expect(options[0].label).toBe("California");
    expect(options[1].label).toBe("New York");
  });

  it("handles empty array", () => {
    expect(subdivisionsForCountry([], "US")).toEqual([]);
  });
});

describe("buildCountriesWithSubdivisions", () => {
  it("returns a set of country codes that have subdivisions", () => {
    const result = buildCountriesWithSubdivisions({
      US: [{ value: "US-CA", label: "California" }],
      CA: [{ value: "CA-ON", label: "Ontario" }],
    });

    expect(result).toBeInstanceOf(Set);
    expect(result.size).toBe(2);
    expect(result.has("US")).toBe(true);
    expect(result.has("CA")).toBe(true);
  });

  it("returns empty set for empty input", () => {
    const result = buildCountriesWithSubdivisions({});
    expect(result.size).toBe(0);
  });

  it("excludes countries not in the map", () => {
    const result = buildCountriesWithSubdivisions({
      US: [{ value: "US-CA", label: "California" }],
    });
    expect(result.has("JP")).toBe(false);
  });
});
