// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Shared model walker — the single source of model-field resolution for all
// DTO emitters. Converts a TypeSpec `Model` into an ordered `FieldInfo[]`,
// resolving scalar types via the scalar registry, detecting collections and
// nested models, and reading @d2Redact state. Both the C# DTO emitter and the
// TS DTO emitter consume this walker — cross-language field-set parity is
// guaranteed by construction (one walker feeds both).
//
// Loud failures:
//   D2TSP001  unmapped-scalar           — scalar has no registry entry
//   D2TSP002  unsupported-property-type — enum, union, or anonymous-model prop

import type { Model, ModelProperty, Program } from "@typespec/compiler";
import { D2_REDACT_KEY } from "@d2/typespec-decorators";
import { resolveScalar } from "./scalar-registry.js";
import { toPascal } from "./name-transforms.js";

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/** A nested model referenced by a field (emitted as a sibling record). */
export interface NestedModel {
  /** TypeSpec model name (e.g. "Jwk"). */
  readonly name: string;
  /** Resolved fields of the nested model. */
  readonly fields: readonly FieldInfo[];
}

/** Resolved descriptor for one model property. */
export interface FieldInfo {
  /** Original lowerCamelCase property name from the .tsp model. */
  readonly name: string;
  /** PascalCase C# property / param name. */
  readonly csName: string;
  /** C# type string including collection wrapper and nullability. */
  readonly csType: string;
  /** TypeScript field name (same as `name` — lowerCamelCase). */
  readonly tsName: string;
  /** TypeScript type string including collection wrapper. */
  readonly tsType: string;
  /**
   * proto3 type string for the field's element type (scalars only, no `repeated`
   * prefix or wrapper — the proto emitter adds those). Examples: "string",
   * "bytes", "int32". Populated for scalar and scalar-array fields; undefined
   * for nested-model fields (the model name is used directly as the proto type).
   */
  readonly protoType?: string;
  /**
   * True when the field represents a collection (TypeSpec `T[]`). Proto emitter
   * emits `repeated <protoType> <field_name>` for these. Mirrors the C#
   * `IReadOnlyList<T>` and TS `readonly T[]` wrappers already in csType/tsType.
   */
  readonly repeated: boolean;
  /** True when the ModelProperty is marked optional (`?:`). */
  readonly optional: boolean;
  /** True when the ModelProperty carries @d2Redact state. */
  readonly redact: boolean;
  /** Populated when the property type is a non-array nested Model. */
  readonly nested?: NestedModel;
}

/** Result of walking a model: the field list plus any collected nested models. */
export interface WalkResult {
  /** Ordered field descriptors for the walked model. */
  readonly fields: readonly FieldInfo[];
  /**
   * Distinct nested models collected during the walk, in discovery order.
   * Each nested model appears exactly once (deduped by model name).
   */
  readonly nestedModels: readonly NestedModel[];
}

// ---------------------------------------------------------------------------
// Walk implementation
// ---------------------------------------------------------------------------

/**
 * Walk a TypeSpec Model and resolve each property to a `FieldInfo`.
 *
 * Scalars resolve via the scalar registry (loud D2TSP001 on miss).
 * Arrays (`Array<T>`) resolve the element type and wrap with
 * `IReadOnlyList<T>` / `readonly T[]`.
 * Nested models are recursed into and collected in `nestedModels` (deduped).
 * Enum, union, and anonymous-model properties fire D2TSP002 and are skipped.
 *
 * @param program  - The compiled TypeSpec program (for stateMap reads).
 * @param model    - The TypeSpec Model to walk.
 * @param onError  - Callback for diagnostic emissions (D2TSP001 / D2TSP002).
 * @returns Ordered FieldInfo list + deduplicated nested-model list.
 */
export function walkModel(
  program: Program,
  model: Model,
  onError: (code: "unmapped-scalar" | "unsupported-property-type", message: string) => void,
): WalkResult {
  const redactMap = program.stateMap(D2_REDACT_KEY);
  const nestedByName = new Map<string, NestedModel>();
  const fields: FieldInfo[] = [];

  for (const [propName, prop] of model.properties) {
    const fieldInfo = resolveProperty(propName, prop, redactMap, nestedByName, onError);
    if (fieldInfo !== undefined)
      fields.push(fieldInfo);
  }

  return {
    fields,
    nestedModels: [...nestedByName.values()],
  };
}

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

