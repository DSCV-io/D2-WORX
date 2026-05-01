import { describe, it, expect } from "vitest";
import {
  countriesToOptions,
  subdivisionsForCountry,
  buildCountriesWithSubdivisions,
} from "../geo-ref-data.js";
import type { CountryDTO, SubdivisionDTO } from "@d2/protos";

function makeCountry(overrides: Partial<CountryDTO> = {}): CountryDTO {
  return {
    iso31661Alpha2Code: "US",
    iso31661Alpha3Code: "USA",
    iso31661NumericCode: "840",
    displayName: "United States",
    officialName: "United States of America",
    phoneNumberPrefix: "1",
    phoneNumberFormat: "(XXX) XXX-XXXX",
    sovereignIso31661Alpha2Code: "",
    primaryCurrencyIso4217AlphaCode: "USD",
    primaryLocaleIetfBcp47Tag: "en-US",
    territoryIso31661Alpha2Codes: [],
    subdivisionIso31662Codes: ["US-CA", "US-NY"],
    currencyIso4217AlphaCodes: ["USD"],
    localeIetfBcp47Tags: ["en-US"],
    geopoliticalEntityShortCodes: [],
    ...overrides,
  };
}

function makeSubdivision(overrides: Partial<SubdivisionDTO> = {}): SubdivisionDTO {
  return {
    iso31662Code: "US-CA",
    shortCode: "CA",
    displayName: "California",
    officialName: "State of California",
    countryIso31661Alpha2Code: "US",
    ...overrides,
  };
}

describe("countriesToOptions", () => {
  it("puts popular countries first, then alphabetical", () => {
    const countries: Record<string, CountryDTO> = {
      US: makeCountry(),
      CA: makeCountry({
        iso31661Alpha2Code: "CA",
        displayName: "Canada",
        phoneNumberPrefix: "1",
        subdivisionIso31662Codes: ["CA-ON"],
      }),
      AF: makeCountry({
        iso31661Alpha2Code: "AF",
        displayName: "Afghanistan",
        phoneNumberPrefix: "93",
        subdivisionIso31662Codes: [],
      }),
    };

    const options = countriesToOptions(countries);

    expect(options).toHaveLength(3);
    // Popular countries first (US before CA per POPULAR_COUNTRIES order)
    expect(options[0].label).toBe("United States");
    expect(options[1].label).toBe("Canada");
    // Non-popular countries alphabetically after
    expect(options[2].label).toBe("Afghanistan");
  });

  it("includes flag path", () => {
    const options = countriesToOptions({ US: makeCountry() });
    expect(options[0].flag).toBe("/flags/4x3/us.svg");
  });

  it("includes phone prefix from proto data", () => {
    const options = countriesToOptions({ US: makeCountry() });
    expect(options[0].phonePrefix).toBe("+1");
  });

  it("falls back to libphonenumber-js for prefix when proto is empty", () => {
    const options = countriesToOptions({
      US: makeCountry({ phoneNumberPrefix: "" }),
    });
    // Should use getCountryCallingCode fallback
    expect(options[0].phonePrefix).toBe("+1");
  });

  it("includes subdivision codes", () => {
    const options = countriesToOptions({ US: makeCountry() });
    expect(options[0].subdivisionCodes).toEqual(["US-CA", "US-NY"]);
  });

  it("handles empty map", () => {
    expect(countriesToOptions({})).toEqual([]);
  });
});

describe("subdivisionsForCountry", () => {
  const subdivisions: Record<string, SubdivisionDTO> = {
    "US-CA": makeSubdivision(),
    "US-NY": makeSubdivision({
      iso31662Code: "US-NY",
      displayName: "New York",
      countryIso31661Alpha2Code: "US",
    }),
    "CA-ON": makeSubdivision({
      iso31662Code: "CA-ON",
      displayName: "Ontario",
      countryIso31661Alpha2Code: "CA",
    }),
  };

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

  it("handles empty map", () => {
    expect(subdivisionsForCountry({}, "US")).toEqual([]);
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
