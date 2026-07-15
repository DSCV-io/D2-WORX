// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------
// Resilience-predicate-shaped fixture TypeSpec contract.
//
// Exercises the @d2Resilience `retryWhen` / `failWhen` custom-predicate
// EMISSION + the gRPC client's retry-sentinel integration end-to-end. The
// output shape carries an array (`itemStatuses`) + a flat bool (`partial`) +
// scalars, so the generated gRPC client + its mappers COMPILE (the proto
// `repeated string` ↔ DTO `IReadOnlyList<string>` and the scalar fields map
// directly), and the predicate exercises: an envelope field (`category` /
// `errorCode`), an array `.contains` + `.count`, and a flat data field —
// enough to drive the real-pipeline sentinel-retry behavioral test.
//
// The richer array-of-MODEL quantifier (`lines.any(l => l.status == "…")`) and
// nested-optional path (`customer.tier`) predicate constructs are carried by a
// SECOND op (`placeOrderV2Fixture`, output `PlaceOrderV2FixtureOutput`). Its
// predicate twin is emitted STANDALONE (the predicate emitter crawls only the
// output MODEL + the predicate AST at gen time) AND it carries a FULL committed
// gRPC client + proto + transport mappers + keys (an optional nested model + an
// array-of-MODEL response survive proto ↔ DTO with full fidelity via the
// per-nested-model sub-mappers); it is driven in BOTH runtimes by the nested
// cross-runtime parity matrix and by the in-memory gRPC round-trip harness.
//
// A THIRD op (`deepNestFixture`, output `DeepNestFixtureOutput`) proves ARBITRARY
// nesting depth: output → `DeepFixtureWidget` (depth 2, optional) →
// `DeepFixturePart[]` (depth 3, array-of-MODEL inside a nested model). It carries
// no @d2Resilience predicate (no predicate twin) — its purpose is the depth-N
// transport-mapper recursion + round-trip proof.
//
// The predicate strings use REAL registry literals (the closed error-code +
// ErrorCategory wire sets the @d2Resilience $onValidate model walk validates):
//   - category "infrastructure_unavailable"  (ErrorCategory wire value)
//   - errorCode "VALIDATION_FAILED"          (error-codes spec code)
//
// Served by a DEDICATED gRPC module (PredicateFixtures) so the existing
// sign / enum fixtures + their byte-gates stay byte-identical.
//
// This is a test fixture, not a real operation. Every fixture-only leaf symbol
// carries a `Fixture` marker in its own name.

import "@d2/typespec-decorators";

using D2;

namespace D2.Fixtures;

/** Input for the place-order fixture operation. */
model PlaceOrderFixtureInput {
  /** The customer placing the order. */
  @d2Field(1)
  customerId: string;
}

/**
 * Output of the place-order fixture operation. A flat-mappable shape — an array
 * of a scalar (`itemStatuses`) + a flat bool (`partial`) + scalars — so the
 * generated gRPC client + mappers compile while the predicate still exercises
 * an array `.contains` / `.count`, a flat data field, and envelope fields.
 */
model PlaceOrderFixtureOutput {
  /** The created order identifier. */
  @d2Field(1)
  orderCode: string;

  /** The per-line fulfillment statuses — a scalar collection (`.contains` / `.count`). */
  @d2Field(2)
  itemStatuses: string[];

  /** Whether the order is a partial fulfillment — a flat bool data field. */
  @d2Field(3)
  partial: boolean;
}

/**
 * Place an order. Fixture gRPC operation carrying @d2Resilience custom
 * predicates so the cross-language predicate emitter + the gRPC client's
 * retry-sentinel mechanism are exercised end-to-end on a real pipeline.
 *
 * retryWhen: retry when the result is an infrastructure-unavailable failure, OR
 *   any line is still PENDING, OR the order is a partial fulfillment.
 * failWhen:  never retry (terminal) when there are zero lines OR the failure is
 *   a validation failure. `failWhen` WINS over `retryWhen`.
 */
@d2Command
@d2ServedBy("PredicateFixtures")
@d2Concern("PredicateFixture")
@d2GrpcMethod("PredicateFixturesOrders", "PlaceOrderFixture")
@d2Resilience(
  "retry(3)",
  #{
    retryWhen: "result.category == \"infrastructure_unavailable\" || result.data.itemStatuses.contains(\"PENDING\") || result.data.partial == true",
    failWhen: "result.data.itemStatuses.count == 0 || result.errorCode == \"VALIDATION_FAILED\"",
  }
)
op placeOrderFixture(input: PlaceOrderFixtureInput): PlaceOrderFixtureOutput;

// ---------------------------------------------------------------------------
// placeOrderV2Fixture — the NESTED-MODEL + array-of-MODEL predicate shape.
//
// `PlaceOrderV2FixtureOutput` carries an optional NESTED model (`customer?` →
// `customer.tier`) and an array-of-MODEL (`lines: PlaceOrderFixtureLine[]` →
// `lines.any(l => l.status == "…")` / `lines.count`). The predicate emitter
// resolves each segment against this model at GEN TIME (a deep `?.`-chain for
// the optional nested path; a LINQ `.Any(...)` / `.some(...)` quantifier with an
// element sub-predicate for the array-of-model) — proving the cross-language
// emission for the shapes the flat `placeOrderFixture` cannot exercise.
//
// A nested-model gRPC RESPONSE IS wire-mappable: the gRPC transport mapper recurses
// each nested-model / array-of-MODEL field through a per-nested-model proto ↔ DTO
// sub-mapper (a `repeated <Message>` for the array, a bare message field for the
// optional nested model — proto3 implicit presence, null when absent). `placeOrderV2Fixture`
// carries a full committed gRPC client + proto + transport mappers + keys (served by
// the dedicated `PredicateFixturesV2` module so the V1 module-level fixtures stay
// byte-identical), round-tripped on the in-memory gRPC harness.