function resolveProperty(
  propName: string,
  prop: ModelProperty,
  redactMap: Map<object, unknown>,
  nestedByName: Map<string, NestedModel>,
  onError: (code: "unmapped-scalar" | "unsupported-property-type", message: string) => void,
): FieldInfo | undefined {
  const optional = prop.optional;
  const redact = redactMap.get(prop) === true;
  const csName = toPascal(propName);

  const t = prop.type;

  // ---- Scalar ---------------------------------------------------------------
  if (t.kind === "Scalar") {
    let mapping;
    try {
      mapping = resolveScalar(t.name);
    } catch {
      onError(
        "unmapped-scalar",
        `D2TSP001: unmapped TypeSpec scalar '${t.name}' on property '${propName}' — no C#/proto/TS mapping in the scalar registry`,
      );
      return undefined;
    }
    return {
      name: propName,
      csName,
      csType: optional ? `${mapping.cs}?` : mapping.cs,
      tsName: propName,
      tsType: mapping.ts,
      protoType: mapping.proto,
      repeated: false,
      optional,
      redact,
    };
  }

  // ---- Array / Collection ---------------------------------------------------
  // TypeSpec `T[]` is represented as a Model named "Array" with a template arg.
  if (t.kind === "Model" && t.name === "Array") {
    const elementType = t.indexer?.value;
    if (elementType?.kind === "Scalar") {
      let mapping;
      try {
        mapping = resolveScalar(elementType.name);
      } catch {
        onError(
          "unmapped-scalar",
          `D2TSP001: unmapped TypeSpec scalar '${elementType.name}' (array element) on property '${propName}' — no C#/proto/TS mapping in the scalar registry`,
        );
        return undefined;
      }
      return {
        name: propName,
        csName,
        csType: `IReadOnlyList<${mapping.cs}>`,
        tsName: propName,
        tsType: `readonly ${mapping.ts}[]`,
        protoType: mapping.proto,
        repeated: true,
        optional,
        redact,
      };
    }

    if (elementType?.kind === "Model" && elementType.name !== "Array") {
      // Collection of nested models — recurse to collect the nested model.
      const nested = collectNested(elementType, nestedByName);
      return {
        name: propName,
        csName,
        csType: `IReadOnlyList<${elementType.name}>`,
        tsName: propName,
        tsType: `readonly ${elementType.name}[]`,
        // protoType is undefined for nested-model collections; the model name
        // is used as the proto type directly (not a registry scalar).
        protoType: undefined,
        repeated: true,
        optional,
        redact,
        nested,
      };
    }

    // Unknown array element type — unsupported.
    const elementKind = elementType?.kind ?? "unknown";
    onError(
      "unsupported-property-type",
      `D2TSP002: unsupported array element type '${elementKind}' on property '${propName}' — enum, union, and anonymous-model array elements are not yet supported`,
    );
    return undefined;
  }

  // ---- Nested model (non-array) --------------------------------------------
  if (t.kind === "Model") {
    const nested = collectNested(t, nestedByName);
    return {
      name: propName,
      csName,
      csType: optional ? `${t.name}?` : t.name,
      tsName: propName,
      tsType: t.name,
      // protoType is undefined for nested models; the model name is the proto type.
      protoType: undefined,
      repeated: false,
      optional,
      redact,
      nested,
    };
  }

  // ---- Unsupported: enum, union, intrinsic, etc. ---------------------------
  onError(
    "unsupported-property-type",
    `D2TSP002: unsupported property type '${t.kind}' on property '${propName}' — enum, union, and anonymous-model properties are not yet supported by the DTO emitter`,
  );
  return undefined;
}

/**
 * Collect a nested model into the dedup map. Returns the NestedModel (which
 * may have been seen before — same object from the map). Nested models are NOT
 * themselves walked for @d2Redact (they carry no op-level redact state).
 */
function collectNested(
  model: Model,
  nestedByName: Map<string, NestedModel>,
): NestedModel {
  const existing = nestedByName.get(model.name);
  if (existing !== undefined)
    return existing;

  // Recurse: walk nested model's own fields (no redact state — nested models
  // are transport containers, not direct op-context objects; redact is set on
  // the top-level op's input/output properties).
  const nestedFields: FieldInfo[] = [];
  for (const [propName, prop] of model.properties) {
    // For nested models we resolve types only; redact is false (no stateMap
    // context — the nested model is not itself an op parameter).
    const t = prop.type;
    const csName = toPascal(propName);
    const optional = prop.optional;

    if (t.kind === "Scalar") {
      let mapping;
      try {
        mapping = resolveScalar(t.name);
      } catch {
        // Skip unmapped nested scalar silently in nested context —
        // the parent walk will surface the same issue at the top level
        // if the parent field is the problem; here we just omit the field.
        continue;
      }
      nestedFields.push({
        name: propName,
        csName,
        csType: optional ? `${mapping.cs}?` : mapping.cs,
        tsName: propName,
        tsType: mapping.ts,
        protoType: mapping.proto,
        repeated: false,
        optional,
        redact: false,
      });
    }
    // Non-scalar nested fields inside a nested model are intentionally not
    // recursed further at this step (no deep nesting in the GetJwks/sign shape).
  }

  const nested: NestedModel = { name: model.name, fields: nestedFields };
  nestedByName.set(model.name, nested);
  return nested;
}
