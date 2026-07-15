// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Result-predicate emitter for the @d2Resilience `retryWhen` / `failWhen`
// custom predicates. Walks the parsed predicate AST (the shared
// `PredicateNode` family from @dcsv-io/d2-typespec-decorators) PLUS the emitter-local
// gen-time model crawl (`predicate-emit-walk.ts`) to emit TWO behaviorally
// identical predicate functions per op — one C#, one TypeScript — over the
// op's reconstructed business `D2Result<<Op>Output?>`, plus the emitter-owned
// retry sentinel the gRPC client throws to opt a business condition into retry.
//
// Mechanism: gen-time model-crawl, NOT runtime reflection. Each
// `result.data.<path>` segment is resolved against the real TypeSpec output
// model at GENERATION time; the emitter writes direct, type-safe, null-safe
// member access (`r.Data?.Items?.Any(...)` in C#, `r.data?.items?.some(...)`
// in TS) — no runtime field lookup, full C#↔TS parity, compile-time safety.
//
// Three artifacts (all GENERATED OUTPUT — byte-gated, never hand-edited):
//   1. <Op>ResiliencePredicates.g.cs  — internal static class with two
//      `internal static readonly Func<D2Result<<Op>Output?>, bool>` fields
//      SR_RetryWhen / SR_FailWhen (the §7.1 non-private static-readonly naming).
//   2. <op>-resilience-predicates.g.ts — `export const <op>RetryWhen` /
//      `<op>FailWhen` parity twins over the TS `D2Result<<Op>Output>` shape.
//   3. D2GeneratedBusinessRetrySignal.g.cs — the emitter-owned sentinel
//      exception the client throws when `retryWhen && !failWhen`; the generated
//      DI-ext's `IsTransient` lambda recognizes it so the REAL keyed pipeline
//      retries. ZERO DcsvIo.D2.Resilience change — the sentinel rides the
//      EXISTING RetryOptions.IsTransient extension point.
//
// Null-propagation: `r.Data` is always nullable (D2Result<T?>.Data is T?), so
// the first data-path segment always emits `?.`; once a `?.` chain begins every
// subsequent access stays `?.` (C# / TS both short-circuit a null chain to
// null/undefined → the bool comparison is false, not an exception). Quantifiers
// and `.count`/`.length` wrap in `?? false` / `?? 0` so the result is always a
// definite boolean / int. Empty-collection semantics: `.Any`/`.some` → false;
// `.All`/`.every` → true (vacuous truth). These identities are the cross-runtime
// parity contract the parity test pins.

import { buildBanner } from "./banner.js";
import { toPascal } from "./name-transforms.js";
import { resolveSegment } from "./predicate-emit-walk.js";
import type { EmittedFile } from "./csharp-dto-emitter.js";
import type { Model } from "@typespec/compiler";
import type {
  ArrayAccessorSegment,
  DataPathNode,
  EnvelopeAccessNode,
  LiteralNode,
  PredicateNode,
} from "@dcsv-io/d2-typespec-decorators";

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/** Inputs for emitting one op's predicate artifacts. */
export interface ResultPredicateEmitInput {
  /** lowerCamelCase op name (e.g. "placeOrder"). */
  readonly opName: string;
  /** Response DTO type name (e.g. "PlaceOrderOutput"). */
  readonly responseModelName: string;
  /** The op's output Model — the root of the gen-time data-path crawl. */
  readonly outputModel?: Model;
  /** C# namespace the predicate class + sentinel land in (the Clients/test ns). */
  readonly clientsNs: string;
  /** DTO C# namespace (where <Op>Output lives) — aliased when it differs from clientsNs. */
  readonly dtoCsharpNs: string;
  /** Source spec path for the banner. */
  readonly sourceSpec: string;
  /** The parsed `retryWhen` predicate, or undefined when absent. */
  readonly retryWhen?: PredicateNode;
  /** The parsed `failWhen` predicate, or undefined when absent. */
  readonly failWhen?: PredicateNode;
}

