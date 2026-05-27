// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, it, expect } from "vitest";
import {
  applySubdivisionsOverlay,
  loadSubdivisionsOverlay,
  type SubdivisionsOverlayFile,
  type SubdivisionAdditionEntry,
  type SubdivisionOverrideEntry,
  type SubdivisionRemovalEntry,
} from "../../src/tier-2/load-overlays.js";
import type { SrcDataSubdivision } from "../../src/tier-2/load-src-data.js";

// -----------------------------------------------------------------------
// Fixture helpers — keep test bodies focused on the policy behavior under
// test rather than on the verbose SrcDataSubdivision shape.
// -----------------------------------------------------------------------

const makeSubdivision = (
  overrides: Partial<SrcDataSubdivision> = {},
): SrcDataSubdivision => ({
  iso31662Code: "ZZ-01",
  shortCode: "01",
  displayName: "Test Subdivision",
  officialName: "The Test Subdivision",
  endonymDisplayName: null,
  countryISO31661Alpha2Code: "ZZ",
  parentISO31662Code: null,
  type: null,
  order: null,
  ...overrides,
});

const makeOverlay = (
  parts: {
    additions?: SubdivisionAdditionEntry[];
    overrides?: SubdivisionOverrideEntry[];
    removals?: SubdivisionRemovalEntry[];
  } = {},
): SubdivisionsOverlayFile => ({
  $generated: false,
  $source: "manual-overlay",
  $schema: "./subdivisions.overlays.schema.json",
  $note: "test fixture",
  catalogVersion: "0.1.0",
  lastEditedAt: "2026-05-23",
  additions: parts.additions ?? [],
  overrides: parts.overrides ?? [],
  removals: parts.removals ?? [],
});

const tracked = (
  id: string,
): { id: string; addedAt: string; reason: string } => ({
  id,
  addedAt: "2026-05-23",
  reason: `policy: ${id} (test rationale)`,
});

// -----------------------------------------------------------------------
// applySubdivisionsOverlay — pure-function behavior
// -----------------------------------------------------------------------

