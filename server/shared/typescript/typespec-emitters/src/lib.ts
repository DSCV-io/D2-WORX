// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { createTypeSpecLibrary, paramMessage } from "@typespec/compiler";

// -----------------------------------------------------------------------
// D2TSP* diagnostic-id family — TypeSpec emitter pipeline diagnostics.
//
// The TypeSpec-native surface uses NAMED diagnostic codes (kebab strings)
// surfaced by the compiler as "@d2/typespec-emitters/<name>". The D2TSP*
// prefix is the cross-tooling grep family registered in docs/SRC_GEN.md §1.2.
//
// Allocated IDs:
//   D2TSP001  unmapped-scalar           — scalar has no C#/proto/TS mapping
//   D2TSP002  unsupported-property-type — enum, union, or anonymous-model prop
//   D2TSP003  missing-cqrs-category     — op carries neither @d2Command nor @d2Query
//                                         (defensive guard in namespace routing; the
//                                         decorator-layer `category-required` invariant
//                                         should prevent this from firing in valid programs)
//   D2TSP004  route-missing-auth-intent — a routed op carries neither @d2RequireAnyScope
//                                         nor @d2RequireAllScopes nor @d2Harmless; every
//                                         routed op must declare an auth intent (deny-by-default)
//   D2TSP005  unsupported-http-verb     — an HTTP verb other than get/post/put/delete/patch
//                                         (e.g. head/options/trace); the route emitter cannot
//                                         map it to a Minimal-API Map* call
//   D2TSP006  idempotent-requires-route — @d2Idempotent present on an op with no @route;
//                                         idempotency gating is REST-only, so an in-process-only
//                                         or gRPC-only op must not carry the gate annotation
// -----------------------------------------------------------------------

/**
 * Library descriptor for the @d2/typespec-emitters package.
 * All diagnostics use severity "error" — every emitter violation fails the
 * TypeSpec compile so authors see hard build failures, not silent warnings.
 */
export const $lib = createTypeSpecLibrary({
  name: "@d2/typespec-emitters",
  diagnostics: {
    /**
     * D2TSP001 — A TypeSpec scalar has no entry in the scalar registry.
     * Emitter cannot proceed without a C#/proto/TS mapping for this type.
     */
    "unmapped-scalar": {
      severity: "error",
      messages: {
        default: paramMessage`unmapped TypeSpec scalar '${"scalar"}' — no C#/proto/TS mapping in the scalar registry`,
      },
    },

    /**
     * D2TSP002 — A model property has a type the DTO emitter cannot yet express
     * (enum, union, anonymous-model, or unrecognized kind). Enum/union support
     * is deferred; these properties must be replaced with a scalar or a named
     * model before the emitter can generate a DTO.
     */
    "unsupported-property-type": {
      severity: "error",
      messages: {
        default: paramMessage`unsupported property type '${"kind"}' on '${"property"}' — enum, union, and anonymous-model properties are not yet supported by the DTO emitter`,
      },
    },

    /**
     * D2TSP003 — An operation carries neither @d2Command nor @d2Query.
     * The handler-interface emitter and namespace-routing logic require exactly
     * one CQRS category on every op. The decorator-layer `category-required`
     * invariant should prevent this in valid programs; this diagnostic fires
     * defensively when the emitter encounters an uncategorized op.
     */
    "missing-cqrs-category": {
      severity: "error",
      messages: {
        default: paramMessage`operation '${"op"}' has no CQRS category — exactly one of @d2Command or @d2Query is required`,
      },
    },

    /**
     * D2TSP004 — A routed operation carries no auth intent.
     * Every operation with an HTTP route (@route) must declare exactly one of
     * @d2RequireAnyScope, @d2RequireAllScopes, or @d2Harmless. An operation with
     * none of these cannot be safely routed — the emitter refuses to emit the route
     * registration (deny-by-default at compile time).
     */
    "route-missing-auth-intent": {
      severity: "error",
      messages: {
        default: paramMessage`routed operation '${"op"}' has no auth intent — exactly one of @d2RequireAnyScope, @d2RequireAllScopes, or @d2Harmless is required`,
      },
    },

    /**
     * D2TSP005 — An operation's HTTP verb is not supported by the route emitter.
     * Only get/post/put/delete/patch map to Minimal-API Map* calls. Verbs such as
     * head/options/trace cannot be emitted; the author must change the verb or
     * implement a hand-written route for that endpoint.
     */
    "unsupported-http-verb": {
      severity: "error",
      messages: {
        default: paramMessage`operation '${"op"}' uses unsupported HTTP verb '${"verb"}' — only get/post/put/delete/patch are supported by the route emitter`,
      },
    },

    /**
     * D2TSP006 — @d2Idempotent is present on an operation that has no @route.
     * Idempotency gating is a REST-only concern; it is meaningless without a
     * public HTTP route. An operation that is only reachable via gRPC or an
     * in-process call cannot carry the generated idempotency gate.
     * Add @route + @post (or another supported verb) to the operation, or remove
     * @d2Idempotent if the operation is not intended to have a REST surface.
     */
    "idempotent-requires-route": {
      severity: "error",
      messages: {
        default: paramMessage`@d2Idempotent on '${"op"}' requires a public HTTP route (@route) — idempotency gating is REST-only and is meaningless without a route`,
      },
    },
  },
});
