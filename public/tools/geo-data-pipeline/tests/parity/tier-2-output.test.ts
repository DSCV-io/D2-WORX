// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { existsSync } from "node:fs";
import { readFile, stat } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it, beforeAll } from "vitest";

/**
 * Cross-catalog parity test suite for Tier 2 output (public/contracts/geo/*.spec.json).
 *
 * Goals:
 *   1. Schema-shape sanity — every generated file has the expected wrapper + entries
 *   2. Cross-catalog FK integrity — every referenced ID exists in its target catalog
 *   3. M:M inverse-nav symmetry — Country.timezoneIanaIdentifiers ↔ Timezone.country*
 *   4. Denormalization integrity —
 *      Locale.firstDayOfWeek === Country[locale.country].firstDayOfWeek
 *   5. Derived-flag consistency — IsSelectable / IsSupported derivations match selectable set
 *   6. Encoding integrity — NBSP and other invisibles preserved as escaped \uXXXX
 *      (not normalized away)
 *   7. Hand-rolled GE: every referenced country exists in countries catalog (Tier 2
 *      runtime can't enforce this since GE precedes Country)
 *
 * These tests are the DRIFT GUARD for the denormalized data model. If a future Tier 2
 * change introduces drift between catalogs, these tests fail loudly.
 */

