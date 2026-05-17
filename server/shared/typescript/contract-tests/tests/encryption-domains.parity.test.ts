// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { EncryptionDomains } from "@d2/encryption-abstractions";
import { canonicalize, loadFixture } from "../src/index.js";

interface CatalogMap {
  readonly [constName: string]: string;
}

describe("encryption-domains parity (.NET EncryptionDomains ↔ TS EncryptionDomains)", () => {
  const fixture = loadFixture<CatalogMap>("encryption-domains", "domains");
  const fixtureMap = fixture.data;
  const fixtureKeys = Object.keys(fixtureMap).sort();
  const tsCatalog = EncryptionDomains as Readonly<Record<string, string>>;
  const tsKeys = Object.keys(tsCatalog).sort();

  it("has identical constName membership", () => {
    expect(tsKeys).toEqual(fixtureKeys);
  });

  for (const constName of fixtureKeys) {
    it(`constant ${constName} has identical wire value`, () => {
      const fixtureValue = fixtureMap[constName];
      const tsValue = tsCatalog[constName];
      expect(tsValue).toBe(fixtureValue);
    });
  }

  it("canonical maps are byte-equal", () => {
    const tsAsMap: Record<string, string> = {};
    for (const k of tsKeys) tsAsMap[k] = tsCatalog[k]!;
    expect(canonicalize(tsAsMap)).toEqual(canonicalize(fixtureMap));
  });

  it("PLAINTEXT sentinel is the closed-catalog 'plaintext' string", () => {
    expect(EncryptionDomains.PLAINTEXT).toBe("plaintext");
  });
});
