// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Compile-host tests for the @d2Resilience result-predicate validation surface:
// the decorator-body registry checks (validateResultPredicate) AND the
// $onValidate model-graph walk (predicate-model-walk) against a real TOutput.
//
// Each rejection asserts BOTH the diagnostic code AND program.hasError() === true
// so a severity regression (a "warning" slipping through) is detectable
// (non-vacuous, per the §1.29 rule and the decorators.test.ts precedent).
// Acceptance cases assert the absence of the predicate diagnostics.

import {
  createTestLibrary,
  createTestWrapper,
  findTestPackageRoot,
} from "@typespec/compiler/testing";
import { HttpTestLibrary } from "@typespec/http/testing";
import { describe, it, expect, beforeAll } from "vitest";
import type { BasicTestRunner } from "@typespec/compiler/testing";
import { createTestHost } from "@typespec/compiler/testing";
import {
  D2_RESILIENCE_RETRY_WHEN_KEY,
  D2_RESILIENCE_FAIL_WHEN_KEY,
} from "../src/index.js";

const D2DecoratorTestLibrary = createTestLibrary({
  name: "@dcsv-io/d2-typespec-decorators",
  packageRoot: await findTestPackageRoot(import.meta.url),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

// A reusable model fixture (the spec §4c PlaceOrder shape) prepended to each
// compile so the model-graph walk has nested + collection + scalar fields.
const MODEL_FIXTURE = `
  model VendorInfo {
    tier: string;
  }

  model OrderItem {
    itemId: string;
    status: string;
    quantity: int32;
  }

  model PlaceOrderOutput {
    orderId: string;
    items: OrderItem[];
    retryCodes: string[];
    vendor?: VendorInfo;
  }
`;

let runner: BasicTestRunner;

beforeAll(async () => {
  const host = await createTestHost({
    libraries: [D2DecoratorTestLibrary, HttpTestLibrary],
  });
  runner = createTestWrapper(host, {
    autoImports: ["@dcsv-io/d2-typespec-decorators"],
    autoUsings: ["D2"],
  });
});

function diagCodes(): string[] {
  return runner.program.diagnostics.map((d) => d.code);
}

const Q = (code: string): string => `@dcsv-io/d2-typespec-decorators/${code}`;

/** Compile an op whose output is PlaceOrderOutput and that carries one predicate. */
async function compileWithPredicate(predicateArg: string): Promise<void> {
  await runner.diagnose(`
    ${MODEL_FIXTURE}

    @d2Query
    @d2Internal
    @d2Resilience("retry()", ${predicateArg})
    op placeOrder(): PlaceOrderOutput;
  `);
}

// ----------------------------------------------------------------
// Valid — model-grounded (no predicate diagnostics)
// ----------------------------------------------------------------

describe("validate_NestedNullablePath_Ok", () => {
  it("accepts result.data.vendor.tier (vendor optional → nullable boundary, no error)", async () => {
    await compileWithPredicate(
      '#{ retryWhen: "result.data.vendor.tier == \\"TRIAL\\"" }',
    );
    expect(diagCodes()).not.toContain(
      Q("resilience-predicate-unknown-output-field"),
    );
    expect(diagCodes()).not.toContain(Q("resilience-predicate-type-mismatch"));
  });
});

describe("validate_AnyQuantifier_Ok", () => {
  it('accepts result.data.items.any(i => i.status == "PENDING")', async () => {
    await compileWithPredicate(
      '#{ retryWhen: "result.data.items.any(i => i.status == \\"PENDING\\")" }',
    );
    expect(diagCodes()).not.toContain(
      Q("resilience-predicate-unknown-element-field"),
    );
    expect(diagCodes()).not.toContain(
      Q("resilience-predicate-not-a-collection"),
    );
  });
});

describe("validate_CountZero_Ok", () => {
  it("accepts result.data.items.count == 0", async () => {
    await compileWithPredicate('#{ failWhen: "result.data.items.count == 0" }');
    expect(diagCodes()).not.toContain(
      Q("resilience-predicate-not-a-collection"),
    );
    expect(diagCodes()).not.toContain(Q("resilience-predicate-type-mismatch"));
  });
});

describe("validate_ContainsScalarCollection_Ok", () => {
  it('accepts result.data.retryCodes.contains("RETRY_ME") (string-element collection)', async () => {
    await compileWithPredicate(
      '#{ retryWhen: "result.data.retryCodes.contains(\\"RETRY_ME\\")" }',
    );
    expect(diagCodes()).not.toContain(
      Q("resilience-predicate-not-a-collection"),
    );
    expect(diagCodes()).not.toContain(Q("resilience-predicate-type-mismatch"));
  });
});

describe("validate_TerminalScalarType_Ok", () => {
  it("accepts result.data.items.any(i => i.quantity == 5) — int field vs int literal", async () => {
    await compileWithPredicate(
      '#{ retryWhen: "result.data.items.any(i => i.quantity == 5)" }',
    );
    expect(diagCodes()).not.toContain(Q("resilience-predicate-type-mismatch"));
    expect(diagCodes()).not.toContain(
      Q("resilience-predicate-unknown-element-field"),
    );
  });
});

describe("validate_EnvelopeOnly_SkipsModelWalk_Ok", () => {
  it("accepts an envelope-only predicate with no data path (no model walk needed)", async () => {
    await compileWithPredicate(
      '#{ retryWhen: "result.category == \\"infrastructure_unavailable\\"" }',
    );
    expect(runner.program.hasError()).toBe(false);
  });
});

// ----------------------------------------------------------------
// Invalid — unknown output field
// ----------------------------------------------------------------

describe("validate_UnknownOutputField_Reject", () => {
  it("rejects result.data.bogus with unknown-output-field + hasError()", async () => {
    await compileWithPredicate('#{ retryWhen: "result.data.bogus == 1" }');
    expect(diagCodes()).toContain(
      Q("resilience-predicate-unknown-output-field"),
    );
    expect(runner.program.hasError()).toBe(true);
  });

  it("rejects a bad mid-path segment result.data.vendor.bogus", async () => {
    await compileWithPredicate(
      '#{ retryWhen: "result.data.vendor.bogus == \\"x\\"" }',
    );
    expect(diagCodes()).toContain(
      Q("resilience-predicate-unknown-output-field"),
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

// ----------------------------------------------------------------
// Invalid — array accessor on a non-collection
// ----------------------------------------------------------------

describe("validate_CountOnNonCollection_Reject", () => {
  it("rejects result.data.orderId.count with not-a-collection + hasError()", async () => {
    await compileWithPredicate(
      '#{ failWhen: "result.data.orderId.count == 0" }',
    );
    expect(diagCodes()).toContain(Q("resilience-predicate-not-a-collection"));
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("validate_AnyOnNonCollection_Reject", () => {
  it("rejects result.data.vendor.any(...) with not-a-collection + hasError()", async () => {
    await compileWithPredicate(
      '#{ retryWhen: "result.data.vendor.any(v => v.tier == \\"X\\")" }',
    );
    expect(diagCodes()).toContain(Q("resilience-predicate-not-a-collection"));
    expect(runner.program.hasError()).toBe(true);
  });
});

// ----------------------------------------------------------------
// Invalid — unknown element field
// ----------------------------------------------------------------

describe("validate_UnknownElementField_Reject", () => {
  it('rejects result.data.items.any(i => i.bogus == "x") with unknown-element-field + hasError()', async () => {
    await compileWithPredicate(
      '#{ retryWhen: "result.data.items.any(i => i.bogus == \\"x\\")" }',
    );
    expect(diagCodes()).toContain(
      Q("resilience-predicate-unknown-element-field"),
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

// ----------------------------------------------------------------
// Invalid — model-dependent terminal type-mismatch
// ----------------------------------------------------------------

describe("validate_TerminalTypeMismatch_Reject", () => {
  it('rejects result.data.items.any(i => i.quantity == "x") (int32 vs string) + hasError()', async () => {
    await compileWithPredicate(
      '#{ retryWhen: "result.data.items.any(i => i.quantity == \\"x\\")" }',
    );
    expect(diagCodes()).toContain(Q("resilience-predicate-type-mismatch"));
    expect(runner.program.hasError()).toBe(true);
  });

  it("rejects result.data.retryCodes.contains(5) (string element vs int literal) + hasError()", async () => {
    await compileWithPredicate(
      '#{ retryWhen: "result.data.retryCodes.contains(5)" }',
    );
    expect(diagCodes()).toContain(Q("resilience-predicate-type-mismatch"));
    expect(runner.program.hasError()).toBe(true);
  });
});

// ----------------------------------------------------------------
// Invalid — unknown error code (registry, decorator-body arm)
// ----------------------------------------------------------------

describe("validate_UnknownErrorCode_Reject", () => {
  it('rejects result.errorCode == "NOT_A_CODE" with unknown-error-code + hasError()', async () => {
    await compileWithPredicate(
      '#{ retryWhen: "result.errorCode == \\"NOT_A_CODE\\"" }',
    );
    expect(diagCodes()).toContain(Q("resilience-predicate-unknown-error-code"));
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("validate_PerDomainErrorCodeAccepted_Ok", () => {
  it("accepts a keycustodian-error-codes code (proves multi-dir aggregation)", async () => {
    await compileWithPredicate(
      '#{ retryWhen: "result.errorCode == \\"KEYCUSTODIAN_KID_INVALID\\"" }',
    );
    expect(diagCodes()).not.toContain(
      Q("resilience-predicate-unknown-error-code"),
    );
  });

  it("accepts a generic error-codes code SERVICE_UNAVAILABLE", async () => {
    await compileWithPredicate(
      '#{ retryWhen: "result.errorCode in (\\"SERVICE_UNAVAILABLE\\", \\"RATE_LIMITED\\")" }',
    );
    expect(diagCodes()).not.toContain(
      Q("resilience-predicate-unknown-error-code"),
    );
  });
});

// ----------------------------------------------------------------
// Invalid — unknown category (registry, decorator-body arm)
// ----------------------------------------------------------------

describe("validate_UnknownCategory_Reject", () => {
  it('rejects result.category == "not_a_category" with unknown-category + hasError()', async () => {
    await compileWithPredicate(
      '#{ retryWhen: "result.category == \\"not_a_category\\"" }',
    );
    expect(diagCodes()).toContain(Q("resilience-predicate-unknown-category"));
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("validate_RealCategoryAccepted_Ok", () => {
  it('accepts result.category == "partial_success" (a real wire string)', async () => {
    await compileWithPredicate(
      '#{ failWhen: "result.category == \\"partial_success\\"" }',
    );
    expect(diagCodes()).not.toContain(
      Q("resilience-predicate-unknown-category"),
    );
  });
});

// ----------------------------------------------------------------
// Invalid — malformed predicate surfaces from the decorator body
// ----------------------------------------------------------------

describe("validate_MalformedPredicate_Reject", () => {
  it("rejects a malformed predicate string with malformed + hasError()", async () => {
    await compileWithPredicate('#{ retryWhen: "result.bogus == 1" }');
    expect(diagCodes()).toContain(Q("resilience-predicate-unknown-field"));
    expect(runner.program.hasError()).toBe(true);
  });
});

// ----------------------------------------------------------------
// Non-Model return with a result.data path
// ----------------------------------------------------------------

describe("validate_NonModelReturnWithDataPath_Reject", () => {
  it("rejects a result.data path on a void-returning op with unknown-output-field + hasError()", async () => {
    await runner.diagnose(`
      @d2Query
      @d2Internal
      @d2Resilience("retry()", #{ retryWhen: "result.data.x == 1" })
      op scalarOut(): void;
    `);
    expect(diagCodes()).toContain(
      Q("resilience-predicate-unknown-output-field"),
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

// ----------------------------------------------------------------
// Back-compat — the existing positional call (no predicates) is untouched
// ----------------------------------------------------------------

describe("validate_BackCompatNoPredicates_Ok", () => {
  it('compiles @d2Resilience("retry()") with no predicate diagnostics + no new-key state', async () => {
    await runner.diagnose(`
      @d2Query
      @d2Internal
      @d2Resilience("retry()")
      op plainResilience(): void;
    `);
    const codes = diagCodes();
    expect(codes.some((c) => c.includes("resilience-predicate-"))).toBe(false);

    const retryEntries = [
      ...runner.program.stateMap(D2_RESILIENCE_RETRY_WHEN_KEY).values(),
    ];
    const failEntries = [
      ...runner.program.stateMap(D2_RESILIENCE_FAIL_WHEN_KEY).values(),
    ];
    expect(retryEntries).toHaveLength(0);
    expect(failEntries).toHaveLength(0);
  });
});

// ----------------------------------------------------------------
// Round-trip — both predicate strings land on their state keys
// ----------------------------------------------------------------

describe("validate_RoundTripBothPredicates_Ok", () => {
  it("stores both retryWhen + failWhen on their state keys via a full compile", async () => {
    await runner.diagnose(`
      ${MODEL_FIXTURE}

      @d2Query
      @d2Internal
      @d2Resilience(
        "retry(3)",
        #{
          retryWhen: "result.category == \\"infrastructure_unavailable\\"",
          failWhen: "result.errorCode == \\"VALIDATION_FAILED\\""
        }
      )
      op roundTrip(): PlaceOrderOutput;
    `);
    const retryEntries = [
      ...runner.program.stateMap(D2_RESILIENCE_RETRY_WHEN_KEY).values(),
    ];
    const failEntries = [
      ...runner.program.stateMap(D2_RESILIENCE_FAIL_WHEN_KEY).values(),
    ];
    expect(retryEntries).toContain(
      'result.category == "infrastructure_unavailable"',
    );
    expect(failEntries).toContain('result.errorCode == "VALIDATION_FAILED"');
  });
});
