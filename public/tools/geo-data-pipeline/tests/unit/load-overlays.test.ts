// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, it, expect } from "vitest";
import {
  applyCountriesOverlay,
  loadCountriesOverlay,
  type CountriesOverlayFile,
  type CountryAdditionEntry,
  type CountryOverrideEntry,
  type CountryRemovalEntry,
} from "../../src/tier-2/load-overlays.js";
import type { SrcDataCountry } from "../../src/tier-2/load-src-data.js";

// -----------------------------------------------------------------------
// Fixture helpers — keep test bodies focused on the policy behavior under
// test rather than on the verbose SrcDataCountry shape.
// -----------------------------------------------------------------------

const makeCountry = (
  overrides: Partial<SrcDataCountry> = {},
): SrcDataCountry => ({
  iso31661Alpha2Code: "ZZ",
  iso31661Alpha3Code: "ZZZ",
  iso31661NumericCode: "999",
  displayName: "Test Country",
  officialName: "The Test Country",
  endonymDisplayName: null,
  sovereignCountryISO31661Alpha2Code: null,
  phoneNumberPrefix: null,
  phoneNumberNationalFormat: null,
  phoneNumberMinDigits: null,
  phoneNumberMaxDigits: null,
  primaryCurrencyISO4217AlphaCode: null,
  primaryLanguageISO6391Code: null,
  primaryLocaleIETFBCP47Tag: null,
  firstDayOfWeek: null,
  weekendStart: null,
  weekendEnd: null,
  measurementSystem: null,
  activeLegalTenderCurrencies: [],
  territoryISO31661Alpha2Codes: [],
  spokenLanguages: [],
  ...overrides,
});

const makeOverlay = (
  parts: {
    additions?: CountryAdditionEntry[];
    overrides?: CountryOverrideEntry[];
    removals?: CountryRemovalEntry[];
  } = {},
): CountriesOverlayFile => ({
  $generated: false,
  $source: "manual-overlay",
  $schema: "./countries.overlays.schema.json",
  $note: "test fixture",
  catalogVersion: "0.1.0",
  lastEditedAt: "2026-05-19",
  additions: parts.additions ?? [],
  overrides: parts.overrides ?? [],
  removals: parts.removals ?? [],
});

const tracked = (
  id: string,
): { id: string; addedAt: string; reason: string } => ({
  id,
  addedAt: "2026-05-19",
  reason: `policy: ${id}`,
});

// -----------------------------------------------------------------------
// applyCountriesOverlay — pure-function behavior
// -----------------------------------------------------------------------

