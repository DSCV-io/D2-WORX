// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  ALL_AUTH_ERROR_CODES,
  AuthErrorCodes,
  AuthFailures,
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

  describe("AuthFailures factory names ↔ auth-error-codes/factory-names.json", () => {
    const fixture = loadFixture<ConstMap>("auth-error-codes", "factory-names");
    const fixtureMap = fixture.data;
    const fixtureCodes = Object.keys(fixtureMap).sort();
    const failures = AuthFailures as unknown as Record<
      string,
      (traceId?: string) => { errorCode?: string }
    >;

    // Per-VALUE pin: the .NET fixture maps code → camelCase factory name; the
    // TS-emitted AuthFailures must expose that exact camelCase method, and
    // calling it must stamp that same code.
    for (const code of fixtureCodes) {
      it(`code ${code} has TS factory '${fixtureMap[code]}' producing the same code`, () => {
        const fnName = fixtureMap[code]!;
        const fn = failures[fnName];
        expect(typeof fn).toBe("function");
        expect(fn!().errorCode).toBe(code);
      });
    }
  });

  describe("AuthFailures wire userMessageKey ↔ auth-error-codes/user-message-keys.json", () => {
    const fixture = loadFixture<ConstMap>(
      "auth-error-codes",
      "user-message-keys",
    );
    const fixtureMap = fixture.data;
    const fixtureCodes = Object.keys(fixtureMap).sort();
    const factoryNames = loadFixture<ConstMap>(
      "auth-error-codes",
      "factory-names",
    ).data;
    const failures = AuthFailures as unknown as Record<
      string,
      (traceId?: string) => { messages: readonly { key: string }[] }
    >;

    // Cross-runtime wire-key parity guard: the .NET fixture pins the ACTUAL
    // .NET wire key (the snake key, e.g. auth_errors_UNAUTHORIZED). The TS
    // factory's wire key (messages[0].key) MUST equal it — directly surfacing
    // any symbol-vs-snake drift between the runtimes.
    for (const code of fixtureCodes) {
      it(`code ${code} TS wire key matches the .NET snake key`, () => {
        const fnName = factoryNames[code]!;
        const result = failures[fnName]!();
        expect(result.messages[0]?.key).toBe(fixtureMap[code]);
      });
    }

    it("canonical maps are byte-equal", () => {
      const tsWireKeys: Record<string, string> = {};
      for (const code of fixtureCodes) {
        const fnName = factoryNames[code]!;
        tsWireKeys[code] = failures[fnName]!().messages[0]!.key;
      }
      expect(canonicalize(tsWireKeys)).toEqual(canonicalize(fixtureMap));
    });
  });
});
