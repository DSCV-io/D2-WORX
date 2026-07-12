// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Direct-unit tests for process-kind + routes/bridge namespace option wiring
// and the real-module host-routing fail-louds (D2TSP014–019).

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
  D2_IN_PROCESS_KEY,
  D2_COMMAND_KEY,
  D2_GRPC_METHOD_KEY,
  D2_REQUIRE_ANY_SCOPE_KEY,
  D2_HARMLESS_KEY,
  D2_FIELD_KEY,
  D2_IDEMPOTENT_KEY,
} from "@d2/typespec-decorators";
import {
  resolveProcessKindByModule,
  resolveStringMapOption,
} from "../src/emitter.js";

const directUnitOps: Operation[] = [];
const directUnitEmitted: Array<{ path: string; content: string }> = [];
const mockVerbMap = new Map<object, string | undefined>();
let mockHttpOpResult: [
  { path: string },
  Array<{ severity: string; message: string }>,
] = [{ path: "/test/path" }, []];

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
    getOperationVerb(_prog: unknown, op: object): string | undefined {
      return mockVerbMap.get(op);
    },
    getHttpOperation(_prog: unknown, _op: object): typeof mockHttpOpResult {
      return mockHttpOpResult;
    },
  };
});

const { $onEmit } = await import("../src/emitter.js");

afterEach(() => {
  directUnitOps.length = 0;
  directUnitEmitted.length = 0;
  mockVerbMap.clear();
  mockHttpOpResult = [{ path: "/test/path" }, []];
  vi.restoreAllMocks();
});

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

function makeFieldMap(...models: Model[]): Map<object, unknown> {
  const m = new Map<object, unknown>();
  for (const model of models) {
    let n = 1;
    for (const prop of (
      model as unknown as { properties: Map<string, ModelProperty> }
    ).properties.values())
      m.set(prop, n++);
  }
  return m;
}

function makeWrappedOp(
  name: string,
  inputModel: Model,
  outputModel: Model,
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
    returnType: outputModel,
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
  options: Record<string, unknown> = {},
): EmitContext {
  return {
    program,
    emitterOutputDir: "/out",
    options,
  } as unknown as EmitContext;
}

const REAL_BASE = {
  "csharp-namespace": "D2.Test.Route",
  "csharp-app-namespace-base": "D2.Test.App.Handlers",
  "csharp-clients-namespace": "D2.Sample.Clients",
  "grpc-service-namespace": "D2.Test.Grpc",
  "proto-package": "d2.test.v1",
  "proto-csharp-namespace": "D2.Test.Protos.V1",
};

async function captureDiagnostics(run: () => Promise<void>): Promise<string[]> {
  const reportedCodes: string[] = [];
  const libModule = await import("../src/lib.js");
  vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
    (_prog, diag: { code: string }) => {
      reportedCodes.push(diag.code);
    },
  );
  await run();
  return reportedCodes;
}

// ---------------------------------------------------------------------------
// Option parse helpers
// ---------------------------------------------------------------------------

