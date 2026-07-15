// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { CountryCode, CurrencyCode } from "@d2/geo-abstractions";
import { describe, expect, it } from "vitest";

import { Currencies, CurrencyLookup } from "../../src/currencies.js";

describe("CurrencyLookup", () => {
  it("byCode entry for every CurrencyCode enum member", () => {
    for (const code of Object.values(CurrencyCode)) {
      const record = CurrencyLookup.byCode[code];
      expect(record, `missing record for ${code}`).toBeDefined();
      expect(record!.iso4217AlphaCode).toBe(code);
    }
  });

  it("byCode cardinality is at least 175", () => {
    expect(Object.keys(CurrencyLookup.byCode).length).toBeGreaterThanOrEqual(
      175,
    );
  });

  // §1.2 category: Domain-specific — per-VALUE pins.
  it("USD fields pinned", () => {
    const usd = Currencies.USD;
    expect(usd.iso4217AlphaCode).toBe(CurrencyCode.USD);
    expect(usd.iso4217NumericCode).toBe("840");
    expect(usd.displayName).toBe("US Dollar");
    expect(usd.decimalPlaces).toBe(2);
    expect(usd.symbol).toBe("$");
    expect(usd.isSupported).toBe(true);
  });

  it("EUR fields pinned", () => {
    const eur = Currencies.EUR;
    expect(eur.iso4217AlphaCode).toBe(CurrencyCode.EUR);
    expect(eur.decimalPlaces).toBe(2);
    expect(eur.symbol).toBe("€");
  });

  it("JPY: zero-decimal currency", () => {
    expect(Currencies.JPY.decimalPlaces).toBe(0);
  });

  it("GBP fields pinned", () => {
    const gbp = Currencies.GBP;
    expect(gbp.symbol).toBe("£");
    expect(gbp.decimalPlaces).toBe(2);
  });

  // §1.2 category: Cross-field — reverse-FK nav populated.
  it("USD: AcceptedInCountries contains US", () => {
    expect(
      Currencies.USD.acceptedInCountryIso31661Alpha2Codes.has(
        CountryCode.US as CountryCode,
      ),
    ).toBe(true);
    expect(
      Currencies.USD.acceptedInCountries.some(
        (c) => c.iso31661Alpha2Code === CountryCode.US,
      ),
    ).toBe(true);
  });

  it("EUR: AcceptedInCountries contains FR + DE", () => {
    const eur = Currencies.EUR;
    expect(
      eur.acceptedInCountryIso31661Alpha2Codes.has(
        CountryCode.FR as CountryCode,
      ),
    ).toBe(true);
    expect(
      eur.acceptedInCountryIso31661Alpha2Codes.has(
        CountryCode.DE as CountryCode,
      ),
    ).toBe(true);
  });

  it("JPY: AcceptedInCountries contains JP", () => {
    expect(
      Currencies.JPY.acceptedInCountryIso31661Alpha2Codes.has(
        CountryCode.JP as CountryCode,
      ),
    ).toBe(true);
  });

  it("every currency: AcceptedInCountries count matches code set count", () => {
    for (const c of CurrencyLookup.all) {
      expect(c.acceptedInCountries.length).toBe(
        c.acceptedInCountryIso31661Alpha2Codes.size,
      );
    }
  });

  it("every currency: displayName non-empty", () => {
    for (const c of Object.values(CurrencyLookup.byCode))
      expect(c.displayName.length).toBeGreaterThan(0);
  });
});
