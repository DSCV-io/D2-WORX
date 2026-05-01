import { describe, it, expect } from "vitest";
import {
  FIRST_NAME,
  LAST_NAME,
  EMAIL,
  PHONE,
  STREET1,
  STREET2,
  STREET3,
  CITY,
  POSTAL_CODE,
  PASSWORD,
  CONFIRM_EMAIL,
  CONFIRM_PASSWORD,
  COUNTRY,
  STATE,
  type FieldPreset,
  type ComboboxPreset,
} from "../field-presets.js";

const inputPresets: [string, FieldPreset][] = [
  ["FIRST_NAME", FIRST_NAME],
  ["LAST_NAME", LAST_NAME],
  ["EMAIL", EMAIL],
  ["PHONE", PHONE],
  ["STREET1", STREET1],
  ["STREET2", STREET2],
  ["STREET3", STREET3],
  ["CITY", CITY],
  ["POSTAL_CODE", POSTAL_CODE],
  ["PASSWORD", PASSWORD],
  ["CONFIRM_EMAIL", CONFIRM_EMAIL],
  ["CONFIRM_PASSWORD", CONFIRM_PASSWORD],
];

const comboboxPresets: [string, ComboboxPreset][] = [
  ["COUNTRY", COUNTRY],
  ["STATE", STATE],
];

describe("field presets — all presets have required properties", () => {
  it.each(inputPresets)("%s has a non-empty label", (_name, preset) => {
    expect(preset.label.trim().length).toBeGreaterThan(0);
  });

  it.each(inputPresets)(
    "%s has a non-empty placeholder or empty string for phone",
    (_name, preset) => {
      expect(typeof preset.placeholder).toBe("string");
    },
  );

  it.each(comboboxPresets)("%s has a non-empty label", (_name, preset) => {
    expect(preset.label.trim().length).toBeGreaterThan(0);
  });

  it.each(comboboxPresets)("%s has a non-empty placeholder", (_name, preset) => {
    expect(preset.placeholder.trim().length).toBeGreaterThan(0);
  });
});

describe("field presets — specific defaults", () => {
  it("FIRST_NAME has correct label and placeholder", () => {
    expect(FIRST_NAME.label).toBe("First Name");
    expect(FIRST_NAME.placeholder).toBe("First");
    expect(FIRST_NAME.type).toBeUndefined();
  });

  it("LAST_NAME has correct label and placeholder", () => {
    expect(LAST_NAME.label).toBe("Last Name");
    expect(LAST_NAME.placeholder).toBe("Last");
  });

  it("EMAIL has type=email", () => {
    expect(EMAIL.type).toBe("email");
  });

  it("PHONE has description about country code", () => {
    expect(PHONE.description).toContain("country code");
  });

  it("POSTAL_CODE label is ZIP / Postal Code", () => {
    expect(POSTAL_CODE.label).toBe("ZIP / Postal Code");
  });

  it("POSTAL_CODE has description about country format", () => {
    expect(POSTAL_CODE.description).toContain("country format");
  });

  it("COUNTRY has searchPlaceholder and emptyMessage", () => {
    expect(COUNTRY.searchPlaceholder).toBe("Search countries...");
    expect(COUNTRY.emptyMessage).toBe("No country found.");
  });

  it("STATE has searchPlaceholder and emptyMessage", () => {
    expect(STATE.searchPlaceholder).toBe("Search states...");
    expect(STATE.emptyMessage).toBe("No state found.");
  });

  it("STREET1 label is Street Address", () => {
    expect(STREET1.label).toBe("Street Address");
  });

  it("STREET2 label is Address Line 2", () => {
    expect(STREET2.label).toBe("Address Line 2");
  });

  it("STREET3 label is Address Line 3", () => {
    expect(STREET3.label).toBe("Address Line 3");
  });

  it("PASSWORD has type=password", () => {
    expect(PASSWORD.type).toBe("password");
    expect(PASSWORD.label).toBe("Password");
    expect(PASSWORD.placeholder).toBe("Enter password");
  });

  it("CONFIRM_EMAIL has type=email", () => {
    expect(CONFIRM_EMAIL.type).toBe("email");
    expect(CONFIRM_EMAIL.label).toBe("Confirm Email");
    expect(CONFIRM_EMAIL.placeholder).toBe("Re-enter your email");
  });

  it("CONFIRM_PASSWORD has type=password", () => {
    expect(CONFIRM_PASSWORD.type).toBe("password");
    expect(CONFIRM_PASSWORD.label).toBe("Confirm Password");
    expect(CONFIRM_PASSWORD.placeholder).toBe("Re-enter your password");
  });
});