describe("resolveStringMapOption / resolveProcessKindByModule", () => {
  it("happy map", () => {
    const m = resolveProcessKindByModule({
      KeyCustodian: "edge-module",
      Audit: "standalone",
    });
    expect(m.get("KeyCustodian")).toBe("edge-module");
    expect(m.get("Audit")).toBe("standalone");
  });

  it("empty / malformed → empty map", () => {
    expect(resolveStringMapOption(undefined).size).toBe(0);
    expect(resolveStringMapOption(null).size).toBe(0);
    expect(resolveStringMapOption([]).size).toBe(0);
    expect(resolveStringMapOption("x").size).toBe(0);
    expect(resolveStringMapOption({ A: "" }).size).toBe(0);
    expect(resolveStringMapOption({ A: 1 }).size).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// missing ServedBy + real-module @route
// ---------------------------------------------------------------------------

describe("Emitter_RealModule_RouteWithoutServedBy_FailLoud", () => {
  it("diagnostic; no hard-derived App.Routes emit", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("NoSbInput", { id: str });
    const outputModel = makeModel("NoSbOutput", { data: str });
    const op = makeWrappedOp("noServedBy", inputModel, outputModel);

    mockVerbMap.set(op, "get");
    mockHttpOpResult = [{ path: "/no-sb" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.read"]]]);
    const commandMap = new Map<object, unknown>([[op, true]]);
    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_COMMAND_KEY) return commandMap;
      return new Map();
    });

    const codes = await captureDiagnostics(async () => {
      await $onEmit(makeBaseContext(program, REAL_BASE));
    });

    expect(codes).toContain("missing-served-by-for-host-routing");
    expect(
      directUnitEmitted.filter((e) =>
        e.path.includes("RouteRegistration.g.cs"),
      ),
    ).toHaveLength(0);
    expect(
      directUnitEmitted.some((e) =>
        e.content.includes("App.Application.Routes"),
      ),
    ).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Missing process-kind / unknown process-kind
// ---------------------------------------------------------------------------

describe("Emitter_RealModule_MissingProcessKind_FailLoud", () => {
  it("servedBy present but map key absent → missing-process-kind", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("PkInput", { id: str });
    const outputModel = makeModel("PkOutput", { data: str });
    const op = makeWrappedOp("missingKind", inputModel, outputModel);

    mockVerbMap.set(op, "get");
    mockHttpOpResult = [{ path: "/mk" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.read"]]]);
    const servedBy = new Map<object, unknown>([[op, "Sample"]]);
    const commandMap = new Map<object, unknown>([[op, true]]);
    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_COMMAND_KEY) return commandMap;
      return new Map();
    });

    const codes = await captureDiagnostics(async () => {
      await $onEmit(
        makeBaseContext(program, {
          ...REAL_BASE,
          // map present but different key
          "process-kind-by-module": { Other: "edge-module" },
        }),
      );
    });

    expect(codes).toContain("missing-process-kind");
    expect(
      directUnitEmitted.filter((e) =>
        e.path.includes("RouteRegistration.g.cs"),
      ),
    ).toHaveLength(0);
  });
});

describe("Emitter_UnknownProcessKind_FailLoud", () => {
  it("unknown value → unknown-process-kind", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("UkInput", { id: str });
    const outputModel = makeModel("UkOutput", { data: str });
    const op = makeWrappedOp("unknownKind", inputModel, outputModel);

    mockVerbMap.set(op, "get");
    mockHttpOpResult = [{ path: "/uk" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.read"]]]);
    const servedBy = new Map<object, unknown>([[op, "Sample"]]);
    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      return new Map();
    });

    const codes = await captureDiagnostics(async () => {
      await $onEmit(
        makeBaseContext(program, {
          ...REAL_BASE,
          "process-kind-by-module": { Sample: "sidecar" },
        }),
      );
    });

    expect(codes).toContain("unknown-process-kind");
  });
});

// ---------------------------------------------------------------------------
// Routes namespace option
// ---------------------------------------------------------------------------

describe("Emitter_RoutesNamespaceOption_EmitsD2EdgeApiRoutes", () => {
  it("key hit → namespace D2.Edge.Api.Routes.KeyCustodian", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("JwksInput", {});
    const outputModel = makeModel("JwksOutput", { kid: str });
    const op = makeWrappedOp("getJwksNs", inputModel, outputModel);

    mockVerbMap.set(op, "get");
    mockHttpOpResult = [{ path: "/.well-known/jwks.json" }, []];

    const harmless = new Map<object, unknown>([[op, true]]);
    const servedBy = new Map<object, unknown>([[op, "KeyCustodian"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);
    const commandMap = new Map<object, unknown>([[op, true]]);
    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_HARMLESS_KEY) return harmless;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      if (key === D2_COMMAND_KEY) return commandMap;
      return new Map();
    });

    await $onEmit(
      makeBaseContext(program, {
        ...REAL_BASE,
        "csharp-clients-namespace": "D2.Edge.KeyCustodian.Client",
        "process-kind-by-module": { KeyCustodian: "edge-module" },
        "csharp-routes-namespace": {
          KeyCustodian: "D2.Edge.Api.Routes.KeyCustodian",
        },
      }),
    );

    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("GetJwksNsRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
    expect(routeFile!.content).toContain(
      "namespace D2.Edge.Api.Routes.KeyCustodian;",
    );
    expect(routeFile!.content).not.toContain("App.Application.Routes");
    // Bridge file must NOT be emitted for edge-module.
    expect(
      directUnitEmitted.filter((e) => e.path.includes("BridgeRegistration")),
    ).toHaveLength(0);
  });
});

describe("Emitter_EdgeModule_MissingRoutesNs_FailLoud", () => {
  it("edge-module + missing routes map key → missing-routes-namespace", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("MrInput", { id: str });
    const outputModel = makeModel("MrOutput", { data: str });
    const op = makeWrappedOp("missingRoutes", inputModel, outputModel);

    mockVerbMap.set(op, "get");
    mockHttpOpResult = [{ path: "/mr" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.read"]]]);
    const servedBy = new Map<object, unknown>([[op, "Sample"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);
    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      return new Map();
    });

    const codes = await captureDiagnostics(async () => {
      await $onEmit(
        makeBaseContext(program, {
          ...REAL_BASE,
          "process-kind-by-module": { Sample: "edge-module" },
          // no csharp-routes-namespace
        }),
      );
    });

    expect(codes).toContain("missing-routes-namespace");
    expect(
      directUnitEmitted.filter((e) =>
        e.path.includes("RouteRegistration.g.cs"),
      ),
    ).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Internal-only: @d2GrpcMethod without @route → gRPC only, no public HTTP
// ---------------------------------------------------------------------------
// Fail-without-fix (§2.3): without the no-verb early-return / process-kind
// gate that skips public HTTP Map* + bridge emit for grpc-only ops, these
// suites would observe BridgeRegistration and/or RouteRegistration files
// (or public Map* content) for ops that must stay internal-only.

describe("Emitter_InternalOnly_GrpcWithoutRoute_NoPublicHttp", () => {
  it("standalone + grpc + no verb → service server present; zero Bridge/RouteRegistration", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("SignInput", { kid: str });
    const outputModel = makeModel("SignOutput", { sig: str });
    const op = makeWrappedOp("signInternal", inputModel, outputModel);

    // No mockVerbMap entry → explicitVerb undefined → emitRouteIfPresent early return
    const servedBy = new Map<object, unknown>([[op, "Audit"]]);
    const commandMap = new Map<object, unknown>([[op, true]]);
    const grpcMethod = new Map<object, unknown>([
      [op, { service: "AuditSvc", method: "Sign", streaming: "unary" }],
    ]);
    const fieldMap = makeFieldMap(inputModel, outputModel);
    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_COMMAND_KEY) return commandMap;
      if (key === D2_GRPC_METHOD_KEY) return grpcMethod;
      if (key === D2_FIELD_KEY) return fieldMap;
      return new Map();
    });

    await $onEmit(
      makeBaseContext(program, {
        ...REAL_BASE,
        "csharp-clients-namespace": "D2.Services.Audit.Client",
        "process-kind-by-module": { Audit: "standalone" },
        "csharp-bridge-namespace": {
          Audit: "D2.Edge.Api.Bridges.Audit",
        },
      }),
    );

    const grpcSvc = directUnitEmitted.find((e) =>
      e.path.includes("AuditSvcService.g.cs"),
    );
    expect(grpcSvc).toBeDefined();
    expect(
      directUnitEmitted.filter((e) => e.path.includes("BridgeRegistration")),
    ).toHaveLength(0);
    expect(
      directUnitEmitted.filter((e) =>
        e.path.includes("RouteRegistration.g.cs"),
      ),
    ).toHaveLength(0);
  });

  it("edge-module + grpc + no verb → service server present; zero Bridge/RouteRegistration", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("SignEmInput", { kid: str });
    const outputModel = makeModel("SignEmOutput", { sig: str });
    const op = makeWrappedOp("signEdgeInternal", inputModel, outputModel);

    const servedBy = new Map<object, unknown>([[op, "KeyCustodian"]]);
    const commandMap = new Map<object, unknown>([[op, true]]);
    const grpcMethod = new Map<object, unknown>([
      [
        op,
        { service: "KeyCustodianSigner", method: "Sign", streaming: "unary" },
      ],
    ]);
    const fieldMap = makeFieldMap(inputModel, outputModel);
    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_COMMAND_KEY) return commandMap;
      if (key === D2_GRPC_METHOD_KEY) return grpcMethod;
      if (key === D2_FIELD_KEY) return fieldMap;
      return new Map();
    });

    await $onEmit(
      makeBaseContext(program, {
        ...REAL_BASE,
        "process-kind-by-module": { KeyCustodian: "edge-module" },
        "csharp-routes-namespace": {
          KeyCustodian: "D2.Edge.Api.Routes.KeyCustodian",
        },
      }),
    );

    const grpcSvc = directUnitEmitted.find((e) =>
      e.path.includes("KeyCustodianSignerService.g.cs"),
    );
    expect(grpcSvc).toBeDefined();
    expect(
      directUnitEmitted.filter((e) => e.path.includes("BridgeRegistration")),
    ).toHaveLength(0);
    expect(
      directUnitEmitted.filter((e) =>
        e.path.includes("RouteRegistration.g.cs"),
      ),
    ).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Standalone bridge path
// ---------------------------------------------------------------------------

describe("Emitter_Standalone_EmitsBridgeAndServiceServer_NotPublicRestOnService", () => {
  it("standalone + route + grpc → bridge file; no in-process façade Map*", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("PingInput", { id: str });
    const outputModel = makeModel("PingOutput", { ok: str });
    const op = makeWrappedOp("pingAudit", inputModel, outputModel);

    mockVerbMap.set(op, "get");
    mockHttpOpResult = [{ path: "/internal/v1/audit/ping" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["internal.audit.ping"]]]);
    const servedBy = new Map<object, unknown>([[op, "Audit"]]);
    const commandMap = new Map<object, unknown>([[op, true]]);
    const grpcMethod = new Map<object, unknown>([
      [op, { service: "AuditSvc", method: "Ping", streaming: "unary" }],
    ]);
    const fieldMap = makeFieldMap(inputModel, outputModel);
    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_COMMAND_KEY) return commandMap;
      if (key === D2_GRPC_METHOD_KEY) return grpcMethod;
      if (key === D2_FIELD_KEY) return fieldMap;
      return new Map();
    });

    await $onEmit(
      makeBaseContext(program, {
        ...REAL_BASE,
        "csharp-clients-namespace": "D2.Services.Audit.Client",
        "process-kind-by-module": { Audit: "standalone" },
        "csharp-bridge-namespace": {
          Audit: "D2.Edge.Api.Bridges.Audit",
        },
      }),
    );

    const bridge = directUnitEmitted.find((e) =>
      e.path.includes("PingAuditBridgeRegistration.g.cs"),
    );
    expect(bridge).toBeDefined();
    expect(bridge!.content).toContain("namespace D2.Edge.Api.Bridges.Audit;");
    expect(bridge!.content).toContain("IAuditGrpcClient");
    expect(bridge!.content).toContain("PingAuditAsync");
    expect(bridge!.content).not.toContain("TransportMappers");

    // MapAll aggregator
    const mapAll = directUnitEmitted.find((e) =>
      e.path.includes("AuditBridgeRegistrations.g.cs"),
    );
    expect(mapAll).toBeDefined();
    expect(mapAll!.content).toContain("MapAllAuditBridges()");

    // No in-process route registration for this op
    expect(
      directUnitEmitted.filter((e) =>
        e.path.includes("PingAuditRouteRegistration.g.cs"),
      ),
    ).toHaveLength(0);

    // gRPC server still emitted
    const grpcSvc = directUnitEmitted.find((e) =>
      e.path.includes("AuditSvcService.g.cs"),
    );
    expect(grpcSvc).toBeDefined();
  });
});

