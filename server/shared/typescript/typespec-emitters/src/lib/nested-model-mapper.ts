// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Shared nested-model / array-of-model mapper-helper emission for the gRPC
// service and client mapper emitters — the structural analogue of
// enum-mapper.ts.
//
// A nested-MODEL DTO field maps to a proto sub-message; an array-of-MODEL
// field maps to a `repeated <Message>`. Unlike a scalar / bytes / enum field
// (which the four field-helpers map by a direct `source.Property` assignment),
// a nested-model field's proto type differs from its DTO type, so the value
// must recurse through a per-nested-model SUB-MAPPER. These helpers emit that
// bridge:
//
//   extension(<Model> source)        { internal Proto<Model> ToProto<Model>() { … } }  (DTO → proto)
//   extension(Proto<Model> source)   { internal <Model> To<Model>() { … } }            (proto → DTO)
//
// Both sub-mappers recurse the nested model's OWN fields via the SAME field-RHS
// logic the top-level mapper uses — so the recursion is depth-AGNOSTIC: a
// nested model that itself references a deeper nested model (or an array of one)
// emits a sub-mapper for the deeper model too, ad infinitum, because the walker
// collects every model at every depth (deduped) and `collectFieldNestedModels`
// walks the full transitive closure here.
//
// The sub-mapper extension blocks are appended into the same mapper file (server
// or client) as the transport mappers that call them — one home per surface (the
// uppermost-node mapper rule), exactly like the per-enum ToWire / Parse<Enum>Wire
// blocks.

import { toPascal } from "./name-transforms.js";
import type { FieldInfo, NestedModel } from "./model-walk.js";

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/**
 * The RHS of a single outbound (DTO → proto) field assignment.
 *
 * A proto3 `repeated` field generates a `RepeatedField<T>` with NO public
 * setter (Microsoft Learn "Create Protobuf messages for .NET apps" — items must
 * be ADDED to the existing collection), so an array-of-model field CANNOT be
 * assigned with `=`; it uses the C# object-initializer collection form
 * `Field = { <enumerable> }` (which desugars to `Field.Add(<enumerable>)`).
 * A scalar / bytes / enum / single-nested field uses a plain `Field = <expr>`.
 */
export interface OutboundAssign {
  /**
   * "assign" → emit `<Pascal> = <expr>,`.
   * "collectionInit" → emit `<Pascal> = { <expr> },` (repeated field; no setter).
   */
  readonly kind: "assign" | "collectionInit";
  /** The RHS expression (for "assign") or the inner enumerable (for "collectionInit"). */
  readonly expr: string;
}

/**
 * Naming config for the per-nested-model sub-mapper extension blocks. The
 * server mapper references the proto nested-message type via a short `Proto<Model>`
 * using-alias and the DTO nested-record type via its bare (or aliased) name; the
 * client mapper references both fully-qualified with `global::<ns>.<Model>`. The
 * sub-mapper METHOD names (`ToProto<Model>` / `To<Model>`) are identical for both.
 */
export interface NestedMapperNaming {
  /** C# type reference for the DTO nested record (e.g. "PlaceOrderLine" or "global::<dtoNs>.PlaceOrderLine"). */
  readonly dtoTypeName: (modelName: string) => string;
  /** C# type reference for the proto nested message (e.g. "ProtoPlaceOrderLine" or "global::<protoNs>.PlaceOrderLine"). */
  readonly protoTypeName: (modelName: string) => string;
}

// ---------------------------------------------------------------------------
// Collection — the full transitive closure of nested models (depth-N)
// ---------------------------------------------------------------------------

/**
 * Collect the distinct nested models referenced (transitively, at ANY depth) by
 * a field list, in first-encounter order (deduped by model name). A nested model
 * whose own fields reference further nested models contributes those too — the
 * walk recurses the full closure, so the sub-mapper emission covers arbitrary
 * nesting depth. The dedup map also terminates a (hypothetical) self-referential
 * model: it is collected exactly once.
 */
export function collectFieldNestedModels(
  fields: readonly FieldInfo[],
): readonly NestedModel[] {
  const seen = new Map<string, NestedModel>();
  collectInto(fields, seen);

  return [...seen.values()];
}