/**
 * Emit the C# + TS predicate files for one op. Returns the predicate file pair;
 * the shared sentinel file is emitted once per module via {@link emitBusinessRetrySignal}.
 * Pure — no I/O.
 *
 * @returns `[csFile, tsFile]`. Empty array when the op carries neither predicate
 *          (the caller emits nothing — the no-predicate path stays byte-identical).
 */
export function emitResultPredicates(
  input: ResultPredicateEmitInput,
): EmittedFile[] {
  if (input.retryWhen === undefined && input.failWhen === undefined) return [];

  const banner = buildBanner(input.sourceSpec);

  return [emitCsharpPredicates(input, banner), emitTsPredicates(input, banner)];
}

/**
 * Emit the emitter-owned retry sentinel (`D2GeneratedBusinessRetrySignal.g.cs`).
 * One per module/namespace — the client throws it when `retryWhen && !failWhen`,
 * and the generated DI-ext's `IsTransient` lambda recognizes it so the real
 * keyed pipeline retries. It carries the captured envelope + mapped data so the
 * client can restore the business result verbatim on retry-budget exhaust.
 * Internal sealed; carries NO logger (the payload is never logged). Pure.
 */
export function emitBusinessRetrySignal(
  clientsNs: string,
  sourceSpec: string,
): EmittedFile {
  const banner = buildBanner(sourceSpec);
  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${clientsNs};`);
  lines.push("");
  // global:: required under namespace DcsvIo.D2.* (CS0234 on bare D2.*).
  lines.push("using global::D2.Services.Protos.Common.V1;");
  lines.push("");
  lines.push("/// <summary>");
  lines.push(
    "/// Generated retry sentinel for @d2Resilience <c>retryWhen</c>. The gRPC client throws",
  );
  lines.push(
    "/// this from inside the resilience-pipeline closure when a business result matches the",
  );
  lines.push(
    "/// <c>retryWhen</c> predicate (and not <c>failWhen</c>); the generated DI extension's",
  );
  lines.push(
    "/// <c>IsTransient</c> lambda recognizes it so the keyed <c>ResilientPipeline</c> retries",
  );
  lines.push(
    "/// the call against ITS budget — opting one named business condition into retry without a",
  );
  lines.push(
    "/// resilience-library change (it rides the existing <c>RetryOptions.IsTransient</c> seam).",
  );
  lines.push(
    '/// Carries the captured <see cref="D2ResultProto"/> envelope so the client restores the',
  );
  lines.push(
    "/// business result verbatim on retry-budget exhaust. Never escapes the generated client;",
  );
  lines.push("/// never logs the payload.");
  lines.push("/// </summary>");
  lines.push(
    "internal sealed class D2GeneratedBusinessRetrySignal : Exception",
  );
  lines.push("{");
  lines.push(
    "    /// <summary>Initializes the sentinel with the captured business-result envelope.</summary>",
  );
  lines.push(
    '    /// <param name="envelope">The captured <see cref="D2ResultProto"/> envelope to restore on give-up.</param>',
  );
  lines.push(
    "    internal D2GeneratedBusinessRetrySignal(D2ResultProto envelope)",
  );
  lines.push("    {");
  lines.push("        Envelope = envelope;");
  lines.push("    }");
  lines.push("");
  lines.push(
    "    /// <summary>Gets the captured business-result envelope, restored verbatim on retry-budget exhaust.</summary>",
  );
  lines.push("    internal D2ResultProto Envelope { get; }");
  lines.push("}");
  lines.push("");

  return {
    fileName: "D2GeneratedBusinessRetrySignal.g.cs",
    content: lines.join("\n"),
  };
}

// ---------------------------------------------------------------------------
// C# emission
// ---------------------------------------------------------------------------

function emitCsharpPredicates(
  input: ResultPredicateEmitInput,
  banner: string,
): EmittedFile {
  const pascalOp = toPascal(input.opName);
  const className = `${pascalOp}ResiliencePredicates`;
  const outputAlias = input.responseModelName;

  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${input.clientsNs};`);
  lines.push("");
  // Alias the DTO output type (global:: rooted) when its namespace differs from
  // the predicate class namespace, mirroring the gRPC-client emitter convention.
  if (input.dtoCsharpNs !== input.clientsNs) {
    lines.push(
      `using ${outputAlias} = global::${input.dtoCsharpNs}.${outputAlias};`,
    );
  }

  // ErrorCategory.ToWire() (for result.category) lives in DcsvIo.D2.ErrorCodes.Category;
  // D2Result lives in DcsvIo.D2.Result. Both imported unconditionally — the class
  // signature always references D2Result, and category is a common predicate field.
  lines.push("using DcsvIo.D2.ErrorCodes.Category;");
  lines.push("using DcsvIo.D2.Result;");
  lines.push("");

  lines.push("/// <summary>");
  lines.push(
    `/// Generated @d2Resilience result-predicates for the <c>${pascalOp}</c> operation.`,
  );
  lines.push(
    "/// Each path segment was resolved against the operation output model at emitter generation",
  );
  lines.push(
    "/// time (no runtime reflection); null-conditional access short-circuits a null mid-path to",
  );
  lines.push(
    "/// a false comparison (never an exception). Empty-collection semantics: <c>.Any()</c> →",
  );
  lines.push("/// false; <c>.All()</c> → true (vacuous truth).");
  lines.push("/// </summary>");
  lines.push(`internal static class ${className}`);
  lines.push("{");

  const emitted: string[] = [];
  if (input.retryWhen !== undefined) {
    emitted.push(
      ...buildCsharpField(
        "SR_RetryWhen",
        "retryWhen",
        input.retryWhen,
        outputAlias,
        input.outputModel,
      ),
    );
  }

  if (input.failWhen !== undefined) {
    if (emitted.length > 0) emitted.push("");
    emitted.push(
      ...buildCsharpField(
        "SR_FailWhen",
        "failWhen",
        input.failWhen,
        outputAlias,
        input.outputModel,
      ),
    );
  }

  for (const line of emitted) lines.push(line);

  lines.push("}");
  lines.push("");

  return { fileName: `${className}.g.cs`, content: lines.join("\n") };
}

