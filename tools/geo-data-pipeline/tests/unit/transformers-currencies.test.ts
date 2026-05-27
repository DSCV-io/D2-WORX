// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, it, expect } from "vitest";
import {
  parseMinorUnit,
  parseIntStrict,
  pickHomeCountryUsage,
  type CurrencyUsageEntry,
} from "../../src/transformers/currencies.js";

describe("parseMinorUnit", () => {
  it("parses '0' to 0", () => {
    expect(parseMinorUnit("0")).toBe(0);
  });

  it("parses '2' to 2", () => {
    expect(parseMinorUnit("2")).toBe(2);
  });

  it("parses '6' to 6 (upper bound)", () => {
    expect(parseMinorUnit("6")).toBe(6);
  });

  it("returns null for 'N.A.' marker", () => {
    expect(parseMinorUnit("N.A.")).toBeNull();
  });

  it("returns null for empty string", () => {
    expect(parseMinorUnit("")).toBeNull();
  });

  it("returns null for negative value", () => {
    expect(parseMinorUnit("-1")).toBeNull();
  });

  it("returns null for fractional value", () => {
    expect(parseMinorUnit("2.5")).toBeNull();
  });

  it("returns null for value above range (>6)", () => {
    expect(parseMinorUnit("7")).toBeNull();
    expect(parseMinorUnit("100")).toBeNull();
  });

  it("returns null for non-numeric input", () => {
    expect(parseMinorUnit("abc")).toBeNull();
  });
});

describe("parseIntStrict", () => {
  it("parses '0' to 0", () => {
    expect(parseIntStrict("0")).toBe(0);
  });

  it("parses '3' to 3", () => {
    expect(parseIntStrict("3")).toBe(3);
  });

  it("returns null for undefined", () => {
    expect(parseIntStrict(undefined)).toBeNull();
  });

  it("returns null for empty string", () => {
    expect(parseIntStrict("")).toBeNull();
  });

  it("returns null for negative value", () => {
    expect(parseIntStrict("-2")).toBeNull();
  });

  it("returns null for fractional value", () => {
    expect(parseIntStrict("1.5")).toBeNull();
  });

  it("returns null for value above range (>6)", () => {
    expect(parseIntStrict("7")).toBeNull();
  });

  it("returns null for non-numeric input", () => {
    expect(parseIntStrict("x")).toBeNull();
  });
});

describe("pickHomeCountryUsage", () => {
  const active = (
    country: string,
    fromDate: string | null = "2000-01-01",
  ): CurrencyUsageEntry => ({
    countryISO31661Alpha2Code: country,
    fromDate,
    toDate: null,
    isLegalTender: true,
  });

  const retired = (country: string, toDate: string): CurrencyUsageEntry => ({
    countryISO31661Alpha2Code: country,
    fromDate: "1900-01-01",
    toDate,
    isLegalTender: true,
  });

  const nonTender = (country: string): CurrencyUsageEntry => ({
    countryISO31661Alpha2Code: country,
    fromDate: "2000-01-01",
    toDate: null,
    isLegalTender: false,
  });

  it("returns null for empty array", () => {
    expect(pickHomeCountryUsage([])).toBeNull();
  });

  it("returns the single active legal-tender entry", () => {
    const usage = [active("US")];
    expect(pickHomeCountryUsage(usage)?.countryISO31661Alpha2Code).toBe("US");
  });

  it("prefers active legal-tender over retired entries", () => {
    const usage = [retired("XX", "1999-01-01"), active("US")];
    expect(pickHomeCountryUsage(usage)?.countryISO31661Alpha2Code).toBe("US");
  });

  it("prefers active legal-tender over non-tender entries", () => {
    const usage = [nonTender("ZZ"), active("US")];
    expect(pickHomeCountryUsage(usage)?.countryISO31661Alpha2Code).toBe("US");
  });

  it("returns first active legal-tender when multiple exist", () => {
    const usage = [active("US"), active("CA")];
    expect(pickHomeCountryUsage(usage)?.countryISO31661Alpha2Code).toBe("US");
  });

  it("falls back to first entry with fromDate when no active", () => {
    const usage = [retired("XX", "1999-01-01"), retired("YY", "1980-01-01")];
    // No active tender — picks earliest entry with fromDate (find returns the first match).
    expect(pickHomeCountryUsage(usage)?.countryISO31661Alpha2Code).toBe("XX");
  });

  it("falls back to first entry when nothing has fromDate either", () => {
    const usage: CurrencyUsageEntry[] = [
      {
        countryISO31661Alpha2Code: "ZZ",
        fromDate: null,
        toDate: "1999-01-01",
        isLegalTender: true,
      },
    ];
    expect(pickHomeCountryUsage(usage)?.countryISO31661Alpha2Code).toBe("ZZ");
  });
});
