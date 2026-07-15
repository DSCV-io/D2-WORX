// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  errorCodeRegistry,
  type ErrorCategory,
} from "@d2/error-codes-registry";

import { canonicalize, loadFixture } from "../src/index.js";

// ---------------------------------------------------------------------------
// Cross-runtime parity guard — error-code registry.
//
// The .NET side emits `contract-tests/fixtures/error-codes-registry/registry.json`
// (via `ErrorCodeRegistryFixtureEmitter`). Each entry carries 8 fields plus the
// extra `userMessageKeyPath` helper field the fixture emitter adds for comparison
// convenience. The TS side exposes the same data via `errorCodeRegistry` from
// `@d2/error-codes-registry`.
//
// PARITY AXES (per code):
//   httpStatus        : fixture.httpStatus         === ts.httpStatus
//   factoryName       : fixture.factoryName         === ts.factoryName
//   factoryShape      : fixture.factoryShape        === ts.factoryShape
//   doc               : fixture.doc                 === ts.doc
//   domain            : fixture.domain              === ts.domain
//   category (wire)   : fixture.category            === ts.category
//     (both are the snake wire string; the .NET ErrorCategory enum's JSON
//     converter serializes it as the snake wire value so both sides agree)
//   userMessageKey    : fixture.userMessageKeySnake  === ts.userMessageKey.key
//     (TKMessage.key is the snake wire form; both sides derive from the same
//     TK.*.* constant path, so the snake forms must be byte-identical)
//
// ADVERSARIAL ASSERTIONS (registry-level):
//   - Total count pinned at 30 (15 generic + 15 auth) — public dual-values half.
//     Private KeyCustodian catalog is excluded from the public ship surface.
//   - Code sets are identical: no code present in one runtime only.
//   - No duplicate codes within either side.
//   - Every category value is one of the 9 canonical wire strings.
//   - Every domain is non-empty.
//   - Unknown-code hard not-found: resolve/has return undefined/false for
//     a code not in the registry.
//   - Case sensitivity: lowercase code does NOT resolve.
// ---------------------------------------------------------------------------

const _EXPECTED_COUNT = 30;

const _VALID_CATEGORIES = new Set<string>([
  "validation_failure",
  "not_found",
  "conflict",
  "policy_denied",
  "rate_limited",
  "payload_too_large",
  "infrastructure_unavailable",
  "internal_error",
  "partial_success",
] satisfies ErrorCategory[]);

// ---------------------------------------------------------------------------
// Fixture shape (the .NET emitter's JSON envelope).
// ---------------------------------------------------------------------------

interface RegistryFixtureEntry {
  readonly code: string;
  readonly httpStatus: number;
  readonly category: string; // snake wire string — parity axis against ts.category
  readonly userMessageKeySnake: string; // snake wire form of TK constant
  readonly userMessageKeyPath: string; // TK.*.* symbol path (informational)
  readonly factoryName: string;
  readonly factoryShape: string;
  readonly doc: string;
  readonly domain: string;
}

type RegistryFixtureData = Record<string, RegistryFixtureEntry>;

const fixture = loadFixture<RegistryFixtureData>(
  "error-codes-registry",
  "registry",
);
const fixtureData = fixture.data;
const fixtureCodes = Object.keys(fixtureData).sort();

// ---------------------------------------------------------------------------
// TS registry as a sorted flat array for canonical comparison.
// ---------------------------------------------------------------------------

const tsAll = errorCodeRegistry.all;
const tsCodes = [...tsAll].map((e) => e.code).sort();
const tsByCode = new Map(tsAll.map((e) => [e.code, e]));

// ---------------------------------------------------------------------------
// 1. Membership — code SETS are identical.
// ---------------------------------------------------------------------------

