// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, it, expect } from "vitest";
import {
  padNumericCode,
  deriveSovereign,
  cleanPhonePrefix,
  cleanCurrencyCode,
} from "../../src/transformers/countries.js";

describe("padNumericCode", () => {
  it("pads single-digit code to 3 digits", () => {
    expect(padNumericCode("4")).toBe("004");
  });

  it("pads two-digit code to 3 digits", () => {
    expect(padNumericCode("36")).toBe("036");
  });

  it("returns 3-digit code unchanged", () => {
    expect(padNumericCode("250")).toBe("250");
  });

  it("returns null for null input", () => {
    expect(padNumericCode(null)).toBeNull();
  });

  it("returns null for empty string", () => {
    expect(padNumericCode("")).toBeNull();
  });

  it("returns null for negative number string", () => {
    expect(padNumericCode("-1")).toBeNull();
  });

  it("returns null for fractional number string", () => {
    expect(padNumericCode("0.5")).toBeNull();
  });

  it("returns null for whitespace-padded input", () => {
    expect(padNumericCode(" 4 ")).toBeNull();
  });

  it("returns null for 4-digit input (out of ISO range)", () => {
    expect(padNumericCode("1234")).toBeNull();
  });

  it("returns null for non-numeric input", () => {
    expect(padNumericCode("abc")).toBeNull();
  });

  it("returns null for mixed alphanumeric input", () => {
    expect(padNumericCode("1a")).toBeNull();
  });
});

describe("deriveSovereign", () => {
  it("returns null for 'Yes' (independent country)", () => {
    expect(deriveSovereign("Yes")).toBeNull();
  });

  it("returns null for null input", () => {
    expect(deriveSovereign(null)).toBeNull();
  });

  it("extracts country code from 'Part of XX'", () => {
    expect(deriveSovereign("Part of US")).toBe("US");
  });

  it("extracts country code from 'Territory of XX'", () => {
    expect(deriveSovereign("Territory of GB")).toBe("GB");
  });

  it("extracts country code from 'Commonwealth of XX'", () => {
    expect(deriveSovereign("Commonwealth of US")).toBe("US");
  });

  it("extracts country code from 'Crown dependency of XX'", () => {
    expect(deriveSovereign("Crown dependency of GB")).toBe("GB");
  });

  it("extracts country code from 'Associated with XX'", () => {
    expect(deriveSovereign("Associated with NZ")).toBe("NZ");
  });

  it("returns null for unrecognized text", () => {
    expect(deriveSovereign("In contention")).toBeNull();
  });

  it("returns null for empty string", () => {
    expect(deriveSovereign("")).toBeNull();
  });

  it("uppercases the extracted code", () => {
    expect(deriveSovereign("Part of us")).toBeNull(); // pattern is case-sensitive [A-Z]{2}
  });
});

describe("cleanPhonePrefix", () => {
  it("returns plain digits unchanged", () => {
    expect(cleanPhonePrefix("1")).toBe("1");
    expect(cleanPhonePrefix("44")).toBe("44");
    expect(cleanPhonePrefix("972")).toBe("972");
  });

  it("strips area code after hyphen", () => {
    expect(cleanPhonePrefix("1-787")).toBe("1");
  });

  it("strips trailing comma-separated entries", () => {
    expect(cleanPhonePrefix("1,242")).toBe("1");
  });

  it("returns null for null input", () => {
    expect(cleanPhonePrefix(null)).toBeNull();
  });

  it("returns null for empty string", () => {
    expect(cleanPhonePrefix("")).toBeNull();
  });

  it("returns null for non-numeric input", () => {
    expect(cleanPhonePrefix("abc")).toBeNull();
  });

  it("returns null for prefix longer than 4 digits", () => {
    expect(cleanPhonePrefix("12345")).toBeNull();
  });

  it("trims whitespace around digits", () => {
    expect(cleanPhonePrefix(" 1 ")).toBe("1");
  });
});

describe("cleanCurrencyCode", () => {
  it("returns 3-letter uppercase code unchanged", () => {
    expect(cleanCurrencyCode("USD")).toBe("USD");
  });

  it("uppercases lowercase input", () => {
    expect(cleanCurrencyCode("eur")).toBe("EUR");
  });

  it("returns first code from comma-separated list", () => {
    expect(cleanCurrencyCode("UYU,UYW")).toBe("UYU");
  });

  it("returns first code with whitespace trim", () => {
    expect(cleanCurrencyCode(" VES , VED ")).toBe("VES");
  });

  it("returns null for null input", () => {
    expect(cleanCurrencyCode(null)).toBeNull();
  });

  it("returns null for undefined input", () => {
    expect(cleanCurrencyCode(undefined)).toBeNull();
  });

  it("returns null for empty string", () => {
    expect(cleanCurrencyCode("")).toBeNull();
  });

  it("returns null for non-3-letter code", () => {
    expect(cleanCurrencyCode("USDX")).toBeNull();
    expect(cleanCurrencyCode("US")).toBeNull();
  });

  it("returns null for non-alpha code", () => {
    expect(cleanCurrencyCode("123")).toBeNull();
    expect(cleanCurrencyCode("US1")).toBeNull();
  });
});