/** A single order line for the placeOrderV2Fixture fixture — an array-of-MODEL element. */
model PlaceOrderFixtureLine {
  /** The line's fulfillment status (drives `lines.any(l => l.status == "…")`). */
  @d2Field(1)
  status: string;
}

/** Customer info for the placeOrderV2Fixture fixture — an OPTIONAL nested model. */
model PlaceOrderV2FixtureCustomer {
  /** The customer tier (drives the nested-optional path `customer.tier`). */
  @d2Field(1)
  tier: string;
}

/** Input for the placeOrderV2Fixture fixture operation. */
model PlaceOrderV2FixtureInput {
  /** The customer placing the order. */
  @d2Field(1)
  customerId: string;
}

/**
 * Output of the placeOrderV2 fixture operation. A RICH shape — an optional nested
 * model (`customer`) + an array-of-MODEL (`lines`) + a scalar — so the predicate
 * exercises a deep optional `?.`-chain and an array-of-model quantifier with an
 * element sub-predicate. The nested-model gRPC response IS wire-mappable: the
 * transport mapper recurses each nested-model / array-of-MODEL field through a
 * per-nested-model proto ↔ DTO sub-mapper, and a full gRPC client is committed.
 */
model PlaceOrderV2FixtureOutput {
  /** The created order identifier. */
  @d2Field(1)
  orderCode: string;

  /** The order lines — an array-of-MODEL (`.any(l => l.status)` / `.count`). */
  @d2Field(2)
  lines: PlaceOrderFixtureLine[];

  /** The customer — an OPTIONAL nested model (`customer.tier`, deep `?.`-chain). */
  @d2Field(3)
  customer?: PlaceOrderV2FixtureCustomer;
}

/**
 * Place an order (rich nested shape). Fixture gRPC operation carrying
 * @d2Resilience custom predicates over a nested-model + array-of-MODEL output so
 * the cross-language predicate emitter's deep `?.`-chain + array-of-model
 * quantifier emission is behaviorally parity-proven. It also carries a full
 * committed gRPC client + proto + transport mappers (the nested-model wire path),
 * round-tripped on the in-memory gRPC harness.
 *
 * retryWhen: retry when the customer is on the TRIAL tier (nested-optional path),
 *   OR any line is still PENDING (array-of-model quantifier).
 * failWhen:  never retry (terminal) when there are zero lines OR the failure is a
 *   validation failure. `failWhen` WINS over `retryWhen`.
 */
@d2Command
@d2ServedBy("PredicateFixtures")
@d2Concern("PredicateFixture")
@d2GrpcMethod("PredicateFixturesOrders", "PlaceOrderV2Fixture")
@d2Resilience(
  "retry(3)",
  #{
    retryWhen: "result.data.customer.tier == \"TRIAL\" || result.data.lines.any(l => l.status == \"PENDING\")",
    failWhen: "result.data.lines.count == 0 || result.errorCode == \"VALIDATION_FAILED\"",
  }
)
op placeOrderV2Fixture(input: PlaceOrderV2FixtureInput): PlaceOrderV2FixtureOutput;

// ---------------------------------------------------------------------------
// deepNestFixture — the ARBITRARY-DEPTH (depth-3) nested-model shape.
//
// `DeepNestFixtureOutput` → optional `DeepFixtureWidget` (depth 2) →
// `DeepFixturePart[]` (depth 3, an array-of-MODEL INSIDE a nested model). Proves
// the transport-mapper recursion crawls arbitrary depth (a per-nested-model
// sub-mapper per level, deduped) and the proto emitter emits every nested message
// at every level. No @d2Resilience predicate (no predicate twin) — this op exists
// purely to prove depth-N.

/** A leaf part for the deepNestFixture fixture — the depth-3 array-of-MODEL element. */
model DeepFixturePart {
  /** The part code. */
  @d2Field(1)
  code: string;
}

/** A widget for the deepNestFixture fixture — depth 2, holding an array-of-MODEL of parts. */
model DeepFixtureWidget {
  /** The widget name. */
  @d2Field(1)
  name: string;

  /** The widget's parts — an array-of-MODEL nested one level deeper. */
  @d2Field(2)
  parts: DeepFixturePart[];
}

/** Input for the deepNestFixture fixture operation. */
model DeepNestFixtureInput {
  /** The gizmo identifier to fetch. */
  @d2Field(1)
  gizmoId: string;
}

/**
 * Output of the deepNestFixture fixture operation — an optional nested `DeepFixtureWidget`
 * (depth 2) whose `parts` is an array-of-MODEL (depth 3). Drives the depth-N
 * transport-mapper recursion + the depth-3 round-trip proof.
 */
model DeepNestFixtureOutput {
  /** The created gizmo identifier. */
  @d2Field(1)
  gizmoId: string;

  /** The gizmo's widget — an OPTIONAL nested model (depth 2) carrying parts (depth 3). */
  @d2Field(2)
  widget?: DeepFixtureWidget;
}

/**
 * Fetch a deeply-nested gizmo. Fixture gRPC operation carrying a depth-3 nested-model
 * output (output → optional widget → array-of-parts) so the transport-mapper's
 * arbitrary-depth recursion + the proto emitter's per-level message emission are
 * round-trip-proven. Served by the dedicated `PredicateFixturesDeep` module.
 */
@d2Command
@d2ServedBy("PredicateFixtures")
@d2Concern("PredicateFixture")
@d2GrpcMethod("PredicateFixturesGizmosDeep", "DeepNestFixture")
op deepNestFixture(input: DeepNestFixtureInput): DeepNestFixtureOutput;
