// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Unit + integration tests for the @d2Resilience result-predicate emitter
// (result-predicate-emitter.ts + predicate-emit-walk.ts). Compiles inline .tsp
// through the TypeSpec test-host to obtain REAL output models, then drives the
// emitter directly over hand-parsed predicate ASTs (parseResultPredicate) and
// asserts the emitted C# / TS access strings + the sentinel. Covers every
// accessor / construct in the grammar §4 emission table — including the array-
// of-MODEL quantifier (`items.any(i => i.status == "…")`) and nested-optional
// path that the committed gRPC fixture (a flat-mappable shape) does not carry,
// so the rich emission is fully exercised here without a compiled gRPC client.

import { describe, it, expect, beforeAll } from "vitest";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";
import type { Model, Program } from "@typespec/compiler";
import { parseResultPredicate } from "@dcsv-io/d2-typespec-decorators";
import type { PredicateNode } from "@dcsv-io/d2-typespec-decorators";
import {
  emitResultPredicates,
  emitBusinessRetrySignal,
} from "../src/lib/result-predicate-emitter.js";
import { resolveSegment } from "../src/lib/predicate-emit-walk.js";

const D2DecoratorTestLibrary = createTestLibrary({
  name: "@dcsv-io/d2-typespec-decorators",
  packageRoot: await findTestPackageRoot(
    new URL(
      "../node_modules/@dcsv-io/d2-typespec-decorators/package.json",
      import.meta.url,
    ).href,
  ),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

const D2EmitterTestLibrary = createTestLibrary({
  name: "@dcsv-io/d2-typespec-emitters",
  packageRoot: await findTestPackageRoot(import.meta.url),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

const NS = "D2.Test.Generated";
const DTO_NS = "D2.Test.Generated";
const SPEC = "public/contracts/typespec/fixtures/test.tsp";

// ---------------------------------------------------------------------------
// Helper: compile inline .tsp and return the program + the named output model.
// ---------------------------------------------------------------------------

async function compileModel(
  host: Awaited<ReturnType<typeof createTestHost>>,
  body: string,
  modelName: string,
): Promise<{ program: Program; model: Model }> {
  host.addTypeSpecFile(
    "main.tsp",
    `
    import "@dcsv-io/d2-typespec-decorators";
    using D2;
    namespace D2.Test;
    ${body}
    `,
  );
  await host.compile("main.tsp", { outputDir: "testing:/out" });
  const program = host.program;
  const globalNs = program.getGlobalNamespaceType();
  const testNs = globalNs.namespaces.get("D2")?.namespaces.get("Test");
  const model = testNs?.models.get(modelName);
  if (model === undefined) throw new Error(`model ${modelName} not found`);

  return { program, model };
}

/** Parse a predicate string into its AST (the decorator validator already gated this; ok here). */
function ast(expr: string): PredicateNode {
  const parsed = parseResultPredicate(expr);
  if (!parsed.ok) throw new Error(`fixture predicate failed to parse: ${expr}`);

  return parsed.root;
}

/** Emit predicates for a model + retryWhen/failWhen and return the two file contents. */
function emit(
  model: Model | undefined,
  responseModelName: string,
  retryWhen: PredicateNode | undefined,
  failWhen: PredicateNode | undefined,
): { cs: string; ts: string } {
  const files = emitResultPredicates({
    opName: "doThing",
    responseModelName,
    outputModel: model,
    clientsNs: NS,
    dtoCsharpNs: DTO_NS,
    sourceSpec: SPEC,
    retryWhen,
    failWhen,
  });
  const cs = files.find((f) => f.fileName.endsWith(".g.cs"))!.content;
  const ts = files.find((f) => f.fileName.endsWith(".g.ts"))!.content;
  return { cs, ts };
}

// ---------------------------------------------------------------------------
// Envelope accessors
// ---------------------------------------------------------------------------

describe("resultPredicateEmitter_EnvelopeAccessors", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;
  let model: Model;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
    ({ model } = await compileModel(
      host,
      `model Thing { code: string; }`,
      "Thing",
    ));
  });

  it("result.success → r.Success / r.success", () => {
    const { cs, ts } = emit(
      model,
      "Thing",
      ast("result.success == true"),
      undefined,
    );
    expect(cs).toContain("r.Success == true");
    expect(ts).toContain("r.success === true");
  });

  it("result.statusCode → (int)r.StatusCode / r.statusCode", () => {
    const { cs, ts } = emit(
      model,
      "Thing",
      ast("result.statusCode == 503"),
      undefined,
    );
    expect(cs).toContain("(int)r.StatusCode == 503");
    expect(ts).toContain("r.statusCode === 503");
  });

  it("result.errorCode → r.ErrorCode / r.errorCode (string compare)", () => {
    const { cs, ts } = emit(
      model,
      "Thing",
      ast('result.errorCode == "X"'),
      undefined,
    );
    expect(cs).toContain('r.ErrorCode == "X"');
    expect(ts).toContain('r.errorCode === "X"');
  });

  it("result.category → r.Category?.ToWire() / r.category", () => {
    const { cs, ts } = emit(
      model,
      "Thing",
      ast('result.category == "conflict"'),
      undefined,
    );
    expect(cs).toContain('r.Category?.ToWire() == "conflict"');
    expect(ts).toContain('r.category === "conflict"');
  });

  it("!= operator maps to != / !==", () => {
    const { cs, ts } = emit(
      model,
      "Thing",
      ast('result.errorCode != "X"'),
      undefined,
    );
    expect(cs).toContain('r.ErrorCode != "X"');
    expect(ts).toContain('r.errorCode !== "X"');
  });

  it("in (...) → new[]{...}.Contains / [...].includes", () => {
    const { cs, ts } = emit(
      model,
      "Thing",
      ast('result.errorCode in ("A", "B")'),
      undefined,
    );
    expect(cs).toContain('new[] { "A", "B" }.Contains(r.ErrorCode)');
    expect(ts).toContain('["A", "B"].includes(r.errorCode)');
  });

  it("&& / || boolean composition with explicit grouping", () => {
    const { cs, ts } = emit(
      model,
      "Thing",
      ast('result.success == true && result.errorCode == "X"'),
      undefined,
    );
    expect(cs).toContain('(r.Success == true && r.ErrorCode == "X")');
    expect(ts).toContain('(r.success === true && r.errorCode === "X")');
  });

  it("bool literal false emits false", () => {
    const { cs, ts } = emit(
      model,
      "Thing",
      ast("result.success == false"),
      undefined,
    );
    expect(cs).toContain("r.Success == false");
    expect(ts).toContain("r.success === false");
  });
});

// ---------------------------------------------------------------------------
// Data-path accessors (flat + nested + null-propagation)
// ---------------------------------------------------------------------------

describe("resultPredicateEmitter_DataPathAccessors", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("flat data field → r.Data?.<Pascal> / r.data?.<camel>", async () => {
    const { model } = await compileModel(
      host,
      `model Flat { partial: boolean; }`,
      "Flat",
    );
    const { cs, ts } = emit(
      model,
      "Flat",
      ast("result.data.partial == true"),
      undefined,
    );
    expect(cs).toContain("r.Data?.Partial == true");
    expect(ts).toContain("r.data?.partial === true");
  });

  it("nested REQUIRED path → ?. at every segment (Data is nullable-rooted)", async () => {
    const { model } = await compileModel(
      host,
      `model Inner { tier: string; } model Outer { inner: Inner; }`,
      "Outer",
    );
    const { cs, ts } = emit(
      model,
      "Outer",
      ast('result.data.inner.tier == "TRIAL"'),
      undefined,
    );
    expect(cs).toContain('r.Data?.Inner?.Tier == "TRIAL"');
    expect(ts).toContain('r.data?.inner?.tier === "TRIAL"');
  });

  it("nested OPTIONAL path resolves the same (?. chain) — deep-nesting proof", async () => {
    const { model } = await compileModel(
      host,
      `model Cust { tier: string; } model Order { customer?: Cust; } model Out { order?: Order; }`,
      "Out",
    );
    const { cs, ts } = emit(
      model,
      "Out",
      ast('result.data.order.customer.tier == "TRIAL"'),
      undefined,
    );
    expect(cs).toContain('r.Data?.Order?.Customer?.Tier == "TRIAL"');
    expect(ts).toContain('r.data?.order?.customer?.tier === "TRIAL"');
  });
});

