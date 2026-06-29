// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Shared model walker — the single source of model-field resolution for all
// DTO emitters. Converts a TypeSpec `Model` into an ordered `FieldInfo[]`,
// resolving scalar types via the scalar registry, detecting collections,
// nested models, and named/inline string-literal enums, and reading @d2Redact
// state. Both the C# DTO emitter and the TS DTO emitter consume this walker —
// cross-language field-set parity is guaranteed by construction (one walker
// feeds both).
//
// Loud failures:
//   D2TSP001  unmapped-scalar           — scalar has no registry entry
//   D2TSP002  unsupported-property-type — anonymous-model / model-variant /
//                                         otherwise-unrecognized prop kind
//   D2TSP007  unsupported-union-shape   — a union whose variants are NOT a
//                                         closed set of string literals
//                                         (mixed-primitive / numeric / model)

import type {
  Enum,
  EnumMember,
  Model,
  ModelProperty,
  Program,
  Type,
  Union,
} from "@typespec/compiler";
import { resolveEncodedName } from "@typespec/compiler";
import { D2_FIELD_KEY, D2_REDACT_KEY } from "@d2/typespec-decorators";
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
  /**
   * The originating TypeSpec Model node. Present when the `NestedModel` was
   * produced by `walkModel` (i.e. from a live TypeSpec compile). Absent in
   * hand-constructed test fixtures where no compiler context is available.
   * Used by callers that need to query TypeSpec state maps (e.g. @d2Reserved)
   * keyed on the compiler's Type objects.
   */
  readonly typeModel?: Model;
}

/** One member of a collected enum (or synthesized string-literal union). */
export interface EnumMemberInfo {
  /** PascalCase C# member identifier (e.g. "ThirdParty"). */
  readonly csName: string;
  /**
   * The canonical wire string for this member — the JSON/proto/TS wire form.
   * For a bare-member named enum this is the member name; for a string-literal
   * member it is the literal value; for an explicit-int member it is STILL the
   * member name (the int is C#-side backing only — the wire is always a string).
   */
  readonly wireValue: string;
  /**
   * True when `csName !== wireValue` — the C# emitter must add
   * `[EnumMember(Value = "<wireValue>")]` so the JSON wire form is the literal,
   * not the PascalCase member name. False when the member name already IS the
   * wire string (no attribute needed).
   */
  readonly needsEnumMember: boolean;
  /**
   * The explicit integer backing value when the source enum member declared one
   * (e.g. `Low: 0`). Undefined for bare-member enums and string-literal unions
   * (the C# backing is then implicit sequential). The wire form is unaffected —
   * it is always `wireValue` (a string) regardless of this backing.
   */
  readonly intValue?: number;
}

/**
 * A closed-string-set enum referenced by a field (emitted as a sibling C# enum
 * + TS const-object). Collected from a named `enum`, a named string-literal
 * `union`, or an inline anonymous string-literal union (synthetic name).
 */
export interface NestedEnum {
  /** C#/TS type name (e.g. "KeyKind" or the synthetic "EnumInputInlineState"). */
  readonly name: string;
  /** Ordered members (source order). */
  readonly members: readonly EnumMemberInfo[];
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
   * For enum fields this is "string" — an enum/union maps to a proto `string`
   * field carrying the member-name wire string (the cross-language enum wire is
   * always a string; type safety lives at the transport mapper).
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
  /**
   * The JSON wire-name override from @encodedName("application/json", "..."),
   * present ONLY when that override DIFFERS from the default camelCase
   * serialization of csName. The C# DTO emitter emits
   * [property: JsonPropertyName("<jsonName>")] for it so the JSON wire form is
   * the canonical override (e.g. "jwks_uri" for a property csName "JwksUri").
   * Undefined when the property carries no @encodedName, or when the override
   * equals the default System.Text.Json wire name (no attribute needed —
   * keeps existing generated output byte-identical). Read via the stock TypeSpec
   * resolveEncodedName(program, prop, "application/json") API, NOT a @d2* state
   * map.
   */
  readonly jsonName?: string;
  /**
   * Author-pinned proto3 field number from @d2Field(n). Populated when the
   * property carries an @d2Field annotation; undefined for unpinned properties
   * (DTO-only / in-process ops). The proto emitter uses this number verbatim
   * instead of assigning positionally; an unpinned property on a proto-bound
   * model is a loud build failure (D2TSP009).
   */
  readonly fieldNumber?: number;
  /** Populated when the property type is a non-array nested Model. */
  readonly nested?: NestedModel;
  /**
   * Populated when the property type (or array element type) is a supported
   * enum / string-literal union. Carries the collected enum descriptor so the
   * transport mapper (proto string ↔ DTO enum) and the byte-gate tests can see
   * the member set. The csType/tsType already reference the enum by name.
   */
  readonly enumRef?: NestedEnum;
}

