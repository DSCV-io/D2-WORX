// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Direct-unit tests for the @d2Resilience predicate-emission dispatch in
// src/emitter.ts ($onEmit). V8 coverage of the predicate-emission block + the
// parseOpPredicate helper requires calling $onEmit with mocked
// @typespec/compiler so the branch executes against the TS source (not dist/).
//
// Covers:
//   - a predicate-bearing gRPC op → predicate C#/TS files + the sentinel emitted,
//     and the parsed ASTs threaded onto the GrpcClientOp (client gains the arm)
//   - a real-module gRPC op WITHOUT a predicate → no predicate files / sentinel
//     (the no-predicate skip + parseOpPredicate returns undefined)

import { describe, it, expect, vi, afterEach } from "vitest";
import type {
  EmitContext,
  Model,
  ModelProperty,
  Operation,
  Program,
  Scalar,
  Type,
} from "@typespec/compiler";
import type * as CompilerNs from "@typespec/compiler";
import {
  D2_SERVED_BY_KEY,
  D2_GRPC_METHOD_KEY,
  D2_COMMAND_KEY,
  D2_RESILIENCE_RETRY_WHEN_KEY,
  D2_RESILIENCE_FAIL_WHEN_KEY,
} from "@d2/typespec-decorators";

const directUnitOps: Operation[] = [];
const directUnitEmitted: Array<{ path: string; content: string }> = [];

vi.mock("@typespec/compiler", async (importOriginal) => {
  const original = await importOriginal<typeof CompilerNs>();
  return {
    ...original,
    navigateProgram(
      _prog: Program,
      visitor: { operation?: (op: Operation) => void },
    ) {
      for (const op of directUnitOps) visitor.operation?.(op);
    },
    emitFile: async (
      _prog: Program,
      opts: { path: string; content: string },
    ) => {
      directUnitEmitted.push({ path: opts.path, content: opts.content });
    },
    resolvePath: (...parts: string[]) => parts.join("/"),
  };
});

const { $onEmit } = await import("../src/emitter.js");

afterEach(() => {
  directUnitOps.length = 0;
  directUnitEmitted.length = 0;
  vi.restoreAllMocks();
});

// ---------------------------------------------------------------------------
// Mock-type builders (native TypeSpec shapes the emitter walks).
// ---------------------------------------------------------------------------

function str(): Scalar {
  return { kind: "Scalar", name: "string" } as unknown as Scalar;
}

function bool(): Scalar {
  return { kind: "Scalar", name: "boolean" } as unknown as Scalar;
}

function arrayOf(element: Type): Model {
  return {
    kind: "Model",
    name: "Array",
    properties: new Map<string, ModelProperty>(),
    indexer: { value: element },
  } as unknown as Model;
}

function model(name: string, props: Record<string, Type>): Model {
  const properties = new Map<string, ModelProperty>();
  for (const [k, v] of Object.entries(props))
    properties.set(k, { type: v, optional: false } as unknown as ModelProperty);

  return { kind: "Model", name, properties } as unknown as Model;
}

function wrappedOp(name: string, input: Model, output: Model): Operation {
  const inputProp = {
    type: input,
    optional: false,
  } as unknown as ModelProperty;
  const wrappedParams = {
    kind: "Model",
    name: "",
    properties: new Map<string, ModelProperty>([["input", inputProp]]),
  } as unknown as Model;
  return {
    name,
    parameters: wrappedParams,
    returnType: output,
    node: undefined,
  } as unknown as Operation;
}

function program(stateMapFn: (key: symbol) => Map<object, unknown>): Program {
  return {
    diagnostics: [],
    stateMap: stateMapFn,
    reportDiagnostic() {},
  } as unknown as Program;
}

function context(prog: Program, options: Record<string, string>): EmitContext {
  return {
    program: prog,
    emitterOutputDir: "/out",
    options,
  } as unknown as EmitContext;
}

const REAL_MODULE_OPTS = {
  "csharp-namespace": "D2.Test.Dto",
  "csharp-clients-namespace": "D2.Test.Clients",
  "csharp-app-namespace-base": "D2.Test.App.Application.Handlers",
  "proto-package": "d2.test.v1",
  "proto-csharp-namespace": "D2.Test.Protos.V1",
  "grpc-service-namespace": "D2.Test.Grpc",
};

function find(suffix: string): string | undefined {
  return directUnitEmitted.find((e) => e.path.endsWith(suffix))?.content;
}

// ---------------------------------------------------------------------------
// Test: predicate-bearing gRPC op → predicate files + sentinel emitted
// ---------------------------------------------------------------------------

