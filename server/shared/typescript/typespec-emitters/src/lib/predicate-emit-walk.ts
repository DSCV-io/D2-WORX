// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Emitter-local gen-time model crawl for the @d2Resilience result-predicate
// emitter. Given the decorated operation's output Model and a parsed predicate
// data path, resolves each path segment to the C# / TS member name, whether the
// segment is optional (→ null-conditional `?.`), and — for collection segments —
// the element type so an `any` / `all` quantifier can recurse into the element
// model for its sub-predicate.
//
// Why this lives HERE (a NEW emitter-local walk), NOT the shared `walkModel`:
//   The shared `model-walk.ts` `collectNested` stops one level deep (it never
//   populates a nested model's nested fields), so an arbitrary-depth path like
//   `result.data.order.customer.tier` is unresolvable through it. Extending the
//   shared `FieldInfo` / `walkModel` would touch every emitter in the fleet (DTO
//   / proto / TS / gRPC) and risks byte-drift across all of them for a
//   predicate-only need. So the predicate emitter walks the native
//   @typespec/compiler Model / ModelProperty / Type API directly here — the
//   emitter-side mirror of the decorator package's `predicate-model-walk.ts`
//   (which does the same native crawl for VALIDATION). This duplicates a small
//   slice of collection / model navigation by necessity; the duplication is the
//   emitter package's own concern and is bounded to this file.
//
// This module performs NO string assembly of the full access chain and emits NO
// diagnostics — the $onValidate model walk is the gate (a path the validator
// accepted is known-resolvable here). It exposes pure per-segment resolution
// primitives the result-predicate emitter composes. No runtime reflection: the
// crawl is gen-time only, producing direct typed member access.

import type { Model, ModelProperty, Type } from "@typespec/compiler";
import { toPascal } from "./name-transforms.js";

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/**
 * Resolution of one plain `field` path segment against the current model node.
 * `csName` / `tsName` are the emitted member names; `optional` drives whether
 * the access uses `?.`. `isArray` / `elementModel` describe a collection field
 * so a following array accessor (`count` / `any` / `all` / `contains`) can be
 * emitted with the correct LINQ / JS form and recurse into the element model.
 */
export interface SegmentResolution {
  /** PascalCase C# property name (e.g. "Items"). */
  readonly csName: string;
  /** lowerCamelCase TS property name (e.g. "items"). */
  readonly tsName: string;
  /** True when the property is declared optional (`?:`) → null-conditional access. */
  readonly optional: boolean;
  /** True when the property's resolved type is a collection (`T[]`). */
  readonly isArray: boolean;
  /**
   * The element Model when the property (or its array element) is a non-array
   * Model — the node an `any` / `all` sub-predicate walks. Undefined when the
   * element is a scalar / enum / union (no sub-model to recurse into).
   */
  readonly elementModel?: Model;
  /**
   * The resolved field Model when the property is a non-array nested Model — the
   * node the NEXT path segment walks. Undefined for scalar / collection / enum /
   * union fields (a deeper field segment is then unresolvable, which the
   * decorator validator already rejected).
   */
  readonly fieldModel?: Model;
}

// ---------------------------------------------------------------------------
// Native @typespec/compiler type-graph helpers
// ---------------------------------------------------------------------------

/** True when a TypeSpec Type is the `Array<T>` collection model. */
function isArrayType(t: Type): t is Model {
  return t.kind === "Model" && t.name === "Array";
}

/** The element Type of an `Array<T>` model, or undefined when unresolved. */
function arrayElement(arrayType: Model): Type | undefined {
  return arrayType.indexer?.value;
}

/** The Model a type resolves to for a deeper walk, or undefined for non-models / arrays. */
function asNestedModel(t: Type | undefined): Model | undefined {
  return t !== undefined && t.kind === "Model" && !isArrayType(t)
    ? t
    : undefined;
}

// ---------------------------------------------------------------------------
// Public resolution primitives
// ---------------------------------------------------------------------------

/**
 * Resolve a single `field` segment against `model`. The $onValidate model walk
 * guarantees the field exists and the path is well-formed, so an absent property
 * here is a contract-level invariant violation (the decorator validator is the
 * gate) — it throws loudly rather than silently emitting broken access (fail-loud,
 * no silent `null`). `model` undefined means a prior segment resolved to a
 * non-model type, which the decorator validator likewise rejected.
 */
export function resolveSegment(
  model: Model | undefined,
  fieldName: string,
): SegmentResolution {
  if (model === undefined)
    throw new Error(
      `predicate-emit-walk: cannot resolve '${fieldName}' — ` +
        `prior segment did not resolve to a model (validator should have rejected this path)`,
    );

  const prop: ModelProperty | undefined = model.properties.get(fieldName);
  if (prop === undefined)
    throw new Error(
      `predicate-emit-walk: '${fieldName}' is not a property of model '${model.name}' ` +
        `(validator should have rejected this path)`,
    );

  const propType: Type = prop.type;
  const isArray = isArrayType(propType);
  const element = isArray ? arrayElement(propType) : undefined;

  return {
    csName: toPascal(fieldName),
    tsName: fieldName,
    optional: prop.optional,
    isArray,
    elementModel: isArray ? asNestedModel(element) : undefined,
    fieldModel: isArray ? undefined : asNestedModel(propType),
  };
}
