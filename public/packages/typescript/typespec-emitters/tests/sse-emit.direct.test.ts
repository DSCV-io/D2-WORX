// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Direct-unit tests for the emitSsePushIfPresent path + the after-walk SSE
// loops in src/emitter.ts.
//
// V8 coverage of the SSE dispatch pass requires calling $onEmit with a mocked
// @typespec/compiler so the branch executes against the TS source (not dist/).
// Mirrors route-emit.direct.test.ts.
//
// Covers:
//   - @d2ServerPush("user") + payload → dispatcher pair + DI-ext + seam emitted
//     (the happy path + the after-walk per-module DI loop + once-per-ns seam loop)
//   - @d2ServerPush("session") → the Session channel-class branch
//   - @d2ServerPush + void output → D2TSP008 + no dispatcher (the emit-gate return)
//   - @d2ServerPush + no @d2ServedBy → dispatcher emitted, no DI grouping
//     (the servedBy-undefined defensive branch)
//   - @d2ServerPush + anonymous output model → <PascalOp>Output fallback name
//   - PURE-push op → NO I<Op>Handler (isPurePush true → handler suppressed)
//   - COMBINED push + @d2GrpcMethod op → I<Op>Handler EMITTED (isPurePush false →
//     the request side keeps its handler; the gate is SELECTIVE)

import { describe, it, expect, vi, afterEach } from "vitest";
import type {
  EmitContext,
  Model,
  ModelProperty,
  Operation,
  Program,
  Scalar,
} from "@typespec/compiler";
import type * as CompilerNs from "@typespec/compiler";
import type * as HttpNs from "@typespec/http";
import {
  D2_SERVED_BY_KEY,
  D2_COMMAND_KEY,
  D2_SERVER_PUSH_KEY,
  D2_GRPC_METHOD_KEY,
} from "@dcsv-io/d2-typespec-decorators";

// ---------------------------------------------------------------------------
// Module-level mock state — vi.mock is hoisted; factories cannot close over
// test-scoped variables.
// ---------------------------------------------------------------------------

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

vi.mock("@typespec/http", async (importOriginal) => {
  const original = await importOriginal<typeof HttpNs>();
  return {
    ...original,
    // No SSE op carries an HTTP verb → route emitter is skipped.
    getOperationVerb(): string | undefined {
      return undefined;
    },
    getHttpOperation(_prog: unknown, _op: object): [{ path: string }, []] {
      return [{ path: "/unused" }, []];
    },
  };
});

const { $onEmit } = await import("../src/emitter.js");

afterEach(() => {
  directUnitOps.length = 0;
  directUnitEmitted.length = 0;
  vi.restoreAllMocks();
});

// ---------------------------------------------------------------------------
// Helpers.
// ---------------------------------------------------------------------------

function makeStringScalar(): Scalar {
  return { kind: "Scalar", name: "string" } as unknown as Scalar;
}

function makeModel(
  name: string,
  props: Record<string, Scalar | Model> = {},
): Model {
  const properties = new Map<string, ModelProperty>();
  for (const [k, v] of Object.entries(props))
    properties.set(k, { type: v, optional: false } as unknown as ModelProperty);
  return { kind: "Model", name, properties } as unknown as Model;
}

function makeWrappedOp(
  name: string,
  inputModel: Model,
  returnType: unknown,
): Operation {
  const inputProp = {
    type: inputModel,
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
    returnType,
    node: undefined,
  } as unknown as Operation;
}

function makeMockProgram(
  stateMapFn: (key: symbol) => Map<object, unknown>,
): Program {
  return {
    diagnostics: [],
    stateMap: stateMapFn,
    reportDiagnostic() {},
  } as unknown as Program;
}

function makeBaseContext(
  program: Program,
  options: Record<string, string | undefined> = {},
): EmitContext {
  return {
    program,
    emitterOutputDir: "/out",
    options,
  } as unknown as EmitContext;
}

const FIXTURE_OPTS = {
  "csharp-namespace": "DcsvIo.D2.Private.Edge.Tests.TypeSpecSse.Generated",
};

function find(name: string): { path: string; content: string } | undefined {
  return directUnitEmitted.find((e) => e.path.endsWith(`/${name}`));
}

// ---------------------------------------------------------------------------
// Test: user-channel push op → dispatcher pair + DI-ext + seam
// ---------------------------------------------------------------------------