// ---------------------------------------------------------------------------
// Array accessors (count / any / all / contains)
// ---------------------------------------------------------------------------

describe("resultPredicateEmitter_ArrayAccessors", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("count → (r.Data?.X?.Count ?? 0) / (r.data?.x?.length ?? 0)", async () => {
    const { model } = await compileModel(
      host,
      `model Out { items: string[]; }`,
      "Out",
    );
    const { cs, ts } = emit(
      model,
      "Out",
      undefined,
      ast("result.data.items.count == 0"),
    );
    expect(cs).toContain("(r.Data?.Items?.Count ?? 0) == 0");
    expect(ts).toContain("(r.data?.items?.length ?? 0) === 0");
  });

  it("contains(scalar) → (r.Data?.X?.Contains(lit) ?? false) / includes", async () => {
    const { model } = await compileModel(
      host,
      `model Out { codes: string[]; }`,
      "Out",
    );
    const { cs, ts } = emit(
      model,
      "Out",
      ast('result.data.codes.contains("RETRY")'),
      undefined,
    );
    expect(cs).toContain('(r.Data?.Codes?.Contains("RETRY") ?? false)');
    expect(ts).toContain('(r.data?.codes?.includes("RETRY") ?? false)');
  });

  it("any(i => i.field) over array-of-MODEL → LINQ Any / .some with element sub-predicate", async () => {
    const { model } = await compileModel(
      host,
      `model Item { status: string; } model Out { items: Item[]; }`,
      "Out",
    );
    const { cs, ts } = emit(
      model,
      "Out",
      ast('result.data.items.any(i => i.status == "PENDING")'),
      undefined,
    );
    expect(cs).toContain(
      '(r.Data?.Items?.Any(i => i.Status == "PENDING") ?? false)',
    );
    expect(ts).toContain(
      '(r.data?.items?.some((i) => i.status === "PENDING") ?? false)',
    );
  });

  it("all(b => ...) over array-of-MODEL → LINQ All / .every (vacuous-true on empty)", async () => {
    const { model } = await compileModel(
      host,
      `model Batch { ok: boolean; } model Out { batches: Batch[]; }`,
      "Out",
    );
    const { cs, ts } = emit(
      model,
      "Out",
      ast("result.data.batches.all(b => b.ok == true)"),
      undefined,
    );
    expect(cs).toContain("(r.Data?.Batches?.All(b => b.Ok == true) ?? false)");
    expect(ts).toContain(
      "(r.data?.batches?.every((b) => b.ok === true) ?? false)",
    );
  });

  it("nested array accessor on a NON-nullable element uses '.' (not '?.')", async () => {
    // b is the bound element (non-nullable); b.tags is a non-optional collection,
    // so the inner `contains` accessor is reached on a non-nullable receiver → '.'.
    const { model } = await compileModel(
      host,
      `model Batch { tags: string[]; } model Out { batches: Batch[]; }`,
      "Out",
    );
    const { cs, ts } = emit(
      model,
      "Out",
      ast('result.data.batches.any(b => b.tags.contains("X"))'),
      undefined,
    );
    // Inner element-rooted array accessor: b.Tags.Contains (no '?.' on the element chain).
    expect(cs).toContain('b.Tags.Contains("X")');
    expect(ts).toContain('b.tags.includes("X")');
  });

  it("nested array .count on a NON-nullable element uses '.Count' / '.length' (not '?.')", async () => {
    const { model } = await compileModel(
      host,
      `model Batch { tags: string[]; } model Out { batches: Batch[]; }`,
      "Out",
    );
    const { cs, ts } = emit(
      model,
      "Out",
      ast("result.data.batches.any(b => b.tags.count == 3)"),
      undefined,
    );
    expect(cs).toContain("(b.Tags.Count ?? 0) == 3");
    expect(ts).toContain("(b.tags.length ?? 0) === 3");
  });
});

