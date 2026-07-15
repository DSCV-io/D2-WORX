// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { CATALOG_PUBLISHED_AT, CATALOG_VERSION } from "@d2/geo-abstractions";

import { canonicalize, loadFixture } from "../src/index.js";

interface CatalogFixture {
  readonly catalogVersion: string;
  readonly catalogPublishedAt: string;
}

describe("geo catalog parity (.NET catalog ↔ TS catalog)", () => {
  const fixture = loadFixture<CatalogFixture>("geo", "catalog");
  const fixtureData = fixture.data;

  it("CATALOG_VERSION matches fixture catalogVersion", () => {
    expect(CATALOG_VERSION).toBe(fixtureData.catalogVersion);
  });

  it("CATALOG_PUBLISHED_AT matches fixture catalogPublishedAt (byte-equal ISO-8601 string)", () => {
    // Both sides snapshot the same spec metadata as a fixed string. Drift
    // means one runtime's catalog was rebuilt without the other — surfaces
    // a regen-mis-sync, not a semantic difference.
    expect(CATALOG_PUBLISHED_AT).toBe(fixtureData.catalogPublishedAt);
  });

  it("canonical maps are byte-equal", () => {
    const tsMap = {
      catalogVersion: CATALOG_VERSION,
      catalogPublishedAt: CATALOG_PUBLISHED_AT,
    } as const;
    expect(canonicalize(tsMap)).toEqual(canonicalize(fixtureData));
  });
});
