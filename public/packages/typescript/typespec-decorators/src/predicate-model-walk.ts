// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Native-TypeSpec model-graph resolver for the @d2Resilience result-predicate
// DSL. Validates a parsed predicate's `result.data.<path>` segments — and the
// element-field paths inside `any` / `all` sub-predicates — against the REAL
// TOutput Model graph of the decorated operation.
//
// Why this lives here (NOT the emitter's walkModel):
//   The emitter package (@d2/typespec-emitters) imports FROM this decorator
//   package (it reads back D2_REDACT_KEY etc.). Importing the emitter's
//   `walkModel` here would create a dependency cycle (decorators → emitter →
//   decorators). So the decorator package walks the native @typespec/compiler
//   Model / ModelProperty / Type API directly. This duplicates a small slice of
//   collection / scalar resolution by necessity — the duplication is justified
//   by the cycle constraint and is the decorator package's own concern. The
//   emitter's richer FieldInfo (csName/tsName/csType, isCollection, element type
//   info for code-gen) is a separate, emitter-owned model crawl.
//
// What it checks (model-dependent — the parser already did the model-free ones):
//   - every data-path field segment exists on the current model node
//     (resilience-predicate-unknown-output-field at the top level;
//      resilience-predicate-unknown-element-field inside a sub-predicate)
//   - an array accessor (count / any / all / contains) only follows a collection
//     segment (resilience-predicate-not-a-collection otherwise)
//   - the `contains` literal type matches the collection's scalar element
//     (resilience-predicate-type-mismatch)
//   - a terminal `== / != / in` comparison's literal type matches the resolved
//     terminal scalar (resilience-predicate-type-mismatch)
//   - a nullable / optional intermediate segment is PERMITTED (recorded only,
//     no diagnostic — null-propagation is correct runtime behavior)
//
// The walk is pure native-API: no walkModel import, no runtime reflection.

import type { Model, ModelProperty, Program, Type } from "@typespec/compiler";
import type {
  ArrayAccessorSegment,
  DataPathNode,
  LiteralNode,
  PathSegment,
  PredicateNode,
  ResultPredicateDiagnosticCode,
} from "./result-predicate-dsl.js";

/** A model-graph validation error to be reported by the $onValidate arm. */
export interface ModelWalkError {
  readonly code: ResultPredicateDiagnosticCode;
  readonly message: string;
}

/**
 * Whether a path segment resolves the predicate against the op's TOutput
 * (top-level `result.data`) or against a quantifier element type (sub-predicate).
 * Selects which "unknown field" diagnostic to raise.
 */
type Origin = "output" | "element";

// ----------------------------------------------------------------
// Scalar-kind classification (maps a TypeSpec scalar to the literal kind it
// admits, mirroring the parser's model-free envelope rules).
// ----------------------------------------------------------------

/** The literal kind a resolved scalar admits in a comparison, or undefined if non-comparable. */
function scalarLiteralKind(
  scalarName: string,
): "bool" | "int" | "string" | undefined {
  if (scalarName === "boolean") return "bool";

  if (
    scalarName === "int8" ||
    scalarName === "int16" ||
    scalarName === "int32" ||
    scalarName === "int64" ||
    scalarName === "uint8" ||
    scalarName === "uint16" ||
    scalarName === "uint32" ||
    scalarName === "uint64" ||
    scalarName === "integer" ||
    scalarName === "safeint"
  )
    return "int";

  if (scalarName === "string" || scalarName === "url") return "string";

  // Other scalars (float, bytes, plainDate, …) are not comparable in the DSL.
  return undefined;
}

// ----------------------------------------------------------------
// Type-graph helpers (native @typespec/compiler API)
// ----------------------------------------------------------------

/** True when a TypeSpec Type is the `Array<T>` collection model. */
function isArray(t: Type): t is Model {
  return t.kind === "Model" && t.name === "Array";
}

/** The element Type of an `Array<T>` model, or undefined when unresolved. */
function arrayElement(arrayType: Model): Type | undefined {
  return arrayType.indexer?.value;
}

// ----------------------------------------------------------------
// Public entry — walk a whole predicate tree
// ----------------------------------------------------------------

