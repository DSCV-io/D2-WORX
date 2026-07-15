// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { EncryptionFrame } from "@dcsv-io/d2-encryption-abstractions";
import { canonicalize, loadFixture } from "../src/index.js";

interface CatalogMap {
  readonly [constName: string]: number;
}

describe("encryption-frame parity (.NET EncryptionFrameLayout ↔ TS EncryptionFrame)", () => {
  const fixture = loadFixture<CatalogMap>("encryption-frame", "layout");
  const fixtureMap = fixture.data;
  const fixtureKeys = Object.keys(fixtureMap).sort();
  const tsCatalog = EncryptionFrame as Readonly<Record<string, number>>;
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

  it("CURRENT_VERSION pinned at 1 (bumping is a wire-breaking change)", () => {
    expect(EncryptionFrame.CURRENT_VERSION).toBe(1);
  });

  it("AES-GCM nonce + tag lengths pinned at GCM-spec values", () => {
    expect(EncryptionFrame.CONSTRAINT_NONCE_LENGTH).toBe(12);
    expect(EncryptionFrame.CONSTRAINT_TAG_LENGTH).toBe(16);
  });
});
