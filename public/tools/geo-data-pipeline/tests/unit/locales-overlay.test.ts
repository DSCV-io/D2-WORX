// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, it, expect } from "vitest";
import {
  applyLocalesOverlay,
  loadLocalesOverlay,
  type LocaleAdditionEntry,
  type LocaleOverrideEntry,
  type LocaleRemovalEntry,
  type LocalesOverlayFile,
} from "../../src/tier-2/load-overlays.js";
import type { SrcDataLocale } from "../../src/tier-2/load-src-data.js";

// -----------------------------------------------------------------------
// Fixture helpers — mirror the countries-overlay test fixture pattern.
// -----------------------------------------------------------------------

const makeLocale = (overrides: Partial<SrcDataLocale> = {}): SrcDataLocale => ({
  ietfBcp47Tag: "xx-XX",
  languageSubtag: "xx",
  regionSubtag: "XX",
  displayName: "Test Locale",
  endonymDisplayName: null,
  decimalSeparator: ".",
  thousandsSeparator: ",",
  dateFormatPattern: "DMY",
  ...overrides,
});

const makeOverlay = (
  parts: {
    additions?: LocaleAdditionEntry[];
    overrides?: LocaleOverrideEntry[];
    removals?: LocaleRemovalEntry[];
  } = {},
): LocalesOverlayFile => ({
  $generated: false,
  $source: "manual-overlay",
  $schema: "./locales.overlays.schema.json",
  $note: "test fixture",
  catalogVersion: "0.1.0",
  lastEditedAt: "2026-05-26",
  additions: parts.additions ?? [],
  overrides: parts.overrides ?? [],
  removals: parts.removals ?? [],
});

const tracked = (
  id: string,
): { id: string; addedAt: string; reason: string } => ({
  id,
  addedAt: "2026-05-26",
  reason: `policy: ${id} test rationale`,
});

// -----------------------------------------------------------------------
// applyLocalesOverlay — pure-function behavior
// -----------------------------------------------------------------------