function buildCsharpField(
  fieldName: string,
  predicateKind: string,
  ast: PredicateNode,
  outputAlias: string,
  outputModel: Model | undefined,
): string[] {
  const lines: string[] = [];
  const expr = emitCsharpNode(ast, "data", outputModel, "r");

  lines.push(
    `    /// <summary>The <c>${predicateKind}</c> predicate over the reconstructed business result.</summary>`,
  );
  lines.push(
    `    internal static readonly Func<D2Result<${outputAlias}?>, bool> ${fieldName} =`,
  );
  lines.push(`        r => ${expr};`);
  return lines;
}

/**
 * Emit one predicate node as a C# boolean expression. `root` is the data-path
 * root ("data" at the top level → `r.Data`; an element-variable name inside a
 * quantifier sub-predicate → the bound lambda param). `model` is the model the
 * data paths resolve against (the output model at the top level; the element
 * model inside a quantifier). `receiver` is the C# receiver expression for the
 * top-level data path ("r" → `r.Data`); for a sub-predicate it is the bound
 * element variable directly.
 */
function emitCsharpNode(
  node: PredicateNode,
  root: string,
  model: Model | undefined,
  receiver: string,
): string {
  if (node.kind === "bool") {
    const left = emitCsharpNode(node.left, root, model, receiver);
    const right = emitCsharpNode(node.right, root, model, receiver);
    // Parenthesize each operand so mixed && / || precedence is explicit and
    // matches the parser's left-associative grouping exactly across languages.
    return `(${left} ${node.op} ${right})`;
  }

  if (node.kind === "booleanAccess") {
    return emitCsharpDataPathBoolean(node.access, model, receiver);
  }

  // ComparisonNode.
  if (node.access.kind === "envelope") {
    return emitCsharpComparison(
      emitCsharpEnvelope(node.access),
      node.op,
      node.rhs,
    );
  }

  const accessExpr = emitCsharpDataPathScalar(node.access, model, receiver);
  return emitCsharpComparison(accessExpr, node.op, node.rhs);
}

/** Map an envelope accessor to its C# expression. */
function emitCsharpEnvelope(node: EnvelopeAccessNode): string {
  switch (node.field) {
    case "success":
      return "r.Success";
    case "statusCode":
      return "(int)r.StatusCode";
    case "errorCode":
      return "r.ErrorCode";
    case "category":
      return "r.Category?.ToWire()";
  }
}

