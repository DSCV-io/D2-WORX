// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  CountryCode,
  LanguageCode,
  WritingDirection,
} from "@d2/geo-abstractions";
import { describe, expect, it } from "vitest";

import { Languages, LanguageLookup } from "../../src/languages.js";

describe("LanguageLookup", () => {
  it("byCode entry for every LanguageCode enum member", () => {
    for (const code of Object.values(LanguageCode)) {
      const record = LanguageLookup.byCode[code];
      expect(record, `missing record for ${code}`).toBeDefined();
      expect(record!.iso6391Code).toBe(code);
    }
  });

  it("byCode cardinality is at least 175", () => {
    expect(Object.keys(LanguageLookup.byCode).length).toBeGreaterThanOrEqual(
      175,
    );
  });

  // §1.2 category: Domain-specific — per-VALUE pins.
  it("en (English) fields pinned", () => {
    const en = Languages.en;
    expect(en.iso6391Code).toBe(LanguageCode.en);
    expect(en.displayName).toBe("English");
    expect(en.writingDirection).toBe(WritingDirection.LTR);
    expect(en.isSupported).toBe(true);
  });

  it("es (Spanish) fields pinned", () => {
    const es = Languages.es;
    expect(es.iso6391Code).toBe(LanguageCode.es);
    expect(es.displayName).toContain("Spanish");
    expect(es.writingDirection).toBe(WritingDirection.LTR);
  });

  it("fr (French) fields pinned", () => {
    expect(Languages.fr.iso6391Code).toBe(LanguageCode.fr);
    expect(Languages.fr.displayName).toBe("French");
  });

  it("ja (Japanese) LTR", () => {
    expect(Languages.ja.writingDirection).toBe(WritingDirection.LTR);
  });

  it("ar (Arabic) is RTL", () => {
    const ar = Languages.ar;
    expect(ar.iso6391Code).toBe(LanguageCode.ar);
    expect(ar.writingDirection).toBe(WritingDirection.RTL);
  });

  it("he (Hebrew) is RTL", () => {
    expect(Languages.he.writingDirection).toBe(WritingDirection.RTL);
  });

  // §1.2 category: Cross-field — reverse-FK nav populated.
  it("en SpokenInCountries contains GB + US + AU", () => {
    const en = Languages.en;
    expect(
      en.spokenInCountryIso31661Alpha2Codes.has(CountryCode.GB as CountryCode),
    ).toBe(true);
    expect(
      en.spokenInCountryIso31661Alpha2Codes.has(CountryCode.US as CountryCode),
    ).toBe(true);
    expect(
      en.spokenInCountryIso31661Alpha2Codes.has(CountryCode.AU as CountryCode),
    ).toBe(true);
    expect(en.spokenInCountries.length).toBeGreaterThanOrEqual(3);
  });

  it("es SpokenInCountries contains ES + MX", () => {
    const es = Languages.es;
    expect(
      es.spokenInCountryIso31661Alpha2Codes.has(CountryCode.ES as CountryCode),
    ).toBe(true);
    expect(
      es.spokenInCountryIso31661Alpha2Codes.has(CountryCode.MX as CountryCode),
    ).toBe(true);
  });

  it("every language: SpokenInCountries length matches code set size", () => {
    for (const lang of LanguageLookup.all) {
      expect(lang.spokenInCountries.length).toBe(
        lang.spokenInCountryIso31661Alpha2Codes.size,
      );
    }
  });
});