// ---------------------------------------------------------------------------
// Field naming + sentinel + class shape
// ---------------------------------------------------------------------------

describe("resultPredicateEmitter_FieldShapeAndSentinel", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;
  let model: Model;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
    ({ model } = await compileModel(
      host,
      `model Thing { code: string; }`,
      "Thing",
    ));
  });

  it("emits SR_RetryWhen + SR_FailWhen internal static readonly Func fields", () => {
    const { cs } = emit(
      model,
      "Thing",
      ast("result.success == true"),
      ast('result.errorCode == "X"'),
    );
    expect(cs).toContain(
      "internal static readonly Func<D2Result<Thing?>, bool> SR_RetryWhen =",
    );
    expect(cs).toContain(
      "internal static readonly Func<D2Result<Thing?>, bool> SR_FailWhen =",
    );
    expect(cs).toContain("internal static class DoThingResiliencePredicates");
  });

  it("emits TS export const <op>RetryWhen / <op>FailWhen", () => {
    const { ts } = emit(
      model,
      "Thing",
      ast("result.success == true"),
      ast('result.errorCode == "X"'),
    );
    expect(ts).toContain(
      "export const doThingRetryWhen = (r: D2Result<Thing>): boolean =>",
    );
    expect(ts).toContain(
      "export const doThingFailWhen = (r: D2Result<Thing>): boolean =>",
    );
  });

  it("only retryWhen present → only SR_RetryWhen emitted", () => {
    const { cs, ts } = emit(
      model,
      "Thing",
      ast("result.success == true"),
      undefined,
    );
    expect(cs).toContain("SR_RetryWhen");
    expect(cs).not.toContain("SR_FailWhen");
    expect(ts).toContain("doThingRetryWhen");
    expect(ts).not.toContain("doThingFailWhen");
  });

  it("only failWhen present → only SR_FailWhen emitted", () => {
    const { cs, ts } = emit(
      model,
      "Thing",
      undefined,
      ast("result.success == false"),
    );
    expect(cs).toContain("SR_FailWhen");
    expect(cs).not.toContain("SR_RetryWhen");
    expect(ts).toContain("doThingFailWhen");
    expect(ts).not.toContain("doThingRetryWhen");
  });

  it("no predicate → emits nothing (the no-predicate path stays byte-identical)", () => {
    const files = emitResultPredicates({
      opName: "doThing",
      responseModelName: "Thing",
      outputModel: model,
      clientsNs: NS,
      dtoCsharpNs: DTO_NS,
      sourceSpec: SPEC,
      retryWhen: undefined,
      failWhen: undefined,
    });
    expect(files).toHaveLength(0);
  });

  it("outputModel:undefined — envelope-only predicates emit without data-path access", () => {
    // When the op's output model is not available (e.g. a void-result op or
    // an envelope-only predicate), emitResultPredicates must still produce two
    // well-formed files. Envelope fields (result.success, result.errorCode,
    // result.statusCode, result.category) are resolved without the model;
    // no data-path segment is accessed.
    const files = emitResultPredicates({
      opName: "doThing",
      responseModelName: "Thing",
      outputModel: undefined,
      clientsNs: NS,
      dtoCsharpNs: DTO_NS,
      sourceSpec: SPEC,
      retryWhen: ast("result.success == false"),
      failWhen: undefined,
    });

    expect(files).toHaveLength(2);
    const cs = files.find((f) => f.fileName.endsWith(".g.cs"))!.content;
    const ts = files.find((f) => f.fileName.endsWith(".g.ts"))!.content;

    // C# predicate references the envelope field (r.Success) without r.Data?.
    expect(cs).toContain("r.Success == false");
    expect(cs).not.toContain("r.Data");

    // TS predicate references the envelope field (r.success) without r.data?.
    expect(ts).toContain("r.success === false");
    expect(ts).not.toContain("r.data");
  });

  it("DTO namespace alias emitted only when it differs from the predicate ns", () => {
    const files = emitResultPredicates({
      opName: "doThing",
      responseModelName: "Thing",
      outputModel: model,
      clientsNs: NS,
      dtoCsharpNs: "D2.Other.Dto",
      sourceSpec: SPEC,
      retryWhen: ast("result.success == true"),
      failWhen: undefined,
    });
    const cs = files.find((f) => f.fileName.endsWith(".g.cs"))!.content;
    expect(cs).toContain("using Thing = global::D2.Other.Dto.Thing;");
  });

  it("sentinel — internal sealed Exception carrying the captured envelope", () => {
    const sentinel = emitBusinessRetrySignal(NS, SPEC);
    expect(sentinel.fileName).toBe("D2GeneratedBusinessRetrySignal.g.cs");
    expect(sentinel.content).toContain(
      "internal sealed class D2GeneratedBusinessRetrySignal : Exception",
    );
    expect(sentinel.content).toContain(
      "internal D2GeneratedBusinessRetrySignal(D2ResultProto envelope)",
    );
    expect(sentinel.content).toContain(
      "internal D2ResultProto Envelope { get; }",
    );
    // PII: the sentinel must NOT log; no [LoggerMessage], no ILogger.
    expect(sentinel.content).not.toContain("LoggerMessage");
    expect(sentinel.content).not.toContain("ILogger");
  });

  it('emitted C# / TS carry no `tk("TK.…")` path literal (§26.7)', () => {
    const { cs, ts } = emit(
      model,
      "Thing",
      ast('result.category == "conflict"'),
      undefined,
    );
    expect(cs).not.toMatch(/tk\(/);
    expect(ts).not.toMatch(/\btk\(/);
  });
});