/**
 * Walk the parsed predicate against the op's output Model, collecting every
 * model-dependent error. Envelope-only predicates (no `result.data` path)
 * produce no walk work. `outputModel` is undefined when the op's return type is
 * NOT a Model (e.g. a scalar or `void`) — any `result.data.<path>` is then an
 * unknown-output-field error.
 *
 * `program` is accepted for call-site symmetry with the rest of the validation
 * surface and future checker-dependent extensions; the resolution itself uses
 * only the linked Model graph on the passed `outputModel`.
 */
export function walkPredicateModel(
  _program: Program,
  outputModel: Model | undefined,
  predicate: PredicateNode,
): readonly ModelWalkError[] {
  const errors: ModelWalkError[] = [];
  walkPredicateNode(outputModel, "output", predicate, errors);
  return errors;
}

// ----------------------------------------------------------------
// Recursive predicate-tree walk
// ----------------------------------------------------------------

/**
 * Walk a predicate (sub-)tree against `model`. `origin` is "output" at the top
 * level (`result.data` rooted, errors are unknown-output-field) and "element"
 * inside a quantifier sub-predicate (elemVar rooted, errors are
 * unknown-element-field). Only data-path accessors need the model — envelope
 * accessors were fully checked by the parser (and never appear inside a
 * sub-predicate, whose root is the bound element variable).
 */
function walkPredicateNode(
  model: Model | undefined,
  origin: Origin,
  node: PredicateNode,
  errors: ModelWalkError[],
): void {
  if (node.kind === "bool") {
    walkPredicateNode(model, origin, node.left, errors);
    walkPredicateNode(model, origin, node.right, errors);
    return;
  }

  if (node.kind === "booleanAccess") {
    // A standalone boolean data path (terminates in any/all/contains).
    walkDataPath(model, node.access, origin, undefined, errors);
    return;
  }

  // ComparisonNode — only data-path accessors need the model.
  if (node.access.kind !== "dataPath") return;

  // The comparison's literal(s) constrain the terminal scalar type. The parser
  // already enforced in-list homogeneity, so the first literal suffices.
  const literals = Array.isArray(node.rhs) ? node.rhs : [node.rhs];
  walkDataPath(model, node.access, origin, literals[0], errors);
}

// ----------------------------------------------------------------
// Data-path resolution
// ----------------------------------------------------------------

/**
 * Resolve a data path left-to-right against `startModel`, validating each
 * segment, array accessor, and (when provided) the terminal comparison literal.
 *
 * @param startModel   The model the path is rooted at (TOutput for a top-level
 *                     `result.data` path; the element model for a sub-predicate).
 *                     `undefined` ⇒ the root was not a Model → unknown-field.
 * @param origin       Selects the unknown-field diagnostic (output vs element).
 * @param terminalLit  The comparison literal constraining the terminal scalar,
 *                     or undefined for a standalone boolean path / `in`-list.
 */
function walkDataPath(
  startModel: Model | undefined,
  path: DataPathNode,
  origin: Origin,
  terminalLit: LiteralNode | undefined,
  errors: ModelWalkError[],
): void {
  const segments = path.segments;

  if (startModel === undefined) {
    // The path's first segment is always a field (parser-enforced).
    const first = segments[0]!;
    errors.push(
      unknownField(origin, segDisplay(first), segments, 0, path.root),
    );
    return;
  }

  // The "current type" pointer threaded through the path. Starts as the model;
  // becomes the resolved field type after each segment.
  let currentModel: Model | undefined = startModel;
  let currentType: Type = startModel;

  for (let i = 0; i < segments.length; i++) {
    const seg = segments[i]!;

    if (seg.kind === "field") {
      // A field access on a non-model (e.g. a scalar produced by a prior
      // segment) or a name absent from the model is an unknown field.
      const prop: ModelProperty | undefined = currentModel?.properties.get(
        seg.name,
      );
      if (prop === undefined) {
        errors.push(unknownField(origin, seg.name, segments, i, path.root));
        return;
      }

      const propType: Type = prop.type;
      currentType = propType;
      currentModel =
        propType.kind === "Model" && !isArray(propType) ? propType : undefined;
      continue;
    }

    // Array accessor — the prior resolved type MUST be a collection.
    if (!isArray(currentType)) {
      errors.push({
        code: "resilience-predicate-not-a-collection",
        message:
          `'${segmentLabel(segments, i, path.root)}' applies '${segDisplay(seg)}' ` +
          "to a field that is not a collection",
      });
      return;
    }

    const element = arrayElement(currentType);
    handleArrayAccessor(seg, element, errors);

    // An array accessor is terminal for this path level: count → int,
    // any/all/contains → boolean. Nothing legal follows it.
    return;
  }

  // Fell through the whole path with no array accessor — the terminal is a
  // scalar (or nested model). A path that reaches here is always a comparison
  // (a standalone boolean path terminates in an any/all/contains accessor and
  // returns above), so `terminalLit` is always defined here.
  checkTerminalScalar(currentType, terminalLit!, errors);
}

