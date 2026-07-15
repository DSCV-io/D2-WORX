// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  DlqFailureMetadataFields,
  DlqFailureCauses,
} from "@dcsv-io/d2-messaging-abstractions";
import { canonicalize, loadFixture } from "../src/index.js";

interface CatalogMap {
  readonly [constName: string]: string;
}

describe("DLQ failure-metadata parity (.NET ↔ TS, two sub-catalogs)", () => {
  describe("fields catalog (DlqFailureMetadataFields)", () => {
    const fixture = loadFixture<CatalogMap>("dlq-failure-metadata", "fields");
    const fixtureMap = fixture.data;
    const fixtureKeys = Object.keys(fixtureMap).sort();
    const tsCatalog = DlqFailureMetadataFields as Readonly<
      Record<string, string>
    >;
    const tsKeys = Object.keys(tsCatalog).sort();

    it("has identical constName membership", () => {
      expect(tsKeys).toEqual(fixtureKeys);
    });

    for (const constName of fixtureKeys) {
      it(`field ${constName} has identical wire value`, () => {
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
  });

  describe("causes catalog (DlqFailureCauses)", () => {
    const fixture = loadFixture<CatalogMap>("dlq-failure-metadata", "causes");
    const fixtureMap = fixture.data;
    const fixtureKeys = Object.keys(fixtureMap).sort();
    const tsCatalog = DlqFailureCauses as Readonly<Record<string, string>>;
    const tsKeys = Object.keys(tsCatalog).sort();

    it("has identical constName membership", () => {
      expect(tsKeys).toEqual(fixtureKeys);
    });

    for (const constName of fixtureKeys) {
      it(`cause ${constName} has identical wire value`, () => {
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
  });
});
