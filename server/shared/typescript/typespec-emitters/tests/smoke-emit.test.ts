// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Tests for the $onEmit entry point.
//
// Two flavors per §1.28 (direct-unit = V8 src/ coverage; integration = pipeline proof):
//
// Direct-unit: call $onEmit with a hand-built mock EmitContext (mock
//   program.stateMap + navigateProgram stand-in) and assert emitGeneratedFile
//   is invoked with the expected manifest. Gives V8 credit to src/emitter.ts
//   so 100% src/ coverage is not reliant on dist/.
//
// Integration: compile a tiny inline .tsp through the TypeSpec test-host
//   with the emitter in the emit list; assert the operations-manifest.json is
//   written and its contents match the fixture. Proves the real tsp compile →
//   $onEmit → emitFile pipeline works end-to-end.

import { describe, it, expect, vi, beforeAll, afterEach } from "vitest";
import type { EmitContext, Model, ModelProperty, Operation, Program, Scalar } from "@typespec/compiler";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";
import {
  D2_SERVED_BY_KEY,
  D2_GRPC_METHOD_KEY,
  D2_IN_PROCESS_KEY,
  D2_COMMAND_KEY,
  D2_QUERY_KEY,
  D2_INTERNAL_KEY,
} from "@d2/typespec-decorators";
import type { OperationsManifest } from "../src/emitter.js";

// ---------------------------------------------------------------------------
// Direct-unit test: $onEmit with a mock EmitContext
//
// vi.mock is hoisted by Vitest to the top of the module before any imports,
// so the mock factory CANNOT reference variables from inside test bodies.
// We use module-level mutable state shared between the factory and the tests.
// ---------------------------------------------------------------------------

// Module-level mock state — cleared before each test that needs it.
const directUnitOps: Operation[] = [];
const directUnitEmitted: Array<{ path: string; content: string }> = [];
let mockServedByMap: Map<object, unknown>;
let mockGrpcMap: Map<object, unknown>;
let mockInProcessMap: Map<object, unknown>;

