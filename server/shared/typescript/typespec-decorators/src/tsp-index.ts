// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// TypeSpec decorator registry — the file lib/main.tsp imports via
// `import "../dist/tsp-index.js"`.
//
// This module is kept separate from index.ts (the package `main`) to avoid
// double-loading: index.ts is the emitter-facing API surface; this file is
// the sole module the .tsp pulls in. The split mirrors @typespec/http's
// layout and prevents double-binding every decorator into the global namespace
// (which would yield `ambiguous-symbol` under `using D2`).
//
// $onValidate is exported here so the TypeSpec compiler can discover and
// register the program-level cross-decorator validation hook — the same
// mechanism used by @typespec/http (its tsp-index re-exports $onValidate from
// validate.ts).

import {
  $d2Audience,
  $d2Command,
  $d2Csrf,
  $d2Field,
  $d2GrpcMethod,
  $d2Harmless,
  $d2Idempotent,
  $d2InProcess,
  $d2Internal,
  $d2Query,
  $d2RateLimitTier,
  $d2Redact,
  $d2RequireAllScopes,
  $d2RequireAnyScope,
  $d2Reserved,
  $d2Resilience,
  $d2ServedBy,
  $d2ServerPush,
} from "./decorators.js";

export { $onValidate } from "./onvalidate.js";

/**
 * Namespace → decorator-name → $fn map consumed by the TypeScript compiler to
 * link each `extern dec` declared under `namespace D2` in lib/main.tsp to its
 * JS implementation.
 *
 * @internal
 */
export const $decorators = {
  D2: {
    d2RequireAnyScope: $d2RequireAnyScope,
    d2RequireAllScopes: $d2RequireAllScopes,
    d2RateLimitTier: $d2RateLimitTier,
    d2Audience: $d2Audience,
    d2ServedBy: $d2ServedBy,
    d2GrpcMethod: $d2GrpcMethod,
    d2Redact: $d2Redact,
    d2ServerPush: $d2ServerPush,
    d2Idempotent: $d2Idempotent,
    d2Resilience: $d2Resilience,
    d2Csrf: $d2Csrf,
    d2Harmless: $d2Harmless,
    d2InProcess: $d2InProcess,
    d2Command: $d2Command,
    d2Query: $d2Query,
    d2Internal: $d2Internal,
    d2Field: $d2Field,
    d2Reserved: $d2Reserved,
  },
} as const;