describe("applyCountriesOverlay", () => {
  it("returns Tier 1 input unchanged when overlay is null + empty applied diagnostic", () => {
    const tier1 = [
      makeCountry({ iso31661Alpha2Code: "DE" }),
      makeCountry({ iso31661Alpha2Code: "FR" }),
    ];
    const result = applyCountriesOverlay(tier1, null);
    expect(result.countries).toEqual(tier1);
    expect(result.applied).toEqual({
      additions: [],
      overrides: [],
      removals: [],
    });
  });

  it("addition appends new entry and emits sorted output", () => {
    const tier1 = [
      makeCountry({ iso31661Alpha2Code: "DE" }),
      makeCountry({ iso31661Alpha2Code: "FR" }),
    ];
    const overlay = makeOverlay({
      additions: [
        {
          ...tracked("XK"),
          data: makeCountry({
            iso31661Alpha2Code: "XK",
            displayName: "Kosovo",
          }),
        },
      ],
    });
    const result = applyCountriesOverlay(tier1, overlay);
    expect(result.countries.map((c) => c.iso31661Alpha2Code)).toEqual([
      "DE",
      "FR",
      "XK",
    ]);
    expect(result.applied.additions).toHaveLength(1);
    expect(result.applied.additions[0]).toMatchObject({
      id: "XK",
      addedAt: "2026-05-19",
      reason: "policy: XK",
    });
  });

  it("addition with id colliding Tier 1 entry throws naming id and suggesting override", () => {
    const tier1 = [makeCountry({ iso31661Alpha2Code: "DE" })];
    const overlay = makeOverlay({
      additions: [
        { ...tracked("DE"), data: makeCountry({ iso31661Alpha2Code: "DE" }) },
      ],
    });
    expect(() => applyCountriesOverlay(tier1, overlay)).toThrow(
      /collides with Tier 1.*DE.*override/s,
    );
  });

  it("addition with id ≠ data.iso31661Alpha2Code throws naming both values", () => {
    const tier1 = [makeCountry({ iso31661Alpha2Code: "DE" })];
    const overlay = makeOverlay({
      additions: [
        {
          ...tracked("XK"),
          data: makeCountry({
            iso31661Alpha2Code: "ZK",
            displayName: "Mismatched",
          }),
        },
      ],
    });
    expect(() => applyCountriesOverlay(tier1, overlay)).toThrow(
      /id="XK".*doesn't match data\.iso31661Alpha2Code="ZK"/s,
    );
  });

  it("override patches named fields on existing entry, preserves others, populates applied", () => {
    const tier1 = [
      makeCountry({
        iso31661Alpha2Code: "DE",
        displayName: "Germany",
        officialName: "Federal Republic of Germany",
        phoneNumberPrefix: "49",
      }),
    ];
    const overlay = makeOverlay({
      overrides: [{ ...tracked("DE"), fields: { displayName: "Deutschland" } }],
    });
    const result = applyCountriesOverlay(tier1, overlay);
    expect(result.countries).toHaveLength(1);
    expect(result.countries[0]?.displayName).toBe("Deutschland");
    // Other fields untouched
    expect(result.countries[0]?.officialName).toBe(
      "Federal Republic of Germany",
    );
    expect(result.countries[0]?.phoneNumberPrefix).toBe("49");
    // Diagnostic captures the patched-field names
    expect(result.applied.overrides).toHaveLength(1);
    expect(result.applied.overrides[0]).toMatchObject({
      id: "DE",
      fields: ["displayName"],
    });
  });

  it("override targeting missing id throws with descriptive message", () => {
    const tier1 = [makeCountry({ iso31661Alpha2Code: "DE" })];
    const overlay = makeOverlay({
      overrides: [{ ...tracked("ZZ"), fields: { displayName: "Phantom" } }],
    });
    expect(() => applyCountriesOverlay(tier1, overlay)).toThrow(
      /override targets countries\[ZZ\].*no such entry exists in Tier 1/s,
    );
  });

  it("removal drops entry by id and populates applied.removals", () => {
    const tier1 = [
      makeCountry({ iso31661Alpha2Code: "DE" }),
      makeCountry({ iso31661Alpha2Code: "FR" }),
    ];
    const overlay = makeOverlay({ removals: [tracked("DE")] });
    const result = applyCountriesOverlay(tier1, overlay);
    expect(result.countries.map((c) => c.iso31661Alpha2Code)).toEqual(["FR"]);
    expect(result.applied.removals).toHaveLength(1);
    expect(result.applied.removals[0]?.id).toBe("DE");
  });

  it("removal of missing id is silent no-op (no throw, no applied entry, output unchanged)", () => {
    const tier1 = [makeCountry({ iso31661Alpha2Code: "DE" })];
    const overlay = makeOverlay({ removals: [tracked("ZZ")] });
    const result = applyCountriesOverlay(tier1, overlay);
    expect(result.countries.map((c) => c.iso31661Alpha2Code)).toEqual(["DE"]);
    expect(result.applied.removals).toHaveLength(0);
  });

  it("apply order is additions → overrides → removals on different ids", () => {
    // Pre-existing: DE, FR. Add XK. Override FR's displayName. Remove DE.
    // End state: XK with default displayName, FR with "France Patched".
    const tier1 = [
      makeCountry({ iso31661Alpha2Code: "DE", displayName: "Germany" }),
      makeCountry({ iso31661Alpha2Code: "FR", displayName: "France" }),
    ];
    const overlay = makeOverlay({
      additions: [
        {
          ...tracked("XK"),
          data: makeCountry({
            iso31661Alpha2Code: "XK",
            displayName: "Kosovo",
          }),
        },
      ],
      overrides: [
        { ...tracked("FR"), fields: { displayName: "France Patched" } },
      ],
      removals: [tracked("DE")],
    });
    const result = applyCountriesOverlay(tier1, overlay);
    expect(result.countries.map((c) => c.iso31661Alpha2Code)).toEqual([
      "FR",
      "XK",
    ]);
    expect(
      result.countries.find((c) => c.iso31661Alpha2Code === "FR")?.displayName,
    ).toBe("France Patched");
    expect(
      result.countries.find((c) => c.iso31661Alpha2Code === "XK")?.displayName,
    ).toBe("Kosovo");
    // Applied diagnostic carries all three
    expect(result.applied.additions).toHaveLength(1);
    expect(result.applied.overrides).toHaveLength(1);
    expect(result.applied.removals).toHaveLength(1);
  });

  it("output is sorted lexicographically by iso31661Alpha2Code (stable under random order)", () => {
    // Tier 1 in random order; overlay adds in random order — output must be alpha-2 ascending.
    const tier1 = [
      makeCountry({ iso31661Alpha2Code: "ZW" }),
      makeCountry({ iso31661Alpha2Code: "DE" }),
      makeCountry({ iso31661Alpha2Code: "AF" }),
    ];
    const overlay = makeOverlay({
      additions: [
        { ...tracked("XK"), data: makeCountry({ iso31661Alpha2Code: "XK" }) },
        { ...tracked("BR"), data: makeCountry({ iso31661Alpha2Code: "BR" }) },
      ],
    });
    const result = applyCountriesOverlay(tier1, overlay);
    expect(result.countries.map((c) => c.iso31661Alpha2Code)).toEqual([
      "AF",
      "BR",
      "DE",
      "XK",
      "ZW",
    ]);
  });

  it("addition-then-override on same id applies both (override patches the addition)", () => {
    const tier1: SrcDataCountry[] = [];
    const overlay = makeOverlay({
      additions: [
        {
          ...tracked("XK"),
          data: makeCountry({
            iso31661Alpha2Code: "XK",
            displayName: "Kosovo",
          }),
        },
      ],
      overrides: [{ ...tracked("XK"), fields: { displayName: "Kosova" } }],
    });
    const result = applyCountriesOverlay(tier1, overlay);
    expect(result.countries).toHaveLength(1);
    expect(result.countries[0]?.displayName).toBe("Kosova");
    expect(result.applied.additions).toHaveLength(1);
    expect(result.applied.overrides).toHaveLength(1);
  });

  it("override-then-removal on same id removes cleanly (override doesn't block removal)", () => {
    const tier1 = [
      makeCountry({ iso31661Alpha2Code: "DE", displayName: "Germany" }),
    ];
    const overlay = makeOverlay({
      overrides: [{ ...tracked("DE"), fields: { displayName: "Deutschland" } }],
      removals: [tracked("DE")],
    });
    const result = applyCountriesOverlay(tier1, overlay);
    expect(result.countries).toHaveLength(0);
    expect(result.applied.overrides).toHaveLength(1);
    expect(result.applied.removals).toHaveLength(1);
  });

  it("preserves optional addedBy field in applied diagnostic when present on overlay entry", () => {
    const tier1: SrcDataCountry[] = [];
    const overlay = makeOverlay({
      additions: [
        {
          id: "XK",
          addedAt: "2026-05-19",
          reason: "test",
          addedBy: "tmoonkeca@example",
          data: makeCountry({ iso31661Alpha2Code: "XK" }),
        },
      ],
    });
    const result = applyCountriesOverlay(tier1, overlay);
    expect(result.applied.additions[0]?.addedBy).toBe("tmoonkeca@example");
  });

  it("empty overlay (no additions/overrides/removals) returns Tier 1 + empty applied", () => {
    const tier1 = [makeCountry({ iso31661Alpha2Code: "DE" })];
    const result = applyCountriesOverlay(tier1, makeOverlay());
    expect(result.countries.map((c) => c.iso31661Alpha2Code)).toEqual(["DE"]);
    expect(result.applied).toEqual({
      additions: [],
      overrides: [],
      removals: [],
    });
  });
});