describe("Emitter_Standalone_MissingBridgeNs_FailLoud", () => {
  it("standalone bridge + missing bridge ns → missing-bridge-namespace", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("BInput", { id: str });
    const outputModel = makeModel("BOutput", { data: str });
    const op = makeWrappedOp("bridgeMissingNs", inputModel, outputModel);

    mockVerbMap.set(op, "post");
    mockHttpOpResult = [{ path: "/b" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["x"]]]);
    const servedBy = new Map<object, unknown>([[op, "Audit"]]);
    const grpcMethod = new Map<object, unknown>([
      [op, { service: "A", method: "B", streaming: "unary" }],
    ]);
    const fieldMap = makeFieldMap(inputModel, outputModel);
    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_GRPC_METHOD_KEY) return grpcMethod;
      if (key === D2_FIELD_KEY) return fieldMap;
      return new Map();
    });

    const codes = await captureDiagnostics(async () => {
      await $onEmit(
        makeBaseContext(program, {
          ...REAL_BASE,
          "process-kind-by-module": { Audit: "standalone" },
        }),
      );
    });

    expect(codes).toContain("missing-bridge-namespace");
    expect(
      directUnitEmitted.filter((e) => e.path.includes("BridgeRegistration")),
    ).toHaveLength(0);
  });
});