/** Result of walking a model: the field list plus any collected nested types. */
export interface WalkResult {
  /** Ordered field descriptors for the walked model. */
  readonly fields: readonly FieldInfo[];
  /**
   * Distinct nested models collected during the walk, in discovery order.
   * Each nested model appears exactly once (deduped by model name).
   */
  readonly nestedModels: readonly NestedModel[];
  /**
   * Distinct enums collected during the walk, in discovery order. Each enum
   * appears exactly once (deduped by name). The C# DTO emitter emits a sibling
   * `public enum` per entry; the TS DTO emitter emits a sibling const-object.
   */
  readonly nestedEnums: readonly NestedEnum[];
}

/** Diagnostic code union surfaced by the walker's onError callback. */
export type WalkErrorCode =
  | "unmapped-scalar"
  | "unsupported-property-type"
  | "unsupported-union-shape";

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
 * Named enums and closed string-literal unions are collected in `nestedEnums`
 * (deduped); the field references the enum by name.
 * Anonymous-model and ambiguous-union properties fire D2TSP002 / D2TSP007 and
 * are skipped.
 *
 * @param program  - The compiled TypeSpec program (for stateMap reads).
 * @param model    - The TypeSpec Model to walk.
 * @param onError  - Callback for diagnostic emissions (D2TSP001/002/007).
 * @returns Ordered FieldInfo list + deduplicated nested-model + nested-enum lists.
 */
export function walkModel(
  program: Program,
  model: Model,
  onError: (code: WalkErrorCode, message: string) => void,
): WalkResult {
  const redactMap = program.stateMap(D2_REDACT_KEY);
  const fieldMap = program.stateMap(D2_FIELD_KEY);
  const nestedByName = new Map<string, NestedModel>();
  const enumsByName = new Map<string, NestedEnum>();
  const fields: FieldInfo[] = [];

  for (const [propName, prop] of model.properties) {
    const fieldInfo = resolveProperty(
      program,
      model.name,
      propName,
      prop,
      redactMap,
      fieldMap,
      nestedByName,
      enumsByName,
      onError,
    );
    if (fieldInfo !== undefined) fields.push(fieldInfo);
  }

  return {
    fields,
    nestedModels: [...nestedByName.values()],
    nestedEnums: [...enumsByName.values()],
  };
}

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

