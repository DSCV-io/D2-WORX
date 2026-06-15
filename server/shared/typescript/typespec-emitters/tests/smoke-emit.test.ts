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
import type { EmitContext, Operation, Program } from "@typespec/compiler";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";
import {
  D2_SERVED_BY_KEY,
  D2_GRPC_METHOD_KEY,
  D2_IN_PROCESS_KEY,
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

      op getStatus(): void;

      @d2ServedBy("Edge")
      @d2InProcess
      op createOrder(): void;

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