/** Validate an array accessor segment against the collection's element type. */
function handleArrayAccessor(
  seg: ArrayAccessorSegment,
  element: Type | undefined,
  errors: ModelWalkError[],
): void {
  if (seg.accessor === "count") return; // count → int; nothing to check on the element

  if (seg.accessor === "contains") {
    // contains(<lit>) — the literal must match the element scalar type.
    if (element?.kind === "Scalar" && seg.literal !== undefined) {
      const expected = scalarLiteralKind(element.name);
      if (expected !== undefined && seg.literal.kind !== expected)
        errors.push({
          code: "resilience-predicate-type-mismatch",
          message: `'contains(...)' expects a ${expected} literal to match the collection element, got a ${seg.literal.kind}`,
        });
    }

    return;
  }

  // any / all — recurse into the element model with the sub-predicate. Inside
  // the sub-predicate, data-path roots are the element variable, so unknown
  // fields are element-field errors (origin "element").
  const elementModel =
    element?.kind === "Model" && !isArray(element) ? element : undefined;
  if (seg.subPredicate !== undefined)
    walkPredicateNode(elementModel, "element", seg.subPredicate, errors);
}

// ----------------------------------------------------------------
// Terminal-scalar literal check
// ----------------------------------------------------------------

function checkTerminalScalar(
  terminalType: Type,
  terminalLit: LiteralNode,
  errors: ModelWalkError[],
): void {
  if (terminalType.kind !== "Scalar") return; // nested model / enum terminal — not literal-compared here

  const expected = scalarLiteralKind(terminalType.name);
  if (expected !== undefined && terminalLit.kind !== expected)
    errors.push({
      code: "resilience-predicate-type-mismatch",
      message: `this path resolves to a ${expected} but is compared with a ${terminalLit.kind} literal ('${terminalLit.value}')`,
    });
}

// ----------------------------------------------------------------
// Diagnostic helpers
// ----------------------------------------------------------------

/**
 * Build an unknown-field error for the field segment at `index` (always a field
 * — the caller only invokes this for a not-found field). `fieldName` is that
 * segment's name; `segments[0..index]` build the path label.
 */
function unknownField(
  origin: Origin,
  fieldName: string,
  segments: readonly PathSegment[],
  index: number,
  root: string,
): ModelWalkError {
  const code: ResultPredicateDiagnosticCode =
    origin === "output"
      ? "resilience-predicate-unknown-output-field"
      : "resilience-predicate-unknown-element-field";

  return {
    code,
    message:
      `'${fieldName}' is not a field on ${origin === "output" ? "the operation output" : `element '${root}'`} ` +
      `at path '${segmentLabel(segments, index, root)}'`,
  };
}

/** Display name of a path segment (field name, or `accessor(...)` for an accessor). */
function segDisplay(seg: PathSegment): string {
  return seg.kind === "field" ? seg.name : `${seg.accessor}(...)`;
}

/** A dotted label of the path up to (and including) `index`, prefixed by the root. */
function segmentLabel(
  segments: readonly PathSegment[],
  index: number,
  root: string,
): string {
  const parts = [root];
  for (let i = 0; i <= index && i < segments.length; i++)
    parts.push(segDisplay(segments[i]!));

  return parts.join(".");
}
