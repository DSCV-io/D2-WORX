// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  CountryCode,
  CurrencyCode,
  LanguageCode,
  MeasurementSystem,
} from "@d2/geo-abstractions";
import { describe, expect, it } from "vitest";

import { Countries, CountryLookup } from "../../src/countries.js";
import { SubdivisionLookup } from "../../src/subdivisions.js";

describe("CountryLookup", () => {
  it("ByCode entry for every CountryCode enum member", () => {
    for (const code of Object.values(CountryCode)) {
      const record = CountryLookup.byCode[code];
      expect(record, `missing record for ${code}`).toBeDefined();
      expect(record!.iso31661Alpha2Code).toBe(code);
    }
  });

  it("ByCode cardinality is at least 249", () => {
    expect(Object.keys(CountryLookup.byCode).length).toBeGreaterThanOrEqual(
      249,
    );
  });

  it("all length matches byCode key count", () => {
    expect(CountryLookup.all.length).toBe(
      Object.keys(CountryLookup.byCode).length,
    );
  });

  it("byIso31661Alpha2 string lookup returns same record as byCode", () => {
    const byCode = CountryLookup.byCode[CountryCode.US];
    const byStr = CountryLookup.byIso31661Alpha2["US"];
    expect(byStr).toBe(byCode);
  });

  it("byIso31661Alpha3 string lookup returns same record as byCode", () => {
    const byCode = CountryLookup.byCode[CountryCode.US];
    const byStr = CountryLookup.byIso31661Alpha3["USA"];
    expect(byStr).toBe(byCode);
  });

  // §1.2 category: Domain-specific — per-VALUE pins for headline countries.
  it("US: all required fields pinned", () => {
    const us = Countries.US;
    expect(us.iso31661Alpha2Code).toBe(CountryCode.US);
    expect(us.iso31661Alpha3Code).toBe("USA");
    expect(us.iso31661NumericCode).toBe("840");
    expect(us.displayName).toBe("United States");
    expect(us.officialName).toBe("United States of America");
    expect(us.phoneNumberPrefix).toBe("1");
    expect(us.measurementSystem).toBe(MeasurementSystem.Imperial);
    expect(us.primaryLanguageIso6391Code).toBe(LanguageCode.en);
    expect(us.primaryCurrencyIso4217AlphaCode).toBe(CurrencyCode.USD);
  });

  it("US: primary nav refs wired", () => {
    const us = Countries.US;
    expect(us.primaryLanguage).toBeDefined();
    expect(us.primaryLanguage!.iso6391Code).toBe(LanguageCode.en);
    expect(us.primaryCurrency).toBeDefined();
    expect(us.primaryCurrency!.iso4217AlphaCode).toBe(CurrencyCode.USD);
    expect(us.primaryLocale).toBeDefined();
    expect(us.primaryLocale!.ietfBcp47Tag).toBe("en-US");
  });

  it("US: territories contain PR (Puerto Rico)", () => {
    expect(
      Countries.US.territoryIso31661Alpha2Codes.has(
        CountryCode.PR as CountryCode,
      ),
    ).toBe(true);
    expect(
      Countries.US.territories.some(
        (t) => t.iso31661Alpha2Code === CountryCode.PR,
      ),
    ).toBe(true);
  });

  it("US: subdivisions count matches the SubdivisionLookup.byCountry index", () => {
    const byCountry = SubdivisionLookup.byCountry[CountryCode.US] ?? [];
    expect(Countries.US.subdivisions.length).toBe(byCountry.length);
    expect(Countries.US.subdivisions.length).toBeGreaterThanOrEqual(50);
  });

  it("JP: all required fields pinned", () => {
    const jp = Countries.JP;
    expect(jp.iso31661Alpha2Code).toBe(CountryCode.JP);
    expect(jp.iso31661Alpha3Code).toBe("JPN");
    expect(jp.primaryLanguageIso6391Code).toBe(LanguageCode.ja);
    expect(jp.primaryCurrencyIso4217AlphaCode).toBe(CurrencyCode.JPY);
  });

  it("GB: headline fields pinned", () => {
    const gb = Countries.GB;
    expect(gb.iso31661Alpha2Code).toBe(CountryCode.GB);
    expect(gb.iso31661Alpha3Code).toBe("GBR");
    expect(gb.primaryCurrencyIso4217AlphaCode).toBe(CurrencyCode.GBP);
  });

  // §1.2 category: Domain-specific — uninhabited territories.
  it("AQ: primaries are undefined", () => {
    const aq = Countries.AQ;
    expect(aq.primaryLanguageIso6391Code).toBeUndefined();
    expect(aq.primaryLanguage).toBeUndefined();
    expect(aq.primaryCurrencyIso4217AlphaCode).toBeUndefined();
    expect(aq.primaryCurrency).toBeUndefined();
    expect(aq.primaryLocaleIetfBcp47Tag).toBeUndefined();
    expect(aq.primaryLocale).toBeUndefined();
  });

  it("BV: primaryLanguage and primaryLocale are undefined (currency carries over from NO)", () => {
    const bv = Countries.BV;
    expect(bv.primaryLanguage).toBeUndefined();
    expect(bv.primaryLocale).toBeUndefined();
  });

  it("HM: primaryLanguage and primaryLocale are undefined (currency carries over from AU)", () => {
    const hm = Countries.HM;
    expect(hm.primaryLanguage).toBeUndefined();
    expect(hm.primaryLocale).toBeUndefined();
  });

  it("AQ: no subdivisions", () => {
    expect(Countries.AQ.subdivisions.length).toBe(0);
  });

  // §1.2 category: Cross-field — nav rep + code rep agreement.
  it("every Country: nav PrimaryLanguage matches code when set", () => {
    for (const country of CountryLookup.all) {
      if (country.primaryLanguage !== undefined) {
        expect(country.primaryLanguageIso6391Code).toBe(
          country.primaryLanguage.iso6391Code,
        );
      }
    }
  });

  it("every Country: currencies count matches code count", () => {
    for (const country of CountryLookup.all) {
      expect(country.currencies.length).toBe(
        country.currencyIso4217AlphaCodes.size,
      );
    }
  });

  it("US: first currency acceptance is USD with dual-rep consistency", () => {
    const us = Countries.US;
    const first = us.currencies[0]!;
    expect(first.iso4217AlphaCode).toBe(CurrencyCode.USD);
    expect(first.currency).toBeDefined();
    expect(first.currency!.iso4217AlphaCode).toBe(CurrencyCode.USD);
  });
});
