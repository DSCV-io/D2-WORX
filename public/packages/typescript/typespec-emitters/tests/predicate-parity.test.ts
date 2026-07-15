// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// The TypeScript half of the cross-language @d2Resilience predicate-behavior
// parity suite. Drives the SAME shared fixture
// (contracts/resilience/predicate-parity.fixture.json) as the .NET
// PredicateParityTests, importing the ACTUAL emitted predicate twin
// (place-order-fixture-resilience-predicates.g.ts — the committed byte-gated fixture)
// and evaluating placeOrderFixtureRetryWhen / placeOrderFixtureFailWhen over every row. An
// identically-shaped reconstructed D2Result must yield the SAME retry / fail
// booleans in TS as in C#; a divergence breaks the cross-language emission
// contract and must be surfaced (not reconciled by editing an expected value).
//
// The emitted .g.ts carries `// @ts-nocheck` (it references the D2Result<T> /
// PlaceOrderFixtureOutput types that wire up only in a real consumer) and lives in the
// edge test project (outside this package's rootDir), so it cannot be a typed
// `import`. Instead the test reads the committed file TEXT and reconstructs the
// two predicate functions from the emitted arrow-function bodies via `new
// Function` — at RUNTIME the predicate is plain JS (type annotations erased, the
// duck-typed fixture object satisfies every property it reads). Driving the
// ACTUAL committed bytes (the byte-gate pins them) keeps this parity test
// NON-VACUOUS — it exercises exactly the emitter output, not a re-declaration.

import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { findRepoRoot } from "./repo-root.js";

type Predicate = (r: unknown) => boolean;

const _REPO = findRepoRoot(import.meta.url);
const _KC_GEN = join(
  _REPO,
  "private/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcPredicate/Generated",
);

/**
 * Read the committed emitted predicate twin and reconstruct `placeOrderFixtureRetryWhen`
 * / `placeOrderFixtureFailWhen` from their emitted arrow-function bodies. The emitted
 * shape is `export const <name> = (r: D2Result<…>): boolean =>\n    <expr>;`.
 */
function loadEmittedPredicates(): {
  retryWhen: Predicate;
  failWhen: Predicate;
} {
  const file = join(_KC_GEN, "place-order-fixture-resilience-predicates.g.ts");
  const text = readFileSync(file, "utf8");
  return {
    retryWhen: extractPredicate(text, "placeOrderFixtureRetryWhen"),
    failWhen: extractPredicate(text, "placeOrderFixtureFailWhen"),
  };
}

/** Extract one emitted predicate's boolean expression and build a callable. */
function extractPredicate(source: string, name: string): Predicate {
  // Match: export const <name> = (r: ...): boolean =>\n    <expr>;
  const re = new RegExp(
    `export const ${name} = \\(r: [^)]*\\): boolean =>\\s*([\\s\\S]*?);`,
  );
  const m = re.exec(source);
  if (m === null) throw new Error(`could not extract predicate ${name}`);

  return new Function("r", `return (${m[1]!.trim()});`) as Predicate;
}

const {
  retryWhen: placeOrderFixtureRetryWhen,
  failWhen: placeOrderFixtureFailWhen,
} = loadEmittedPredicates();

// ---------------------------------------------------------------------------
// Shared fixture loader
// ---------------------------------------------------------------------------

interface ParityData {
  readonly orderCode: string;
  readonly itemStatuses: readonly string[];
  readonly partial: boolean;
}

interface ParityCase {
  readonly name: string;
  readonly success: boolean;
  readonly statusCode: number;
  readonly errorCode?: string;
  readonly category?: string;
  readonly data?: ParityData;
  readonly expectedRetry: boolean;
  readonly expectedFail: boolean;
}

interface FixtureFile {
  readonly schemaVersion: number;
  readonly cases: readonly ParityCase[];
}

function loadFixture(): FixtureFile {
  const path = join(
    _REPO,
    "contracts/resilience/predicate-parity.fixture.json",
  );
  return JSON.parse(readFileSync(path, "utf8")) as FixtureFile;
}

/**
 * Build the runtime D2Result-shaped object the emitted predicate reads. A null
 * `data` / `errorCode` / `category` maps to `undefined` (the TS D2Result uses
 * `T | undefined`, and JSON `null` from a .NET nullable normalizes to undefined
 * at the deserialization boundary) so the predicate's `?.` short-circuits.
 */
