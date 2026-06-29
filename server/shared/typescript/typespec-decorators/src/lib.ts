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

    /** The audience string supplied to @d2Audience is not a declared protocol
     *  audience in contracts/auth-protocol-audiences/protocol-audiences.spec.json. */
    "unknown-audience": {
      severity: "error",
      messages: {
        default: paramMessage`audience '${"value"}' is not a declared protocol audience in auth-protocol-audiences/protocol-audiences.spec.json`,
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
     *  owner to generate the I<Owner>Api interface name. */
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

    // ----------------------------------------------------------------
    // @d2Resilience result-predicate DSL diagnostics (retryWhen / failWhen)
    // ----------------------------------------------------------------

    /** A retryWhen / failWhen predicate string is syntactically malformed. */
    "resilience-predicate-malformed": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Resilience ${"which"} predicate is malformed: ${"detail"}`,
      },
    },

    /** A `result.<x>` accessor uses a field outside {success, statusCode, errorCode, category, data}. */
    "resilience-predicate-unknown-field": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Resilience ${"which"} predicate uses an unknown accessor: ${"detail"}`,
      },
    },

    /** A `result.data.<path>` segment is not a field on the operation's output model. */
    "resilience-predicate-unknown-output-field": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Resilience ${"which"} predicate references an unknown output field: ${"detail"}`,
      },
    },

    /** A `result.errorCode` literal is not declared in any *-error-codes spec. */
    "resilience-predicate-unknown-error-code": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Resilience ${"which"} predicate references an unknown error code: ${"detail"}`,
      },
    },

    /** A `result.category` literal is not a declared ErrorCategory wire string. */
    "resilience-predicate-unknown-category": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Resilience ${"which"} predicate references an unknown error category: ${"detail"}`,
      },
    },

    /** A comparison literal's type does not match the accessor's type. */
    "resilience-predicate-type-mismatch": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Resilience ${"which"} predicate has a type mismatch: ${"detail"}`,
      },
    },

    /** An array accessor (count / any / all / contains) was applied to a non-collection field. */
    "resilience-predicate-not-a-collection": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Resilience ${"which"} predicate applies an array accessor to a non-collection: ${"detail"}`,
      },
    },

    /** A field referenced inside an any/all sub-predicate is not on the element type. */
    "resilience-predicate-unknown-element-field": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Resilience ${"which"} predicate references an unknown element field: ${"detail"}`,
      },
    },

    /** A nested quantifier re-binds an element variable already in scope. */
    "resilience-predicate-shadowed-elem-var": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Resilience ${"which"} predicate shadows an element variable: ${"detail"}`,
      },
    },

    // ----------------------------------------------------------------
    // @d2Field field-number validation
    // ----------------------------------------------------------------

    /**
     * @d2Field field number is invalid. Valid range: integer ≥ 1, ≤ 536870911
     * (proto3 max), and NOT in the protobuf implementation-reserved range 19000–19999.
     */
    "invalid-field-number": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Field number ${"value"} is invalid — must be an integer ≥ 1, ≤ 536870911, and not in the protobuf reserved range 19000–19999`,
      },
    },

    /**
     * A number in the @d2Reserved variadic is invalid. Valid range: integer ≥ 1,
     * ≤ 536870911 (proto3 max), and NOT in the protobuf implementation-reserved
     * range 19000–19999. This applies the same validity rules as @d2Field so that
     * reserved-slot numbers are held to the same constraints as live field numbers.
     */
    "invalid-reserved-number": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Reserved number ${"value"} is invalid — must be an integer ≥ 1, ≤ 536870911, and not in the protobuf reserved range 19000–19999`,
      },
    },

    /**
     * A name token in the @d2Reserved names string is not a valid proto3
     * identifier. Proto3 identifiers must be non-empty and match
     * /^[A-Za-z_][A-Za-z0-9_]*$/ — an invalid name would inject unexpected
     * content into the emitted `reserved "..."` proto3 declaration.
     */
    "invalid-reserved-name": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Reserved name '${"value"}' is not a valid proto3 identifier — must match ^[A-Za-z_][A-Za-z0-9_]*$`,
      },
    },
  },
});
