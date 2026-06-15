// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { createTypeSpecLibrary, paramMessage } from "@typespec/compiler";

// Library descriptor for the @d2/typespec-decorators package.
// All diagnostics use severity "error" — every contract violation fails the
// TypeSpec compile so authors see hard build failures, not silent warnings.
export const $lib = createTypeSpecLibrary({
  name: "@d2/typespec-decorators",
  diagnostics: {
    // ----------------------------------------------------------------
    // In-decorator eager value-set checks (run in each $fn body)
    // ----------------------------------------------------------------

    /** @d2RateLimitTier value is not one of the allowed tier strings. */
    "invalid-rate-limit-tier": {
      severity: "error",
      messages: {
        default: paramMessage`rate-limit tier '${"value"}' is invalid — expected one of: Standard, Elevated, Restricted`,
      },
    },

    /** @d2GrpcMethod streaming value is not one of the allowed mode strings. */
    "invalid-grpc-streaming": {
      severity: "error",
      messages: {
        default: paramMessage`gRPC streaming mode '${"value"}' is invalid — expected one of: unary, serverStream, clientStream, bidiStream`,
      },
    },

    /** @d2ServerPush pushTarget value is not one of the allowed target strings. */
    "invalid-push-target": {
      severity: "error",
      messages: {
        default: paramMessage`server-push target '${"value"}' is invalid — expected one of: user, session`,
      },
    },

    /** @d2Csrf posture value is not one of the allowed posture strings. */
    "invalid-csrf-posture": {
      severity: "error",
      messages: {
        default: paramMessage`CSRF posture '${"value"}' is invalid — expected one of: required, exempt`,
      },
    },

    /** @d2Idempotent keySource value is not one of the allowed source strings. */
    "invalid-idempotent-key-source": {
      severity: "error",
      messages: {
        default: paramMessage`idempotency keySource '${"value"}' is invalid — expected one of: header, derived`,
      },
    },

    /** @d2Idempotent ttlSeconds is not a positive integer. */
    "invalid-idempotent-ttl": {
      severity: "error",
      messages: {
        default: paramMessage`idempotency ttlSeconds must be > 0 (got ${"value"})`,
      },
    },

    /** @d2Idempotent keySource "derived" was given but no field names were supplied. */
    "idempotent-derived-requires-fields": {
      severity: "error",
      messages: {
        default:
          'idempotency keySource "derived" requires at least one field name',
      },
    },

    /** @d2Idempotent keySource "header" was given together with field names, which is invalid. */
    "idempotent-header-forbids-fields": {
      severity: "error",
      messages: {
        default: 'idempotency keySource "header" does not take field names',
      },
    },

    /** A scope name supplied to @d2RequireAnyScope or @d2RequireAllScopes is not declared
     *  in contracts/auth-scopes/scopes.spec.json. */
    "unknown-scope": {
      severity: "error",
      messages: {
        default: paramMessage`scope '${"value"}' is not declared in scopes.spec.json`,
      },
    },

    /** The audience string supplied to @d2Audience is not declared in
     *  contracts/auth-audiences/audiences.spec.json and is not the
     *  self-audience "d2-edge". */
    "unknown-audience": {
      severity: "error",
      messages: {
        default: paramMessage`audience '${"value"}' is not declared in audiences.spec.json (and is not the 'd2-edge' self-audience)`,
      },
    },

    /** @d2ServedBy owner is an empty or whitespace-only string. */
    "empty-served-by": {
      severity: "error",
      messages: {
        default: "@d2ServedBy owner must be a non-empty string",
      },
    },

    // ----------------------------------------------------------------
    // $onValidate cross-decorator checks (run once after all decorators apply)
    // ----------------------------------------------------------------

    /** @d2RateLimitTier was applied to an operation that has no public HTTP @route.
     *  Internal operations bypass Edge rate-limiting and must not carry a tier. */
    "rate-tier-requires-route": {
      severity: "error",
      messages: {
        default: paramMessage`@d2RateLimitTier on '${"op"}' requires a public HTTP route (@route) — internal-only operations bypass Edge rate-limiting and must not carry a tier`,
      },
    },

    /** @d2Harmless and a scope decorator were applied to the same operation.
     *  An auth-exempt operation cannot also require scopes. */
    "harmless-scope-conflict": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Harmless on '${"op"}' is mutually exclusive with scope requirements (@d2RequireAnyScope / @d2RequireAllScopes) — an auth-exempt op cannot also require scopes`,
      },
    },

    /** @d2InProcess was applied to an op with no @d2ServedBy. A leaf needs a named
     *  owner to generate the I<Owner>InternalApi interface name. */
    "inprocess-requires-served-by": {
      severity: "error",
      messages: {
        default: paramMessage`@d2InProcess on '${"op"}' requires @d2ServedBy — an in-process leaf needs a named owner to generate its interface`,
      },
    },

    /** An operation declares neither @d2Command nor @d2Query. Exactly one CQRS category is required. */
    "category-required": {
      severity: "error",
      messages: {
        default: paramMessage`operation '${"op"}' must declare @d2Command or @d2Query — exactly one CQRS category is required`,
      },
    },

    /** An operation declares both @d2Command and @d2Query. The categories are mutually exclusive. */
    "category-exclusive": {
      severity: "error",
      messages: {
        default: paramMessage`operation '${"op"}' declares both @d2Command and @d2Query — exactly one CQRS category is required`,
      },
    },

    /** @d2Internal was combined with a cross-boundary exposure decorator. An internal op is not callable from outside. */
    "internal-op-exposed": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Internal on '${"op"}' cannot be combined with ${"decorator"} — an internal op is not callable across any boundary`,
      },
    },

    /** An operation declares no exposure decorator and is not marked @d2Internal. Every op must declare its callability. */
    "exposure-or-internal-required": {
      severity: "error",
      messages: {
        default: paramMessage`operation '${"op"}' must declare a transport (@route / @d2GrpcMethod / @d2InProcess) or be marked @d2Internal`,
      },
    },

    // ----------------------------------------------------------------
    // @d2Resilience pipeline-expression DSL parser diagnostics
    // ----------------------------------------------------------------

    /** The pipeline-expression string is syntactically malformed. */
    "resilience-malformed": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Resilience expression is malformed: ${"detail"}`,
      },
    },

    /** The pipeline-expression contains a policy name not in
     *  {retry, circuitBreaker, singleflight}. */
    "resilience-unknown-policy": {
      severity: "error",
      messages: {
        default: paramMessage`unknown resilience policy '${"policy"}' — expected one of: retry, circuitBreaker, singleflight`,
      },
    },

    /** The pipeline-expression contains a tunable name not defined for the policy,
     *  or any argument on singleflight (which takes no arguments). */
    "resilience-unknown-arg": {
      severity: "error",
      messages: {
        default: paramMessage`'${"policy"}' has no tunable '${"arg"}'`,
      },
    },

    /** A tunable value has the wrong type or is outside the allowed range. */
    "resilience-bad-arg": {
      severity: "error",
      messages: {
        default: paramMessage`'${"policy"}.${"arg"}' is invalid: ${"detail"}`,
      },
    },

    /** More than one nested policy call appears in a single policy's arg list.
     *  A resilience pipeline is a linear stack — each policy wraps at most one inner policy. */
    "resilience-multiple-inner": {
      severity: "error",
      messages: {
        default: paramMessage`'${"policy"}' wraps more than one inner policy — a resilience pipeline is a linear stack`,
      },
    },

    /** A positional argument appears after a named argument in the same policy's arg list.
     *  Positional arguments must come before named arguments. */
    "resilience-positional-after-named": {
      severity: "error",
      messages: {
        default: paramMessage`'${"policy"}' has a positional argument after a named one — positional args must come first`,
      },
    },
  },
});