describe("$onEmit_sseDirect_UserChannelPush", () => {
  it("@d2ServerPush('user') + payload → dispatcher pair + DI-ext + seam emitted", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("OrderShippedInput", { orderId: str });
    const outputModel = makeModel("OrderShippedOutput", { orderId: str });
    const op = makeWrappedOp("orderShipped", inputModel, outputModel);

    const command = new Map<object, unknown>([[op, true]]);
    const servedBy = new Map<object, unknown>([[op, "PushFixtures"]]);
    const push = new Map<object, unknown>([[op, "user"]]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_COMMAND_KEY) return command;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_SERVER_PUSH_KEY) return push;
      return new Map();
    });

    await $onEmit(makeBaseContext(program, FIXTURE_OPTS));

    const iface = find("IOrderShippedDispatcher.g.cs");
    expect(iface).toBeDefined();
    expect(iface!.content).toContain(
      "public interface IOrderShippedDispatcher",
    );

    const impl = find("OrderShippedDispatcher.g.cs");
    expect(impl).toBeDefined();
    expect(impl!.content).toContain(
      "new D2GeneratedSseChannelTarget(D2GeneratedSseChannelClass.User, targetId),",
    );
    expect(impl!.content).toContain('"orderShipped", payload, ct);');

    const diExt = find("PushFixturesSseDispatchersGenerated.g.cs");
    expect(diExt).toBeDefined();
    expect(diExt!.content).toContain(
      "services.AddTransient<IOrderShippedDispatcher, OrderShippedDispatcher>();",
    );

    const seam = find("D2GeneratedSseEmitSink.g.cs");
    expect(seam).toBeDefined();
    expect(seam!.content).toContain("public interface D2GeneratedSseEmitSink");
  });
});

// ---------------------------------------------------------------------------
// Test: session-channel push op → Session channel-class branch
// ---------------------------------------------------------------------------

describe("$onEmit_sseDirect_SessionChannelPush", () => {
  it("@d2ServerPush('session') → impl bakes the Session channel class", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("SessionExpiringInput", { sessionId: str });
    const outputModel = makeModel("SessionExpiringOutput", { sessionId: str });
    const op = makeWrappedOp("sessionExpiring", inputModel, outputModel);

    const command = new Map<object, unknown>([[op, true]]);
    const servedBy = new Map<object, unknown>([[op, "PushFixtures"]]);
    const push = new Map<object, unknown>([[op, "session"]]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_COMMAND_KEY) return command;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_SERVER_PUSH_KEY) return push;
      return new Map();
    });

    await $onEmit(makeBaseContext(program, FIXTURE_OPTS));

    const impl = find("SessionExpiringDispatcher.g.cs");
    expect(impl).toBeDefined();
    expect(impl!.content).toContain(
      "new D2GeneratedSseChannelTarget(D2GeneratedSseChannelClass.Session, targetId),",
    );
    expect(impl!.content).not.toContain("ChannelClass.User");
  });
});

// ---------------------------------------------------------------------------
// Test: void-output push op → D2TSP008 + no dispatcher
// ---------------------------------------------------------------------------

describe("$onEmit_sseDirect_VoidOutput_D2TSP008", () => {
  it("@d2ServerPush op with a void return → D2TSP008 fired, no dispatcher emitted", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("PingInput", { id: str });
    const op = makeWrappedOp("ping", inputModel, {
      kind: "Intrinsic",
      name: "void",
    });

    const command = new Map<object, unknown>([[op, true]]);
    const servedBy = new Map<object, unknown>([[op, "PushFixtures"]]);
    const push = new Map<object, unknown>([[op, "user"]]);

    directUnitOps.push(op);

    const reportedCodes: string[] = [];
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => {
        reportedCodes.push(diag.code);
      },
    );

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_COMMAND_KEY) return command;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_SERVER_PUSH_KEY) return push;
      return new Map();
    });

    await $onEmit(makeBaseContext(program, FIXTURE_OPTS));

    expect(reportedCodes).toContain("server-push-requires-payload");
    expect(find("IPingDispatcher.g.cs")).toBeUndefined();
    expect(find("PingDispatcher.g.cs")).toBeUndefined();
    // No seam either — no namespace was tracked (the gate returned before tracking).
    expect(find("D2GeneratedSseEmitSink.g.cs")).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// Test: push op WITHOUT @d2ServedBy → dispatcher emitted, no DI grouping
// ---------------------------------------------------------------------------