vi.mock("@typespec/compiler", async (importOriginal) => {
  const original = await importOriginal<typeof import("@typespec/compiler")>();
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

// Import AFTER the mock registrations so the module under test uses mocked deps.
const { $onEmit } = await import("../src/emitter.js");

afterEach(() => {
  // Reset shared state after each test to prevent cross-test contamination.
  directUnitOps.length = 0;
  directUnitEmitted.length = 0;
  vi.restoreAllMocks();
});

describe("$onEmit_directUnit_SmokeMockContext", () => {
  it("emits operations-manifest.json with the expected manifest shape", async () => {
    // Build fixed mock operations for this test.
    const opBare = { name: "getStatus" } as unknown as Operation;
    const opServedBy = { name: "createOrder" } as unknown as Operation;
    const opGrpc = { name: "pushEvent" } as unknown as Operation;

    // Populate the module-level arrays (the vi.mock factory reads these).
    directUnitOps.push(opBare, opServedBy, opGrpc);

    // State maps for the three decorator keys.
    mockServedByMap = new Map<object, unknown>([[opServedBy, "Edge"]]);
    mockGrpcMap = new Map<object, unknown>([
      [opGrpc, { service: "Push", method: "PushEvent", streaming: "unary" }],
    ]);
    mockInProcessMap = new Map<object, unknown>([[opServedBy, true]]);

    // Build minimal mock program that returns the correct state map per key.
    const mockProgram = {
      stateMap(key: symbol): Map<object, unknown> {
        if (key === D2_SERVED_BY_KEY) return mockServedByMap;
        if (key === D2_GRPC_METHOD_KEY) return mockGrpcMap;
        if (key === D2_IN_PROCESS_KEY) return mockInProcessMap;
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    expect(directUnitEmitted).toHaveLength(1);
    const written = directUnitEmitted[0]!;
    expect(written.path).toContain("operations-manifest.json");

    const manifest = JSON.parse(written.content) as OperationsManifest;
    expect(manifest.emitter).toBe("@d2/typespec-emitters");
    expect(manifest.operationCount).toBe(3);

    // Bare op — no decorators applied.
    const bare = manifest.operations.find((o) => o.name === "getStatus");
    expect(bare).toBeDefined();
    expect(bare!.servedBy).toBeUndefined();
    expect(bare!.hasGrpc).toBe(false);
    expect(bare!.inProcess).toBe(false);

    // Op with @d2ServedBy("Edge") + @d2InProcess.
    const served = manifest.operations.find((o) => o.name === "createOrder");
    expect(served).toBeDefined();
    expect(served!.servedBy).toBe("Edge");
    expect(served!.hasGrpc).toBe(false);
    expect(served!.inProcess).toBe(true);

    // Op with @d2GrpcMethod.
    const grpc = manifest.operations.find((o) => o.name === "pushEvent");
    expect(grpc).toBeDefined();
    expect(grpc!.servedBy).toBeUndefined();
    expect(grpc!.hasGrpc).toBe(true);
    expect(grpc!.inProcess).toBe(false);
  });
});

describe("$onEmit_directUnit_DtoPairEmission", () => {
  it("emits manifest + C# + TS DTO files when op has concrete input and output models", async () => {
    // Build a model stub with a scalar string property.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const kidProp = { type: stringScalar, optional: false } as unknown as ModelProperty;

    const inputModel = {
      kind: "Model",
      name: "GetJwksInput",
      properties: new Map<string, ModelProperty>(),
    } as unknown as Model;

    const outputModel = {
      kind: "Model",
      name: "GetJwksOutput",
      properties: new Map<string, ModelProperty>([["kid", kidProp]]),
    } as unknown as Model;

    // Op with parameters wrapping inputModel as a single named param.
    const inputProp = { type: inputModel, optional: false } as unknown as ModelProperty;
    const wrappedParams = {
      kind: "Model",
      name: "",
      properties: new Map<string, ModelProperty>([["input", inputProp]]),
    } as unknown as Model;

    const op = {
      name: "getJwks",
      parameters: wrappedParams,
      returnType: outputModel,
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    const mockProgram = {
      diagnostics: [],
      stateMap(_key: symbol): Map<object, unknown> {
        return new Map();
      },
      reportDiagnostic() {},
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: { "csharp-namespace": "D2.Test" },
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // Manifest + GetJwksInput.g.cs + GetJwksOutput.g.cs + get-jwks-dto.g.ts = 4 files.
    expect(directUnitEmitted.length).toBeGreaterThanOrEqual(4);

    const paths = directUnitEmitted.map((e) => e.path);
    expect(paths.some((p) => p.includes("operations-manifest.json"))).toBe(true);
    expect(paths.some((p) => p.includes("GetJwksInput.g.cs"))).toBe(true);
    expect(paths.some((p) => p.includes("GetJwksOutput.g.cs"))).toBe(true);
    expect(paths.some((p) => p.includes("get-jwks-dto.g.ts"))).toBe(true);
  });

  it("reportDiagnostic is called and no DTO files emitted for an unmapped scalar", async () => {
    const utcDateTimeScalar = { kind: "Scalar", name: "utcDateTime" } as unknown as Scalar;
    const badProp = { type: utcDateTimeScalar, optional: false } as unknown as ModelProperty;

    const inputModel = {
      kind: "Model",
      name: "BadInput",
      properties: new Map<string, ModelProperty>([["timestamp", badProp]]),
    } as unknown as Model;

    const op = {
      name: "badOp",
      parameters: inputModel,
      returnType: { kind: "Intrinsic", name: "void" },
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    const reportedDiagnostics: Array<{ code: string }> = [];
    const mockProgram = {
      diagnostics: [],
      stateMap(_key: symbol): Map<object, unknown> {
        return new Map();
      },
    } as unknown as Program;

    // Patch $lib.reportDiagnostic to capture calls.
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => { reportedDiagnostics.push({ code: diag.code }); },
    );

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: { "csharp-namespace": "D2.Test" },
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // Unmapped scalar fires D2TSP001; no DTO files emitted.
    expect(reportedDiagnostics.some((d) => d.code === "unmapped-scalar")).toBe(true);
    const dtoFiles = directUnitEmitted.filter(
      (e) => e.path.endsWith(".g.cs") || e.path.endsWith(".g.ts"),
    );
    expect(dtoFiles).toHaveLength(0);
  });

  it("reportDiagnostic is called with unsupported-property-type for enum properties", async () => {
    // Build a model with an enum property (D2TSP002 triggers unsupported-property-type).
    const enumType = { kind: "Enum", name: "Status" } as unknown as Model;
    const enumProp = { type: enumType, optional: false } as unknown as ModelProperty;
    const inputModel = {
      kind: "Model",
      name: "BadEnumInput",
      properties: new Map<string, ModelProperty>([["status", enumProp]]),
    } as unknown as Model;

    const op = {
      name: "badEnum",
      parameters: inputModel,
      returnType: { kind: "Intrinsic", name: "void" },
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    const mockProgram = {
      diagnostics: [],
      stateMap(_key: symbol): Map<object, unknown> {
        return new Map();
      },
    } as unknown as Program;

    const reportedDiagnostics: Array<{ code: string }> = [];
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => { reportedDiagnostics.push({ code: diag.code }); },
    );

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {},
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // D2TSP002 fires the unsupported-property-type diagnostic.
    expect(reportedDiagnostics.some((d) => d.code === "unsupported-property-type")).toBe(true);
    const dtoFiles = directUnitEmitted.filter(
      (e) => e.path.endsWith(".g.cs") || e.path.endsWith(".g.ts"),
    );
    expect(dtoFiles).toHaveLength(0);
  });

  it("multi-param op → resolveSingleNamedParam returns undefined (size !== 1 branch)", async () => {
    // Op with 2 named parameters — `params.properties.size !== 1` is true,
    // resolveSingleNamedParam returns undefined and raw params model is used directly.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const prop1 = { type: stringScalar, optional: false } as unknown as ModelProperty;
    const prop2 = { type: stringScalar, optional: false } as unknown as ModelProperty;
    const multiParams = {
      kind: "Model",
      name: "",
      properties: new Map<string, ModelProperty>([["id", prop1], ["name", prop2]]),
    } as unknown as Model;

    const op = {
      name: "multiParam",
      parameters: multiParams,
      returnType: { kind: "Intrinsic", name: "void" },
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    const mockProgram = {
      diagnostics: [],
      stateMap(_key: symbol): Map<object, unknown> {
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {},
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // Multi-param op uses the raw params model — both scalar fields become params.
    const csFile = directUnitEmitted.find((e) => e.path.includes("MultiParamInput.g.cs"));
    expect(csFile).toBeDefined();
    expect(csFile!.content).toContain("string Id");
    expect(csFile!.content).toContain("string Name");
  });

  it("single Array-typed param → resolveSingleNamedParam returns undefined (prop.type.name === 'Array')", async () => {
    // When the single named param wraps an Array model, resolveSingleNamedParam should
    // return undefined (not unwrap it) — exercising the `prop.type.name !== "Array"` false branch.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const arrayModel = {
      kind: "Model",
      name: "Array",
      indexer: { value: stringScalar },
      properties: new Map<string, ModelProperty>(),
    } as unknown as Model;

    const arrayProp = { type: arrayModel, optional: false } as unknown as ModelProperty;
    const wrappedParams = {
      kind: "Model",
      name: "",
      properties: new Map<string, ModelProperty>([["items", arrayProp]]),
    } as unknown as Model;

    const op = {
      name: "arrayParam",
      parameters: wrappedParams,
      returnType: { kind: "Intrinsic", name: "void" },
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    const mockProgram = {
      diagnostics: [],
      stateMap(_key: symbol): Map<object, unknown> {
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {},
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // The op was processed — manifest was emitted.
    expect(directUnitEmitted.some((e) => e.path.includes("operations-manifest.json"))).toBe(true);
  });

  it("single scalar param op uses raw params model (resolveSingleNamedParam returns undefined)", async () => {
    // Op with a single named parameter that is a Scalar (not a Model) —
    // resolveSingleNamedParam should return undefined and the raw params model is used.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const scalarProp = { type: stringScalar, optional: false } as unknown as ModelProperty;
    const rawParams = {
      kind: "Model",
      name: "",
      properties: new Map<string, ModelProperty>([["id", scalarProp]]),
    } as unknown as Model;

    const op = {
      name: "scalarParam",
      parameters: rawParams,
      returnType: { kind: "Intrinsic", name: "void" },
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    const mockProgram = {
      diagnostics: [],
      stateMap(_key: symbol): Map<object, unknown> {
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {},
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // ScalarParam input should appear as a field (from raw params model directly).
    const csFile = directUnitEmitted.find((e) => e.path.includes("ScalarParamInput.g.cs"));
    expect(csFile).toBeDefined();
    expect(csFile!.content).toContain("string Id");
  });

  it("void-output op emits only input DTO (outputModel undefined branch)", async () => {
    // Op with input model but void return — exercises outputModel === undefined branch.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const prop = { type: stringScalar, optional: false } as unknown as ModelProperty;
    const inputModel = {
      kind: "Model",
      name: "VoidOutInput",
      properties: new Map<string, ModelProperty>([["id", prop]]),
    } as unknown as Model;

    const op = {
      name: "voidOut",
      parameters: inputModel,
      returnType: { kind: "Intrinsic", name: "void" },
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    const mockProgram = {
      diagnostics: [],
      stateMap(_key: symbol): Map<object, unknown> {
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {},
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // Both C# files still emitted (Input + Output pair), even when outputModel is void.
    expect(directUnitEmitted.some((e) => e.path.includes("VoidOutInput.g.cs"))).toBe(true);
  });

  it("input-less op (no parameters field) with output → inputModel undefined branch in emitDtoPair", async () => {
    // Op with NO parameters property at all + a concrete output — exercises the
    // `rawParams === undefined → inputModel = undefined` branch, then enters
    // emitDtoPair with `inputModel=undefined`, hitting the `: { fields:[], nestedModels:[] }` branch.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const prop = { type: stringScalar, optional: false } as unknown as ModelProperty;
    const outputModel = {
      kind: "Model",
      name: "NoInputOutput",
      properties: new Map<string, ModelProperty>([["result", prop]]),
    } as unknown as Model;

    // Op has NO parameters field (undefined) — rawParams is undefined after cast.
    const op = {
      name: "noInput",
      // parameters is intentionally omitted — rawParams becomes undefined.
      returnType: outputModel,
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    const mockProgram = {
      diagnostics: [],
      stateMap(_key: symbol): Map<object, unknown> {
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {},
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    expect(directUnitEmitted.some((e) => e.path.includes("NoInputOutput.g.cs"))).toBe(true);
  });

  it("tryGetSpecPath returns file path when op.node.file.path is present", async () => {
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const prop = { type: stringScalar, optional: false } as unknown as ModelProperty;
    const model = {
      kind: "Model",
      name: "SpecPathInput",
      properties: new Map<string, ModelProperty>([["id", prop]]),
    } as unknown as Model;

    const op = {
      name: "withSpec",
      parameters: model,
      returnType: { kind: "Intrinsic", name: "void" },
      // Provide the node.file.path that tryGetSpecPath reads.
      node: { file: { path: "contracts/typespec/test.tsp" } },
    } as unknown as Operation;

    directUnitOps.push(op);

    const mockProgram = {
      diagnostics: [],
      stateMap(_key: symbol): Map<object, unknown> {
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {},
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // The spec hint from node.file.path should appear in the emitted C# banner.
    const csFile = directUnitEmitted.find((e) => e.path.endsWith("WithSpecInput.g.cs"));
    expect(csFile).toBeDefined();
    expect(csFile!.content).toContain("contracts/typespec/test.tsp");
  });

  it("op with @d2GrpcMethod + concrete models → proto + service + mapper emitted", async () => {
    // Exercise the emitProtoAndGrpcService path in src/emitter.ts.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const bytesScalar = { kind: "Scalar", name: "bytes" } as unknown as Scalar;
    const kidProp = { type: stringScalar, optional: false } as unknown as ModelProperty;
    const payloadProp = { type: bytesScalar, optional: false } as unknown as ModelProperty;
    const sigProp = { type: stringScalar, optional: false } as unknown as ModelProperty;

    const inputModel = {
      kind: "Model",
      name: "SignInput",
      properties: new Map<string, ModelProperty>([["kid", kidProp], ["payload", payloadProp]]),
    } as unknown as Model;

    const outputModel = {
      kind: "Model",
      name: "SignOutput",
      properties: new Map<string, ModelProperty>([["signature", sigProp]]),
    } as unknown as Model;

    // Wrap inputModel as a single named param (matches the sign op shape).
    const inputProp = { type: inputModel, optional: false } as unknown as ModelProperty;
    const wrappedParams = {
      kind: "Model",
      name: "",
      properties: new Map<string, ModelProperty>([["input", inputProp]]),
    } as unknown as Model;

    const op = {
      name: "sign",
      parameters: wrappedParams,
      returnType: outputModel,
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    // Wire @d2GrpcMethod state map for this op.
    const grpcMap = new Map<object, unknown>([
      [op, { service: "KeyCustodianSigner", method: "Sign", streaming: "unary" }],
    ]);

    const mockProgram = {
      diagnostics: [],
      stateMap(key: symbol): Map<object, unknown> {
        if (key === D2_GRPC_METHOD_KEY) return grpcMap;
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {
        "csharp-namespace": "D2.Test",
        "proto-package": "d2.test.v1",
        "proto-csharp-namespace": "D2.Test.Protos.V1",
        "grpc-service-namespace": "D2.Test.Grpc",
      },
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    const paths = directUnitEmitted.map((e) => e.path);
    // .proto file emitted.
    expect(paths.some((p) => p.includes(".g.proto"))).toBe(true);
    // gRPC service class emitted.
    expect(paths.some((p) => p.includes("KeyCustodianSignerService.g.cs"))).toBe(true);
    // Transport mapper emitted.
    expect(paths.some((p) => p.includes("SignTransportMappers.g.cs"))).toBe(true);
  });

  it("op with @d2GrpcMethod + unmapped scalar → reportDiagnostic called, no proto emitted", async () => {
    // Exercise the onError path inside emitProtoAndGrpcService.
    const utcScalar = { kind: "Scalar", name: "utcDateTime" } as unknown as Scalar;
    const badProp = { type: utcScalar, optional: false } as unknown as ModelProperty;

    const badModel = {
      kind: "Model",
      name: "BadGrpcInput",
      properties: new Map<string, ModelProperty>([["when", badProp]]),
    } as unknown as Model;

    const op = {
      name: "badGrpc",
      parameters: badModel,
      returnType: { kind: "Intrinsic", name: "void" } as unknown as Model,
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    const grpcMap = new Map<object, unknown>([
      [op, { service: "MySvc", method: "Do", streaming: "unary" }],
    ]);

    const reportedDiagnostics: Array<{ code: string }> = [];
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => { reportedDiagnostics.push({ code: diag.code }); },
    );

    const mockProgram = {
      diagnostics: [],
      stateMap(key: symbol): Map<object, unknown> {
        if (key === D2_GRPC_METHOD_KEY) return grpcMap;
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {
        "csharp-namespace": "D2.Test",
        "proto-package": "d2.test.v1",
        "proto-csharp-namespace": "D2.Test.Protos.V1",
        "grpc-service-namespace": "D2.Test.Grpc",
      },
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    expect(reportedDiagnostics.some((d) => d.code === "unmapped-scalar")).toBe(true);
    expect(directUnitEmitted.filter((e) => e.path.endsWith(".g.proto"))).toHaveLength(0);
  });

  it("op with @d2GrpcMethod + invalid streaming mode → reportDiagnostic called (else if branch)", async () => {
    // Exercise the `else if (code === "invalid-streaming-mode")` branch in emitProtoAndGrpcService.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const prop = { type: stringScalar, optional: false } as unknown as ModelProperty;
    const inputModel = {
      kind: "Model",
      name: "DoInput",
      properties: new Map<string, ModelProperty>([["id", prop]]),
    } as unknown as Model;

    const op = {
      name: "doOp",
      parameters: inputModel,
      returnType: { kind: "Intrinsic", name: "void" } as unknown as Model,
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    // Use an invalid streaming mode — triggers onError with "invalid-streaming-mode".
    const grpcMap = new Map<object, unknown>([
      [op, { service: "MySvc", method: "Do", streaming: "bidirectional" }],
    ]);

    const reportedDiagnostics: Array<{ code: string }> = [];
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => { reportedDiagnostics.push({ code: diag.code }); },
    );

    const mockProgram = {
      diagnostics: [],
      stateMap(key: symbol): Map<object, unknown> {
        if (key === D2_GRPC_METHOD_KEY) return grpcMap;
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {
        "csharp-namespace": "D2.Test",
        "proto-package": "d2.test.v1",
        "proto-csharp-namespace": "D2.Test.Protos.V1",
        "grpc-service-namespace": "D2.Test.Grpc",
      },
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // Invalid streaming mode fires diagnostic (mapped via the else-if branch to unmapped-scalar code).
    expect(reportedDiagnostics.some((d) => d.code === "unmapped-scalar")).toBe(true);
    expect(directUnitEmitted.filter((e) => e.path.endsWith(".g.proto"))).toHaveLength(0);
  });

  it("op with @d2GrpcMethod + enum field → reportDiagnostic unsupported-property-type (else branch)", async () => {
    // Exercise the `else` branch in emitProtoAndGrpcService onError:
    // walkModel fires "unsupported-property-type" for enum properties.
    const enumType = { kind: "Enum", name: "Status" } as unknown as Model;
    const enumProp = { type: enumType, optional: false } as unknown as ModelProperty;
    const inputModel = {
      kind: "Model",
      name: "EnumGrpcInput",
      properties: new Map<string, ModelProperty>([["status", enumProp]]),
    } as unknown as Model;

    const op = {
      name: "enumGrpc",
      parameters: inputModel,
      returnType: { kind: "Intrinsic", name: "void" } as unknown as Model,
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    const grpcMap = new Map<object, unknown>([
      [op, { service: "MySvc", method: "EnumOp", streaming: "unary" }],
    ]);

    const reportedDiagnostics: Array<{ code: string }> = [];
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => { reportedDiagnostics.push({ code: diag.code }); },
    );

    const mockProgram = {
      diagnostics: [],
      stateMap(key: symbol): Map<object, unknown> {
        if (key === D2_GRPC_METHOD_KEY) return grpcMap;
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {
        "csharp-namespace": "D2.Test",
        "proto-package": "d2.test.v1",
        "proto-csharp-namespace": "D2.Test.Protos.V1",
        "grpc-service-namespace": "D2.Test.Grpc",
      },
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // Enum property fires unsupported-property-type diagnostic.
    expect(reportedDiagnostics.some((d) => d.code === "unsupported-property-type")).toBe(true);
    expect(directUnitEmitted.filter((e) => e.path.endsWith(".g.proto"))).toHaveLength(0);
  });

  it("op with @d2GrpcMethod + no input model → inputModel undefined branch + fallback name used", async () => {
    // Exercise inputModel === undefined in emitProtoAndGrpcService (lines 250 + 261 false branches).
    // Op has no parameters (undefined) but has a concrete output model.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const prop = { type: stringScalar, optional: false } as unknown as ModelProperty;
    const outputModel = {
      kind: "Model",
      name: "PingOutput",
      properties: new Map<string, ModelProperty>([["message", prop]]),
    } as unknown as Model;

    // Op with NO parameters — rawParams is undefined, inputModel stays undefined.
    const op = {
      name: "ping",
      // parameters intentionally omitted
      returnType: outputModel,
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    const grpcMap = new Map<object, unknown>([
      [op, { service: "PingSvc", method: "Ping", streaming: "unary" }],
    ]);

    const mockProgram = {
      diagnostics: [],
      stateMap(key: symbol): Map<object, unknown> {
        if (key === D2_GRPC_METHOD_KEY) return grpcMap;
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {
        "csharp-namespace": "D2.Test",
        "proto-package": "d2.test.v1",
        "proto-csharp-namespace": "D2.Test.Protos.V1",
        "grpc-service-namespace": "D2.Test.Grpc",
      },
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    const paths = directUnitEmitted.map((e) => e.path);
    // Proto file emitted. Message names use <Method>Request / <Method>Response convention.
    expect(paths.some((p) => p.includes(".g.proto"))).toBe(true);
    const protoFile = directUnitEmitted.find((e) => e.path.endsWith(".g.proto"));
    expect(protoFile!.content).toContain("message PingRequest {}");
    expect(protoFile!.content).toContain("message PingResponse {");
  });
});

// ---------------------------------------------------------------------------
// Namespace routing branches (direct-unit): exercises resolveDtoNamespace +
// resolveHandlerNamespace with csharp-app-namespace-base set.
//
// These branches are NOT reachable from integration tests (which run through
// dist/ and only credit V8 for src/ when the source is instrumented directly).
// ---------------------------------------------------------------------------

describe("$onEmit_directUnit_NamespaceRouting", () => {
  it("exposed op + csClientsNamespace → DTOs land in Clients namespace (lines 299-300)", async () => {
    // Exposed op (@d2InProcess) + Clients namespace set → DTOs go to Clients ns.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const kidProp = { type: stringScalar, optional: false } as unknown as ModelProperty;
    const inputModel = {
      kind: "Model",
      name: "GetJwksInput",
      properties: new Map<string, ModelProperty>(),
    } as unknown as Model;
    const outputModel = {
      kind: "Model",
      name: "GetJwksOutput",
      properties: new Map<string, ModelProperty>([["kid", kidProp]]),
    } as unknown as Model;

    const op = {
      name: "getJwks",
      parameters: inputModel,
      returnType: outputModel,
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    // @d2Query + @d2InProcess → isExposed=true, category="Queries".
    const queryMap = new Map<object, unknown>([[op, true]]);
    const inProcessMap = new Map<object, unknown>([[op, true]]);

    const mockProgram = {
      diagnostics: [],
      stateMap(key: symbol): Map<object, unknown> {
        if (key === D2_QUERY_KEY) return queryMap;
        if (key === D2_IN_PROCESS_KEY) return inProcessMap;
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {
        "csharp-namespace": "D2.Test.Fixture",
        "csharp-clients-namespace": "D2.Edge.KeyCustodian.Clients",
        "csharp-app-namespace-base": "D2.Edge.KeyCustodian.App.Application.Handlers",
        "grpc-service-namespace": "D2.Test.Grpc",
      },
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // DTOs should land in the Clients namespace, not the fixture or app namespace.
    const csOutput = directUnitEmitted.find((e) => e.path.includes("GetJwksOutput.g.cs"));
    expect(csOutput).toBeDefined();
    expect(csOutput!.content).toContain("namespace D2.Edge.KeyCustodian.Clients;");
  });

  it("internal op + csAppNamespaceBase + category → DTOs land in app CQRS namespace (lines 302-306)", async () => {
    // Internal op (@d2Internal + @d2Query) + app-namespace-base set → DTOs go to app CQRS ns.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const prop = { type: stringScalar, optional: false } as unknown as ModelProperty;
    const outputModel = {
      kind: "Model",
      name: "ListKeysOutput",
      properties: new Map<string, ModelProperty>([["keys", prop]]),
    } as unknown as Model;

    const op = {
      name: "listKeys",
      parameters: { kind: "Model", name: "", properties: new Map() } as unknown as Model,
      returnType: outputModel,
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    // @d2Query + @d2Internal → isExposed=false, isInternal=true, category="Queries".
    const queryMap = new Map<object, unknown>([[op, true]]);
    const internalMap = new Map<object, unknown>([[op, true]]);

    const mockProgram = {
      diagnostics: [],
      stateMap(key: symbol): Map<object, unknown> {
        if (key === D2_QUERY_KEY) return queryMap;
        if (key === D2_INTERNAL_KEY) return internalMap;
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {
        "csharp-namespace": "D2.Test.Fixture",
        "csharp-clients-namespace": "D2.Edge.KeyCustodian.Clients",
        "csharp-app-namespace-base": "D2.Edge.KeyCustodian.App.Application.Handlers",
        "grpc-service-namespace": "D2.Test.Grpc",
      },
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // DTOs should land in the per-op CQRS app namespace, not fixture or Clients ns.
    const csOutput = directUnitEmitted.find((e) => e.path.includes("ListKeysOutput.g.cs"));
    expect(csOutput).toBeDefined();
    expect(csOutput!.content).toContain(
      "namespace D2.Edge.KeyCustodian.App.Application.Handlers.Queries.ListKeys;",
    );
  });

  it("op with csAppNamespaceBase + missing CQRS category → D2TSP003 fires + falls back (lines 307-313)", async () => {
    // Op missing both @d2Command and @d2Query → category undefined → D2TSP003 + fallback.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const prop = { type: stringScalar, optional: false } as unknown as ModelProperty;
    const outputModel = {
      kind: "Model",
      name: "NoCategoryOutput",
      properties: new Map<string, ModelProperty>([["result", prop]]),
    } as unknown as Model;

    const op = {
      name: "noCategory",
      parameters: { kind: "Model", name: "", properties: new Map() } as unknown as Model,
      returnType: outputModel,
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    // No command or query state → category resolves to undefined.
    // @d2Internal is set so isInternal=true → hits the isInternal||!isExposed branch.
    const internalMap = new Map<object, unknown>([[op, true]]);

    const reportedDiagnostics: Array<{ code: string }> = [];
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => { reportedDiagnostics.push({ code: diag.code }); },
    );

    const mockProgram = {
      diagnostics: [],
      stateMap(key: symbol): Map<object, unknown> {
        if (key === D2_INTERNAL_KEY) return internalMap;
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {
        "csharp-namespace": "D2.Test.Fixture",
        "csharp-app-namespace-base": "D2.Edge.KeyCustodian.App.Application.Handlers",
        "grpc-service-namespace": "D2.Test.Grpc",
      },
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // D2TSP003 fires.
    expect(reportedDiagnostics.some((d) => d.code === "missing-cqrs-category")).toBe(true);
    // DTOs still emit (fallback to fixture ns — loud but not crash).
    const csOutput = directUnitEmitted.find((e) => e.path.includes("NoCategoryOutput.g.cs"));
    expect(csOutput).toBeDefined();
    expect(csOutput!.content).toContain("namespace D2.Test.Fixture;");
  });

  it("exposed op + csAppNamespaceBase + no csClientsNamespace → falls back to fixture ns (lines 316-317)", async () => {
    // Exposed op but Clients namespace not configured → falls back to csNamespace.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const prop = { type: stringScalar, optional: false } as unknown as ModelProperty;
    const outputModel = {
      kind: "Model",
      name: "ExposedNoClientOutput",
      properties: new Map<string, ModelProperty>([["value", prop]]),
    } as unknown as Model;

    const op = {
      name: "exposedNoClient",
      parameters: { kind: "Model", name: "", properties: new Map() } as unknown as Model,
      returnType: outputModel,
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    // @d2Query + @d2InProcess → isExposed=true; no csClientsNamespace set.
    const queryMap = new Map<object, unknown>([[op, true]]);
    const inProcessMap = new Map<object, unknown>([[op, true]]);

    const mockProgram = {
      diagnostics: [],
      stateMap(key: symbol): Map<object, unknown> {
        if (key === D2_QUERY_KEY) return queryMap;
        if (key === D2_IN_PROCESS_KEY) return inProcessMap;
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {
        "csharp-namespace": "D2.Test.Fixture",
        // csharp-clients-namespace intentionally absent.
        "csharp-app-namespace-base": "D2.Edge.KeyCustodian.App.Application.Handlers",
        "grpc-service-namespace": "D2.Test.Grpc",
      },
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // No Clients ns → falls back to fixture namespace.
    const csOutput = directUnitEmitted.find((e) => e.path.includes("ExposedNoClientOutput.g.cs"));
    expect(csOutput).toBeDefined();
    expect(csOutput!.content).toContain("namespace D2.Test.Fixture;");
  });

  it("Command op (@d2Command, not @d2Query) → resolveCategory returns 'Commands' (line 264)", async () => {
    // Exercises the `isCommand && !isQuery` branch in resolveCategory.
    // The internal-op test hits the Queries branch; this hits the Commands branch.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const prop = { type: stringScalar, optional: false } as unknown as ModelProperty;
    const outputModel = {
      kind: "Model",
      name: "CreateKeyOutput",
      properties: new Map<string, ModelProperty>([["id", prop]]),
    } as unknown as Model;

    const op = {
      name: "createKey",
      parameters: { kind: "Model", name: "", properties: new Map() } as unknown as Model,
      returnType: outputModel,
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    // @d2Command + @d2Internal → isCommand=true, isQuery=false → "Commands".
    const commandMap = new Map<object, unknown>([[op, true]]);
    const internalMap = new Map<object, unknown>([[op, true]]);

    const mockProgram = {
      diagnostics: [],
      stateMap(key: symbol): Map<object, unknown> {
        if (key === D2_COMMAND_KEY) return commandMap;
        if (key === D2_INTERNAL_KEY) return internalMap;
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {
        "csharp-namespace": "D2.Test.Fixture",
        "csharp-app-namespace-base": "D2.Edge.KeyCustodian.App.Application.Handlers",
        "grpc-service-namespace": "D2.Test.Grpc",
      },
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // DTOs land in Commands CQRS path.
    const csOutput = directUnitEmitted.find((e) => e.path.includes("CreateKeyOutput.g.cs"));
    expect(csOutput).toBeDefined();
    expect(csOutput!.content).toContain(
      "namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.CreateKey;",
    );
  });

  it("op name with empty string in toPascalFromCamel guard (line 344) — zero-length fallback path", async () => {
    // toPascalFromCamel("") → returns "" immediately (the early-return guard on line 344).
    // resolveDtoNamespace is called BEFORE emitDtoPair / emitHandlerInterface, so
    // toPascalFromCamel(opName) at line 304 fires before emitHandlerInterface's own guard.
    // We force dtoEmitSucceeded=false (unmapped scalar) so emitHandlerInterface is never
    // reached, which would otherwise throw for opName="".
    const utcDateTimeScalar = { kind: "Scalar", name: "utcDateTime" } as unknown as Scalar;
    const badProp = { type: utcDateTimeScalar, optional: false } as unknown as ModelProperty;
    const outputModel = {
      kind: "Model",
      name: "EmptyNameOutput",
      properties: new Map<string, ModelProperty>([["when", badProp]]),
    } as unknown as Model;

    const op = {
      name: "",
      parameters: { kind: "Model", name: "", properties: new Map() } as unknown as Model,
      returnType: outputModel,
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    // @d2Query + @d2Internal → category="Queries"; toPascalFromCamel("") fires at line 304.
    const queryMap = new Map<object, unknown>([[op, true]]);
    const internalMap = new Map<object, unknown>([[op, true]]);

    const reportedDiagnostics: Array<{ code: string }> = [];
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => { reportedDiagnostics.push({ code: diag.code }); },
    );

    const mockProgram = {
      diagnostics: [],
      stateMap(key: symbol): Map<object, unknown> {
        if (key === D2_QUERY_KEY) return queryMap;
        if (key === D2_INTERNAL_KEY) return internalMap;
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {
        "csharp-namespace": "D2.Test.Fixture",
        "csharp-app-namespace-base": "D2.Edge.KeyCustodian.App.Application.Handlers",
        "grpc-service-namespace": "D2.Test.Grpc",
      },
    } as unknown as EmitContext;

    // resolveDtoNamespace calls toPascalFromCamel("") at line 304 (guard fires → returns "").
    // emitDtoPair returns false (unmapped utcDateTime scalar) → emitHandlerInterface skipped.
    await $onEmit(mockContext);

    // DTO error fired (unmapped scalar) — dtoEmitSucceeded=false.
    expect(reportedDiagnostics.some((d) => d.code === "unmapped-scalar")).toBe(true);
    // Manifest still emitted.
    expect(directUnitEmitted.some((e) => e.path.includes("operations-manifest.json"))).toBe(true);
  });

  it("exposed op + csClientsNamespace + csAppNamespaceBase + servedBy → façade files emitted (lines 215-217, 253-257)", async () => {
    // Exercises the two uncovered branches in emitter.ts:
    //   lines 215-217: inner `if (servedBy !== undefined && servedBy.length > 0)` block
    //                  that pushes into exposedOpsByModule.
    //   lines 253-257: the `for (const [moduleName, moduleOps] of exposedOpsByModule)` loop
    //                  that calls emitFacade + emits the three facade files.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const kidProp = { type: stringScalar, optional: false } as unknown as ModelProperty;

    const inputModel = {
      kind: "Model",
      name: "GetJwksInput",
      properties: new Map<string, ModelProperty>(),
    } as unknown as Model;

    const outputModel = {
      kind: "Model",
      name: "GetJwksOutput",
      properties: new Map<string, ModelProperty>([["kid", kidProp]]),
    } as unknown as Model;

    // Wrap inputModel as a single named param (standard convention).
    const inputProp = { type: inputModel, optional: false } as unknown as ModelProperty;
    const wrappedParams = {
      kind: "Model",
      name: "",
      properties: new Map<string, ModelProperty>([["input", inputProp]]),
    } as unknown as Model;

    const op = {
      name: "getJwks",
      parameters: wrappedParams,
      returnType: outputModel,
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    // @d2Query + @d2InProcess → isExposed=true, category="Queries".
    // @d2ServedBy("KeyCustodian") → stateMap returns "KeyCustodian" for D2_SERVED_BY_KEY.
    const queryMap = new Map<object, unknown>([[op, true]]);
    const inProcessMap = new Map<object, unknown>([[op, true]]);
    const servedByMap = new Map<object, unknown>([[op, "KeyCustodian"]]);

    const mockProgram = {
      diagnostics: [],
      stateMap(key: symbol): Map<object, unknown> {
        if (key === D2_QUERY_KEY) return queryMap;
        if (key === D2_IN_PROCESS_KEY) return inProcessMap;
        if (key === D2_SERVED_BY_KEY) return servedByMap;
        return new Map();
      },
      reportDiagnostic() {},
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {
        "csharp-namespace": "D2.Test.Fixture",
        "csharp-clients-namespace": "D2.Edge.KeyCustodian.Clients",
        "csharp-app-namespace-base": "D2.Edge.KeyCustodian.App.Application.Handlers",
      },
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    const paths = directUnitEmitted.map((e) => e.path);

    // Façade interface file emitted (line 253-257 loop fired).
    expect(paths.some((p) => p.includes("IKeyCustodianInternalApi.g.cs"))).toBe(true);

    // Impl file emitted (the concrete class, not the interface — ends with /KeyCustodianInternalApi.g.cs).
    expect(paths.some((p) => p.endsWith("/KeyCustodianInternalApi.g.cs"))).toBe(true);

    // DI extension file emitted.
    expect(paths.some((p) => p.includes("KeyCustodianClientsGenerated.g.cs"))).toBe(true);

    // Interface content is in the Clients namespace (Clients-project file).
    const ifaceFile = directUnitEmitted.find((e) => e.path.includes("IKeyCustodianInternalApi.g.cs"));
    expect(ifaceFile).toBeDefined();
    expect(ifaceFile!.content).toContain("namespace D2.Edge.KeyCustodian.Clients;");
    expect(ifaceFile!.content).toContain("GetJwksAsync(");

    // Impl file is in the app namespace root (stripped .Handlers suffix).
    const implFile = directUnitEmitted.find((e) => e.path.endsWith("/KeyCustodianInternalApi.g.cs"));
    expect(implFile).toBeDefined();
    expect(implFile!.content).toContain("namespace D2.Edge.KeyCustodian.App.Application;");
  });

  it("resolveHandlerNamespace falls back to grpcServiceNs when csAppNamespaceBase set + category undefined (lines 336-337)", async () => {
    // csAppNamespaceBase is set but category is undefined (no @d2Command or @d2Query) →
    // resolveHandlerNamespace falls back to grpcServiceNs.
    // Op is exposed (@d2InProcess) so DTOs emit; no category so handler ns falls back.
    const stringScalar = { kind: "Scalar", name: "string" } as unknown as Scalar;
    const prop = { type: stringScalar, optional: false } as unknown as ModelProperty;
    const outputModel = {
      kind: "Model",
      name: "NoCatHandlerOutput",
      properties: new Map<string, ModelProperty>([["val", prop]]),
    } as unknown as Model;

    const op = {
      name: "noCatHandler",
      parameters: { kind: "Model", name: "", properties: new Map() } as unknown as Model,
      returnType: outputModel,
      node: undefined,
    } as unknown as Operation;

    directUnitOps.push(op);

    // @d2InProcess → isExposed=true; no command/query → category=undefined.
    const inProcessMap = new Map<object, unknown>([[op, true]]);

    // Spy to capture D2TSP003 (fires for missing category).
    const reportedDiagnostics: Array<{ code: string }> = [];
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => { reportedDiagnostics.push({ code: diag.code }); },
    );

    const mockProgram = {
      diagnostics: [],
      stateMap(key: symbol): Map<object, unknown> {
        if (key === D2_IN_PROCESS_KEY) return inProcessMap;
        return new Map();
      },
    } as unknown as Program;

    const mockContext = {
      program: mockProgram,
      emitterOutputDir: "/out",
      options: {
        "csharp-namespace": "D2.Test.Fixture",
        "csharp-clients-namespace": "D2.Edge.KeyCustodian.Clients",
        "csharp-app-namespace-base": "D2.Edge.KeyCustodian.App.Application.Handlers",
        "grpc-service-namespace": "D2.Test.Grpc",
      },
    } as unknown as EmitContext;

    await $onEmit(mockContext);

    // DTOs go to Clients (exposed + csClientsNamespace configured).
    const csOutput = directUnitEmitted.find((e) => e.path.includes("NoCatHandlerOutput.g.cs"));
    expect(csOutput).toBeDefined();
    expect(csOutput!.content).toContain("namespace D2.Edge.KeyCustodian.Clients;");

    // Handler interface emitted — namespace falls back to grpcServiceNs because category=undefined.
    const handlerFile = directUnitEmitted.find((e) => e.path.includes("INoCatHandlerHandler.g.cs"));
    expect(handlerFile).toBeDefined();
    expect(handlerFile!.content).toContain("namespace D2.Test.Grpc;");
  });
});

// ---------------------------------------------------------------------------
// Integration test: full tsp compile → $onEmit → emitFile pipeline
//
// Uses the TypeSpec test-host to compile an inline .tsp file. The emitter
// package is mounted as a test library so the host can load $onEmit.
// The host captures emitted files in an in-memory FS; we assert the manifest.
// ---------------------------------------------------------------------------

// Mount the decorators library (the .tsp fixture needs @d2/typespec-decorators).
const D2DecoratorTestLibrary = createTestLibrary({
  name: "@d2/typespec-decorators",
  packageRoot: await findTestPackageRoot(
    new URL(
      "../node_modules/@d2/typespec-decorators/package.json",
      import.meta.url,
    ).href,
  ),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

// Mount this emitter package so the host can call its $onEmit.
const D2EmitterTestLibrary = createTestLibrary({
  name: "@d2/typespec-emitters",
  packageRoot: await findTestPackageRoot(import.meta.url),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

describe("$onEmit_integration_SmokeManifest", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("compiles inline .tsp and emits manifest without errors", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;

      namespace TestNs;

      @d2Query
      @d2Internal
      op getStatus(): void;

      @d2Query
      @d2ServedBy("Edge")
      @d2InProcess
      op createOrder(): void;

      @d2Command
      @d2ServedBy("Push")
      @d2InProcess
      @d2GrpcMethod("Push", "PushEvent")
      op pushEvent(): void;
    `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      outputDir: "testing:/out",
    });

    // The test-host uses an in-memory FS. Some TypeSpec versions expose
    // emitter output through host.outDir / host.fs. The key assertion is
    // that compilation completed without errors (the emitter ran without
    // throwing), and the program has no error diagnostics.
    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // Retrieve the manifest from the in-memory FS.
    // The TypeSpec test-host on Windows virtualizes paths with a "Z:/test/" prefix,
    // so we locate the entry by suffix rather than an exact key.
    const stored = (host as unknown as { fs?: Map<string, string> }).fs;
    expect(stored).toBeInstanceOf(Map);
    const manifestKey = [...(stored as Map<string, string>).keys()].find((k) =>
      k.endsWith("operations-manifest.json"),
    );
    expect(manifestKey).toBeDefined();

    const manifestContent = (stored as Map<string, string>).get(manifestKey!)!;
    const manifest = JSON.parse(manifestContent) as OperationsManifest;
    expect(manifest.emitter).toBe("@d2/typespec-emitters");
    expect(manifest.operationCount).toBe(3);

    // Op with @d2ServedBy("Edge") + @d2InProcess.
    const served = manifest.operations.find((o) => o.name === "createOrder");
    expect(served).toBeDefined();
    expect(served!.servedBy).toBe("Edge");
    expect(served!.inProcess).toBe(true);

    // Op with @d2GrpcMethod.
    const grpc = manifest.operations.find((o) => o.name === "pushEvent");
    expect(grpc).toBeDefined();
    expect(grpc!.hasGrpc).toBe(true);

    // Bare op — no decorators.
    const bare = manifest.operations.find((o) => o.name === "getStatus");
    expect(bare).toBeDefined();
    expect(bare!.servedBy).toBeUndefined();
    expect(bare!.hasGrpc).toBe(false);
    expect(bare!.inProcess).toBe(false);
  });
});
