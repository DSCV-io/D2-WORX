// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  CountryCode,
  LanguageCode,
  type SubdivisionCode,
} from "@dcsv-io/d2-geo-abstractions";
import { describe, expect, it } from "vitest";

import { Countries, CountryLookup } from "../../src/countries.js";
import { GeopoliticalEntityLookup } from "../../src/geopolitical-entities.js";
import { LanguageLookup } from "../../src/languages.js";
import { LocaleLookup } from "../../src/locales.js";
import { SubdivisionLookup } from "../../src/subdivisions.js";
import { initializeGeoData } from "../../src/generated/geo-data-initializer.g.js";

describe("GeoDataInitializer coordinator", () => {
  // §1.2 category: State-lifecycle — module-init side-effect already ran.
  it("post-init: every Country has a wired primaryLanguage OR null code", () => {
    for (const country of CountryLookup.all) {
      if (country.primaryLanguageIso6391Code === undefined)
        expect(country.primaryLanguage).toBeUndefined();
    }
  });

  it("post-init: AQ, BV, HM all have undefined primaryLanguage", () => {
    expect(Countries.AQ.primaryLanguage).toBeUndefined();
    expect(Countries.BV.primaryLanguage).toBeUndefined();
    expect(Countries.HM.primaryLanguage).toBeUndefined();
  });

  it("post-init: AQ has undefined primaryCurrency (BV/HM carry sovereign-country currency)", () => {
    // AQ (Antarctica) has no primary currency per spec. BV and HM carry
    // sovereign-country currency (NOK from Norway / AUD from Australia)
    // even though they have no resident population.
    expect(Countries.AQ.primaryCurrency).toBeUndefined();
  });

  it("post-init: AQ, BV, HM all have undefined primaryLocale", () => {
    expect(Countries.AQ.primaryLocale).toBeUndefined();
    expect(Countries.BV.primaryLocale).toBeUndefined();
    expect(Countries.HM.primaryLocale).toBeUndefined();
  });

  it("post-init: every subdivision's country nav matches its country code", () => {
    for (const sub of SubdivisionLookup.all) {
      expect(sub.country).toBeDefined();
      expect(sub.country!.iso31661Alpha2Code).toBe(
        sub.countryIso31661Alpha2Code,
      );
    }
  });

  it("post-init: every locale's nav refs agree with its codes", () => {
    for (const locale of LocaleLookup.all) {
      if (locale.language !== undefined)
        expect(locale.languageIso6391Code).toBe(locale.language.iso6391Code);
      if (locale.country !== undefined)
        expect(locale.countryIso31661Alpha2Code).toBe(
          locale.country.iso31661Alpha2Code,
        );
    }
  });

  it("post-init: every geopolitical entity's member nav matches code set", () => {
    for (const entity of GeopoliticalEntityLookup.all) {
      expect(entity.memberCountries.length).toBe(
        entity.memberCountryIso31661Alpha2Codes.size,
      );
    }
  });

  it("post-init: every language's SpokenInCountries nav matches code set", () => {
    for (const lang of LanguageLookup.all) {
      expect(lang.spokenInCountries.length).toBe(
        lang.spokenInCountryIso31661Alpha2Codes.size,
      );
    }
  });

  // §1.2 category: State-lifecycle — idempotent re-invocation.
  it("initializeGeoData: calling twice does not throw or rebuild state", () => {
    const usBefore = CountryLookup.byCode[CountryCode.US];
    const enBefore = LanguageLookup.byCode[LanguageCode.en];
    const nyBefore = SubdivisionLookup.byCode["US-NY" as SubdivisionCode];

    expect(() => initializeGeoData()).not.toThrow();
    expect(() => initializeGeoData()).not.toThrow();

    expect(CountryLookup.byCode[CountryCode.US]).toBe(usBefore);
    expect(LanguageLookup.byCode[LanguageCode.en]).toBe(enBefore);
    expect(SubdivisionLookup.byCode["US-NY" as SubdivisionCode]).toBe(nyBefore);
  });

  it("post-init: every Country.subdivisions count matches byCountry index", () => {
    for (const country of CountryLookup.all) {
      const indexEntry =
        SubdivisionLookup.byCountry[country.iso31661Alpha2Code] ?? [];
      expect(country.subdivisions.length).toBe(indexEntry.length);
    }
  });
});