function resolveProperty(
  program: Program,
  modelName: string,
  propName: string,
  prop: ModelProperty,
  redactMap: Map<object, unknown>,
  fieldMap: Map<object, unknown>,
  nestedByName: Map<string, NestedModel>,
  enumsByName: Map<string, NestedEnum>,
  onError: (code: WalkErrorCode, message: string) => void,
): FieldInfo | undefined {
  const optional = prop.optional;
  const redact = redactMap.get(prop) === true;
  const fieldNumber =
    typeof fieldMap.get(prop) === "number"
      ? (fieldMap.get(prop) as number)
      : undefined;
  const csName = toPascal(propName);

  // The @encodedName("application/json", "...") override, kept ONLY when it
  // differs from System.Text.Json's default camelCase wire name for csName.
  // A property with no @encodedName (every current op) or whose override equals
  // the default wire name yields jsonName === undefined → the C# DTO emitter
  // emits NO [JsonPropertyName] attribute → existing generated output is
  // byte-identical (the differs-from-default guard).
  const jsonName = resolveJsonName(program, prop, csName);

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
      jsonName,
      fieldNumber,
    };
  }

  // ---- Array / Collection ---------------------------------------------------
  // TypeSpec `T[]` is represented as a Model named "Array" with a template arg.
  if (t.kind === "Model" && t.name === "Array") {
    return resolveArrayProperty(
      program,
      modelName,
      propName,
      t,
      csName,
      optional,
      redact,
      jsonName,
      fieldNumber,
      nestedByName,
      enumsByName,
      onError,
      fieldMap,
    );
  }

  // ---- Named enum (non-array) ----------------------------------------------
  if (t.kind === "Enum") {
    const collected = collectNamedEnum(t, enumsByName, propName, onError);
    if (collected === undefined) return undefined;
    return {
      name: propName,
      csName,
      csType: optional ? `${collected.name}?` : collected.name,
      tsName: propName,
      tsType: collected.name,
      // An enum maps to a proto `string` field (the member-name wire string).
      protoType: "string",
      repeated: false,
      optional,
      redact,
      jsonName,
      fieldNumber,
      enumRef: collected,
    };
  }

  // ---- Union (non-array) ----------------------------------------------------
  if (t.kind === "Union") {
    const resolved = resolveUnionProperty(
      modelName,
      propName,
      t,
      optional,
      enumsByName,
      onError,
    );
    if (resolved === undefined) return undefined;
    return {
      name: propName,
      csName,
      csType: resolved.optional ? `${resolved.enum.name}?` : resolved.enum.name,
      tsName: propName,
      tsType: resolved.enum.name,
      protoType: "string",
      repeated: false,
      // S-6: a `<literals> | null` union normalizes to optional.
      optional: resolved.optional,
      redact,
      jsonName,
      fieldNumber,
      enumRef: resolved.enum,
    };
  }

  // ---- Nested model (non-array) --------------------------------------------
  if (t.kind === "Model") {
    const nested = collectNested(
      program,
      t,
      nestedByName,
      enumsByName,
      onError,
      fieldMap,
    );
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
      jsonName,
      fieldNumber,
      nested,
    };
  }

  // ---- Unsupported: intrinsic, etc. ----------------------------------------
  onError(
    "unsupported-property-type",
    `D2TSP002: unsupported property type '${t.kind}' on property '${propName}' — expected a scalar, a named model, a supported enum/string-literal union, or an array thereof`,
  );
  return undefined;
}

/**
 * Resolve the JSON wire-name override for a property, returning it ONLY when it
 * differs from the default System.Text.Json camelCase serialization of csName.
 *
 * `resolveEncodedName` returns the `@encodedName("application/json", "…")` value
 * when present, otherwise the property's own TypeSpec name — it NEVER returns
 * undefined. System.Text.Json (default policy) serializes a PascalCase property
 * `JwksUri` as `jwksUri` (first char lowered), so the default wire name is
 * `csName[0].toLowerCase() + csName.slice(1)`. The override is kept only when it
 * diverges from that default — a property with no `@encodedName` (its resolved
 * name is its lowerCamel TypeSpec name, which equals the default wire name), or
 * one whose override happens to equal the camelCase default, yields `undefined`
 * so NO [JsonPropertyName] attribute is emitted and existing generated output
 * stays byte-identical (the byte-gate-safety property).
 */
function resolveJsonName(
  program: Program,
  prop: ModelProperty,
  csName: string,
): string | undefined {
  const encoded = resolveEncodedName(program, prop, "application/json");

  const defaultJsonName =
    csName.length > 0 ? csName[0]!.toLowerCase() + csName.slice(1) : csName;

  return encoded !== defaultJsonName ? encoded : undefined;
}

/**
 * Resolve an `Array` (TypeSpec `T[]`) property. The element may be a scalar, a
 * nested model, or a supported enum/string-literal union. Anything else is a
 * loud failure (D2TSP002 / D2TSP007).
 */