function collectInto(
  fields: readonly FieldInfo[],
  seen: Map<string, NestedModel>,
): void {
  for (const f of fields) {
    if (f.nested === undefined) continue;

    if (!seen.has(f.nested.name)) {
      seen.set(f.nested.name, f.nested);
      // Recurse the nested model's OWN fields — this is what makes the closure
      // depth-N. The dedup guard above runs BEFORE the recursion, so a cycle
      // (a model that transitively references itself) terminates here.
      collectInto(f.nested.fields, seen);
    }
  }
}

// ---------------------------------------------------------------------------
// Field-RHS builders — shared by the server + client mappers (both directions)
// ---------------------------------------------------------------------------

/**
 * Build the outbound (DTO → proto) assignment for ONE nested-model / array-of-
 * model field. Returns `undefined` when the field is NOT a nested model (the
 * caller falls through to its scalar / bytes / enum handling).
 *
 *   single nested model  → `<source>.<P> is null ? null : <source>.<P>.ToProto<Model>()`  (assign)
 *   array-of-model       → `<source>.<P>.Select(x => x.ToProto<Model>())`                  (collectionInit)
 *
 * The single-nested arm is null-guarded (proto3 message fields carry implicit
 * presence — an absent nested model is `null` on the wire, never a default
 * instance). The array arm has no per-field null guard: a non-optional
 * array-of-model DTO field is a non-null `IReadOnlyList<T>` by the DTO record
 * contract, and an EMPTY list projects to an empty enumerable (the `repeated`
 * field stays empty). An OPTIONAL array-of-model field is null-coalesced to an
 * empty enumerable so the collection-init never dereferences null.
 */
export function buildDtoToProtoNested(
  f: FieldInfo,
  source: string,
): OutboundAssign | undefined {
  if (f.nested === undefined) return undefined;

  const propName = toPascal(f.name);
  const subMapper = `ToProto${f.nested.name}`;

  if (f.repeated) {
    // RepeatedField<T> has no setter → collection-initializer form. An optional
    // array is null-coalesced to an empty sequence so `.Select` never hits null.
    const projected = f.optional
      ? `(${source}.${propName} ?? []).Select(x => x.${subMapper}())`
      : `${source}.${propName}.Select(x => x.${subMapper}())`;

    return { kind: "collectionInit", expr: projected };
  }

  // Single nested model — proto3 implicit presence: null when absent.
  return {
    kind: "assign",
    expr: `${source}.${propName} is null ? null : ${source}.${propName}.${subMapper}()`,
  };
}

/**
 * Build the inbound (proto → DTO) constructor-argument expression for ONE
 * nested-model / array-of-model field. Returns `undefined` when the field is NOT
 * a nested model (the caller falls through to its scalar / bytes handling).
 *
 *   single nested model  → `<source>.<P> is null ? null : <source>.<P>.To<Model>()`
 *   array-of-model       → `<source>.<P>.Select(x => x.To<Model>()).ToList()`
 *
 * A `RepeatedField<T>` is never null (it is `IList<T>`); `.Select(...).ToList()`
 * lands an `IReadOnlyList<T>` for the DTO ctor. The single-nested arm is
 * null-guarded for proto3 implicit presence (absent → `null`).
 */
export function buildProtoToDtoNested(
  f: FieldInfo,
  source: string,
): string | undefined {
  if (f.nested === undefined) return undefined;

  const propName = toPascal(f.name);
  const subMapper = `To${f.nested.name}`;

  if (f.repeated)
    return `${source}.${propName}.Select(x => x.${subMapper}()).ToList()`;

  return `${source}.${propName} is null ? null : ${source}.${propName}.${subMapper}()`;
}

// ---------------------------------------------------------------------------
// Sub-mapper extension-block emission (both directions, per model)
// ---------------------------------------------------------------------------

/**
 * Emit the per-nested-model sub-mapper extension blocks (proto ↔ DTO) for the
 * given models, appended (indented 4 spaces) to a mapper static class body.
 * `pushLine` receives each line; the caller controls placement inside the class.
 *
 * For each model, two extension blocks are emitted:
 *   - DTO → proto: `extension(<Dto> source) { internal <Proto> ToProto<Model>() { … } }`
 *   - proto → DTO: `extension(<Proto> source) { internal <Dto> To<Model>() { … } }`
 *
 * Each body recurses the model's own fields via the SAME field-RHS builders the
 * top-level mapper uses, so a deeper nested model inside this one recurses through
 * ITS sub-mapper (already emitted — `models` is the full transitive closure).
 */