describe("Emitter_Standalone_RouteWithoutGrpc_FailLoud", () => {
  it("standalone + route without grpc → standalone-route-requires-grpc", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("SInput", { id: str });
    const outputModel = makeModel("SOutput", { data: str });
    const op = makeWrappedOp("standaloneNoGrpc", inputModel, outputModel);

    mockVerbMap.set(op, "get");
    mockHttpOpResult = [{ path: "/s" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["x"]]]);
    const servedBy = new Map<object, unknown>([[op, "Audit"]]);
    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      return new Map();
    });

    const codes = await captureDiagnostics(async () => {
      await $onEmit(
        makeBaseContext(program, {
          ...REAL_BASE,
          "process-kind-by-module": { Audit: "standalone" },
          "csharp-bridge-namespace": {
            Audit: "D2.Edge.Api.Bridges.Audit",
          },
        }),
      );
    });

    expect(codes).toContain("standalone-route-requires-grpc");
    expect(
      directUnitEmitted.filter((e) =>
        e.path.includes("RouteRegistration.g.cs"),
      ),
    ).toHaveLength(0);
    expect(
      directUnitEmitted.filter((e) => e.path.includes("BridgeRegistration")),
    ).toHaveLength(0);
  });
});

// Fail-without-fix (§2.3): without buildIdempotencyGate weave +
// idempotentNamespaces.add(bridgeNs) on the standalone bridge path, this
// suite would not observe D2GeneratedIdempotencyStore / Idempotency-Key /
// TryGetAsync / StoreAsync under the bridge registration namespace.