describe("$onEmit_predicateEmitDirect_PredicateBearingOp", () => {
  it("emits the C#/TS predicates + the sentinel and threads the AST onto the client", async () => {
    const input = model("PlaceOrderFixtureInput", { customerId: str() });
    const output = model("PlaceOrderFixtureOutput", {
      orderCode: str(),
      itemStatuses: arrayOf(str()),
      partial: bool(),
    });
    const op = wrappedOp("placeOrderFixture", input, output);

    const prog = program((key) => {
      if (key === D2_SERVED_BY_KEY)
        return new Map<object, unknown>([[op, "PredicateFixtures"]]);
      if (key === D2_GRPC_METHOD_KEY)
        return new Map<object, unknown>([
          [
            op,
            {
              service: "PredicateFixturesOrders",
              method: "PlaceOrderFixture",
              streaming: "unary",
            },
          ],
        ]);
      if (key === D2_COMMAND_KEY) return new Map<object, unknown>([[op, true]]);
      if (key === D2_RESILIENCE_RETRY_WHEN_KEY)
        return new Map<object, unknown>([
          [
            op,
            'result.category == "infrastructure_unavailable" || result.data.itemStatuses.contains("PENDING") || result.data.partial == true',
          ],
        ]);
      if (key === D2_RESILIENCE_FAIL_WHEN_KEY)
        return new Map<object, unknown>([
          [
            op,
            'result.data.itemStatuses.count == 0 || result.errorCode == "VALIDATION_FAILED"',
          ],
        ]);

      return new Map<object, unknown>();
    });

    directUnitOps.push(op);
    await $onEmit(context(prog, REAL_MODULE_OPTS));

    const predCs = find("PlaceOrderFixtureResiliencePredicates.g.cs");
    expect(predCs).toBeDefined();
    expect(predCs).toContain(
      "internal static readonly Func<D2Result<PlaceOrderFixtureOutput?>, bool> SR_RetryWhen",
    );
    expect(predCs).toContain(
      'r.Category?.ToWire() == "infrastructure_unavailable"',
    );
    expect(predCs).toContain(
      '(r.Data?.ItemStatuses?.Contains("PENDING") ?? false)',
    );

    const predTs = find("place-order-fixture-resilience-predicates.g.ts");
    expect(predTs).toBeDefined();
    expect(predTs).toContain("export const placeOrderFixtureRetryWhen");

    const sentinel = find("D2GeneratedBusinessRetrySignal.g.cs");
    expect(sentinel).toBeDefined();
    expect(sentinel).toContain(
      "internal sealed class D2GeneratedBusinessRetrySignal : Exception",
    );

    // The AST threaded onto the client → the impl gains the sentinel throw arm.
    const impl =
      find("/PlaceOrderFixtureResiliencePredicatesGrpcClient.g.cs") ??
      directUnitEmitted.find(
        (e) =>
          e.path.endsWith("PredicateFixturesGrpcClient.g.cs") &&
          !e.path.endsWith("IPredicateFixturesGrpcClient.g.cs"),
      )?.content;
    expect(impl).toContain(
      "throw new D2GeneratedBusinessRetrySignal(businessResult.ToProto());",
    );
  });
});

// ---------------------------------------------------------------------------
// Test: real-module gRPC op WITHOUT a predicate → no predicate files / sentinel
// ---------------------------------------------------------------------------

describe("$onEmit_predicateEmitDirect_NoPredicateOp", () => {
  it("a no-predicate real-module gRPC op emits NO predicate files and NO sentinel", async () => {
    const input = model("PingInput", { id: str() });
    const output = model("PingOutput", { ok: bool() });
    const op = wrappedOp("ping", input, output);

    const prog = program((key) => {
      if (key === D2_SERVED_BY_KEY)
        return new Map<object, unknown>([[op, "PlainFixtures"]]);
      if (key === D2_GRPC_METHOD_KEY)
        return new Map<object, unknown>([
          [
            op,
            {
              service: "PlainFixturesPinger",
              method: "Ping",
              streaming: "unary",
            },
          ],
        ]);
      if (key === D2_COMMAND_KEY) return new Map<object, unknown>([[op, true]]);

      return new Map<object, unknown>();
    });

    directUnitOps.push(op);
    await $onEmit(context(prog, REAL_MODULE_OPTS));

    expect(find("PingResiliencePredicates.g.cs")).toBeUndefined();
    expect(find("D2GeneratedBusinessRetrySignal.g.cs")).toBeUndefined();
    // The client still emits (no predicate arm).
    expect(find("PlainFixturesGrpcClientsGenerated.g.cs")).toBeDefined();
  });
});