describe("applyLocalesOverlay", () => {
  it("returns Tier 1 input unchanged when overlay is null + empty applied diagnostic", () => {
    const tier1 = [
      makeLocale({ ietfBcp47Tag: "en-US" }),
      makeLocale({ ietfBcp47Tag: "fr-FR" }),
    ];
    const result = applyLocalesOverlay(tier1, null);
    expect(result.locales).toEqual(tier1);
    expect(result.applied).toEqual({
      additions: [],
      overrides: [],
      removals: [],
    });
  });

  it("addition appends new entry and emits sorted output", () => {
    const tier1 = [
      makeLocale({ ietfBcp47Tag: "en-US" }),
      makeLocale({ ietfBcp47Tag: "fr-FR" }),
    ];
    const overlay = makeOverlay({
      additions: [
        {
          ...tracked("fr-TF"),
          data: makeLocale({
            ietfBcp47Tag: "fr-TF",
            languageSubtag: "fr",
            regionSubtag: "TF",
            displayName: "French (French Southern Territories)",
          }),
        },
      ],
    });
    const result = applyLocalesOverlay(tier1, overlay);
    expect(result.locales.map((l) => l.ietfBcp47Tag)).toEqual([
      "en-US",
      "fr-FR",
      "fr-TF",
    ]);
    expect(result.applied.additions).toHaveLength(1);
    expect(result.applied.additions[0]).toMatchObject({
      id: "fr-TF",
      addedAt: "2026-05-26",
    });
  });

  it("addition with id colliding Tier 1 throws naming id + suggesting override", () => {
    const tier1 = [makeLocale({ ietfBcp47Tag: "en-US" })];
    const overlay = makeOverlay({
      additions: [
        {
          ...tracked("en-US"),
          data: makeLocale({ ietfBcp47Tag: "en-US" }),
        },
      ],
    });
    expect(() => applyLocalesOverlay(tier1, overlay)).toThrow(
      /collides with Tier 1.*en-US.*override/s,
    );
  });

  it("addition with id != data.ietfBcp47Tag throws naming both values", () => {
    const tier1 = [makeLocale({ ietfBcp47Tag: "en-US" })];
    const overlay = makeOverlay({
      additions: [
        {
          ...tracked("fr-TF"),
          data: makeLocale({ ietfBcp47Tag: "fr-ZZ" }),
        },
      ],
    });
    expect(() => applyLocalesOverlay(tier1, overlay)).toThrow(
      /id="fr-TF".*doesn't match data\.ietfBcp47Tag="fr-ZZ"/s,
    );
  });

  it("override patches named fields on existing entry, preserves others", () => {
    const tier1 = [
      makeLocale({
        ietfBcp47Tag: "en-US",
        displayName: "English (United States)",
        decimalSeparator: ".",
      }),
    ];
    const overlay = makeOverlay({
      overrides: [
        {
          ...tracked("en-US"),
          fields: { displayName: "American English" },
        },
      ],
    });
    const result = applyLocalesOverlay(tier1, overlay);
    expect(result.locales).toHaveLength(1);
    expect(result.locales[0]?.displayName).toBe("American English");
    expect(result.locales[0]?.decimalSeparator).toBe(".");
    expect(result.applied.overrides).toHaveLength(1);
    expect(result.applied.overrides[0]).toMatchObject({
      id: "en-US",
      fields: ["displayName"],
    });
  });

  it("override targeting missing id throws with descriptive message", () => {
    const tier1 = [makeLocale({ ietfBcp47Tag: "en-US" })];
    const overlay = makeOverlay({
      overrides: [{ ...tracked("zz-ZZ"), fields: { displayName: "Phantom" } }],
    });
    expect(() => applyLocalesOverlay(tier1, overlay)).toThrow(
      /override targets locales\[zz-ZZ\].*no such entry exists in Tier 1/s,
    );
  });

  it("removal drops entry by id and populates applied.removals", () => {
    const tier1 = [
      makeLocale({ ietfBcp47Tag: "en-US" }),
      makeLocale({ ietfBcp47Tag: "fr-FR" }),
    ];
    const overlay = makeOverlay({ removals: [tracked("en-US")] });
    const result = applyLocalesOverlay(tier1, overlay);
    expect(result.locales.map((l) => l.ietfBcp47Tag)).toEqual(["fr-FR"]);
    expect(result.applied.removals).toHaveLength(1);
    expect(result.applied.removals[0]?.id).toBe("en-US");
  });

  it("removal of missing id is silent no-op (no throw, no applied entry)", () => {
    const tier1 = [makeLocale({ ietfBcp47Tag: "en-US" })];
    const overlay = makeOverlay({ removals: [tracked("zz-ZZ")] });
    const result = applyLocalesOverlay(tier1, overlay);
    expect(result.locales.map((l) => l.ietfBcp47Tag)).toEqual(["en-US"]);
    expect(result.applied.removals).toHaveLength(0);
  });

  it("apply order is additions -> overrides -> removals on different ids", () => {
    const tier1 = [
      makeLocale({ ietfBcp47Tag: "en-US", displayName: "American" }),
      makeLocale({ ietfBcp47Tag: "fr-FR", displayName: "French" }),
    ];
    const overlay = makeOverlay({
      additions: [
        {
          ...tracked("fr-TF"),
          data: makeLocale({
            ietfBcp47Tag: "fr-TF",
            displayName: "French Southern",
          }),
        },
      ],
      overrides: [
        { ...tracked("fr-FR"), fields: { displayName: "French Patched" } },
      ],
      removals: [tracked("en-US")],
    });
    const result = applyLocalesOverlay(tier1, overlay);
    expect(result.locales.map((l) => l.ietfBcp47Tag)).toEqual([
      "fr-FR",
      "fr-TF",
    ]);
    expect(
      result.locales.find((l) => l.ietfBcp47Tag === "fr-FR")?.displayName,
    ).toBe("French Patched");
    expect(result.applied.additions).toHaveLength(1);
    expect(result.applied.overrides).toHaveLength(1);
    expect(result.applied.removals).toHaveLength(1);
  });

  it("output is sorted lexicographically by ietfBcp47Tag", () => {
    const tier1 = [
      makeLocale({ ietfBcp47Tag: "zh-Hant-TW" }),
      makeLocale({ ietfBcp47Tag: "en-US" }),
      makeLocale({ ietfBcp47Tag: "ar-EG" }),
    ];
    const overlay = makeOverlay({
      additions: [
        { ...tracked("fr-TF"), data: makeLocale({ ietfBcp47Tag: "fr-TF" }) },
        { ...tracked("bn-IN"), data: makeLocale({ ietfBcp47Tag: "bn-IN" }) },
      ],
    });
    const result = applyLocalesOverlay(tier1, overlay);
    expect(result.locales.map((l) => l.ietfBcp47Tag)).toEqual([
      "ar-EG",
      "bn-IN",
      "en-US",
      "fr-TF",
      "zh-Hant-TW",
    ]);
  });

  it("addition-then-override on same id applies both (override patches the addition)", () => {
    const tier1: SrcDataLocale[] = [];
    const overlay = makeOverlay({
      additions: [
        {
          ...tracked("fr-TF"),
          data: makeLocale({
            ietfBcp47Tag: "fr-TF",
            displayName: "French Southern Territories",
          }),
        },
      ],
      overrides: [
        {
          ...tracked("fr-TF"),
          fields: { displayName: "TAAF Patched" },
        },
      ],
    });
    const result = applyLocalesOverlay(tier1, overlay);
    expect(result.locales).toHaveLength(1);
    expect(result.locales[0]?.displayName).toBe("TAAF Patched");
    expect(result.applied.additions).toHaveLength(1);
    expect(result.applied.overrides).toHaveLength(1);
  });

  it("override-then-removal on same id removes cleanly", () => {
    const tier1 = [
      makeLocale({ ietfBcp47Tag: "en-US", displayName: "American" }),
    ];
    const overlay = makeOverlay({
      overrides: [{ ...tracked("en-US"), fields: { displayName: "Patched" } }],
      removals: [tracked("en-US")],
    });
    const result = applyLocalesOverlay(tier1, overlay);
    expect(result.locales).toHaveLength(0);
    expect(result.applied.overrides).toHaveLength(1);
    expect(result.applied.removals).toHaveLength(1);
  });

  it("preserves optional addedBy field in applied diagnostic when present", () => {
    const tier1: SrcDataLocale[] = [];
    const overlay = makeOverlay({
      additions: [
        {
          id: "fr-TF",
          addedAt: "2026-05-26",
          reason: "TAAF needs an entry",
          addedBy: "orchestrator",
          data: makeLocale({ ietfBcp47Tag: "fr-TF" }),
        },
      ],
    });
    const result = applyLocalesOverlay(tier1, overlay);
    expect(result.applied.additions[0]?.addedBy).toBe("orchestrator");
  });

  it("empty overlay returns Tier 1 + empty applied", () => {
    const tier1 = [makeLocale({ ietfBcp47Tag: "en-US" })];
    const result = applyLocalesOverlay(tier1, makeOverlay());
    expect(result.locales.map((l) => l.ietfBcp47Tag)).toEqual(["en-US"]);
    expect(result.applied).toEqual({
      additions: [],
      overrides: [],
      removals: [],
    });
  });
});

// -----------------------------------------------------------------------
// loadLocalesOverlay — file I/O smoke against shipped overlay
// -----------------------------------------------------------------------

describe("loadLocalesOverlay", () => {
  it("returns a parsed LocalesOverlayFile with fr-TF when shipped overlay exists", async () => {
    const overlay = await loadLocalesOverlay();
    expect(overlay).not.toBeNull();
    expect(overlay?.$generated).toBe(false);
    expect(overlay?.$source).toBe("manual-overlay");
    expect(Array.isArray(overlay?.additions)).toBe(true);
    expect(Array.isArray(overlay?.overrides)).toBe(true);
    expect(Array.isArray(overlay?.removals)).toBe(true);
    // Sanity-pin: the fr-TF entry is committed (the canonical first example).
    const frTF = overlay?.additions.find((a) => a.id === "fr-TF");
    expect(frTF).toBeDefined();
    expect(frTF?.data.ietfBcp47Tag).toBe("fr-TF");
    expect(frTF?.data.languageSubtag).toBe("fr");
    expect(frTF?.data.regionSubtag).toBe("TF");
  });
});