describe("Emitter_Standalone_Idempotent_WeavesGateAndSeam", () => {
  it("standalone + @d2Idempotent → bridge gate + D2GeneratedIdempotencyStore seam", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("CreateEventInput", { id: str });
    const outputModel = makeModel("CreateEventOutput", { ok: str });
    const op = makeWrappedOp("createAuditEvent", inputModel, outputModel);

    mockVerbMap.set(op, "post");
    mockHttpOpResult = [{ path: "/internal/v1/audit/events" }, []];

    const anyScopes = new Map<object, unknown>([
      [op, ["internal.audit.write"]],
    ]);
    const servedBy = new Map<object, unknown>([[op, "Audit"]]);
    const commandMap = new Map<object, unknown>([[op, true]]);
    const grpcMethod = new Map<object, unknown>([
      [op, { service: "AuditSvc", method: "CreateEvent", streaming: "unary" }],
    ]);
    const idempotent = new Map<object, unknown>([
      [
        op,
        {
          keySource: "header",
          ttlSeconds: 86400,
          fields: [],
        },
      ],
    ]);
    const fieldMap = makeFieldMap(inputModel, outputModel);
    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_COMMAND_KEY) return commandMap;
      if (key === D2_GRPC_METHOD_KEY) return grpcMethod;
      if (key === D2_IDEMPOTENT_KEY) return idempotent;
      if (key === D2_FIELD_KEY) return fieldMap;
      return new Map();
    });

    await $onEmit(
      makeBaseContext(program, {
        ...REAL_BASE,
        "csharp-clients-namespace": "D2.Services.Audit.Client",
        "process-kind-by-module": { Audit: "standalone" },
        "csharp-bridge-namespace": {
          Audit: "D2.Edge.Api.Bridges.Audit",
        },
      }),
    );

    const bridge = directUnitEmitted.find((e) =>
      e.path.includes("CreateAuditEventBridgeRegistration.g.cs"),
    );
    expect(bridge).toBeDefined();
    expect(bridge!.content).toContain("D2GeneratedIdempotencyStore store");
    expect(bridge!.content).toContain('Headers["Idempotency-Key"]');
    expect(bridge!.content).toContain("client.CreateAuditEventAsync");

    const seam = directUnitEmitted.find((e) =>
      e.path.includes("D2GeneratedIdempotencyStore.g.cs"),
    );
    expect(seam).toBeDefined();
    expect(seam!.content).toContain("namespace D2.Edge.Api.Bridges.Audit;");
  });
});

// ---------------------------------------------------------------------------
// Fixture mode still works without process-kind
// ---------------------------------------------------------------------------

describe("Emitter_FixtureMode_WithoutProcessKind_StillEmits", () => {
  it("fixture options without process-kind still emit routes", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("FixInput", { id: str });
    const outputModel = makeModel("FixOutput", { data: str });
    const op = makeWrappedOp("fixtureRoute", inputModel, outputModel);

    mockVerbMap.set(op, "post");
    mockHttpOpResult = [{ path: "/fix" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.write"]]]);
    const servedBy = new Map<object, unknown>([[op, "SignFixture"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);
    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      return new Map();
    });

    await $onEmit(
      makeBaseContext(program, {
        "csharp-namespace": "D2.Test.Route",
        "grpc-service-namespace": "D2.Test.Grpc",
        "proto-package": "d2.test.v1",
        "proto-csharp-namespace": "D2.Test.Protos.V1",
      }),
    );

    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("FixtureRouteRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
    expect(routeFile!.content).toContain("namespace D2.Test.Grpc;");
  });
});