export function emitNestedModelMapperHelpers(
  pushLine: (line: string) => void,
  models: readonly NestedModel[],
  naming: NestedMapperNaming,
): void {
  for (const m of models) {
    const dtoType = naming.dtoTypeName(m.name);
    const protoType = naming.protoTypeName(m.name);

    // ---- DTO → proto sub-mapper ----
    pushLine("");
    pushLine(`    extension(${dtoType} source)`);
    pushLine("    {");
    pushLine(
      `        /// <summary>Maps the <c>${m.name}</c> DTO to its proto message.</summary>`,
    );
    pushLine(`        internal ${protoType} ToProto${m.name}()`);
    pushLine("        {");
    emitDtoToProtoBody(pushLine, m, protoType);
    pushLine("        }");
    pushLine("    }");
    pushLine("");

    // ---- proto → DTO sub-mapper ----
    pushLine(`    extension(${protoType} source)`);
    pushLine("    {");
    pushLine(
      `        /// <summary>Maps the <c>${m.name}</c> proto message to its DTO.</summary>`,
    );
    pushLine(`        internal ${dtoType} To${m.name}()`);
    pushLine("        {");
    emitProtoToDtoBody(pushLine, m, dtoType);
    pushLine("        }");
    pushLine("    }");
  }
}

/** Emit the DTO → proto sub-mapper body (object-initializer over the model's fields). */
function emitDtoToProtoBody(
  pushLine: (line: string) => void,
  model: NestedModel,
  protoType: string,
): void {
  if (model.fields.length === 0) {
    pushLine(`            return new ${protoType}();`);

    return;
  }

  pushLine(`            return new ${protoType}`);
  pushLine("            {");
  for (const f of model.fields) {
    const propName = toPascal(f.name);
    const assign = buildOutboundFieldAssign(f, "source");
    if (assign.kind === "collectionInit")
      pushLine(`                ${propName} = { ${assign.expr} },`);
    else pushLine(`                ${propName} = ${assign.expr},`);
  }

  pushLine("            };");
}

/** Emit the proto → DTO sub-mapper body (positional ctor over the model's fields). */
function emitProtoToDtoBody(
  pushLine: (line: string) => void,
  model: NestedModel,
  dtoType: string,
): void {
  if (model.fields.length === 0) {
    pushLine(`            return new ${dtoType}();`);

    return;
  }

  const args = model.fields.map((f) => buildInboundFieldArg(f, "source"));
  pushLine(`            return new ${dtoType}(${args.join(", ")});`);
}

// ---------------------------------------------------------------------------
// Internal per-field arms used INSIDE a sub-mapper body (self-contained — a
// nested model carries only scalar / bytes / nested / array fields; an enum
// inside a nested model is a separately-scoped concern not exercised here).
// ---------------------------------------------------------------------------

/** One outbound (DTO → proto) field inside a sub-mapper body. */
function buildOutboundFieldAssign(
  f: FieldInfo,
  source: string,
): OutboundAssign {
  const nested = buildDtoToProtoNested(f, source);
  if (nested !== undefined) return nested;

  const propName = toPascal(f.name);
  if (f.csType === "byte[]" || f.csType === "byte[]?")
    return {
      kind: "assign",
      expr: `global::Google.Protobuf.ByteString.CopyFrom(${source}.${propName})`,
    };

  return { kind: "assign", expr: `${source}.${propName}` };
}

/** One inbound (proto → DTO) ctor arg inside a sub-mapper body. */
function buildInboundFieldArg(f: FieldInfo, source: string): string {
  const nested = buildProtoToDtoNested(f, source);
  if (nested !== undefined) return nested;

  const propName = toPascal(f.name);
  if (f.csType === "byte[]" || f.csType === "byte[]?")
    return `${source}.${propName}.ToByteArray()`;

  return `${source}.${propName}`;
}
