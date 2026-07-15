// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------
// Declares the @d2* decorator vocabulary under namespace D2.
// The JS implementations are registered by importing tsp-index.js, which
// binds each `extern dec` name to its $fn via the $decorators export —
// the same pattern used by @typespec/http.

import "../dist/tsp-index.js";

namespace D2;

using TypeSpec.Reflection;

/**
 * Scope guard — at-least-one of the supplied scopes must be present on the
 * token (`ScopeMatch.Any`). Mirrors `RequireAnyScope(...)`.
 */
extern dec d2RequireAnyScope(target: Operation, ...scopes: valueof string[]);

/**
 * Scope guard — every supplied scope must be present on the token
 * (`ScopeMatch.All`). Mirrors `RequireAllScopes(...)`.
 */
extern dec d2RequireAllScopes(target: Operation, ...scopes: valueof string[]);

/** Rate-limit tier for a REST-bound operation (Standard | Elevated | Restricted). */
extern dec d2RateLimitTier(target: Operation, tier: valueof string);

/** Expected JWT audience claim value. */
extern dec d2Audience(target: Operation, audience: valueof string);

/** Owning module/service for the operation's handler body. */
extern dec d2ServedBy(target: Operation, owner: valueof string);

/**
 * Co-location concern for a client-exposed operation. The `concern` segment
 * names the folder + namespace segment the op's transport DTOs live in
 * (`<csharp-clients-namespace>.<concern>`), co-located with the hand-written
 * runtime that serves them. The C# DTO emitter fails loud (D2TSP013) when a
 * client-exposed op omits it.
 */
extern dec d2Concern(target: Operation, concern: valueof string);

/**
 * Dual-binding marker: binds this operation to a gRPC service+method.
 * `streaming` selects the proto rpc form (unary | serverStream | clientStream |
 * bidiStream); omit to default to `unary`.
 */
extern dec d2GrpcMethod(
  target: Operation,
  service: valueof string,
  method: valueof string,
  streaming?: valueof string
);

/**
 * Marks a model property as sensitive — redacted from structured logs. The
 * REQUIRED `reason` names the data class and must be one of the
 * `DcsvIo.D2.Utilities.Enums.RedactReason` member names: `Unspecified`,
 * `PersonalInformation`, `FinancialInformation`, `SecretInformation`,
 * `VerboseContent`, `Other`. The C# DTO emitter maps it to
 * `[property: RedactData(Reason = RedactReason.<reason>)]`. The reason is
 * mandatory and fail-closed: a bare `@d2Redact` does not compile (missing
 * argument) and an unknown reason string is a compile error, so secret-adjacent
 * material is never silently misclassified as personal information.
 */
extern dec d2Redact(target: ModelProperty, reason: valueof string);

/** Binds an operation to a server-initiated push; `pushTarget` selects the channel class. */
extern dec d2ServerPush(target: Operation, pushTarget: valueof string);

/**
 * Marks a mutating operation as idempotent — drives a generated dedupe gate.
 * `keySource` selects key extraction; `ttlSeconds` is the replay window; `fields`
 * (for `"derived"` keySource) are the input property names hashed into the key,
 * in declaration order.
 */
extern dec d2Idempotent(
  target: Operation,
  keySource: valueof string,
  ttlSeconds: valueof int32,
  ...fields: valueof string[]
);

/**
 * Optional custom result-predicates for @d2Resilience. Both are minimal
 * result-expression DSL strings reaching the D2Result envelope + the wrapped
 * TOutput fields, compile-validated against the op's output shape and the closed
 * error-code / error-category registries.
 */
model D2ResiliencePredicates {
  /** Opt a business result INTO the retry decision (true ⇒ retry). */
  retryWhen?: string;

  /** Force a terminal fail — suppress retry (true ⇒ return verbatim, no retry). `failWhen` wins over `retryWhen`. */
  failWhen?: string;
}

/**
 * Wraps an operation's outbound call in a resilience pipeline expressed as a
 * composable DSL string (e.g. `"retry(circuitBreaker(singleflight()))"`).
 * The DSL is parsed and validated at TypeSpec compile time by `validateResilience()`.
 * The raw expression is stored in the program state map; emitters re-parse it via the
 * exported `parse()` function to walk the `ResiliencePolicyNode` AST at code-gen time.
 *
 * The optional `predicates` arg supplies custom `retryWhen` / `failWhen`
 * result-predicate strings (TypeSpec has no call-site named args, so they ride a
 * single options-model value). Each is parsed by `validateResultPredicate()` and
 * model-validated against the op's TOutput in $onValidate. The existing
 * single-positional form `@d2Resilience("retry()")` keeps compiling unchanged.
 */
extern dec d2Resilience(
  target: Operation,
  pipeline: valueof string,
  predicates?: valueof D2ResiliencePredicates
);

/** CSRF posture override for an operation; browser-facing mutations are protected by default. */
extern dec d2Csrf(target: Operation, posture: valueof string);

/** Marks an operation as auth-pipeline-exempt (health probes / discovery only). */
extern dec d2Harmless(target: Operation);

/**
 * Marks an operation as eligible for in-process leaf invocation (a co-hosted module
 * calling the owner directly, no network hop). The explicit leaf-vs-gRPC trigger;
 * requires @d2ServedBy. Drives the generated I<Owner>Api leaf interface.
 */
extern dec d2InProcess(target: Operation);

/** Marks an operation as a Command — it mutates persistent/shared state (drives the Commands/ folder). */
extern dec d2Command(target: Operation);

/** Marks an operation as a Query — read-only, no persistent/shared-state mutation (drives the Queries/ folder). */
extern dec d2Query(target: Operation);

/** Marks an operation as in-app-only — no cross-boundary surface is generated; structurally uncallable from outside. */
extern dec d2Internal(target: Operation);

/**
 * Pins a model property to an explicit proto3 field number. The number must be
 * a positive integer in the range [1, 536870911] and must NOT fall in the
 * protobuf implementation-reserved range 19000–19999.
 *
 * Author-pinned numbers guarantee wire stability: the proto emitter uses this
 * number verbatim instead of assigning positionally, so reordering, inserting,
 * or removing properties does not silently renumber surviving fields.
 *
 * The proto emitter requires every field on a @d2GrpcMethod-bound model to carry
 * @d2Field; an unpinned field is a loud build failure for proto-bound models.
 */
extern dec d2Field(target: ModelProperty, number: valueof int32);

/**
 * Declares author-owned reserved field numbers and names on a model. Reserved
 * entries document proto3 slots that were previously used and must never be
 * reused. The `names` parameter is a comma-separated list of formerly-used field
 * names; the variadic `...numbers` accepts the removed field numbers.
 *
 * The proto emitter emits `reserved N, M to P;` and `reserved "old_name";`
 * lines at the top of the affected message block (ascending, deduped,
 * range-collapsed).
 */
extern dec d2Reserved(target: Model, names: valueof string, ...numbers: valueof int32[]);
