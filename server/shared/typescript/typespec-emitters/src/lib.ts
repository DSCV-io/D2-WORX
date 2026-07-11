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
//   D2TSP007  unsupported-union-shape   — a union property whose variants are NOT a closed set
//                                         of string literals (mixed-primitive, mixed-literal-kind,
//                                         numeric-literal-only, or otherwise non-string-literal).
//                                         Named/inline string-literal unions and named enums ARE
//                                         supported (mapped to a cross-language enum); these
//                                         ambiguous shapes are not — there is no single C#/proto/TS
//                                         representation. Replace with a named enum or a closed
//                                         string-literal union.
//   D2TSP008  server-push-requires-payload — a @d2ServerPush op whose output has no emittable
//                                         payload (a void return, or an output model with zero
//                                         fields and zero nested models). The op's output model IS
//                                         the event payload, so a payload-less push is almost
//                                         certainly an author mistake; the dispatch emitter loud-
//                                         fails rather than emitting a dispatcher with an empty
//                                         payload record. Give the op a non-empty output model.
//   D2TSP009  unpinned-proto-field        — a model property on a @d2GrpcMethod-bound model
//                                         lacks a @d2Field(n) pin; positional assignment is
//                                         disabled (fires only in the proto emitter).
//   D2TSP010  channel-segment-mismatch    — the wire-generation channel segment disagrees
//                                         across emitted wire surfaces (proto-package vs
//                                         proto-csharp-namespace trailing segment, or either
//                                         vs the @versioned active-version channel). Every
//                                         surface must carry the same V<N>(alpha|beta)?
//                                         generation; fix the mismatched tspconfig option.
//   D2TSP011  duplicate-field-number      — two or more properties on the same proto-bound
//                                         model carry the same @d2Field(N) pin. Duplicate
//                                         field numbers produce invalid proto3 that protoc
//                                         rejects; caught early at tsp compile time.
//   D2TSP013  missing-concern           — a client-exposed op (real-module mode:
//                                         csharp-clients-namespace + csharp-app-namespace-base
//                                         set, @d2ServedBy present, not @d2Internal) carries no
//                                         @d2Concern. The concern names the folder + namespace
//                                         segment the op's transport DTOs live in
//                                         (<clients-ns>.<Concern>); without it the emitter cannot
//                                         place them by concern. Add @d2Concern("<Segment>") to the op.
//   D2TSP014  missing-served-by-for-host-routing — real-module mode + @route op carries no
//                                         @d2ServedBy; host routing (process-kind + routes/bridge
//                                         namespace maps) is keyed by ServedBy and cannot hard-derive
//                                         App….Routes for production Edge hosts.
//   D2TSP015  missing-process-kind      — real-module mode + @route op has @d2ServedBy but the
//                                         process-kind-by-module map has no entry (or the map is
//                                         absent). Values must be "edge-module" | "standalone".
//   D2TSP016  unknown-process-kind      — process-kind-by-module entry is not in the closed set
//                                         "edge-module" | "standalone".
//   D2TSP017  missing-routes-namespace  — edge-module op with @route needs csharp-routes-namespace
//                                         map entry for its ServedBy (production host Map* ns).
//   D2TSP018  missing-bridge-namespace  — standalone op with @route + @d2GrpcMethod needs
//                                         csharp-bridge-namespace map entry for its ServedBy.
//   D2TSP019  standalone-route-requires-grpc — standalone process-kind op has @route but no
//                                         @d2GrpcMethod; public HTTP without a gRPC backend hop
//                                         is invalid for the Edge bridge model.
//   D2TSP012  RETIRED — nested-model redaction is now fully supported; the number
//                                         is NOT reused. (Formerly nested-redact-unsupported:
//                                         a @d2Redact on a nested-model property. The shared
//                                         model walker now threads the reason into nested fields
//                                         at any depth, so there is no unsupported placement left
//                                         to guard; misclassified/unknown reasons stay loud at the
//                                         decorator layer + the emitter's closed-set check. The
//                                         number stays retired so historical build-failure reports
//                                         remain traceable.)
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
     * D2TSP002 — A model property has a type the DTO emitter cannot express
     * (an anonymous model, a model-variant union, or an otherwise unrecognized
     * kind). Named enums and closed string-literal unions ARE supported (mapped
     * to a cross-language enum); use D2TSP007 for ambiguous union shapes. Replace
     * the property with a scalar, a named model, a named enum, or a closed
     * string-literal union before the emitter can generate a DTO.
     */
    "unsupported-property-type": {
      severity: "error",
      messages: {
        default: paramMessage`unsupported property type '${"kind"}' on '${"property"}' — expected a scalar, a named model, a supported enum/string-literal union, or an array thereof`,
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

    /**
     * D2TSP007 — A union property has a shape the emitter cannot map to a
     * cross-language enum. Only a closed set of string literals (or a named
     * enum) maps to a C#/proto/TS enum convention. Mixed-primitive, mixed-
     * literal-kind, numeric-literal-only, discriminated, or model unions have no
     * single cross-language representation and are rejected. Replace the property
     * with a named enum or a closed string-literal union.
     */
    "unsupported-union-shape": {
      severity: "error",
      messages: {
        default: paramMessage`union property '${"property"}' has an unsupported shape — only a closed set of string literals (or a named enum) maps to a cross-language enum; mixed-primitive, numeric-literal, discriminated, or model unions are not supported`,
      },
    },

    /**
     * D2TSP009 — A model property on a @d2GrpcMethod-bound model is missing its
     * required @d2Field(n) pin. Author-pinned field numbers are mandatory for all
     * proto-bound models; positional assignment is permanently disabled to prevent
     * silent wire-format breaks on reorder/insert/delete. Add @d2Field(N) to every
     * property of every model used in a @d2GrpcMethod operation's input and output.
     *
     * This diagnostic fires ONLY inside the proto emitter (reached only for
     * @d2GrpcMethod ops); DTO-only / in-process ops with unpinned fields compile clean.
     */
    "unpinned-proto-field": {
      severity: "error",
      messages: {
        default: paramMessage`${"detail"}`,
      },
    },

    /**
     * D2TSP008 — A @d2ServerPush operation has no emittable event payload.
     * The op's output model IS the dispatched event payload. A void return (or
     * an output model with zero fields and zero nested models) leaves nothing to
     * push — almost certainly an author mistake. The dispatch emitter refuses to
     * emit a dispatcher carrying an empty payload record. Give the operation a
     * non-empty output model.
     */
    "server-push-requires-payload": {
      severity: "error",
      messages: {
        default: paramMessage`@d2ServerPush on '${"op"}' has no event payload — the op's output model is the dispatched payload, so it must declare at least one field; a void or empty output cannot be pushed`,
      },
    },

    /**
     * D2TSP010 — The wire-generation channel segment disagrees across emitted
     * wire surfaces. The proto-package channel (e.g. "v2alpha") and the trailing
     * segment of proto-csharp-namespace (e.g. "V2Alpha") must agree on the same
     * V<N>(alpha|beta)? generation. When a @versioned active-version channel is
     * present it must also agree. Fix the mismatched tspconfig option so every
     * surface carries the same generation.
     */
    "channel-segment-mismatch": {
      severity: "error",
      messages: {
        default: paramMessage`${"detail"}`,
      },
    },

    /**
     * D2TSP011 — Two or more properties on the same proto-bound model carry the
     * same @d2Field(N) pin. Duplicate field numbers produce invalid proto3 that
     * protoc rejects. Assign each property a unique field number.
     */
    "duplicate-field-number": {
      severity: "error",
      messages: {
        default: paramMessage`${"detail"}`,
      },
    },

    /**
     * D2TSP013 — A client-exposed operation carries no @d2Concern. In real-module
     * mode (csharp-clients-namespace + csharp-app-namespace-base configured), an
     * exposed op's transport DTOs are placed in a concern-named namespace + folder
     * (<clients-ns>.<Concern>) co-located with the hand-written runtime that serves
     * them. Without a concern the emitter cannot route them. Add
     * @d2Concern("<Segment>") to the operation.
     */
    "missing-concern": {
      severity: "error",
      messages: {
        default: paramMessage`client-exposed operation '${"op"}' has no @d2Concern — an exposed op's transport DTOs are placed by concern (<clients-ns>.<Concern>); add @d2Concern("<Segment>")`,
      },
    },

    /**
     * D2TSP014 — A real-module routed operation has no @d2ServedBy. Process-kind
     * and routes/bridge namespace maps are keyed by ServedBy; without it the
     * emitter would hard-derive App….Routes and lie that App owns AspNetCore.
     * Add @d2ServedBy("<Module>") and a process-kind-by-module entry.
     */
    "missing-served-by-for-host-routing": {
      severity: "error",
      messages: {
        default: paramMessage`routed operation '${"op"}' has no @d2ServedBy — real-module host routing requires @d2ServedBy plus process-kind-by-module and routes/bridge namespace map entries; hard-derived App….Routes is forbidden`,
      },
    },

    /**
     * D2TSP015 — A real-module routed operation has @d2ServedBy but no
     * process-kind-by-module map entry. Values must be "edge-module" or
     * "standalone". Add the ServedBy key to process-kind-by-module in tspconfig.
     */
    "missing-process-kind": {
      severity: "error",
      messages: {
        default: paramMessage`routed operation '${"op"}' (servedBy '${"servedBy"}') has no process-kind-by-module entry — add '${"servedBy"}: edge-module|standalone' to tspconfig`,
      },
    },

    /**
     * D2TSP016 — process-kind-by-module value is outside the closed set.
     */
    "unknown-process-kind": {
      severity: "error",
      messages: {
        default: paramMessage`process-kind-by-module['${"servedBy"}'] = '${"kind"}' is not a known process kind — expected "edge-module" or "standalone"`,
      },
    },

    /**
     * D2TSP017 — An edge-module routed op needs csharp-routes-namespace[ServedBy].
     */
    "missing-routes-namespace": {
      severity: "error",
      messages: {
        default: paramMessage`edge-module routed operation '${"op"}' (servedBy '${"servedBy"}') has no csharp-routes-namespace entry — set the Edge.Api routes namespace for this module in tspconfig`,
      },
    },

    /**
     * D2TSP018 — A standalone bridge op needs csharp-bridge-namespace[ServedBy].
     */
    "missing-bridge-namespace": {
      severity: "error",
      messages: {
        default: paramMessage`standalone bridge operation '${"op"}' (servedBy '${"servedBy"}') has no csharp-bridge-namespace entry — set the Edge.Api bridges namespace for this module in tspconfig`,
      },
    },

    /**
     * D2TSP019 — Standalone public HTTP requires a gRPC backend hop.
     */
    "standalone-route-requires-grpc": {
      severity: "error",
      messages: {
        default: paramMessage`standalone operation '${"op"}' has @route but no @d2GrpcMethod — public HTTP for standalone services must bridge to a gRPC backend hop`,
      },
    },
  },
});