/**
 * Emit a data-path that terminates in a SCALAR (a field chain, or a `.count`
 * accessor) — the left side of a comparison. Threads null-conditional access:
 * the top-level `r.Data` is nullable, so the first segment is `?.`; once a `?.`
 * is emitted the chain stays `?.`. A `.count` terminal wraps in `(… ?? 0)`.
 */
function emitCsharpDataPathScalar(
  path: DataPathNode,
  model: Model | undefined,
  receiver: string,
): string {
  const built = buildCsharpChain(path, model, receiver);
  if (built.countTerminal) return `(${built.expr} ?? 0)`;

  return built.expr;
}

/**
 * Emit a data-path that terminates in a BOOLEAN array accessor (`any` / `all` /
 * `contains`) — a standalone predicate term. Wraps in `(… ?? false)` because the
 * null-conditional chain can short-circuit to null.
 */
function emitCsharpDataPathBoolean(
  path: DataPathNode,
  model: Model | undefined,
  receiver: string,
): string {
  const built = buildCsharpChain(path, model, receiver);
  return `(${built.expr} ?? false)`;
}

interface BuiltChain {
  readonly expr: string;
  /** True when the chain terminates in a `.Count` / `.length` accessor (int-valued). */
  readonly countTerminal: boolean;
}

/**
 * Build the C# member-access chain for a data path, resolving each segment
 * against the model graph. Returns the expression + whether the terminal is a
 * `.count` accessor (so the caller can wrap `?? 0`).
 */
function buildCsharpChain(
  path: DataPathNode,
  model: Model | undefined,
  receiver: string,
): BuiltChain {
  // Top-level root "data" → the receiver's `.Data` (always nullable, so the
  // chain is nullable from the first field). A sub-predicate root is the bound
  // element variable directly (non-nullable until a field marks otherwise).
  const topLevel = path.root === "data";
  let expr = topLevel ? `${receiver}.Data` : receiver;
  let nullable = topLevel; // r.Data is T? → the chain is nullable from the start.
  let currentModel: Model | undefined = model;
  let countTerminal = false;

  for (const seg of path.segments) {
    if (seg.kind === "field") {
      const res = resolveSegment(currentModel, seg.name);
      const dot = nullable ? "?." : ".";
      expr += `${dot}${res.csName}`;
      nullable = nullable || res.optional;
      // A collection field carries its element model forward (the receiver for a
      // following array accessor's quantifier sub-predicate); a nested model
      // field carries the field model forward for the next field segment.
      currentModel = res.isArray ? res.elementModel : res.fieldModel;
      continue;
    }

    const built = emitCsharpArrayAccessor(seg, expr, nullable, currentModel);
    expr = built.expr;
    countTerminal = built.countTerminal;
  }

  return { expr, countTerminal };
}

interface ArrayAccessorEmission {
  readonly expr: string;
  readonly countTerminal: boolean;
}

/**
 * Emit a C# array accessor onto an already-built collection receiver `expr`.
 * `count` → `?.Count` (int); `any` / `all` → LINQ `?.Any/.All(elem => sub)`;
 * `contains` → `?.Contains(lit)`. The `?.` is applied when the chain is nullable.
 */
function emitCsharpArrayAccessor(
  seg: ArrayAccessorSegment,
  receiverExpr: string,
  nullable: boolean,
  elementModel: Model | undefined,
): ArrayAccessorEmission {
  const dot = nullable ? "?." : ".";

  if (seg.accessor === "count")
    return { expr: `${receiverExpr}${dot}Count`, countTerminal: true };

  if (seg.accessor === "contains") {
    const lit = csharpLiteral(seg.literal!);
    return {
      expr: `${receiverExpr}${dot}Contains(${lit})`,
      countTerminal: false,
    };
  }

  // any / all — LINQ quantifier over the element model. The bound element var is
  // the receiver of the sub-predicate; its data paths resolve against elementModel.
  const linq = seg.accessor === "any" ? "Any" : "All";
  const elemVar = seg.elemVar!;
  const sub = emitCsharpNode(seg.subPredicate!, elemVar, elementModel, elemVar);
  return {
    expr: `${receiverExpr}${dot}${linq}(${elemVar} => ${sub})`,
    countTerminal: false,
  };
}