function resolveArrayProperty(
  program: Program,
  modelName: string,
  propName: string,
  arrayType: Model,
  csName: string,
  optional: boolean,
  redact: boolean,
  jsonName: string | undefined,
  fieldNumber: number | undefined,
  nestedByName: Map<string, NestedModel>,
  enumsByName: Map<string, NestedEnum>,
  onError: (code: WalkErrorCode, message: string) => void,
  fieldMap: Map<object, unknown>,
): FieldInfo | undefined {
  const elementType = arrayType.indexer?.value;

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
      jsonName,
      fieldNumber,
    };
  }

  if (elementType?.kind === "Enum") {
    // Array of a supported enum (S-1/S-2 element) — collect the enum, the
    // element type is the enum name; proto element type is `string`.
    const collected = collectNamedEnum(
      elementType,
      enumsByName,
      propName,
      onError,
    );
    if (collected === undefined) return undefined;
    return {
      name: propName,
      csName,
      csType: `IReadOnlyList<${collected.name}>`,
      tsName: propName,
      tsType: `readonly ${collected.name}[]`,
      protoType: "string",
      repeated: true,
      optional,
      redact,
      jsonName,
      fieldNumber,
      enumRef: collected,
    };
  }

  if (elementType?.kind === "Union") {
    // Array of a closed string-literal union element. `null` in an array
    // element is not meaningful (arrays carry no per-element optionality here),
    // so a `| null` element variant is rejected by the resolver (mustNotBeNull).
    const resolved = resolveUnionProperty(
      modelName,
      propName,
      elementType,
      false,
      enumsByName,
      onError,
      /* allowNullVariant */ false,
    );
    if (resolved === undefined) return undefined;
    return {
      name: propName,
      csName,
      csType: `IReadOnlyList<${resolved.enum.name}>`,
      tsName: propName,
      tsType: `readonly ${resolved.enum.name}[]`,
      protoType: "string",
      repeated: true,
      optional,
      redact,
      jsonName,
      fieldNumber,
      enumRef: resolved.enum,
    };
  }

  if (elementType?.kind === "Model" && elementType.name !== "Array") {
    // Collection of nested models — recurse to collect the nested model.
    const nested = collectNested(
      program,
      elementType,
      nestedByName,
      enumsByName,
      onError,
      fieldMap,
    );
    return {
      name: propName,
      csName,
      csType: `IReadOnlyList<${elementType.name}>`,
      tsName: propName,
      tsType: `readonly ${elementType.name}[]`,
      // protoType is undefined for nested-model collections; the model name is
      // used as the proto type directly (not a registry scalar).
      protoType: undefined,
      repeated: true,
      optional,
      redact,
      jsonName,
      fieldNumber,
      nested,
    };
  }

  // Unknown array element type — unsupported.
  const elementKind = elementType?.kind ?? "unknown";
  onError(
    "unsupported-property-type",
    `D2TSP002: unsupported array element type '${elementKind}' on property '${propName}' — only scalar, named-model, named-enum, or string-literal-union elements are supported`,
  );
  return undefined;
}

/**
 * Collect a named `enum` into the dedup map and return the descriptor. A named
 * enum may carry bare members (`Rsa`), string-valued members (`active: "active"`),
 * or explicit-int members (`Low: 0`). The wire form is always a string (the
 * member name, or the string-literal value); explicit ints are C#-side backing.
 */
function collectNamedEnum(
  e: Enum,
  enumsByName: Map<string, NestedEnum>,
  propName: string,
  onError: (code: WalkErrorCode, message: string) => void,
): NestedEnum | undefined {
  // A malformed/empty enum (no members map) has no cross-language form — loud.
  const rawMembers = e.members !== undefined ? [...e.members.values()] : [];
  const members = buildEnumMembers(rawMembers);
  // A genuinely-empty enum (no members) is malformed for a DTO field — loud.
  if (members.length === 0) {
    onError(
      "unsupported-property-type",
      `D2TSP002: enum '${e.name}' on property '${propName}' has no members — an empty enum has no cross-language representation`,
    );
    return undefined;
  }

  return registerEnum(e.name, members, enumsByName, propName, onError);
}

/** Build the ordered member descriptors for a named enum's members. */
function buildEnumMembers(members: readonly EnumMember[]): EnumMemberInfo[] {
  const result: EnumMemberInfo[] = [];

  for (const m of members) {
    const csName = sanitizeIdentifier(m.name);

    if (typeof m.value === "number") {
      // Explicit-int member — wire form is STILL the member name (string wire).
      result.push({
        csName,
        wireValue: csName,
        needsEnumMember: false,
        intValue: m.value,
      });
    } else if (typeof m.value === "string") {
      // String-valued member — the literal IS the wire string.
      result.push({
        csName,
        wireValue: m.value,
        needsEnumMember: csName !== m.value,
      });
    } else {
      // Bare member (no value) — the member name is the wire string.
      result.push({ csName, wireValue: csName, needsEnumMember: false });
    }
  }

  return result;
}

