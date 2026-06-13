// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { AuthFailures } from "@d2/auth-abstractions";
import {
  ALL_ERROR_CODES,
  ErrorCodes,
  getErrorHttpStatus,
  type D2Result,
} from "@d2/result";
import * as resultFactories from "@d2/result";

import { canonicalize, loadFixture } from "../src/index.js";

interface ConstMap {
  readonly [constName: string]: string;
}

interface HttpStatusMap {
  readonly [constName: string]: number;
}

describe("error-codes parity (.NET catalog ↔ TS catalog)", () => {
  describe("ErrorCodes ↔ error-codes/codes.json", () => {
    const fixture = loadFixture<ConstMap>("error-codes", "codes");
    const fixtureMap = fixture.data;
    const fixtureKeys = Object.keys(fixtureMap).sort();
    const tsKeys = Object.keys(ErrorCodes).sort();
    const tsCatalog = ErrorCodes as unknown as ConstMap;

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

    it("ALL_ERROR_CODES contains every fixture entry", () => {
      expect([...ALL_ERROR_CODES].sort()).toEqual(fixtureKeys);
    });
  });

  describe("getErrorHttpStatus ↔ error-codes/http-statuses.json", () => {
    const fixture = loadFixture<HttpStatusMap>("error-codes", "http-statuses");
    const fixtureMap = fixture.data;
    const fixtureKeys = Object.keys(fixtureMap).sort();

    // Per-VALUE pin: every code's HTTP status asserted individually so a
    // drift names the specific code + the .NET-vs-TS divergence.
    for (const code of fixtureKeys) {
      it(`code ${code} has identical httpStatus mapping`, () => {
        const fixtureStatus = fixtureMap[code];
        const tsStatus = getErrorHttpStatus(code);
        expect(tsStatus).toBe(fixtureStatus);
      });
    }
  });
});

// ---------------------------------------------------------------------------
// Factory / capability parity (.NET ↔ TS). Asserts the generic FACTORY surface
// and the auth DOMAIN-failure surface match across runtimes by CAPABILITY, not
// structure. The .NET side delivers a typed domain failure via two classes
// (AuthFailures + AuthFailures<T>); the TS side via ONE generic method whose
// `void` default spans the untyped + typed cases. So the invariant is phrased
// behaviorally ("typed + untyped both stamp the right code + render"), never
// "same class count".
// ---------------------------------------------------------------------------

const repoRoot = (() => {
  let dir = dirname(fileURLToPath(import.meta.url));
  for (;;) {
    try {
      readFileSync(join(dir, "pnpm-workspace.yaml"), "utf8");
      return dir;
    } catch {
      const parent = dirname(dir);
      if (parent === dir)
        throw new Error("could not locate repo root (no pnpm-workspace.yaml)");
      dir = parent;
    }
  }
})();

interface SpecEntry {
  readonly code: string;
  readonly httpStatus: number;
  readonly category: string;
  readonly factoryName: string;
  readonly factoryShape: string;
  readonly userMessageKey: string;
}

function loadSpec(...parts: string[]): readonly SpecEntry[] {
  const parsed = JSON.parse(readFileSync(join(repoRoot, ...parts), "utf8")) as {
    readonly errorCodes: readonly SpecEntry[];
  };
  return parsed.errorCodes;
}

function camelCase(pascal: string): string {
  return pascal.length === 0
    ? pascal
    : pascal[0]!.toLowerCase() + pascal.slice(1);
}

function snakeFromSymbolPath(symbolPath: string): string {
  const segments = symbolPath.split(".");
  const domain = segments[1]![0]!.toLowerCase() + segments[1]!.slice(1);
  const category = segments[2]![0]!.toLowerCase() + segments[2]!.slice(1);
  return `${domain}_${category}_${segments[3]}`;
}

const genericFactories = resultFactories as unknown as Record<
  string,
  (opts?: unknown) => D2Result<unknown>
>;

describe("factory capability parity (.NET base factories ↔ TS base factories)", () => {
  const genericSpec = loadSpec(
    "contracts",
    "error-codes",
    "error-codes.spec.json",
  );

  for (const entry of genericSpec) {
    if (entry.factoryShape === "none") continue;

    const fnName = camelCase(entry.factoryName);

    // long test description — cannot wrap
    it(`${entry.code}: TS ${fnName}() stamps the spec code + status + snake wire key + category`, () => {
      const fn = genericFactories[fnName];
      expect(typeof fn).toBe("function");

      const result = fn!();
      // Same errorCode the spec declares (= the .NET factory's errorCode).
      expect(result.errorCode).toBe(entry.code);
      // Same statusCode the spec declares (= the .NET HttpStatusCode).
      expect(result.statusCode).toBe(entry.httpStatus);
      // Same wire userMessageKey (snake) the .NET factory emits.
      expect(result.messages[0]?.key).toBe(
        snakeFromSymbolPath(entry.userMessageKey),
      );
      // Same category wire string the spec declares (= the .NET factory's Category).
      expect(result.category).toBe(entry.category);
    });
  }
});

// long describe label — cannot wrap
describe("auth domain-failure capability parity (.NET AuthFailures + AuthFailures<T> ↔ TS generic method)", () => {
  const authSpec = loadSpec(
    "contracts",
    "auth-error-codes",
    "auth-error-codes.spec.json",
  );

  const failures = AuthFailures as unknown as Record<
    string,
    <T = void>(opts?: { traceId?: string }) => D2Result<T>
  >;

  for (const entry of authSpec) {
    if (entry.factoryShape === "none") continue;

    const fnName = camelCase(entry.factoryName);

    // long test description — cannot wrap
    it(`${entry.code}: untyped x() AND typed x<T>() both stamp the code + the same wire key`, () => {
      const fn = failures[fnName];
      expect(typeof fn).toBe("function");

      // Untyped call → the .NET AuthFailures (non-generic) equivalent.
      const untyped = fn!();
      // Typed call → the .NET AuthFailures<T> equivalent. One TS method spans
      // both; the .NET side needs two classes for the same capability.
      const typed = fn!<{ id: string }>();

      const wireKey = snakeFromSymbolPath(entry.userMessageKey);

      expect(untyped.errorCode).toBe(entry.code);
      expect(typed.errorCode).toBe(entry.code);
      expect(untyped.messages[0]?.key).toBe(wireKey);
      expect(typed.messages[0]?.key).toBe(wireKey);
    });
  }
});
