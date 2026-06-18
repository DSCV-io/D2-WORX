// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
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

/** Marks a model property as PII to be redacted in structured logs. */
extern dec d2Redact(target: ModelProperty);

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
 * Wraps an operation's outbound call in a resilience pipeline expressed as a
 * composable DSL string (e.g. `"retry(circuitBreaker(singleflight()))"`).
 * The DSL is parsed and validated at TypeSpec compile time by `validateResilience()`.
 * The raw expression is stored in the program state map; emitters re-parse it via the
 * exported `parse()` function to walk the `ResiliencePolicyNode` AST at code-gen time.
 */
extern dec d2Resilience(target: Operation, pipeline: valueof string);

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