// -----------------------------------------------------------------------
// loadCountriesOverlay — file I/O smoke against shipped overlay
// -----------------------------------------------------------------------

describe("loadCountriesOverlay", () => {
  it("returns a parsed CountriesOverlayFile when the shipped overlay file exists", async () => {
    // The pipeline ships contracts/geo/overlays/countries.overlays.spec.json with
    // the Kosovo (XK) addition. The loader reads from the fixed REPO_ROOT-relative
    // path; this asserts the parse round-trip against the committed file. If the
    // shipped file is ever removed, the null-path is also tested implicitly by the
    // contract that `applyCountriesOverlay(tier1, null)` returns Tier 1 unchanged
    // (see the apply-suite null test).
    const overlay = await loadCountriesOverlay();
    expect(overlay).not.toBeNull();
    expect(overlay?.$generated).toBe(false);
    expect(overlay?.$source).toBe("manual-overlay");
    expect(Array.isArray(overlay?.additions)).toBe(true);
    expect(Array.isArray(overlay?.overrides)).toBe(true);
    expect(Array.isArray(overlay?.removals)).toBe(true);
    // Sanity-pin: the XK entry is committed (the canonical example).
    const xk = overlay?.additions.find((a) => a.id === "XK");
    expect(xk).toBeDefined();
    expect(xk?.data.iso31661Alpha2Code).toBe("XK");
  });
});