describe("applySubdivisionsOverlay", () => {
  it("returns Tier 1 input unchanged when overlay is null + empty applied diagnostic", () => {
    const tier1 = [
      makeSubdivision({ iso31662Code: "US-NY" }),
      makeSubdivision({ iso31662Code: "US-CA" }),
    ];
    const result = applySubdivisionsOverlay(tier1, null);
    expect(result.subdivisions).toEqual(tier1);
    expect(result.applied).toEqual({
      additions: [],
      overrides: [],
      removals: [],
    });
  });

  it("empty overlay (no additions/overrides/removals) returns Tier 1 + empty applied", () => {
    const tier1 = [makeSubdivision({ iso31662Code: "US-NY" })];
    const result = applySubdivisionsOverlay(tier1, makeOverlay());
    expect(result.subdivisions.map((s) => s.iso31662Code)).toEqual(["US-NY"]);
    expect(result.applied).toEqual({
      additions: [],
      overrides: [],
      removals: [],
    });
  });

  it("addition appends new entry and emits sorted output", () => {
    const tier1 = [
      makeSubdivision({ iso31662Code: "US-NY" }),
      makeSubdivision({ iso31662Code: "US-CA" }),
    ];
    const overlay = makeOverlay({
      additions: [
        {
          ...tracked("US-ZZ"),
          data: makeSubdivision({
            iso31662Code: "US-ZZ",
            displayName: "Zee State",
            countryISO31661Alpha2Code: "US",
          }),
        },
      ],
    });
    const result = applySubdivisionsOverlay(tier1, overlay);
    expect(result.subdivisions.map((s) => s.iso31662Code)).toEqual([
      "US-CA",
      "US-NY",
      "US-ZZ",
    ]);
    expect(result.applied.additions).toHaveLength(1);
    expect(result.applied.additions[0]).toMatchObject({
      id: "US-ZZ",
      addedAt: "2026-05-23",
    });
  });

  it("addition with id colliding Tier 1 entry throws naming id and suggesting override", () => {
    const tier1 = [makeSubdivision({ iso31662Code: "US-NY" })];
    const overlay = makeOverlay({
      additions: [
        {
          ...tracked("US-NY"),
          data: makeSubdivision({ iso31662Code: "US-NY" }),
        },
      ],
    });
    expect(() => applySubdivisionsOverlay(tier1, overlay)).toThrow(
      /collides with Tier 1.*US-NY.*override/s,
    );
  });

  it("addition with id ≠ data.iso31662Code throws naming both values", () => {
    const tier1 = [makeSubdivision({ iso31662Code: "US-NY" })];
    const overlay = makeOverlay({
      additions: [
        {
          ...tracked("US-ZZ"),
          data: makeSubdivision({
            iso31662Code: "US-XX",
            displayName: "Mismatched",
          }),
        },
      ],
    });
    expect(() => applySubdivisionsOverlay(tier1, overlay)).toThrow(
      /id="US-ZZ".*doesn't match data\.iso31662Code="US-XX"/s,
    );
  });

  it("override patches named fields on existing entry, preserves others, populates applied", () => {
    const tier1 = [
      makeSubdivision({
        iso31662Code: "IR-22",
        displayName: "Markazi",
        officialName: "Markazi",
        countryISO31661Alpha2Code: "IR",
        shortCode: "22",
      }),
    ];
    const overlay = makeOverlay({
      overrides: [
        {
          ...tracked("IR-22"),
          fields: {
            displayName: "Hormozgān",
            officialName: "Hormozgān",
          },
        },
      ],
    });
    const result = applySubdivisionsOverlay(tier1, overlay);
    expect(result.subdivisions).toHaveLength(1);
    expect(result.subdivisions[0]?.displayName).toBe("Hormozgān");
    expect(result.subdivisions[0]?.officialName).toBe("Hormozgān");
    // Other fields untouched
    expect(result.subdivisions[0]?.countryISO31661Alpha2Code).toBe("IR");
    expect(result.subdivisions[0]?.shortCode).toBe("22");
    // Diagnostic captures the patched-field names
    expect(result.applied.overrides).toHaveLength(1);
    expect(result.applied.overrides[0]).toMatchObject({
      id: "IR-22",
      fields: ["displayName", "officialName"],
    });
  });

  it("override targeting missing id throws with descriptive message", () => {
    const tier1 = [makeSubdivision({ iso31662Code: "US-NY" })];
    const overlay = makeOverlay({
      overrides: [
        { ...tracked("ZZ-99"), fields: { displayName: "Phantom" } },
      ],
    });
    expect(() => applySubdivisionsOverlay(tier1, overlay)).toThrow(
      /override targets subdivisions\[ZZ-99\].*no such entry exists in Tier 1/s,
    );
  });

  it("removal drops entry by id and populates applied.removals", () => {
    const tier1 = [
      makeSubdivision({ iso31662Code: "US-NY" }),
      makeSubdivision({ iso31662Code: "US-CA" }),
    ];
    const overlay = makeOverlay({ removals: [tracked("US-NY")] });
    const result = applySubdivisionsOverlay(tier1, overlay);
    expect(result.subdivisions.map((s) => s.iso31662Code)).toEqual(["US-CA"]);
    expect(result.applied.removals).toHaveLength(1);
    expect(result.applied.removals[0]?.id).toBe("US-NY");
  });

  it("apply order is additions → overrides → removals on different ids", () => {
    const tier1 = [
      makeSubdivision({ iso31662Code: "US-NY", displayName: "New York" }),
      makeSubdivision({ iso31662Code: "US-CA", displayName: "California" }),
    ];
    const overlay = makeOverlay({
      additions: [
        {
          ...tracked("US-ZZ"),
          data: makeSubdivision({
            iso31662Code: "US-ZZ",
            displayName: "Zee State",
          }),
        },
      ],
      overrides: [
        { ...tracked("US-CA"), fields: { displayName: "Cali Patched" } },
      ],
      removals: [tracked("US-NY")],
    });
    const result = applySubdivisionsOverlay(tier1, overlay);
    expect(result.subdivisions.map((s) => s.iso31662Code)).toEqual([
      "US-CA",
      "US-ZZ",
    ]);
    expect(
      result.subdivisions.find((s) => s.iso31662Code === "US-CA")?.displayName,
    ).toBe("Cali Patched");
    expect(result.applied.additions).toHaveLength(1);
    expect(result.applied.overrides).toHaveLength(1);
    expect(result.applied.removals).toHaveLength(1);
  });
});

// -----------------------------------------------------------------------
// loadSubdivisionsOverlay — file I/O smoke against shipped overlay
// -----------------------------------------------------------------------

describe("loadSubdivisionsOverlay", () => {
  it("returns a parsed SubdivisionsOverlayFile when the shipped overlay file exists", async () => {
    // The pipeline ships contracts/geo/overlays/subdivisions.overlays.spec.json as the
    // canonical placeholder for future patches. Post 2026-05-23 source-priority flip
    // (Wikidata.en primary), the file's overrides array is empty — the prior IR-22
    // override is no longer needed because Wikidata.en correctly returns "Hormozgan
    // Province" without an overlay. The file remains so future patches land here.
    const overlay = await loadSubdivisionsOverlay();
    expect(overlay).not.toBeNull();
    expect(overlay?.$generated).toBe(false);
    expect(overlay?.$source).toBe("manual-overlay");
    expect(Array.isArray(overlay?.additions)).toBe(true);
    expect(Array.isArray(overlay?.overrides)).toBe(true);
    expect(Array.isArray(overlay?.removals)).toBe(true);
    // Sanity-pin: the file starts empty post architecture flip. If a future entry
    // lands here, update this assertion to pin the canonical example.
    expect(overlay?.additions).toHaveLength(0);
    expect(overlay?.overrides).toHaveLength(0);
    expect(overlay?.removals).toHaveLength(0);
  });
});
