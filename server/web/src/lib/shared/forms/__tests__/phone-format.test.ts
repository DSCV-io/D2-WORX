import { describe, it, expect } from "vitest";
import {
  formatPhoneDisplay,
  formatPhoneAsYouType,
  getCountryCallingCode,
  parsePhone,
} from "../phone-format.js";

describe("formatPhoneDisplay", () => {
  it("formats US number", () => {
    const result = formatPhoneDisplay("+12025551234", "US");
    expect(result).toBe("(202) 555-1234");
  });

  it("formats GB number", () => {
    const result = formatPhoneDisplay("+442071234567", "GB");
    expect(result).toContain("20 7123 4567");
  });

  it("returns original string on invalid number", () => {
    const result = formatPhoneDisplay("invalid", "US");
    expect(result).toBe("invalid");
  });
});

describe("formatPhoneAsYouType", () => {
  it("progressively formats US number", () => {
    const result = formatPhoneAsYouType("2025551234", "US");
    expect(result.formatted).toContain("(202)");
    expect(result.national).toBe("2025551234");
  });

  it("handles partial input", () => {
    const result = formatPhoneAsYouType("555", "US");
    expect(result.formatted).toBeTruthy();
  });

  it("handles empty input", () => {
    const result = formatPhoneAsYouType("", "US");
    expect(result.formatted).toBe("");
  });

  it("formats CA number (same as US)", () => {
    const result = formatPhoneAsYouType("4165551234", "CA");
    expect(result.formatted).toContain("(416)");
  });
});

describe("getCountryCallingCode", () => {
  it("returns US code", () => {
    expect(getCountryCallingCode("US")).toBe("+1");
  });

  it("returns GB code", () => {
    expect(getCountryCallingCode("GB")).toBe("+44");
  });

  it("returns DE code", () => {
    expect(getCountryCallingCode("DE")).toBe("+49");
  });

  it("returns JP code", () => {
    expect(getCountryCallingCode("JP")).toBe("+81");
  });

  it("returns empty for invalid code", () => {
    expect(getCountryCallingCode("XX")).toBe("");
  });
});

describe("parsePhone", () => {
  it("parses valid US number", () => {
    const result = parsePhone("+12025551234");
    expect(result).not.toBeNull();
    expect(result!.countryCode).toBe("US");
    expect(result!.nationalNumber).toBe("2025551234");
    expect(result!.e164).toBe("+12025551234");
  });

  it("parses number with default country", () => {
    const result = parsePhone("2025551234", "US");
    expect(result).not.toBeNull();
    expect(result!.e164).toBe("+12025551234");
  });

  it("returns null for invalid input", () => {
    expect(parsePhone("invalid")).toBeNull();
  });

  it("returns null for empty string", () => {
    expect(parsePhone("")).toBeNull();
  });
});