describe("error-codes-registry parity (.NET registry ↔ TS registry)", () => {
  describe("code-set membership", () => {
    it(`total count is pinned at ${_EXPECTED_COUNT}`, () => {
      expect(fixtureCodes.length).toBe(_EXPECTED_COUNT);
      expect(tsAll.length).toBe(_EXPECTED_COUNT);
    });

    it("TS registry contains every code in the .NET fixture", () => {
      const missingInTs = fixtureCodes.filter((c) => !tsByCode.has(c));
      expect(missingInTs).toEqual([]);
    });

    it(".NET fixture contains every code in the TS registry", () => {
      const missingInFixture = tsCodes.filter((c) => !(c in fixtureData));
      expect(missingInFixture).toEqual([]);
    });

    it("sorted code lists are identical (byte-equal membership)", () => {
      expect(tsCodes).toEqual(fixtureCodes);
    });

    it("no duplicate codes in the .NET fixture", () => {
      const seen = new Set<string>();
      const dupes: string[] = [];
      for (const code of fixtureCodes)
        if (!seen.has(code)) seen.add(code);
        else dupes.push(code);
      expect(dupes).toEqual([]);
    });

    it("no duplicate codes in the TS registry", () => {
      const seen = new Set<string>();
      const dupes: string[] = [];
      for (const entry of tsAll)
        if (!seen.has(entry.code)) seen.add(entry.code);
        else dupes.push(entry.code);
      expect(dupes).toEqual([]);
    });
  });

  // -------------------------------------------------------------------------
  // 2. Per-code data equality — data-driven so failures name the code + field.
  // -------------------------------------------------------------------------

  describe("per-code data equality", () => {
    for (const code of fixtureCodes) {
      const fx = fixtureData[code]!;
      const ts = tsByCode.get(code);

      it(`${code}: TS entry exists`, () => {
        expect(ts).toBeDefined();
      });

      it(`${code}: httpStatus`, () => {
        expect(ts!.httpStatus).toBe(fx.httpStatus);
      });

      it(`${code}: factoryName`, () => {
        expect(ts!.factoryName).toBe(fx.factoryName);
      });

      it(`${code}: factoryShape`, () => {
        expect(ts!.factoryShape).toBe(fx.factoryShape);
      });

      it(`${code}: doc`, () => {
        expect(ts!.doc).toBe(fx.doc);
      });

      it(`${code}: domain`, () => {
        expect(ts!.domain).toBe(fx.domain);
      });

      // category: fixture.category (snake wire string) === ts.category (snake union value)
      it(`${code}: category wire string — fixture.category === ts.category`, () => {
        expect(ts!.category).toBe(fx.category);
      });

      // userMessageKey: fixture.userMessageKeySnake === ts.userMessageKey.key
      it(`${code}: userMessageKey.key === fixture.userMessageKeySnake`, () => {
        expect(ts!.userMessageKey.key).toBe(fx.userMessageKeySnake);
      });
    }
  });

  // -------------------------------------------------------------------------
  // 3. Adversarial registry-level assertions.
  // -------------------------------------------------------------------------

  describe("adversarial registry-level assertions", () => {
    it("every TS category value is a canonical wire string", () => {
      const badCategories = tsAll
        .filter((e) => !_VALID_CATEGORIES.has(e.category))
        .map((e) => `${e.code}: "${e.category}"`);
      expect(badCategories).toEqual([]);
    });

    it("every domain is non-empty", () => {
      const emptyDomain = tsAll.filter((e) => !e.domain).map((e) => e.code);
      expect(emptyDomain).toEqual([]);
    });

    it("every fixture category is a canonical wire string", () => {
      const badCategories = fixtureCodes
        .filter((c) => !_VALID_CATEGORIES.has(fixtureData[c]!.category))
        .map((c) => `${c}: "${fixtureData[c]!.category}"`);
      expect(badCategories).toEqual([]);
    });

    it("every fixture domain is non-empty", () => {
      const emptyDomain = fixtureCodes.filter((c) => !fixtureData[c]!.domain);
      expect(emptyDomain).toEqual([]);
    });

    it("unknown code resolves to undefined (hard not-found)", () => {
      expect(errorCodeRegistry.resolve("NOPE_NOT_A_CODE")).toBeUndefined();
    });

    it("unknown code has() returns false", () => {
      expect(errorCodeRegistry.has("NOPE_NOT_A_CODE")).toBe(false);
    });

    it("empty string resolves to undefined", () => {
      expect(errorCodeRegistry.resolve("")).toBeUndefined();
    });

    it("lowercase code does NOT resolve (case-sensitive)", () => {
      // "not_found" is lowercase — should NOT match the registered "NOT_FOUND"
      expect(errorCodeRegistry.resolve("not_found")).toBeUndefined();
    });

    it("known code resolves to the expected entry (smoke: NOT_FOUND)", () => {
      const info = errorCodeRegistry.resolve("NOT_FOUND");
      expect(info).toBeDefined();
      expect(info!.code).toBe("NOT_FOUND");
      expect(info!.httpStatus).toBe(404);
      expect(info!.category).toBe("not_found");
      expect(info!.domain).toBe("common");
    });

    it("known auth code resolves correctly (smoke: AUTH_JWT_EXPIRED)", () => {
      const info = errorCodeRegistry.resolve("AUTH_JWT_EXPIRED");
      expect(info).toBeDefined();
      expect(info!.code).toBe("AUTH_JWT_EXPIRED");
      expect(info!.httpStatus).toBe(401);
      expect(info!.category).toBe("validation_failure");
      expect(info!.domain).toBe("auth");
    });

    it("all common codes resolve with domain = 'common'", () => {
      // Public registry: non-AUTH codes are common (no KEYCUSTODIAN product domain).
      const nonCommon = tsAll
        .filter((e) => !e.code.startsWith("AUTH_"))
        .filter((e) => e.domain !== "common")
        .map((e) => e.code);
      expect(nonCommon).toEqual([]);
    });

    it("all AUTH_* codes resolve with domain = 'auth'", () => {
      const wrongDomain = tsAll
        .filter((e) => e.code.startsWith("AUTH_"))
        .filter((e) => e.domain !== "auth")
        .map((e) => e.code);
      expect(wrongDomain).toEqual([]);
    });

    it("public registry contains zero KEYCUSTODIAN_* product codes", () => {
      const kc = tsAll
        .filter((e) => e.code.startsWith("KEYCUSTODIAN_"))
        .map((e) => e.code);
      expect(kc).toEqual([]);
    });
  });

  // -------------------------------------------------------------------------
  // 4. Canonical map comparison — the full data record byte-for-byte equal.
  // ---------------------------------------------------------------------------
  //
  // Builds a comparable map from TS registry: code → { httpStatus, category,
  // factoryName, factoryShape, doc, domain, userMessageKeySnake } — same fields
  // the fixture exposes as the parity axis. The fixture's category is the snake
  // wire string (same as ts.category) since the .NET ErrorCategoryJsonConverter
  // serializes it that way.
  // -------------------------------------------------------------------------

  describe("canonical map byte-equality (.NET fixture ↔ TS registry)", () => {
    it("canonical maps of parity fields are byte-equal", () => {
      // Build a normalized comparable map from the fixture.
      const fixtureComparable: Record<
        string,
        {
          httpStatus: number;
          category: string;
          factoryName: string;
          factoryShape: string;
          doc: string;
          domain: string;
          userMessageKeySnake: string;
        }
      > = {};
      for (const code of fixtureCodes) {
        const fx = fixtureData[code]!;
        fixtureComparable[code] = {
          httpStatus: fx.httpStatus,
          category: fx.category,
          factoryName: fx.factoryName,
          factoryShape: fx.factoryShape,
          doc: fx.doc,
          domain: fx.domain,
          userMessageKeySnake: fx.userMessageKeySnake,
        };
      }

      // Build the same shape from the TS registry.
      const tsComparable: typeof fixtureComparable = {};
      for (const entry of tsAll) {
        tsComparable[entry.code] = {
          httpStatus: entry.httpStatus,
          category: entry.category,
          factoryName: entry.factoryName,
          factoryShape: entry.factoryShape,
          doc: entry.doc,
          domain: entry.domain,
          userMessageKeySnake: entry.userMessageKey.key,
        };
      }

      expect(canonicalize(tsComparable)).toEqual(
        canonicalize(fixtureComparable),
      );
    });
  });
});
