// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  CountryCode,
  DayOfWeek,
  LanguageCode,
  type LocaleCode,
} from "@dcsv-io/d2-geo-abstractions";
import { describe, expect, it } from "vitest";

import { LocaleLookup } from "../../src/locales.js";

describe("LocaleLookup", () => {
  it("byCode cardinality is at least 700", () => {
    expect(Object.keys(LocaleLookup.byCode).length).toBeGreaterThanOrEqual(700);
  });

  it("all length matches byCode key count", () => {
    expect(LocaleLookup.all.length).toBe(
      Object.keys(LocaleLookup.byCode).length,
    );
  });

  // §1.2 category: Domain-specific — headline pins.
  it("en-US fields pinned", () => {
    const enUS = LocaleLookup.byCode["en-US" as LocaleCode];
    expect(enUS).toBeDefined();
    expect(enUS!.ietfBcp47Tag).toBe("en-US");
    expect(enUS!.languageIso6391Code).toBe(LanguageCode.en);
    expect(enUS!.countryIso31661Alpha2Code).toBe(CountryCode.US);
    expect(enUS!.language).toBeDefined();
    expect(enUS!.language!.iso6391Code).toBe(LanguageCode.en);
    expect(enUS!.country).toBeDefined();
    expect(enUS!.country!.iso31661Alpha2Code).toBe(CountryCode.US);
    expect(enUS!.decimalSeparator).toBe(".");
    expect(enUS!.thousandsSeparator).toBe(",");
    expect(enUS!.isSelectable).toBe(true);
    expect(enUS!.firstDayOfWeek).toBe(DayOfWeek.Sunday);
  });

  it("en-GB nav wired", () => {
    const enGB = LocaleLookup.byCode["en-GB" as LocaleCode];
    expect(enGB!.languageIso6391Code).toBe(LanguageCode.en);
    expect(enGB!.countryIso31661Alpha2Code).toBe(CountryCode.GB);
    expect(enGB!.country!.iso31661Alpha2Code).toBe(CountryCode.GB);
  });

  it("fr-FR nav wired", () => {
    const frFR = LocaleLookup.byCode["fr-FR" as LocaleCode];
    expect(frFR!.languageIso6391Code).toBe(LanguageCode.fr);
    expect(frFR!.countryIso31661Alpha2Code).toBe(CountryCode.FR);
    expect(frFR!.language!.iso6391Code).toBe(LanguageCode.fr);
    expect(frFR!.country!.iso31661Alpha2Code).toBe(CountryCode.FR);
  });

  it("pt-BR nav wired", () => {
    const ptBR = LocaleLookup.byCode["pt-BR" as LocaleCode];
    expect(ptBR!.languageIso6391Code).toBe(LanguageCode.pt);
    expect(ptBR!.countryIso31661Alpha2Code).toBe(CountryCode.BR);
  });

  // §1.2 category: Cross-field — code+nav agreement.
  it("every locale: language nav matches code when set", () => {
    for (const locale of LocaleLookup.all) {
      if (locale.language !== undefined)
        expect(locale.languageIso6391Code).toBe(locale.language.iso6391Code);
    }
  });

  it("every locale: country nav matches code when set", () => {
    for (const locale of LocaleLookup.all) {
      if (locale.country !== undefined)
        expect(locale.countryIso31661Alpha2Code).toBe(
          locale.country.iso31661Alpha2Code,
        );
    }
  });
});
