// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
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
// SECOND op (`placeOrderV2`, output `PlaceOrderV2Output`). Its predicate twin is
// emitted STANDALONE (the predicate emitter crawls only the output MODEL + the
// predicate AST at gen time — it needs no gRPC transport mapper) and is driven
// in BOTH runtimes by the nested cross-runtime parity matrix; the gRPC client /
// transport mapper for `placeOrderV2` is deliberately NOT committed (a nested-
// model gRPC response is not yet wire-mappable — a tracked limitation, see the
// roadmap §G note referenced from VALIDATION.md). So the rich emission is fully
// behavior-proven cross-language WITHOUT a compiled gRPC client for that shape.
//
// The predicate strings use REAL registry literals (the closed error-code +
// ErrorCategory wire sets the @d2Resilience $onValidate model walk validates):
//   - category "infrastructure_unavailable"  (ErrorCategory wire value)
//   - errorCode "VALIDATION_FAILED"          (error-codes spec code)
//
// Served by a DEDICATED gRPC module (PredicateFixtures) so the existing
// sign / signWithKind fixtures + their byte-gates stay byte-identical.
//
// This is a test fixture, not a real operation.

import "@d2/typespec-decorators";

using D2;

namespace D2.Fixtures;

/** Input for the place-order fixture operation. */
model PlaceOrderInput {
  /** The customer placing the order. */
  customerId: string;
}

/**
 * Output of the place-order fixture operation. A flat-mappable shape — an array
 * of a scalar (`itemStatuses`) + a flat bool (`partial`) + scalars — so the
 * generated gRPC client + mappers compile while the predicate still exercises
 * an array `.contains` / `.count`, a flat data field, and envelope fields.
 */
model PlaceOrderOutput {
  /** The created order identifier. */
  orderCode: string;

  /** The per-line fulfillment statuses — a scalar collection (`.contains` / `.count`). */
  itemStatuses: string[];

  /** Whether the order is a partial fulfillment — a flat bool data field. */
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
@d2GrpcMethod("PredicateFixturesOrders", "PlaceOrder")
@d2Resilience(
  "retry(3)",
  #{
    retryWhen: "result.category == \"infrastructure_unavailable\" || result.data.itemStatuses.contains(\"PENDING\") || result.data.partial == true",
    failWhen: "result.data.itemStatuses.count == 0 || result.errorCode == \"VALIDATION_FAILED\"",
  }
)
op placeOrder(input: PlaceOrderInput): PlaceOrderOutput;

// ---------------------------------------------------------------------------
// placeOrderV2 — the NESTED-MODEL + array-of-MODEL predicate shape.
//
// `PlaceOrderV2Output` carries an optional NESTED model (`customer?` →
// `customer.tier`) and an array-of-MODEL (`lines: PlaceOrderLine[]` →
// `lines.any(l => l.status == "…")` / `lines.count`). The predicate emitter
// resolves each segment against this model at GEN TIME (a deep `?.`-chain for
// the optional nested path; a LINQ `.Any(...)` / `.some(...)` quantifier with an
// element sub-predicate for the array-of-model) — proving the cross-language
// emission for the shapes the flat `placeOrder` cannot exercise.
//
// A nested-model gRPC RESPONSE is NOT yet wire-mappable (the gRPC transport
// mapper handles scalar / bytes / enum fields only — a tracked limitation), so
// the gRPC client + proto + transport mappers for this op are NOT committed; the
// predicate twin is emitted standalone (model + AST only) and behavior-proven in
// both runtimes by the nested cross-runtime parity matrix.

/** A single order line for the placeOrderV2 fixture — an array-of-MODEL element. */
model PlaceOrderLine {
  /** The line's fulfillment status (drives `lines.any(l => l.status == "…")`). */
  status: string;
}

/** Customer info for the placeOrderV2 fixture — an OPTIONAL nested model. */
model PlaceOrderV2Customer {
  /** The customer tier (drives the nested-optional path `customer.tier`). */
  tier: string;
}

/** Input for the placeOrderV2 fixture operation. */
model PlaceOrderV2Input {
  /** The customer placing the order. */
  customerId: string;
}

/**
 * Output of the placeOrderV2 fixture operation. A RICH shape — an optional nested
 * model (`customer`) + an array-of-MODEL (`lines`) + a scalar — so the predicate
 * exercises a deep optional `?.`-chain and an array-of-model quantifier with an
 * element sub-predicate. A nested-model gRPC response is not yet wire-mappable, so
 * no gRPC client is committed for this op (the predicate twin emits standalone).
 */
model PlaceOrderV2Output {
  /** The created order identifier. */
  orderCode: string;

  /** The order lines — an array-of-MODEL (`.any(l => l.status)` / `.count`). */
  lines: PlaceOrderLine[];

  /** The customer — an OPTIONAL nested model (`customer.tier`, deep `?.`-chain). */
  customer?: PlaceOrderV2Customer;
}

/**
 * Place an order (rich nested shape). Fixture gRPC operation carrying
 * @d2Resilience custom predicates over a nested-model + array-of-MODEL output so
 * the cross-language predicate emitter's deep `?.`-chain + array-of-model
 * quantifier emission is behaviorally parity-proven. The gRPC client/transport
 * mapper for this op is NOT committed (nested-model gRPC responses are a tracked
 * limitation); only the predicate twin + DTOs are.
 *
 * retryWhen: retry when the customer is on the TRIAL tier (nested-optional path),
 *   OR any line is still PENDING (array-of-model quantifier).
 * failWhen:  never retry (terminal) when there are zero lines OR the failure is a
 *   validation failure. `failWhen` WINS over `retryWhen`.
 */
@d2Command
@d2ServedBy("PredicateFixtures")
@d2GrpcMethod("PredicateFixturesOrders", "PlaceOrderV2")
@d2Resilience(
  "retry(3)",
  #{
    retryWhen: "result.data.customer.tier == \"TRIAL\" || result.data.lines.any(l => l.status == \"PENDING\")",
    failWhen: "result.data.lines.count == 0 || result.errorCode == \"VALIDATION_FAILED\"",
  }
)
op placeOrderV2(input: PlaceOrderV2Input): PlaceOrderV2Output;
