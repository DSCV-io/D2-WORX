// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { SealedFrame } from "@dcsv-io/d2-encryption-abstractions";
import { canonicalize, loadFixture } from "../src/index.js";

interface CatalogMap {
  readonly [constName: string]: number;
}

describe("encryption-frame-sealed parity (.NET SealedFrameLayout ↔ TS SealedFrame)", () => {
  const fixture = loadFixture<CatalogMap>("encryption-frame-sealed", "layout");
  const fixtureMap = fixture.data;
  const fixtureKeys = Object.keys(fixtureMap).sort();
  const tsCatalog = SealedFrame as Readonly<Record<string, number>>;
  const tsKeys = Object.keys(tsCatalog).sort();

  it("has identical constName membership", () => {
    expect(tsKeys).toEqual(fixtureKeys);
  });

  for (const constName of fixtureKeys) {
    it(`constant ${constName} has identical integer value`, () => {
      const fixtureValue = fixtureMap[constName];
      const tsValue = tsCatalog[constName];
      expect(tsValue).toBe(fixtureValue);
    });
  }

  it("canonical maps are byte-equal", () => {
    const tsAsMap: Record<string, number> = {};
    for (const k of tsKeys) tsAsMap[k] = tsCatalog[k]!;
    expect(canonicalize(tsAsMap)).toEqual(canonicalize(fixtureMap));
  });

  it("CURRENT_VERSION pinned at 2 (bumping is a wire-breaking change)", () => {
    expect(SealedFrame.CURRENT_VERSION).toBe(2);
  });

  it("AES-GCM nonce + tag lengths pinned at GCM-spec values", () => {
    expect(SealedFrame.CONSTRAINT_NONCE_LENGTH).toBe(12);
    expect(SealedFrame.CONSTRAINT_TAG_LENGTH).toBe(16);
  });

  it("eph_pub length prefix pinned at 2 bytes big-endian with a 256-byte cap", () => {
    expect(SealedFrame.CONSTRAINT_EPH_PUB_LENGTH_PREFIX_SIZE).toBe(2);
    expect(SealedFrame.CONSTRAINT_MAX_EPH_PUB_LENGTH).toBe(256);
  });
});
