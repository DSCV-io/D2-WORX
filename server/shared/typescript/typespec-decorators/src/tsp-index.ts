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

import {
  $d2Audience,
  $d2Csrf,
  $d2GrpcMethod,
  $d2Harmless,
  $d2Idempotent,
  $d2RateLimitTier,
  $d2Redact,
  $d2RequireAllScopes,
  $d2RequireAnyScope,
  $d2Resilience,
  $d2ServedBy,
  $d2ServerPush,
} from "./decorators.js";

/**
 * Namespace → decorator-name → $fn map consumed by the TypeSpec compiler to
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
  },
} as const;