function toResult(c: ParityCase): unknown {
  return {
    success: c.success,
    statusCode: c.statusCode,
    errorCode: c.errorCode ?? undefined,
    category: c.category ?? undefined,
    data: c.data ?? undefined,
  };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("predicateParity_SharedFixtureLoads", () => {
  it("the shared fixture is present and non-empty", () => {
    const fx = loadFixture();
    expect(fx.cases.length).toBeGreaterThan(0);
  });

  it("the matrix is non-vacuous — both retry and fail span true AND false", () => {
    const cases = loadFixture().cases;
    expect(cases.some((c) => c.expectedRetry)).toBe(true);
    expect(cases.some((c) => !c.expectedRetry)).toBe(true);
    expect(cases.some((c) => c.expectedFail)).toBe(true);
    expect(cases.some((c) => !c.expectedFail)).toBe(true);
    // failWhen-wins row (both true) present.
    expect(cases.some((c) => c.expectedRetry && c.expectedFail)).toBe(true);
  });
});

describe("predicateParity_EmittedTsPredicate_MatchesExpected", () => {
  it("placeOrderFixtureRetryWhen + placeOrderFixtureFailWhen match the cross-language expectation for every row", () => {
    const fx = loadFixture();

    for (const c of fx.cases) {
      const r = toResult(c);
      expect(
        placeOrderFixtureRetryWhen(r),
        `retryWhen for '${c.name}' must match the cross-language expectation`,
      ).toBe(c.expectedRetry);
      expect(
        placeOrderFixtureFailWhen(r),
        `failWhen for '${c.name}' must match the cross-language expectation`,
      ).toBe(c.expectedFail);
    }
  });
});

// ---------------------------------------------------------------------------
// placeOrderV2 — the NESTED-model + array-of-MODEL parity matrix.
//
// Drives the SAME shared nested fixture (predicate-parity-nested.fixture.json) as
// the .NET PredicateParityTests, executing the ACTUAL committed emitted twin
// (place-order-v2-fixture-resilience-predicates.g.ts) over a deep `?.`-chain
// (customer.tier) + an array-of-MODEL quantifier (lines.any(l => l.status)). This
// is the cross-runtime BEHAVIORAL proof of the rich emission the flat placeOrder
// matrix cannot exercise — the emitted predicate is EVALUATED, not string-asserted.
// ---------------------------------------------------------------------------

interface ParityLine {
  readonly status: string;
}

interface ParityDataV2 {
  readonly orderCode: string;
  readonly lines: readonly ParityLine[];
  readonly customer: { readonly tier: string } | null;
}

interface ParityCaseV2 {
  readonly name: string;
  readonly success: boolean;
  readonly statusCode: number;
  readonly errorCode?: string;
  readonly data?: ParityDataV2;
  readonly expectedRetry: boolean;
  readonly expectedFail: boolean;
}

interface FixtureFileV2 {
  readonly schemaVersion: number;
  readonly cases: readonly ParityCaseV2[];
}

function loadFixtureV2(): FixtureFileV2 {
  const path = join(
    _REPO,
    "contracts/resilience/predicate-parity-nested.fixture.json",
  );
  return JSON.parse(readFileSync(path, "utf8")) as FixtureFileV2;
}

function loadEmittedPredicatesV2(): {
  retryWhen: Predicate;
  failWhen: Predicate;
} {
  const file = join(
    _KC_GEN,
    "place-order-v2-fixture-resilience-predicates.g.ts",
  );
  const text = readFileSync(file, "utf8");
  return {
    retryWhen: extractPredicate(text, "placeOrderV2FixtureRetryWhen"),
    failWhen: extractPredicate(text, "placeOrderV2FixtureFailWhen"),
  };
}

function toResultV2(c: ParityCaseV2): unknown {
  return {
    success: c.success,
    statusCode: c.statusCode,
    errorCode: c.errorCode ?? undefined,
    data: c.data ?? undefined,
  };
}

const { retryWhen: placeOrderV2RetryWhen, failWhen: placeOrderV2FailWhen } =
  loadEmittedPredicatesV2();

describe("predicateParityV2_NestedAndArrayOfModel_SharedFixtureLoads", () => {
  it("the shared nested fixture is present and non-empty", () => {
    expect(loadFixtureV2().cases.length).toBeGreaterThan(0);
  });

  it("the nested matrix is non-vacuous AND genuinely exercises the nested path + array quantifier", () => {
    const cases = loadFixtureV2().cases;
    // Both outcomes for both predicates + a failWhen-wins (both-true) row.
    expect(cases.some((c) => c.expectedRetry)).toBe(true);
    expect(cases.some((c) => !c.expectedRetry)).toBe(true);
    expect(cases.some((c) => c.expectedFail)).toBe(true);
    expect(cases.some((c) => !c.expectedFail)).toBe(true);
    expect(cases.some((c) => c.expectedRetry && c.expectedFail)).toBe(true);
    // The nested path is genuinely traversed: a present TRIAL customer with NO
    // PENDING line drives retry SOLELY via customer.tier (so the deep ?.-chain
    // must resolve), and a present non-TRIAL customer is non-vacuously false.
    expect(
      cases.some(
        (c) =>
          c.data?.customer?.tier === "TRIAL" &&
          !c.data.lines.some((l) => l.status === "PENDING") &&
          c.expectedRetry,
      ),
      "a row must drive retry via the nested customer.tier path alone",
    ).toBe(true);
    // The array-of-model quantifier is genuinely traversed: a row with NO
    // customer but a PENDING line drives retry SOLELY via lines.any(...).
    expect(
      cases.some(
        (c) =>
          (c.data?.customer ?? null) === null &&
          (c.data?.lines.some((l) => l.status === "PENDING") ?? false) &&
          c.expectedRetry,
      ),
      "a row must drive retry via the array-of-model lines.any(...) quantifier alone",
    ).toBe(true);
  });
});

describe("predicateParityV2_EmittedTsPredicate_MatchesExpected", () => {
  it("placeOrderV2RetryWhen + placeOrderV2FailWhen (deep ?.-chain + array quantifier) match the cross-language expectation for every row", () => {
    const fx = loadFixtureV2();

    for (const c of fx.cases) {
      const r = toResultV2(c);
      expect(
        placeOrderV2RetryWhen(r),
        `retryWhen for '${c.name}' must match the cross-language expectation`,
      ).toBe(c.expectedRetry);
      expect(
        placeOrderV2FailWhen(r),
        `failWhen for '${c.name}' must match the cross-language expectation`,
      ).toBe(c.expectedFail);
    }
  });
});