/**
 * Resolve a union property to a collected enum, or fail loud. Returns the
 * collected enum + whether the field is optional (S-6: a `| null` variant
 * makes the field optional and is stripped from the member set).
 *
 * @param allowNullVariant - When false (array-element context), a `null`
 *   variant is itself a loud failure (an array element cannot be nullable here).
 */
function resolveUnionProperty(
  modelName: string,
  propName: string,
  u: Union,
  declaredOptional: boolean,
  enumsByName: Map<string, NestedEnum>,
  onError: (code: WalkErrorCode, message: string) => void,
  allowNullVariant = true,
): { enum: NestedEnum; optional: boolean } | undefined {
  // A malformed/empty union (no variants map) has no closed string set — loud.
  const variants = u.variants !== undefined ? [...u.variants.values()] : [];
  let sawNull = false;
  const stringLiterals: string[] = [];

  for (const v of variants) {
    const vt = v.type;
    if (vt.kind === "String") {
      stringLiterals.push(vt.value);
    } else if (isNullIntrinsic(vt)) {
      sawNull = true;
    } else {
      // Any non-string-literal variant (scalar, numeric literal, boolean
      // literal, model, nested union) makes this an unsupported union shape.
      onError(
        "unsupported-union-shape",
        `D2TSP007: union property '${propName}' has an unsupported shape — only a closed set of string literals (or a named enum) maps to a cross-language enum; mixed-primitive, numeric-literal, discriminated, or model unions are not supported`,
      );
      return undefined;
    }
  }

  if (sawNull && !allowNullVariant) {
    onError(
      "unsupported-union-shape",
      `D2TSP007: union property '${propName}' has a 'null' variant in an array-element position — a nullable array element has no cross-language representation`,
    );
    return undefined;
  }

  // A union of ONLY `null` (or empty) is not a closed string set.
  if (stringLiterals.length === 0) {
    onError(
      "unsupported-union-shape",
      `D2TSP007: union property '${propName}' has no string-literal variants — only a closed set of string literals (or a named enum) maps to a cross-language enum`,
    );
    return undefined;
  }

  const members = stringLiterals.map((lit) => {
    const csName = sanitizeIdentifier(lit);
    return { csName, wireValue: lit, needsEnumMember: csName !== lit };
  });

  // Named union → use its name; anonymous inline union → synthesize a name from
  // the owning model + the PascalCase property name (e.g. EnumInputInlineState).
  const enumName =
    u.name !== undefined && u.name.length > 0
      ? u.name
      : `${modelName}${toPascal(propName)}`;

  const collected = registerEnum(
    enumName,
    members,
    enumsByName,
    propName,
    onError,
  );
  if (collected === undefined) return undefined;

  return { enum: collected, optional: declaredOptional || sawNull };
}

/**
 * Register an enum descriptor in the dedup map. Returns the existing entry when
 * the same name was already collected with an IDENTICAL member set (legitimate
 * reuse across Input/Output). A name collision with a DIFFERENT member set is a
 * loud failure (the synthetic-name guard) — never a silent merge.
 */
function registerEnum(
  name: string,
  members: readonly EnumMemberInfo[],
  enumsByName: Map<string, NestedEnum>,
  propName: string,
  onError: (code: WalkErrorCode, message: string) => void,
): NestedEnum | undefined {
  const existing = enumsByName.get(name);
  if (existing !== undefined) {
    if (!sameMembers(existing.members, members)) {
      onError(
        "unsupported-union-shape",
        `D2TSP007: enum name '${name}' (from property '${propName}') collides with a different declaration of the same name — rename the property or the enum so the synthetic name is unique`,
      );
      return undefined;
    }
    return existing;
  }

  const collected: NestedEnum = { name, members };
  enumsByName.set(name, collected);
  return collected;
}

/** True when two member lists are identical (name + wire + backing). */
function sameMembers(
  a: readonly EnumMemberInfo[],
  b: readonly EnumMemberInfo[],
): boolean {
  if (a.length !== b.length) return false;

  for (let i = 0; i < a.length; i++) {
    const x = a[i]!;
    const y = b[i]!;
    if (
      x.csName !== y.csName ||
      x.wireValue !== y.wireValue ||
      x.needsEnumMember !== y.needsEnumMember ||
      x.intValue !== y.intValue
    )
      return false;
  }

  return true;
}