/** Emit a C# comparison (`== / != / in`) with null-safe literal semantics. */
function emitCsharpComparison(
  accessExpr: string,
  op: "==" | "!=" | "in",
  rhs: LiteralNode | readonly LiteralNode[],
): string {
  if (op === "in") {
    const lits = (rhs as readonly LiteralNode[]).map(csharpLiteral);
    // new[] { … }.Contains(x) — null-safe on a nullable LHS (a null x simply is
    // not a member → false), and uniform across scalar / nullable receivers.
    return `new[] { ${lits.join(", ")} }.Contains(${accessExpr})`;
  }

  const lit = csharpLiteral(rhs as LiteralNode);
  return `${accessExpr} ${op} ${lit}`;
}

/** Render a literal node as a C# literal. */
function csharpLiteral(lit: LiteralNode): string {
  if (lit.kind === "string") {
    const escaped = lit.value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
    return `"${escaped}"`;
  }

  if (lit.kind === "bool") return lit.value === "true" ? "true" : "false";

  return lit.value; // int — already a numeric token.
}

// ---------------------------------------------------------------------------
// TS emission
// ---------------------------------------------------------------------------

function emitTsPredicates(
  input: ResultPredicateEmitInput,
  banner: string,
): EmittedFile {
  const lines: string[] = [];

  lines.push(banner.trimEnd());
  lines.push("");
  lines.push("/* eslint-disable */");
  lines.push("// @ts-nocheck");
  lines.push(
    "// Generated @d2Resilience result-predicates (TypeScript parity twin). The C# predicates",
  );
  lines.push(
    `// (${toPascal(input.opName)}ResiliencePredicates.g.cs) and these MUST be behaviorally identical for the`,
  );
  lines.push(
    "// same business result — proven by the cross-runtime predicate-parity test. Null-conditional",
  );
  lines.push(
    "// access short-circuits a null/undefined mid-path to a false comparison (never a throw);",
  );
  lines.push(
    "// quantifiers + .length wrap in `?? false` / `?? 0`. Empty-collection semantics: .some() →",
  );
  lines.push("// false; .every() → true (vacuous truth).");
  lines.push("");

  if (input.retryWhen !== undefined) {
    lines.push(
      ...buildTsConst(
        `${input.opName}RetryWhen`,
        input.retryWhen,
        input.responseModelName,
        input.outputModel,
      ),
    );
  }

  if (input.failWhen !== undefined) {
    if (input.retryWhen !== undefined) lines.push("");
    lines.push(
      ...buildTsConst(
        `${input.opName}FailWhen`,
        input.failWhen,
        input.responseModelName,
        input.outputModel,
      ),
    );
  }

  lines.push("");

  return {
    fileName: `${kebab(input.opName)}-resilience-predicates.g.ts`,
    content: lines.join("\n"),
  };
}

function buildTsConst(
  constName: string,
  ast: PredicateNode,
  responseModelName: string,
  outputModel: Model | undefined,
): string[] {
  const expr = emitTsNode(ast, "data", outputModel, "r");
  return [
    `export const ${constName} = (r: D2Result<${responseModelName}>): boolean =>`,
    `    ${expr};`,
  ];
}

function emitTsNode(
  node: PredicateNode,
  root: string,
  model: Model | undefined,
  receiver: string,
): string {
  if (node.kind === "bool") {
    const left = emitTsNode(node.left, root, model, receiver);
    const right = emitTsNode(node.right, root, model, receiver);
    return `(${left} ${node.op} ${right})`;
  }

  if (node.kind === "booleanAccess") {
    return emitTsDataPathBoolean(node.access, model, receiver);
  }

  if (node.access.kind === "envelope") {
    return emitTsComparison(emitTsEnvelope(node.access), node.op, node.rhs);
  }

  const accessExpr = emitTsDataPathScalar(node.access, model, receiver);
  return emitTsComparison(accessExpr, node.op, node.rhs);
}

