// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  PROBLEM_DETAILS_CONTENT_TYPE,
  PROBLEM_TYPE_URI_PREFIX,
  ProblemDetailsExtensionKeys,
  ProblemDetailsTitles,
} from "@dcsv-io/d2-headers";

import { canonicalize, loadFixture } from "../src/index.js";

interface ConstMap {
  readonly [constName: string]: string;
}

describe("problem-details parity (.NET catalog ↔ TS catalog)", () => {
  describe("PROBLEM_TYPE_URI_PREFIX ↔ problem-details/uri-prefix.json", () => {
    const fixture = loadFixture<ConstMap>("problem-details", "uri-prefix");
    const fixturePrefix = fixture.data["TYPE_URI_PREFIX"];

    it("has identical wire value", () => {
      expect(PROBLEM_TYPE_URI_PREFIX).toBe(fixturePrefix);
    });
  });

  describe("PROBLEM_DETAILS_CONTENT_TYPE ↔ problem-details/content-type.json", () => {
    const fixture = loadFixture<ConstMap>("problem-details", "content-type");
    const fixtureContentType = fixture.data["CONTENT_TYPE"];

    it("has identical wire value", () => {
      expect(PROBLEM_DETAILS_CONTENT_TYPE).toBe(fixtureContentType);
    });
  });

  describe("ProblemDetailsExtensionKeys ↔ problem-details/extension-keys.json", () => {
    const fixture = loadFixture<ConstMap>("problem-details", "extension-keys");
    const fixtureMap = fixture.data;
    const fixtureKeys = Object.keys(fixtureMap).sort();
    const tsKeys = Object.keys(ProblemDetailsExtensionKeys).sort();
    const tsCatalog = ProblemDetailsExtensionKeys as unknown as ConstMap;

    it("has identical constName membership", () => {
      expect(tsKeys).toEqual(fixtureKeys);
    });

    // Per-VALUE pin: every fixture entry asserted individually so a
    // failure message names the specific drifted constant.
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
  });

  describe("ProblemDetailsTitles ↔ problem-details/titles.json", () => {
    const fixture = loadFixture<ConstMap>("problem-details", "titles");
    const fixtureMap = fixture.data;
    const fixtureKeys = Object.keys(fixtureMap).sort();
    const tsKeys = Object.keys(ProblemDetailsTitles).sort();
    const tsCatalog = ProblemDetailsTitles as unknown as ConstMap;

    it("has identical constName membership", () => {
      expect(tsKeys).toEqual(fixtureKeys);
    });

    // Per-VALUE pin: every fixture entry asserted individually so a
    // failure message names the specific drifted constant.
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
  });
});
