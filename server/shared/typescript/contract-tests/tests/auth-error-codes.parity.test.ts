// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  ALL_AUTH_ERROR_CODES,
  AuthErrorCodes,
  getAuthErrorHttpStatus,
} from "@d2/auth-abstractions";

import { canonicalize, loadFixture } from "../src/index.js";

interface ConstMap {
  readonly [constName: string]: string;
}

interface HttpStatusMap {
  readonly [constName: string]: number;
}

describe("auth-error-codes parity (.NET catalog ↔ TS catalog)", () => {
  describe("AuthErrorCodes ↔ auth-error-codes/codes.json", () => {
    const fixture = loadFixture<ConstMap>("auth-error-codes", "codes");
    const fixtureMap = fixture.data;
    const fixtureKeys = Object.keys(fixtureMap).sort();
    const tsKeys = Object.keys(AuthErrorCodes).sort();
    const tsCatalog = AuthErrorCodes as unknown as ConstMap;

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

    it("ALL_AUTH_ERROR_CODES contains every fixture entry", () => {
      expect([...ALL_AUTH_ERROR_CODES].sort()).toEqual(fixtureKeys);
    });
  });

  describe("getAuthErrorHttpStatus ↔ auth-error-codes/http-statuses.json", () => {
    const fixture = loadFixture<HttpStatusMap>(
      "auth-error-codes",
      "http-statuses",
    );
    const fixtureMap = fixture.data;
    const fixtureKeys = Object.keys(fixtureMap).sort();

    // Per-VALUE pin: every code's HTTP status asserted individually so a
    // drift names the specific code + the .NET-vs-TS divergence.
    for (const code of fixtureKeys) {
      it(`code ${code} has identical httpStatus mapping`, () => {
        const fixtureStatus = fixtureMap[code];
        const tsStatus = getAuthErrorHttpStatus(code);
        expect(tsStatus).toBe(fixtureStatus);
      });
    }
  });
});