// ---------------------------------------------------------------------------
// predicate-emit-walk: direct segment resolution (E2 unit)
// ---------------------------------------------------------------------------

describe("predicateEmitWalk_ResolveSegment", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("resolves a scalar field → csName/tsName, not array, not model", async () => {
    const { model } = await compileModel(
      host,
      `model Out { orderCode: string; }`,
      "Out",
    );
    const res = resolveSegment(model, "orderCode");
    expect(res.csName).toBe("OrderCode");
    expect(res.tsName).toBe("orderCode");
    expect(res.optional).toBe(false);
    expect(res.isArray).toBe(false);
    expect(res.elementModel).toBeUndefined();
    expect(res.fieldModel).toBeUndefined();
  });

  it("resolves an optional field → optional true", async () => {
    const { model } = await compileModel(
      host,
      `model Inner { x: string; } model Out { inner?: Inner; }`,
      "Out",
    );
    const res = resolveSegment(model, "inner");
    expect(res.optional).toBe(true);
    expect(res.fieldModel?.name).toBe("Inner");
  });

  it("resolves a scalar array → isArray true, no element model", async () => {
    const { model } = await compileModel(
      host,
      `model Out { codes: string[]; }`,
      "Out",
    );
    const res = resolveSegment(model, "codes");
    expect(res.isArray).toBe(true);
    expect(res.elementModel).toBeUndefined();
  });

  it("resolves an array-of-model → isArray true + element model carried", async () => {
    const { model } = await compileModel(
      host,
      `model Item { status: string; } model Out { items: Item[]; }`,
      "Out",
    );
    const res = resolveSegment(model, "items");
    expect(res.isArray).toBe(true);
    expect(res.elementModel?.name).toBe("Item");
  });

  it("throws loud when the model is undefined (validator is the gate)", () => {
    expect(() => resolveSegment(undefined, "x")).toThrow(
      /prior segment did not resolve to a model/,
    );
  });

  it("throws loud when the field is absent (validator is the gate)", async () => {
    const { model } = await compileModel(
      host,
      `model Out { a: string; }`,
      "Out",
    );
    expect(() => resolveSegment(model, "nope")).toThrow(
      /is not a property of model/,
    );
  });
});