describe("$onEmit_sseDirect_NoServedBy_DispatcherButNoDiExt", () => {
  it("@d2ServerPush with no @d2ServedBy → dispatcher emitted, no DI-ext", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("LonelyInput", { id: str });
    const outputModel = makeModel("LonelyOutput", { id: str });
    const op = makeWrappedOp("lonely", inputModel, outputModel);

    const command = new Map<object, unknown>([[op, true]]);
    const push = new Map<object, unknown>([[op, "user"]]);
    // No @d2ServedBy entry.

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_COMMAND_KEY) return command;
      if (key === D2_SERVER_PUSH_KEY) return push;
      return new Map();
    });

    await $onEmit(makeBaseContext(program, FIXTURE_OPTS));

    // The dispatcher pair still emits (the dispatch layer is host-independent).
    expect(find("ILonelyDispatcher.g.cs")).toBeDefined();
    expect(find("LonelyDispatcher.g.cs")).toBeDefined();
    // The seam still emits (the namespace was tracked).
    expect(find("D2GeneratedSseEmitSink.g.cs")).toBeDefined();
    // But NO DI-ext — there is no module to name it.
    expect(
      directUnitEmitted.filter((e) =>
        e.path.includes("SseDispatchersGenerated.g.cs"),
      ),
    ).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Test: anonymous output model → <PascalOp>Output fallback name
// ---------------------------------------------------------------------------

describe("$onEmit_sseDirect_AnonymousOutput_FallbackName", () => {
  it("@d2ServerPush with an unnamed output model → <PascalOp>Output payload name", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("FallbackInput", { id: str });
    // Output model with an empty name → exercises the outputTypeName ?? fallback.
    const outputModel = makeModel("", { id: str });
    const op = makeWrappedOp("fallback", inputModel, outputModel);

    const command = new Map<object, unknown>([[op, true]]);
    const servedBy = new Map<object, unknown>([[op, "PushFixtures"]]);
    const push = new Map<object, unknown>([[op, "user"]]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_COMMAND_KEY) return command;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_SERVER_PUSH_KEY) return push;
      return new Map();
    });

    await $onEmit(makeBaseContext(program, FIXTURE_OPTS));

    const impl = find("FallbackDispatcher.g.cs");
    expect(impl).toBeDefined();
    // The payload type falls back to "FallbackOutput" (the <PascalOp>Output convention).
    expect(impl!.content).toContain("FallbackOutput payload");
  });
});

// ---------------------------------------------------------------------------
// Test: PURE-push op → isPurePush true → NO I<Op>Handler (suppression)
// ---------------------------------------------------------------------------

describe("$onEmit_sseDirect_PurePush_SuppressesHandler", () => {
  it("a PURE @d2ServerPush op (no other exposure) emits the dispatcher but NO I<Op>Handler", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("OrderShippedInput", { orderId: str });
    const outputModel = makeModel("OrderShippedOutput", { orderId: str });
    const op = makeWrappedOp("orderShipped", inputModel, outputModel);

    const command = new Map<object, unknown>([[op, true]]);
    const servedBy = new Map<object, unknown>([[op, "PushFixtures"]]);
    const push = new Map<object, unknown>([[op, "user"]]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_COMMAND_KEY) return command;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_SERVER_PUSH_KEY) return push;
      return new Map();
    });

    await $onEmit(makeBaseContext(program, FIXTURE_OPTS));

    // The dispatcher IS emitted (the pure-push op's only generated surface).
    expect(find("OrderShippedDispatcher.g.cs")).toBeDefined();
    // isPurePush(op) === true → the handler interface is suppressed. A pure-push
    // op is a caller, not a request server; it never registers a handler.
    expect(find("IOrderShippedHandler.g.cs")).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// Test: COMBINED push + @d2GrpcMethod → isPurePush false → I<Op>Handler EMITTED
// ---------------------------------------------------------------------------

describe("$onEmit_sseDirect_CombinedPushGrpc_EmitsHandler", () => {
  it("a @d2ServerPush op that ALSO carries @d2GrpcMethod is NOT pure-push → handler + dispatcher both emitted", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("OrderShippedInput", { orderId: str });
    const outputModel = makeModel("OrderShippedOutput", { orderId: str });
    const op = makeWrappedOp("orderShipped", inputModel, outputModel);

    const command = new Map<object, unknown>([[op, true]]);
    const servedBy = new Map<object, unknown>([[op, "PushFixtures"]]);
    const push = new Map<object, unknown>([[op, "user"]]);
    // The request side: @d2GrpcMethod makes the op NOT pure-push.
    const grpc = new Map<object, unknown>([
      [op, { service: "OrderSvc", method: "OrderShipped", streaming: "unary" }],
    ]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_COMMAND_KEY) return command;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_SERVER_PUSH_KEY) return push;
      if (key === D2_GRPC_METHOD_KEY) return grpc;
      return new Map();
    });

    await $onEmit(makeBaseContext(program, FIXTURE_OPTS));

    // isPurePush(op) === false (it has a gRPC request side) → the handler IS
    // emitted for the request side — the gate is SELECTIVE.
    expect(find("IOrderShippedHandler.g.cs")).toBeDefined();
    // The push side still emits its dispatcher.
    expect(find("OrderShippedDispatcher.g.cs")).toBeDefined();
  });
});
