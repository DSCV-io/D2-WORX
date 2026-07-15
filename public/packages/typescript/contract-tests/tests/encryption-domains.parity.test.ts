// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  ConsumerServiceByDomain,
  EncryptionDomains,
  EncryptionDomainModes,
} from "@d2/encryption-abstractions";
import { canonicalize, loadFixture } from "../src/index.js";

interface CatalogMap {
  readonly [constName: string]: string;
}

interface ModeEntry {
  readonly mode: string;
  readonly consumerService?: string;
}

interface ModesMap {
  readonly [domainValue: string]: ModeEntry;
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

describe("encryption-domains mode parity (.NET EncryptionDomainModes ↔ TS EncryptionDomainModes)", () => {
  const modes = loadFixture<ModesMap>("encryption-domains", "modes").data;
  const modeKeys = Object.keys(modes).sort();
  const tsModes = EncryptionDomainModes as Readonly<Record<string, string>>;
  const tsModeKeys = Object.keys(tsModes).sort();

  it("EncryptionDomainModes covers exactly the catalog wire values", () => {
    expect(tsModeKeys).toEqual(modeKeys);
  });

  for (const domain of modeKeys) {
    it(`domain ${domain} has identical mode`, () => {
      expect(tsModes[domain]).toBe(modes[domain]!.mode);
    });
  }

  it("ConsumerServiceByDomain matches the sealed subset of the fixture", () => {
    const expected: Record<string, string> = {};
    for (const domain of modeKeys) {
      const consumer = modes[domain]!.consumerService;
      if (consumer !== undefined) expected[domain] = consumer;
    }
    const tsConsumers = ConsumerServiceByDomain as Readonly<
      Record<string, string>
    >;
    expect(canonicalize(tsConsumers)).toEqual(canonicalize(expected));
  });

  it("every sealed domain resolves its own service id as consumerService", () => {
    for (const domain of modeKeys) {
      if (modes[domain]!.mode !== "sealed") continue;
      expect(modes[domain]!.consumerService).toBe(domain);
      expect(
        (ConsumerServiceByDomain as Readonly<Record<string, string>>)[domain],
      ).toBe(domain);
    }
  });

  it("no symmetric / plaintext domain carries a consumerService", () => {
    const tsConsumers = ConsumerServiceByDomain as Readonly<
      Record<string, string>
    >;
    for (const domain of modeKeys) {
      if (modes[domain]!.mode === "sealed") continue;
      expect(modes[domain]!.consumerService).toBeUndefined();
      expect(tsConsumers[domain]).toBeUndefined();
    }
  });
});