function locateGeoDir(): string {
  let dir = dirname(fileURLToPath(import.meta.url));
  for (let i = 0; i < 12; i++) {
    const candidate = join(dir, "public", "contracts", "geo");
    if (existsSync(candidate)) return candidate;
    const parent = resolve(dir, "..");
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error(
    "could not locate public/contracts/geo from " +
      dirname(fileURLToPath(import.meta.url)),
  );
}

const GEO_DIR = locateGeoDir();
// REPO_ROOT is three levels up from public/contracts/geo
const REPO_ROOT = resolve(GEO_DIR, "..", "..", "..");

interface SpecFile<T> {
  $generated: boolean;
  $source: string;
  entries: T[];
}

interface Country {
  iso31661Alpha2Code: string;
  firstDayOfWeek: string;
  weekendStart: string;
  weekendEnd: string;
  measurementSystem: string;
  primaryLanguageISO6391Code: string | null;
  primaryCurrencyISO4217AlphaCode: string | null;
  primaryLocaleIETFBCP47Tag: string | null;
  geopoliticalEntityShortCodes: string[];
  subdivisionISO31662Codes: string[];
  timezoneIanaIdentifiers: string[];
  localeIETFBCP47Tags: string[];
  spokenLanguageISO6391Codes: string[];
  territoryISO31661Alpha2Codes: string[];
  currencies: Array<{ iso4217AlphaCode: string; level: string }>;
}

interface Subdivision {
  iso31662Code: string;
  countryISO31661Alpha2Code: string;
}

interface Currency {
  iso4217AlphaCode: string;
  isSupported: boolean;
}

interface Language {
  iso6391Code: string;
  writingDirection: string;
  isSupported: boolean;
  spokenInCountryISO31661Alpha2Codes: string[];
}

interface Locale {
  ietfBcp47Tag: string;
  languageISO6391Code: string;
  countryISO31661Alpha2Code: string | null;
  isSelectable: boolean;
  firstDayOfWeek: string;
  decimalSeparator: string;
  thousandsSeparator: string;
  dateFormatPattern: string;
}

interface Timezone {
  ianaIdentifier: string;
  countryISO31661Alpha2Code: string | null;
  coApplicableCountryISO31661Alpha2Codes: string[];
}

interface GeopoliticalEntity {
  shortCode: string;
  countryISO31661Alpha2Codes: string[];
}

let countries: Country[];
let countriesByCode: Map<string, Country>;
let subdivisions: Subdivision[];
let currencies: Currency[];
let languages: Language[];
let languagesByCode: Map<string, Language>;
let locales: Locale[];
let localesByTag: Map<string, Locale>;
let timezones: Timezone[];
let timezonesById: Map<string, Timezone>;
let ges: GeopoliticalEntity[];

beforeAll(async () => {
  const [
    countriesFile,
    subdivisionsFile,
    currenciesFile,
    languagesFile,
    localesFile,
    timezonesFile,
    gesFile,
  ] = await Promise.all([
    readJson<SpecFile<Country>>("countries.spec.json"),
    readJson<SpecFile<Subdivision>>("subdivisions.spec.json"),
    readJson<SpecFile<Currency>>("currencies.spec.json"),
    readJson<SpecFile<Language>>("languages.spec.json"),
    readJson<SpecFile<Locale>>("locales.spec.json"),
    readJson<SpecFile<Timezone>>("timezones.spec.json"),
    readJson<SpecFile<GeopoliticalEntity>>("geopolitical-entities.spec.json"),
  ]);
  countries = countriesFile.entries;
  countriesByCode = new Map(countries.map((c) => [c.iso31661Alpha2Code, c]));
  subdivisions = subdivisionsFile.entries;
  currencies = currenciesFile.entries;
  languages = languagesFile.entries;
  languagesByCode = new Map(languages.map((l) => [l.iso6391Code, l]));
  locales = localesFile.entries;
  localesByTag = new Map(locales.map((l) => [l.ietfBcp47Tag, l]));
  timezones = timezonesFile.entries;
  timezonesById = new Map(timezones.map((t) => [t.ianaIdentifier, t]));
  ges = gesFile.entries;
});

async function readJson<T>(filename: string): Promise<T> {
  return JSON.parse(await readFile(resolve(GEO_DIR, filename), "utf8")) as T;
}

// -------------------------------------------------------------------------
// 1. Schema-shape sanity
// -------------------------------------------------------------------------

describe("Tier 2 file shape", () => {
  it("all 6 generated catalogs have $generated=true + $source=pipeline-derived", async () => {
    const files = [
      "countries",
      "subdivisions",
      "currencies",
      "languages",
      "locales",
      "timezones",
    ];
    for (const f of files) {
      const data = await readJson<{ $generated: boolean; $source: string }>(
        `${f}.spec.json`,
      );
      expect(data.$generated).toBe(true);
      expect(data.$source).toBe("pipeline-derived");
    }
  });

  it("hand-rolled geopolitical-entities has $generated=false + $source=manual", async () => {
    const data = await readJson<{ $generated: boolean; $source: string }>(
      "geopolitical-entities.spec.json",
    );
    expect(data.$generated).toBe(false);
    expect(data.$source).toBe("manual");
  });
});

// -------------------------------------------------------------------------
// 2. Cross-catalog FK integrity
// -------------------------------------------------------------------------

describe("Cross-catalog FK integrity", () => {
  it("Subdivision.countryISO31661Alpha2Code references a real country", () => {
    const orphans = subdivisions.filter(
      (s) => !countriesByCode.has(s.countryISO31661Alpha2Code),
    );
    expect(orphans, JSON.stringify(orphans.slice(0, 5))).toEqual([]);
  });

  it("Country.subdivisionISO31662Codes references real subdivisions", () => {
    const validSubdivisions = new Set(subdivisions.map((s) => s.iso31662Code));
    const orphans: string[] = [];
    for (const c of countries) {
      for (const code of c.subdivisionISO31662Codes) {
        if (!validSubdivisions.has(code))
          orphans.push(`${c.iso31661Alpha2Code}->${code}`);
      }
    }
    expect(orphans).toEqual([]);
  });

  it("Country.timezoneIanaIdentifiers references real timezones", () => {
    const orphans: string[] = [];
    for (const c of countries) {
      for (const id of c.timezoneIanaIdentifiers) {
        if (!timezonesById.has(id))
          orphans.push(`${c.iso31661Alpha2Code}->${id}`);
      }
    }
    expect(orphans).toEqual([]);
  });

  it("Country.localeIETFBCP47Tags references real locales", () => {
    const orphans: string[] = [];
    for (const c of countries) {
      for (const tag of c.localeIETFBCP47Tags) {
        if (!localesByTag.has(tag))
          orphans.push(`${c.iso31661Alpha2Code}->${tag}`);
      }
    }
    expect(orphans).toEqual([]);
  });

  it("Country.primaryLanguageISO6391Code references a real language (when 2-letter)", () => {
    // CLDR territoryInfo carries ISO 639-3 fallback codes (fil, cmn, tet, niu, pau, tkl,
    // tvl, wls) for small-population countries whose primary language has no ISO 639-1
    // assignment. Our Language catalog ships ISO 639-1 only, so 639-3 entries here are
    // expected orphans — Tier 2 / consumers can decide how to handle them.
    const orphans = countries.filter(
      (c) =>
        c.primaryLanguageISO6391Code !== null &&
        /^[a-z]{2}$/.test(c.primaryLanguageISO6391Code) &&
        !languagesByCode.has(c.primaryLanguageISO6391Code),
    );
    expect(
      orphans.map(
        (c) => `${c.iso31661Alpha2Code}->${c.primaryLanguageISO6391Code}`,
      ),
    ).toEqual([]);
  });

  it("Country.territoryISO31661Alpha2Codes references real countries", () => {
    const orphans: string[] = [];
    for (const c of countries) {
      for (const code of c.territoryISO31661Alpha2Codes) {
        if (!countriesByCode.has(code))
          orphans.push(`${c.iso31661Alpha2Code}->${code}`);
      }
    }
    expect(orphans).toEqual([]);
  });

  it("Locale.countryISO31661Alpha2Code (when set) references a real country", () => {
    const orphans = locales.filter(
      (l) =>
        l.countryISO31661Alpha2Code !== null &&
        !countriesByCode.has(l.countryISO31661Alpha2Code),
    );
    expect(
      orphans.map((l) => `${l.ietfBcp47Tag}->${l.countryISO31661Alpha2Code}`),
    ).toEqual([]);
  });

  it("Locale.languageISO6391Code references a real language (when 2-letter)", () => {
    // Locales can carry ISO 639-3 lang subtags ("yue", "fil", "fur", etc.) that aren't in our
    // ISO 639-1-only Language catalog; only check 2-letter subtags.
    const orphans = locales.filter(
      (l) =>
        /^[a-z]{2}$/.test(l.languageISO6391Code) &&
        !languagesByCode.has(l.languageISO6391Code),
    );
    expect(
      orphans.map((l) => `${l.ietfBcp47Tag}->${l.languageISO6391Code}`),
    ).toEqual([]);
  });

  it("Timezone.countryISO31661Alpha2Code (when set) references a real country", () => {
    const orphans = timezones.filter(
      (t) =>
        t.countryISO31661Alpha2Code !== null &&
        !countriesByCode.has(t.countryISO31661Alpha2Code),
    );
    expect(
      orphans.map((t) => `${t.ianaIdentifier}->${t.countryISO31661Alpha2Code}`),
    ).toEqual([]);
  });

  it("Currency in Country.currencies references a real currency", () => {
    const validCurrencies = new Set(currencies.map((c) => c.iso4217AlphaCode));
    const orphans: string[] = [];
    for (const c of countries) {
      for (const cur of c.currencies) {
        if (!validCurrencies.has(cur.iso4217AlphaCode)) {
          orphans.push(`${c.iso31661Alpha2Code}->${cur.iso4217AlphaCode}`);
        }
      }
    }
    expect(orphans).toEqual([]);
  });
});

// -------------------------------------------------------------------------
// 3. M:M inverse-nav symmetry
// -------------------------------------------------------------------------

describe("M:M inverse-nav symmetry", () => {
  it("Country.subdivisionISO31662Codes ↔ Subdivision.countryISO31661Alpha2Code", () => {
    // Forward: for each subdivision, its country MUST list it
    const missingInverse: string[] = [];
    for (const s of subdivisions) {
      const c = countriesByCode.get(s.countryISO31661Alpha2Code);
      if (!c?.subdivisionISO31662Codes.includes(s.iso31662Code)) {
        missingInverse.push(
          `${s.countryISO31661Alpha2Code} missing ${s.iso31662Code}`,
        );
      }
    }
    expect(missingInverse).toEqual([]);
  });

  it("Country.timezoneIanaIdentifiers <-> Timezone (primary OR coApplicable)", () => {
    // Forward: each timezone's primary country + every co-applicable MUST list it
    const missingInverse: string[] = [];
    for (const t of timezones) {
      if (t.countryISO31661Alpha2Code) {
        const c = countriesByCode.get(t.countryISO31661Alpha2Code);
        if (!c?.timezoneIanaIdentifiers.includes(t.ianaIdentifier)) {
          missingInverse.push(
            `${t.countryISO31661Alpha2Code} missing ${t.ianaIdentifier}`,
          );
        }
      }
      for (const co of t.coApplicableCountryISO31661Alpha2Codes) {
        const c = countriesByCode.get(co);
        if (!c?.timezoneIanaIdentifiers.includes(t.ianaIdentifier)) {
          missingInverse.push(
            `${co} missing co-applicable ${t.ianaIdentifier}`,
          );
        }
      }
    }
    expect(missingInverse).toEqual([]);
  });

  it("Country.geopoliticalEntityShortCodes ↔ GE.countryISO31661Alpha2Codes", () => {
    // Forward: each (country, GE) edge in the manual catalog MUST appear inverted in Country
    const missingInverse: string[] = [];
    for (const ge of ges) {
      for (const countryCode of ge.countryISO31661Alpha2Codes) {
        const c = countriesByCode.get(countryCode);
        if (!c) continue; // orphan country reference — caught by GE-orphan test
        if (!c.geopoliticalEntityShortCodes.includes(ge.shortCode)) {
          missingInverse.push(`${countryCode} missing ${ge.shortCode}`);
        }
      }
    }
    expect(missingInverse).toEqual([]);
  });

  it("Language.spokenInCountryISO31661Alpha2Codes ↔ Country.spokenLanguageISO6391Codes", () => {
    // Forward: each (lang, country) edge from Country.spoken* MUST appear inverted in Language
    const missingInverse: string[] = [];
    for (const c of countries) {
      for (const lang of c.spokenLanguageISO6391Codes) {
        const l = languagesByCode.get(lang);
        if (!l) continue; // ISO 639-3 lang code without a 2-letter entry — skip
        if (
          !l.spokenInCountryISO31661Alpha2Codes.includes(c.iso31661Alpha2Code)
        ) {
          missingInverse.push(`${lang} missing ${c.iso31661Alpha2Code}`);
        }
      }
    }
    expect(missingInverse).toEqual([]);
  });

  it("Locale.countryISO31661Alpha2Code ↔ Country.localeIETFBCP47Tags", () => {
    const missingInverse: string[] = [];
    for (const l of locales) {
      if (!l.countryISO31661Alpha2Code) continue;
      const c = countriesByCode.get(l.countryISO31661Alpha2Code);
      if (!c?.localeIETFBCP47Tags.includes(l.ietfBcp47Tag)) {
        missingInverse.push(
          `${l.countryISO31661Alpha2Code} missing ${l.ietfBcp47Tag}`,
        );
      }
    }
    expect(missingInverse).toEqual([]);
  });
});

// -------------------------------------------------------------------------
// 4. Denormalization integrity (Locale region-derived fields)
// -------------------------------------------------------------------------

describe("Locale denormalization integrity", () => {
  it("Locale.firstDayOfWeek MUST match Country.firstDayOfWeek (when country is known)", () => {
    const drifts: string[] = [];
    for (const l of locales) {
      if (!l.countryISO31661Alpha2Code) continue;
      const c = countriesByCode.get(l.countryISO31661Alpha2Code);
      if (!c) continue;
      if (l.firstDayOfWeek !== c.firstDayOfWeek) {
        drifts.push(
          `${l.ietfBcp47Tag}: locale=${l.firstDayOfWeek} country=${c.firstDayOfWeek}`,
        );
      }
    }
    expect(drifts, JSON.stringify(drifts.slice(0, 5))).toEqual([]);
  });
});

// -------------------------------------------------------------------------
// 5. Derived-flag consistency (IsSupported / IsSelectable)
// -------------------------------------------------------------------------

describe("Derived flag consistency", () => {
  it("Currency.isSupported iff a selectable Locale's country uses it as primary", async () => {
    const messagesDir = resolve(REPO_ROOT, "public", "contracts", "messages");
    const dirExists = await stat(messagesDir).catch(() => null);
    if (!dirExists?.isDirectory()) return; // skip when contracts/messages doesn't exist

    const selectableLocales = locales.filter((l) => l.isSelectable);
    const expectedSupportedCurrencies = new Set<string>();
    for (const sl of selectableLocales) {
      if (!sl.countryISO31661Alpha2Code) continue;
      const country = countriesByCode.get(sl.countryISO31661Alpha2Code);
      if (country?.primaryCurrencyISO4217AlphaCode) {
        expectedSupportedCurrencies.add(
          country.primaryCurrencyISO4217AlphaCode,
        );
      }
    }
    for (const c of currencies) {
      const expected = expectedSupportedCurrencies.has(c.iso4217AlphaCode);
      expect(c.isSupported, `currency ${c.iso4217AlphaCode}`).toBe(expected);
    }
  });

  it("Language.isSupported iff a selectable Locale shares its lang subtag", () => {
    const selectableLangs = new Set(
      locales.filter((l) => l.isSelectable).map((l) => l.languageISO6391Code),
    );
    for (const lang of languages) {
      const expected = selectableLangs.has(lang.iso6391Code);
      expect(lang.isSupported, `language ${lang.iso6391Code}`).toBe(expected);
    }
  });
});

// -------------------------------------------------------------------------
// 6. Encoding integrity — invisibles preserved
// -------------------------------------------------------------------------

describe("Encoding integrity (NBSP / RLM / etc. preservation)", () => {
  it("fr-FR thousandsSeparator is an invisible-space (NBSP U+00A0 or NNBSP U+202F)", () => {
    // CLDR migrated French thousands separator from NBSP (U+00A0) to NARROW NBSP (U+202F)
    // in CLDR 33+; some other Latin-script locales (sv, fi, hu, kk-Cyrl) still use NBSP.
    // The point of the test: whatever the codepoint, it MUST be an invisible space character
    // that the escape-encoder preserved through the write-read cycle (not silently
    // normalized to ASCII " ").
    const fr = localesByTag.get("fr-FR");
    expect(fr).toBeDefined();
    const cp = fr!.thousandsSeparator.charCodeAt(0);
    const ALLOWED_INVISIBLE_SPACES = [0x00a0, 0x202f];
    expect(
      ALLOWED_INVISIBLE_SPACES,
      `fr-FR thousands separator codepoint U+${cp.toString(16)}`,
    ).toContain(cp);
  });

  it("en-US has ASCII separators", () => {
    const enUS = localesByTag.get("en-US");
    expect(enUS).toBeDefined();
    expect(enUS!.decimalSeparator).toBe(".");
    expect(enUS!.thousandsSeparator).toBe(",");
  });
});

// -------------------------------------------------------------------------
// 7. GE-Country reference sanity (hand-rolled)
// -------------------------------------------------------------------------

describe("GeopoliticalEntity references", () => {
  it("GE.countryISO31661Alpha2Codes references real countries", () => {
    const orphans = new Set<string>();
    for (const ge of ges) {
      for (const cc of ge.countryISO31661Alpha2Codes) {
        if (!countriesByCode.has(cc)) orphans.add(cc);
      }
    }
    expect([...orphans]).toEqual([]);
  });
});
