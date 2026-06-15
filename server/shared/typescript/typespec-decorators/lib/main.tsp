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