function emitTsEnvelope(node: EnvelopeAccessNode): string {
  switch (node.field) {
    case "success":
      return "r.success";
    case "statusCode":
      return "r.statusCode";
    case "errorCode":
      return "r.errorCode";
    case "category":
      return "r.category";
  }
}

function emitTsDataPathScalar(
  path: DataPathNode,
  model: Model | undefined,
  receiver: string,
): string {
  const built = buildTsChain(path, model, receiver);
  if (built.countTerminal) return `(${built.expr} ?? 0)`;

  return built.expr;
}

function emitTsDataPathBoolean(
  path: DataPathNode,
  model: Model | undefined,
  receiver: string,
): string {
  const built = buildTsChain(path, model, receiver);
  return `(${built.expr} ?? false)`;
}

function buildTsChain(
  path: DataPathNode,
  model: Model | undefined,
  receiver: string,
): BuiltChain {
  const topLevel = path.root === "data";
  let expr = topLevel ? `${receiver}.data` : receiver;
  let nullable = topLevel; // r.data is T | undefined.
  let currentModel: Model | undefined = model;
  let countTerminal = false;

  for (const seg of path.segments) {
    if (seg.kind === "field") {
      const res = resolveSegment(currentModel, seg.name);
      const dot = nullable ? "?." : ".";
      expr += `${dot}${res.tsName}`;
      nullable = nullable || res.optional;
      currentModel = res.isArray ? res.elementModel : res.fieldModel;
      continue;
    }

    const built = emitTsArrayAccessor(seg, expr, nullable, currentModel);
    expr = built.expr;
    countTerminal = built.countTerminal;
  }

  return { expr, countTerminal };
}

function emitTsArrayAccessor(
  seg: ArrayAccessorSegment,
  receiverExpr: string,
  nullable: boolean,
  elementModel: Model | undefined,
): ArrayAccessorEmission {
  const dot = nullable ? "?." : ".";

  // NB: a boolean array accessor (`some` / `every` / `includes`) is NOT self-
  // wrapped in `?? false` here — it always terminates a data path and reaches
  // the caller as a `booleanAccess` node, which applies the single outer
  // `(… ?? false)` wrap (mirrors the C# side). `.length` (count) is the int
  // terminal wrapped `(… ?? 0)` by the scalar caller.
  if (seg.accessor === "count")
    return { expr: `${receiverExpr}${dot}length`, countTerminal: true };

  if (seg.accessor === "contains") {
    const lit = tsLiteral(seg.literal!);
    return {
      expr: `${receiverExpr}${dot}includes(${lit})`,
      countTerminal: false,
    };
  }

  const fn = seg.accessor === "any" ? "some" : "every";
  const elemVar = seg.elemVar!;
  const sub = emitTsNode(seg.subPredicate!, elemVar, elementModel, elemVar);
  return {
    expr: `${receiverExpr}${dot}${fn}((${elemVar}) => ${sub})`,
    countTerminal: false,
  };
}

function emitTsComparison(
  accessExpr: string,
  op: "==" | "!=" | "in",
  rhs: LiteralNode | readonly LiteralNode[],
): string {
  if (op === "in") {
    const lits = (rhs as readonly LiteralNode[]).map(tsLiteral);
    return `[${lits.join(", ")}].includes(${accessExpr})`;
  }

  const lit = tsLiteral(rhs as LiteralNode);
  const tsOp = op === "==" ? "===" : "!==";
  return `${accessExpr} ${tsOp} ${lit}`;
}

function tsLiteral(lit: LiteralNode): string {
  if (lit.kind === "string") {
    const escaped = lit.value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
    return `"${escaped}"`;
  }

  if (lit.kind === "bool") return lit.value === "true" ? "true" : "false";

  return lit.value;
}

// ---------------------------------------------------------------------------
// Utilities
// ---------------------------------------------------------------------------

/** Convert a lowerCamelCase op name to kebab-case for the TS file name. */
function kebab(s: string): string {
  return s.replace(/([a-z0-9])([A-Z])/g, "$1-$2").toLowerCase();
}