/** True when a union variant type is the `null` intrinsic. */
function isNullIntrinsic(t: Type): boolean {
  return t.kind === "Intrinsic" && t.name === "null";
}

/**
 * Sanitize an arbitrary string literal into a PascalCase C# identifier.
 * Non-identifier characters (e.g. '-' in "third-party") are treated as word
 * separators; the result is PascalCased ("ThirdParty"). A leading digit is
 * prefixed with '_' so the result is always a valid C# identifier.
 *
 * Both regexes are linear with bounded input (literal strings) — Bucket 2 per
 * the regex-redos discipline; no matchTimeout / JIT pre-warm needed.
 */
function sanitizeIdentifier(literal: string): string {
  // Split on any run of non-alphanumeric characters, PascalCase each segment.
  const segments = literal.split(/[^A-Za-z0-9]+/).filter((s) => s.length > 0);
  const pascal = segments.map((s) => s[0]!.toUpperCase() + s.slice(1)).join("");
  // A literal that is already a valid identifier with no separators round-trips
  // through the segment join unchanged (e.g. "active" → "Active", "Rsa" → "Rsa").
  if (pascal.length === 0) return "_";
  return /^[0-9]/.test(pascal) ? `_${pascal}` : pascal;
}

/**
 * Collect a nested model into the dedup map. Returns the NestedModel (which
 * may have been seen before — same object from the map). Nested models carry NO
 * op-level @d2Redact state (they are transport containers, not direct op-context
 * objects), so each is walked against an EMPTY redact map → every nested field's
 * `redact` is false.
 *
 * Depth-N: a nested model's own fields are resolved by the SAME `resolveProperty`
 * logic the top-level op model uses — so a nested model that itself references a
 * deeper nested model (or an array of one), an enum, a union, a scalar, or a
 * scalar/model array all resolve identically and recurse to arbitrary depth. The
 * dedup map is registered BEFORE the field walk (with a placeholder), so a cyclic
 * or self-referential model terminates: the recursive `collectNested` for the same
 * name finds the in-progress entry and returns it instead of recursing forever.
 *
 * Strict fail-loud: an unmapped nested scalar / unsupported nested type fires the
 * SAME loud diagnostic (D2TSP001 / D2TSP002 / D2TSP007) as a top-level field — it
 * is NEVER silently omitted. The field is dropped only AFTER the loud diagnostic,
 * exactly like a top-level field.
 */
function collectNested(
  program: Program,
  model: Model,
  nestedByName: Map<string, NestedModel>,
  enumsByName: Map<string, NestedEnum>,
  onError: (code: WalkErrorCode, message: string) => void,
  fieldMap?: Map<object, unknown>,
): NestedModel {
  const existing = nestedByName.get(model.name);
  if (existing !== undefined) return existing;

  // Register a mutable placeholder BEFORE walking the fields so a self-reference
  // (or a cycle through deeper models) sees an in-progress entry and terminates.
  const nestedFields: FieldInfo[] = [];
  const nested: NestedModel = {
    name: model.name,
    fields: nestedFields,
    typeModel: model,
  };
  nestedByName.set(model.name, nested);

  // Nested models carry no redact state — walk against an empty redact map so every
  // resolved field's `redact` is false. Resolution is otherwise identical to a
  // top-level field (scalars, optionals, arrays, deeper nested models, enums/unions),
  // which is what makes nested support depth-agnostic + uniformly loud.
  // The fieldMap (for @d2Field pins) is shared from the outer walkModel so nested
  // model properties can carry their own field-number pins.
  const emptyRedactMap = new Map<object, unknown>();
  const resolvedFieldMap = fieldMap ?? new Map<object, unknown>();

  for (const [propName, prop] of model.properties) {
    const fieldInfo = resolveProperty(
      program,
      model.name,
      propName,
      prop,
      emptyRedactMap,
      resolvedFieldMap,
      nestedByName,
      enumsByName,
      onError,
    );

    if (fieldInfo !== undefined) nestedFields.push(fieldInfo);
  }

  return nested;
}
