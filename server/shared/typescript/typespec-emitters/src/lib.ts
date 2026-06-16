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
  },
});
