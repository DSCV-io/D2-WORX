// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Direct-unit tests for the emitRouteIfPresent path in src/emitter.ts.
//
// V8 coverage of emitter.ts lines 627-679 and 703-731 requires calling $onEmit
// with mocked @typespec/compiler + @typespec/http so the route-emission branch
// executes against the TS source (not the dist/).
//
// Covers:
//   - supportedVerb + requireAnyScope + façade delegation → route file emitted
//   - unsupported verb (@head) → D2TSP005 diagnostic, no route file
//   - no auth intent → D2TSP004 diagnostic, no route file
//   - getHttpOperation returning error diagnostic → surfaced via unmapped-scalar
//   - requireAllScopes branch
//   - harmless branch
//   - rateTier + csrf markers present → included in emitted file
//   - handler delegation (not @d2InProcess)
//   - fixture mode vs real-module mode delegationTarget branches (703-731)

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
  D2_REQUIRE_ALL_SCOPES_KEY,
  D2_HARMLESS_KEY,
  D2_RATE_LIMIT_TIER_KEY,
  D2_CSRF_KEY,
  D2_IDEMPOTENT_KEY,
  D2_RESILIENCE_KEY,
  D2_RESILIENCE_RETRY_WHEN_KEY,
  D2_RESILIENCE_FAIL_WHEN_KEY,
  D2_FIELD_KEY,
} from "@d2/typespec-decorators";

// ---------------------------------------------------------------------------
// Module-level mock state — vi.mock is hoisted; factories cannot close over
// variables declared inside tests. We use mutable module-level containers.
// ---------------------------------------------------------------------------

// Operations fed to the mocked navigateProgram.
const directUnitOps: Operation[] = [];

// Files written by the mocked emitFile.
const directUnitEmitted: Array<{ path: string; content: string }> = [];

// Mutable return values for the @typespec/http mock.
// getOperationVerb returns the value stored per op here (or undefined for no verb).
const mockVerbMap = new Map<object, string | undefined>();

// getHttpOperation returns this value (default: { path: "/test/path" } with no diags).
let mockHttpOpResult: [
  { path: string },
  Array<{ severity: string; message: string }>,
] = [{ path: "/test/path" }, []];

// ---------------------------------------------------------------------------
// @typespec/compiler mock: navigateProgram + emitFile.
// ---------------------------------------------------------------------------

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

// ---------------------------------------------------------------------------
// @typespec/http mock: getOperationVerb + getHttpOperation.
// ---------------------------------------------------------------------------

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

// Import AFTER mock registrations so the module under test uses the mocks.
const { $onEmit } = await import("../src/emitter.js");

afterEach(() => {
  directUnitOps.length = 0;
  directUnitEmitted.length = 0;
  mockVerbMap.clear();
  mockHttpOpResult = [{ path: "/test/path" }, []];
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

/**
 * Build a D2_FIELD_KEY state-map entry for all properties of the given models,
 * assigning sequential 1-based field numbers per model in property-declaration order.
 * Used by direct-unit tests to satisfy the `@d2Field` pinning requirement so
 * D2TSP009 does not fire for mock proto-bound models.
 */
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

// ---------------------------------------------------------------------------
// Shared emitter options for fixture mode (no csAppNamespaceBase).
// ---------------------------------------------------------------------------

const FIXTURE_OPTS = {
  "csharp-namespace": "D2.Test.Route",
  "grpc-service-namespace": "D2.Test.Grpc",
  "proto-package": "d2.test.v1",
  "proto-csharp-namespace": "D2.Test.Protos.V1",
};

// ---------------------------------------------------------------------------
// Test: supported verb + requireAnyScope + façade delegation → route emitted
// (covers lines 627-679 supportedVerbs, scopePolicy any, then 697-714 façade)
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_SupportedVerbFacade", () => {
  it("post verb + requireAnyScope + @d2InProcess → route file with MapPost + RequireAnyScope", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("SignInput", { kid: str });
    const outputModel = makeModel("SignOutput", { signature: str });
    const op = makeWrappedOp("sign", inputModel, outputModel);

    // Wire verb + state maps.
    mockVerbMap.set(op, "post");
    mockHttpOpResult = [{ path: "/internal/v1/sample/sign" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.write"]]]);
    const servedBy = new Map<object, unknown>([[op, "Sample"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      return new Map();
    });

    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("SignRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
    expect(routeFile!.content).toContain("MapPost");
    expect(routeFile!.content).toContain('"/internal/v1/sample/sign"');
    expect(routeFile!.content).toContain("RequireAnyScope");
    expect(routeFile!.content).toContain('"self.write"');
    // Fixture façade delegation.
    expect(routeFile!.content).toContain("ISampleSignerFacade");
    expect(routeFile!.content).toContain("SignAsync");
  });
});

// ---------------------------------------------------------------------------
// Test: unsupported verb (@head) → D2TSP005, no route emitted (line 628-634)
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_UnsupportedVerb", () => {
  it("head verb → D2TSP005 diagnostic reported, no route file emitted", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("HeadInput", { id: str });
    const outputModel = makeModel("HeadOutput", { exists: str });
    const op = makeWrappedOp("headCheck", inputModel, outputModel);

    mockVerbMap.set(op, "head"); // unsupported

    const anyScopes = new Map<object, unknown>([[op, ["self.read"]]]);
    directUnitOps.push(op);

    const reportedCodes: string[] = [];
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => {
        reportedCodes.push(diag.code);
      },
    );

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      return new Map();
    });

    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    expect(reportedCodes).toContain("unsupported-http-verb");
    expect(
      directUnitEmitted.filter((e) =>
        e.path.includes("RouteRegistration.g.cs"),
      ),
    ).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Test: no auth intent → D2TSP004 (lines 666-674)
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_MissingAuthIntent", () => {
  it("routed op with no @d2RequireAnyScope/@d2RequireAllScopes/@d2Harmless → D2TSP004", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("NoAuthInput", { id: str });
    const outputModel = makeModel("NoAuthOutput", { result: str });
    const op = makeWrappedOp("noAuth", inputModel, outputModel);

    mockVerbMap.set(op, "post");

    directUnitOps.push(op);

    const reportedCodes: string[] = [];
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => {
        reportedCodes.push(diag.code);
      },
    );

    const program = makeMockProgram((_key: symbol) => new Map());
    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    expect(reportedCodes).toContain("route-missing-auth-intent");
    expect(
      directUnitEmitted.filter((e) =>
        e.path.includes("RouteRegistration.g.cs"),
      ),
    ).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Test: getHttpOperation returns error diagnostic → surfaced (lines 641-649)
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_HttpOpError", () => {
  it("getHttpOperation error diagnostic → unmapped-scalar diagnostic reported", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("ErrInput", { id: str });
    const outputModel = makeModel("ErrOutput", { data: str });
    const op = makeWrappedOp("errOp", inputModel, outputModel);

    mockVerbMap.set(op, "get");
    mockHttpOpResult = [
      { path: "/err" },
      [{ severity: "error", message: "test HTTP error" }],
    ];

    const anyScopes = new Map<object, unknown>([[op, ["self.read"]]]);
    directUnitOps.push(op);

    const reportedCodes: string[] = [];
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => {
        reportedCodes.push(diag.code);
      },
    );

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      return new Map();
    });

    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    expect(reportedCodes).toContain("unmapped-scalar");
  });
});

// ---------------------------------------------------------------------------
// Test: requireAllScopes branch (lines 662-663)
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_RequireAllScopes", () => {
  it("@d2RequireAllScopes op → RequireAllScopes in emitted route", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("AllScopesInput", { id: str });
    const outputModel = makeModel("AllScopesOutput", { data: str });
    const op = makeWrappedOp("allScopes", inputModel, outputModel);

    mockVerbMap.set(op, "get");
    mockHttpOpResult = [{ path: "/all-scopes" }, []];

    const allScopes = new Map<object, unknown>([
      [op, ["self.read", "self.write"]],
    ]);
    const servedBy = new Map<object, unknown>([[op, "Sample"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ALL_SCOPES_KEY) return allScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      return new Map();
    });

    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("AllScopesRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
    expect(routeFile!.content).toContain("RequireAllScopes");
    expect(routeFile!.content).toContain('"self.read"');
    expect(routeFile!.content).toContain('"self.write"');
    expect(routeFile!.content).not.toContain("RequireAnyScope");
  });
});

// ---------------------------------------------------------------------------
// Test: harmless branch (lines 664-665)
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_Harmless", () => {
  it("@d2Harmless op → MarkAsD2HarmlessEndpoint in emitted route", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("HealthInput", {});
    const outputModel = makeModel("HealthOutput", { status: str });
    const op = makeWrappedOp("health", inputModel, outputModel);

    mockVerbMap.set(op, "get");
    mockHttpOpResult = [{ path: "/healthz" }, []];

    const harmlessMap = new Map<object, unknown>([[op, true]]);
    const servedBy = new Map<object, unknown>([[op, "Edge"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_HARMLESS_KEY) return harmlessMap;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      return new Map();
    });

    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("HealthRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
    expect(routeFile!.content).toContain("MarkAsD2HarmlessEndpoint");
    expect(routeFile!.content).not.toContain("RequireAnyScope");
  });
});

// ---------------------------------------------------------------------------
// Test: rateTier + csrf markers (lines 677-687)
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_Markers", () => {
  it("rateTier + csrf → D2GeneratedRateLimitTier + D2GeneratedCsrfPosture in emitted file", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("MarkedInput", { id: str });
    const outputModel = makeModel("MarkedOutput", { data: str });
    const op = makeWrappedOp("markedOp", inputModel, outputModel);

    mockVerbMap.set(op, "post");
    mockHttpOpResult = [{ path: "/marked" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.write"]]]);
    const servedBy = new Map<object, unknown>([[op, "Edge"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);
    const rateTier = new Map<object, unknown>([[op, { tier: "Standard" }]]);
    const csrf = new Map<object, unknown>([[op, { posture: "exempt" }]]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      if (key === D2_RATE_LIMIT_TIER_KEY) return rateTier;
      if (key === D2_CSRF_KEY) return csrf;
      return new Map();
    });

    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("MarkedOpRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
    expect(routeFile!.content).toContain("D2GeneratedRateLimitTier");
    expect(routeFile!.content).toContain("D2GeneratedCsrfPosture");
  });

  it("rateTier as plain string (not object) → handled correctly (677-680 string branch)", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("StringTierInput", { id: str });
    const outputModel = makeModel("StringTierOutput", { data: str });
    const op = makeWrappedOp("stringTier", inputModel, outputModel);

    mockVerbMap.set(op, "get");
    mockHttpOpResult = [{ path: "/string-tier" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.read"]]]);
    const servedBy = new Map<object, unknown>([[op, "Edge"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);
    // Plain string (not { tier: "..." }) — exercises the `typeof === "string"` branch.
    const rateTier = new Map<object, unknown>([[op, "Premium"]]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      if (key === D2_RATE_LIMIT_TIER_KEY) return rateTier;
      return new Map();
    });

    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("StringTierRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
    expect(routeFile!.content).toContain("D2GeneratedRateLimitTier");
    expect(routeFile!.content).toContain('"Premium"');
  });
});

// ---------------------------------------------------------------------------
// Test: handler delegation (not @d2InProcess) → lines 715-727
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_HandlerDelegation", () => {
  it("op WITHOUT @d2InProcess → handler delegation (I<Op>Handler, HandleAsync)", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("DirectInput", { id: str });
    const outputModel = makeModel("DirectOutput", { result: str });
    const op = makeWrappedOp("directOp", inputModel, outputModel);

    mockVerbMap.set(op, "post");
    mockHttpOpResult = [{ path: "/direct" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.write"]]]);
    // No @d2InProcess set.

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      return new Map();
    });

    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("DirectOpRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
    expect(routeFile!.content).toContain("IDirectOpHandler");
    expect(routeFile!.content).toContain("HandleAsync");
    expect(routeFile!.content).not.toContain("Facade");
  });
});

// ---------------------------------------------------------------------------
// Test: real-module mode (csAppNamespaceBase set) → lines 703-714 true branch
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_RealModuleFacade", () => {
  it("@d2InProcess + csAppNamespaceBase set → ISampleApi (real-module path)", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("RealInput", { id: str });
    const outputModel = makeModel("RealOutput", { data: str });
    const op = makeWrappedOp("realOp", inputModel, outputModel);

    mockVerbMap.set(op, "post");
    mockHttpOpResult = [{ path: "/real/op" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.write"]]]);
    const servedBy = new Map<object, unknown>([[op, "Sample"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);
    const commandMap = new Map<object, unknown>([[op, true]]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      if (key === D2_COMMAND_KEY) return commandMap;
      return new Map();
    });

    // Real-module mode: clients-ns + app-base + process-kind + routes-ns.
    const ctx = makeBaseContext(program, {
      "csharp-namespace": "D2.Test.Route",
      "csharp-app-namespace-base": "D2.Test.App.Handlers",
      "csharp-clients-namespace": "D2.Sample.Clients",
      "grpc-service-namespace": "D2.Test.Grpc",
      "proto-package": "d2.test.v1",
      "proto-csharp-namespace": "D2.Test.Protos.V1",
      "process-kind-by-module": { Sample: "edge-module" },
      "csharp-routes-namespace": {
        Sample: "D2.Edge.Api.Routes.Sample",
      },
    });
    await $onEmit(ctx);

    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("RealOpRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
    // In real-module mode, the façade type is I<ServedBy>Api.
    expect(routeFile!.content).toContain("ISampleApi");
    expect(routeFile!.content).not.toContain("SignerFacade");
    expect(routeFile!.content).toContain(
      "namespace D2.Edge.Api.Routes.Sample;",
    );
  });
});

// ---------------------------------------------------------------------------
// Test: csrf as plain string (line 686 string branch)
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_CsrfStringBranch", () => {
  it("csrfRaw as plain string (not object) → handled correctly (line 686 string branch)", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("CsrfStrInput", { id: str });
    const outputModel = makeModel("CsrfStrOutput", { data: str });
    const op = makeWrappedOp("csrfStr", inputModel, outputModel);

    mockVerbMap.set(op, "post");
    mockHttpOpResult = [{ path: "/csrf-str" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.write"]]]);
    const servedBy = new Map<object, unknown>([[op, "Edge"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);
    // csrf as plain string (not { posture: "..." }).
    const csrf = new Map<object, unknown>([[op, "required"]]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      if (key === D2_CSRF_KEY) return csrf;
      return new Map();
    });

    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("CsrfStrRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
    expect(routeFile!.content).toContain("D2GeneratedCsrfPosture");
    expect(routeFile!.content).toContain('"required"');
  });
});

// ---------------------------------------------------------------------------
// Test: handler delegation with csAppNamespaceBase but category=undefined → line 726 fallback
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_HandlerCategoryUndefined", () => {
  it("handler delegation + csAppNamespaceBase + no @d2Command/@d2Query → grpcServiceNs fallback (line 726)", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("NoCatInput", { id: str });
    const outputModel = makeModel("NoCatOutput", { data: str });
    const op = makeWrappedOp("noCat", inputModel, outputModel);

    mockVerbMap.set(op, "get");
    mockHttpOpResult = [{ path: "/no-cat" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.read"]]]);
    // No @d2InProcess → handler delegation. No @d2Command/@d2Query → category=undefined.

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      return new Map();
    });

    // Provide csAppNamespaceBase so the `csAppNamespaceBase !== undefined` branch is true,
    // but category is undefined → falls back to grpcServiceNs.
    const ctx = makeBaseContext(program, {
      "csharp-namespace": "D2.Test.Route",
      "csharp-app-namespace-base": "D2.Test.App.Handlers",
      "grpc-service-namespace": "D2.Test.Grpc",
      "proto-package": "d2.test.v1",
      "proto-csharp-namespace": "D2.Test.Protos.V1",
    });
    await $onEmit(ctx);

    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("NoCatRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
    expect(routeFile!.content).toContain("INoCatHandler");
  });
});

// ---------------------------------------------------------------------------
// Test: inputModel undefined → fallback name used (lines 735-738 ?? branch)
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_UndefinedInputModel", () => {
  it("op with no parameters (inputModel=undefined) → fallback <PascalOp>Input name used", async () => {
    // Op has NO parameters — inputModel stays undefined inside $onEmit.
    // This exercises the `(inputModel?.name?.length ?? 0) > 0` false branch on line 735.
    const str = makeStringScalar();
    const outputModel = makeModel("NoParamsOutput", { data: str });

    const op = {
      name: "noParams",
      // parameters intentionally omitted — rawParams becomes undefined
      returnType: outputModel,
      node: undefined,
    } as unknown as Operation;

    mockVerbMap.set(op, "get");
    mockHttpOpResult = [{ path: "/no-params" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.read"]]]);
    const servedBy = new Map<object, unknown>([[op, "Edge"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      return new Map();
    });

    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("NoParamsRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
    // inputModel was undefined → fallback name "NoParamsInput" used.
    expect(routeFile!.content).toContain("NoParamsInput");
  });
});

// ---------------------------------------------------------------------------
// Test: getHttpOperation returns warning (not error) diagnostic → line 642 false branch
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_HttpOpWarning", () => {
  it("getHttpOperation warning-severity diagnostic → NOT reported via unmapped-scalar", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("WarnInput", { id: str });
    const outputModel = makeModel("WarnOutput", { data: str });
    const op = makeWrappedOp("warnOp", inputModel, outputModel);

    mockVerbMap.set(op, "get");
    // Warning diagnostic (not error) — hits the `if (d.severity === "error")` false branch.
    mockHttpOpResult = [
      { path: "/warn" },
      [{ severity: "warning", message: "test warning" }],
    ];

    const anyScopes = new Map<object, unknown>([[op, ["self.read"]]]);
    const servedBy = new Map<object, unknown>([[op, "Edge"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);

    directUnitOps.push(op);

    const reportedCodes: string[] = [];
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => {
        reportedCodes.push(diag.code);
      },
    );

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      return new Map();
    });

    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    // Warning-severity should NOT fire unmapped-scalar (only errors do).
    expect(reportedCodes).not.toContain("unmapped-scalar");
    // Route still emitted (warning doesn't abort route emission).
    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("WarnOpRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Test: handler delegation + csAppNamespaceBase + category DEFINED → line 724 true branch
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_HandlerCategoryDefined", () => {
  it("handler delegation + csAppNamespaceBase + @d2Command → <base>.Commands.<Op> namespace (line 725)", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("CatInput", { id: str });
    const outputModel = makeModel("CatOutput", { data: str });
    const op = makeWrappedOp("catOp", inputModel, outputModel);

    mockVerbMap.set(op, "post");
    mockHttpOpResult = [{ path: "/cat" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.write"]]]);
    const commandMap = new Map<object, unknown>([[op, true]]);
    // No @d2InProcess → handler delegation.

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_COMMAND_KEY) return commandMap;
      return new Map();
    });

    // Real-module mode with csAppNamespaceBase + @d2Command → namespace includes "Commands".
    const ctx = makeBaseContext(program, {
      "csharp-namespace": "D2.Test.Route",
      "csharp-app-namespace-base": "D2.Test.App.Handlers",
      "grpc-service-namespace": "D2.Test.Grpc",
      "proto-package": "d2.test.v1",
      "proto-csharp-namespace": "D2.Test.Protos.V1",
    });
    await $onEmit(ctx);

    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("CatOpRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
    expect(routeFile!.content).toContain("ICatOpHandler");
    // Namespace should include "Commands" (category resolved).
    expect(routeFile!.content).toContain("Commands");
  });
});

// ---------------------------------------------------------------------------
// Test: outputModel undefined → fallback <PascalOp>Output name (line 738 ?? branch)
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_UndefinedOutputModel", () => {
  it("op returning void (outputModel=undefined) → fallback <PascalOp>Output name (line 738)", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("VoidRetInput", { id: str });

    // Op with a void return type — outputModel will be undefined inside $onEmit.
    const inputProp = {
      type: inputModel,
      optional: false,
    } as unknown as ModelProperty;
    const wrappedParams = {
      kind: "Model",
      name: "",
      properties: new Map<string, ModelProperty>([["input", inputProp]]),
    } as unknown as Model;

    const op = {
      name: "voidRet",
      parameters: wrappedParams,
      returnType: { kind: "Intrinsic", name: "void" },
      node: undefined,
    } as unknown as Operation;

    mockVerbMap.set(op, "get");
    mockHttpOpResult = [{ path: "/void-ret" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.read"]]]);
    const servedBy = new Map<object, unknown>([[op, "Edge"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      return new Map();
    });

    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("VoidRetRouteRegistration.g.cs"),
    );
    // Route is emitted even with void return (outputModel fallback ran without error).
    expect(routeFile).toBeDefined();
    // inputTypeName is used in the lambda signature.
    expect(routeFile!.content).toContain("VoidRetInput");
  });
});

// ---------------------------------------------------------------------------
// Test: gRPC real-module façade branch (emitter.ts lines 253-259)
//
// The branch fires when:
//   - op has @d2GrpcMethod (grpcPayload !== undefined)
//   - op has @d2InProcess (grpcInProcess === true)
//   - op has @d2ServedBy (grpcServedBy is a non-empty string)
//   - csAppNamespaceBase AND csClientsNamespace are BOTH configured (real-module mode)
//
// When those four conditions hold, the gRPC service uses I<ServedBy>Api
// (the production façade type in the Clients namespace) instead of the fixture
// I<ServedBy>SignerFacade. This is the only branch in $onEmit that is not
// covered by fixture-mode tests (V8 integration compiles give no src credit).
// ---------------------------------------------------------------------------

describe("$onEmit_grpcDirect_RealModuleFacadeBranch_EmitterLines253To259", () => {
  it("@d2GrpcMethod + @d2InProcess + real-module options → gRPC service emitted with I<ServedBy>Api", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("GrpcInput", { kid: str });
    const outputModel = makeModel("GrpcOutput", { signature: str });
    const op = makeWrappedOp("grpcSign", inputModel, outputModel);

    // This op has no HTTP verb → route emitter is skipped; only gRPC fires.
    // mockVerbMap has no entry for this op → getOperationVerb returns undefined.

    const commandMap = new Map<object, unknown>([[op, true]]);
    const servedBy = new Map<object, unknown>([[op, "Sample"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);
    // grpcPayload must be defined so the gRPC block fires.
    const grpcMethod = new Map<object, unknown>([
      [op, { service: "Svc", method: "Do", streaming: "unary" }],
    ]);
    // Field-number pins for all mock model properties so D2TSP009 does not fire.
    const fieldMap = makeFieldMap(inputModel, outputModel);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_COMMAND_KEY) return commandMap;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      if (key === D2_GRPC_METHOD_KEY) return grpcMethod;
      if (key === D2_FIELD_KEY) return fieldMap;
      return new Map();
    });

    // Real-module mode: BOTH csAppNamespaceBase AND csClientsNamespace configured.
    // This exercises the TRUE branch of the ternary on lines 253-258.
    const ctx = makeBaseContext(program, {
      "csharp-namespace": "D2.Test.Ns",
      "csharp-app-namespace-base": "D2.Test.App.Handlers",
      "csharp-clients-namespace": "D2.Sample.Clients",
      "proto-package": "d2.test.v1",
      "proto-csharp-namespace": "D2.Test.Protos.V1",
      "grpc-service-namespace": "D2.Test.Grpc",
    });
    await $onEmit(ctx);

    // The gRPC service file must use I<ServedBy>Api (real-module façade path).
    const grpcFile = directUnitEmitted.find((e) =>
      e.path.includes("SvcService.g.cs"),
    );
    expect(grpcFile).toBeDefined();
    // Real-module branch: typeName = I${grpcServedBy}Api = ISampleApi.
    expect(grpcFile!.content).toContain("ISampleApi");
    // targetNamespace = csClientsNamespace = "D2.Sample.Clients".
    expect(grpcFile!.content).toContain("D2.Sample.Clients");
    // Method name = GrpcSignAsync (PascalOp from op.name = "grpcSign").
    expect(grpcFile!.content).toContain("GrpcSignAsync");
    // Must NOT use the fixture naming.
    expect(grpcFile!.content).not.toContain("SignerFacade");
  });
});

// ---------------------------------------------------------------------------
// Test: gRPC fixture-mode façade branch (emitter.ts lines 253-259 FALSE branch)
//
// Exercises the FALSE branch of the ternaries on lines 253-258:
//   csAppNamespaceBase === undefined (fixture mode) → I<ServedBy>SignerFacade
//   targetNamespace = `${grpcServiceNs}.Facade`
// ---------------------------------------------------------------------------

describe("$onEmit_grpcDirect_FixtureFacadeBranch_EmitterLines253To259", () => {
  it("@d2GrpcMethod + @d2InProcess + fixture-mode options → gRPC service emitted with I<ServedBy>SignerFacade", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("FixGrpcInput", { kid: str });
    const outputModel = makeModel("FixGrpcOutput", { sig: str });
    const op = makeWrappedOp("fixGrpc", inputModel, outputModel);

    const servedBy = new Map<object, unknown>([[op, "Sample"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);
    const grpcMethod = new Map<object, unknown>([
      [op, { service: "FixSvc", method: "FixDo", streaming: "unary" }],
    ]);
    const commandMap = new Map<object, unknown>([[op, true]]);
    // Field-number pins for all mock model properties so D2TSP009 does not fire.
    const fieldMap = makeFieldMap(inputModel, outputModel);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_COMMAND_KEY) return commandMap;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      if (key === D2_GRPC_METHOD_KEY) return grpcMethod;
      if (key === D2_FIELD_KEY) return fieldMap;
      return new Map();
    });

    // Fixture mode: NO csAppNamespaceBase and NO csClientsNamespace configured.
    // This exercises the FALSE branch of the ternaries on lines 253-258:
    //   facadeTypeName = I${grpcServedBy}SignerFacade = ISampleSignerFacade
    //   facadeNs       = `${grpcServiceNs}.Facade`
    const ctx = makeBaseContext(program, {
      "csharp-namespace": "D2.Test.Ns",
      "proto-package": "d2.test.v1",
      "proto-csharp-namespace": "D2.Test.Protos.V1",
      "grpc-service-namespace": "D2.Test.Grpc",
    });
    await $onEmit(ctx);

    // The gRPC service file must use the fixture façade naming.
    const grpcFile = directUnitEmitted.find((e) =>
      e.path.includes("FixSvcService.g.cs"),
    );
    expect(grpcFile).toBeDefined();
    // Fixture mode: I<ServedBy>SignerFacade = ISampleSignerFacade.
    expect(grpcFile!.content).toContain("ISampleSignerFacade");
    // targetNamespace = "D2.Test.Grpc.Facade".
    expect(grpcFile!.content).toContain("D2.Test.Grpc.Facade");
    // Must NOT use the real-module naming.
    expect(grpcFile!.content).not.toContain("ISampleApi");
  });
});

// ---------------------------------------------------------------------------
// Test: D2TSP006 — @d2Idempotent on op WITHOUT @route → error diagnostic
//
// emitter.ts lines 690-695: when idempotentPayload is present but explicitVerb
// is undefined (no @route), D2TSP006 (idempotent-requires-route) fires.
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_IdempotentWithoutRoute_D2TSP006", () => {
  it("@d2Idempotent on op with no verb → D2TSP006 idempotent-requires-route fired", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("IdempNoRouteInput", { kid: str });
    const outputModel = makeModel("IdempNoRouteOutput", { result: str });
    const op = makeWrappedOp("idempNoRoute", inputModel, outputModel);

    // No mockVerbMap entry for this op → getOperationVerb returns undefined (no @route).
    // idempotentPayload IS set → should trigger D2TSP006 and return.
    const idempotentMap = new Map<object, unknown>([
      [op, { keySource: "header", ttlSeconds: 86400, fields: [] }],
    ]);

    directUnitOps.push(op);

    const reportedCodes: string[] = [];
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => {
        reportedCodes.push(diag.code);
      },
    );

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_IDEMPOTENT_KEY) return idempotentMap;
      return new Map();
    });

    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    // D2TSP006 must have fired.
    expect(reportedCodes).toContain("idempotent-requires-route");
    // No route file emitted (early return after D2TSP006).
    expect(
      directUnitEmitted.filter((e) =>
        e.path.includes("RouteRegistration.g.cs"),
      ),
    ).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Test: @d2Idempotent (header) on op WITH @route → gate config built + seam emitted
//
// emitter.ts lines 825-836: when idempotentPayload !== undefined AND explicitVerb
// is defined, the idempotency config is built, namespace tracked, and
// D2GeneratedIdempotencyStore.g.cs is emitted once for the namespace.
// ---------------------------------------------------------------------------

describe("$onEmit_routeEmitDirect_IdempotentWithRoute_SeamAndGate", () => {
  it("@d2Idempotent('header', 86400) + @post route → idempotency config built + seam emitted", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("IdempSignInput", { kid: str });
    const outputModel = makeModel("IdempSignOutput", { signature: str });
    const op = makeWrappedOp("idempSign", inputModel, outputModel);

    mockVerbMap.set(op, "post");
    mockHttpOpResult = [{ path: "/internal/v1/sample/sign" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.write"]]]);
    const servedBy = new Map<object, unknown>([[op, "Sample"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);
    const idempotentMap = new Map<object, unknown>([
      [op, { keySource: "header", ttlSeconds: 86400, fields: [] }],
    ]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      if (key === D2_IDEMPOTENT_KEY) return idempotentMap;
      return new Map();
    });

    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    // Route registration contains the idempotency gate.
    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("IdempSignRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
    expect(routeFile!.content).toContain("D2GeneratedIdempotencyStore store");
    expect(routeFile!.content).toContain("TryGetAsync");
    expect(routeFile!.content).toContain("StoreAsync");
    expect(routeFile!.content).toContain("Idempotency-Key");

    // Seam file emitted for the namespace.
    const seamFile = directUnitEmitted.find((e) =>
      e.path.includes("D2GeneratedIdempotencyStore.g.cs"),
    );
    expect(seamFile).toBeDefined();
    expect(seamFile!.content).toContain("D2GeneratedIdempotencyStore");
    expect(seamFile!.content).toContain("TryGetAsync");
    expect(seamFile!.content).toContain("StoreAsync");
  });

  it("@d2Idempotent('derived', 3600, 'kid') + @post route → Pascal field mapping + derived key gate", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("IdempDerivedInput", { kid: str });
    const outputModel = makeModel("IdempDerivedOutput", { signature: str });
    const op = makeWrappedOp("idempDerived", inputModel, outputModel);

    mockVerbMap.set(op, "post");
    mockHttpOpResult = [{ path: "/internal/v1/sample/sign-derived" }, []];

    const anyScopes = new Map<object, unknown>([[op, ["self.write"]]]);
    const servedBy = new Map<object, unknown>([[op, "Sample"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);
    const idempotentMap = new Map<object, unknown>([
      [op, { keySource: "derived", ttlSeconds: 3600, fields: ["kid"] }],
    ]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_REQUIRE_ANY_SCOPE_KEY) return anyScopes;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      if (key === D2_IDEMPOTENT_KEY) return idempotentMap;
      return new Map();
    });

    const ctx = makeBaseContext(program, FIXTURE_OPTS);
    await $onEmit(ctx);

    // Route contains SHA256 derived key logic and PascalCase 'Kid' field access.
    const routeFile = directUnitEmitted.find((e) =>
      e.path.includes("IdempDerivedRouteRegistration.g.cs"),
    );
    expect(routeFile).toBeDefined();
    expect(routeFile!.content).toContain("SHA256");
    expect(routeFile!.content).toContain("Kid");
    expect(routeFile!.content).toContain("StoreAsync");

    // Seam emitted.
    const seamFile = directUnitEmitted.find((e) =>
      e.path.includes("D2GeneratedIdempotencyStore.g.cs"),
    );
    expect(seamFile).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Test: a mixed-primitive union DTO field → D2TSP007 via the DTO onError path
// (covers the emitter.ts unsupported-union-shape branch in emitDtoPair).
// ---------------------------------------------------------------------------

describe("$onEmit_unionShape_DtoPath_D2TSP007", () => {
  it("an in-process op with a string|int32 union field → unsupported-union-shape diagnostic, no DTO file", async () => {
    const mixedUnion = {
      kind: "Union",
      variants: new Map<string | symbol, { type: unknown }>([
        [Symbol("a"), { type: { kind: "Scalar", name: "string" } }],
        [Symbol("b"), { type: { kind: "Scalar", name: "int32" } }],
      ]),
    } as unknown as Model;
    const inputModel = makeModel("BadInput", {
      mixed: mixedUnion,
    });
    const outputModel = makeModel("BadOutput", { ok: makeStringScalar() });
    const op = makeWrappedOp("badUnion", inputModel, outputModel);

    const servedBy = new Map<object, unknown>([[op, "X"]]);
    const inProcess = new Map<object, unknown>([[op, true]]);
    const command = new Map<object, unknown>([[op, true]]);
    directUnitOps.push(op);

    const reportedCodes: string[] = [];
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => {
        reportedCodes.push(diag.code);
      },
    );

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_IN_PROCESS_KEY) return inProcess;
      if (key === D2_COMMAND_KEY) return command;
      return new Map();
    });

    await $onEmit(makeBaseContext(program, FIXTURE_OPTS));

    expect(reportedCodes).toContain("unsupported-union-shape");
    // No partial DTO file for the failing op.
    expect(
      directUnitEmitted.find((e) => e.path.includes("BadInput.g.cs")),
    ).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// Test: a mixed-primitive union on a @d2GrpcMethod op → D2TSP007 via the proto
// onError path (covers the emitter.ts unsupported-union-shape branch in
// emitProtoAndGrpcService).
// ---------------------------------------------------------------------------

describe("$onEmit_unionShape_ProtoPath_D2TSP007", () => {
  it("a @d2GrpcMethod op with a string|int32 union field → unsupported-union-shape, no proto file", async () => {
    const mixedUnion = {
      kind: "Union",
      variants: new Map<string | symbol, { type: unknown }>([
        [Symbol("a"), { type: { kind: "Scalar", name: "string" } }],
        [Symbol("b"), { type: { kind: "Scalar", name: "int32" } }],
      ]),
    } as unknown as Model;
    const inputModel = makeModel("BadGrpcInput", {
      kid: makeStringScalar(),
      mixed: mixedUnion,
    });
    const outputModel = makeModel("BadGrpcOutput", {
      signature: makeStringScalar(),
    });
    const op = makeWrappedOp("badGrpcUnion", inputModel, outputModel);

    const servedBy = new Map<object, unknown>([[op, "X"]]);
    const command = new Map<object, unknown>([[op, true]]);
    const grpc = new Map<object, unknown>([
      [op, { service: "XSigner", method: "Bad", streaming: "unary" }],
    ]);
    directUnitOps.push(op);

    const reportedCodes: string[] = [];
    const libModule = await import("../src/lib.js");
    vi.spyOn(libModule.$lib, "reportDiagnostic").mockImplementation(
      (_prog, diag: { code: string }) => {
        reportedCodes.push(diag.code);
      },
    );

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_COMMAND_KEY) return command;
      if (key === D2_GRPC_METHOD_KEY) return grpc;
      return new Map();
    });

    await $onEmit(makeBaseContext(program, FIXTURE_OPTS));

    expect(reportedCodes).toContain("unsupported-union-shape");
    // No partial proto for the failing op.
    expect(
      directUnitEmitted.find((e) => e.path.includes("x_signer_bad.g.proto")),
    ).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// Test: TS SSR gRPC client + the @d2Resilience predicate retry-arm via $onEmit
// (covers the TS gRPC client after-walk loop + parseRetryBudget parse + walk +
// retry-node maxAttempts extraction, against the INSTRUMENTED src — the test-host
// integration tests run the dist build and give no src coverage credit).
// ---------------------------------------------------------------------------

describe("$onEmit_tsGrpcClientDirect_PredicateRetryArm_RealModule", () => {
  it("@d2GrpcMethod + @d2Resilience(retry(3)) → TS gRPC client folds in the predicate retry-arm with maxAttempts 3", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("OrderInput", { customerId: str });
    const outputModel = makeModel("OrderOutput", { orderCode: str });
    const op = makeWrappedOp("placeOrderDirect", inputModel, outputModel);

    // No HTTP verb → route skipped; only the gRPC + TS-client path fires.
    const commandMap = new Map<object, unknown>([[op, true]]);
    const servedBy = new Map<object, unknown>([[op, "OrderFixtures"]]);
    const grpcMethod = new Map<object, unknown>([
      [
        op,
        { service: "OrderSvc", method: "PlaceOrderDirect", streaming: "unary" },
      ],
    ]);
    // @d2Resilience pipeline DSL + the predicate strings (the gen-time parse target).
    const resilience = new Map<object, unknown>([[op, "retry(3)"]]);
    const retryWhen = new Map<object, unknown>([
      [op, 'result.data.orderCode == "PENDING"'],
    ]);
    const failWhen = new Map<object, unknown>([
      [op, 'result.errorCode == "VALIDATION_FAILED"'],
    ]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_COMMAND_KEY) return commandMap;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_GRPC_METHOD_KEY) return grpcMethod;
      if (key === D2_RESILIENCE_KEY) return resilience;
      if (key === D2_RESILIENCE_RETRY_WHEN_KEY) return retryWhen;
      if (key === D2_RESILIENCE_FAIL_WHEN_KEY) return failWhen;
      return new Map();
    });

    const ctx = makeBaseContext(program, {
      "csharp-namespace": "D2.Test.Ns",
      "csharp-app-namespace-base": "D2.Test.App.Handlers",
      "csharp-clients-namespace": "D2.OrderFixtures.Clients",
      "proto-package": "d2.test.v1",
      "proto-csharp-namespace": "D2.Test.Protos.V1",
      "grpc-service-namespace": "D2.Test.Grpc",
    });
    await $onEmit(ctx);

    // The TS SSR gRPC client is emitted with the predicate retry-arm folded in.
    const tsClient = directUnitEmitted.find((e) =>
      e.path.includes("order-fixtures-grpc-client.g.ts"),
    );
    expect(tsClient).toBeDefined();
    expect(tsClient!.content).toContain(
      "export interface OrderFixturesGrpcClient {",
    );
    // parseRetryBudget extracted maxAttempts 3 from the retry(3) DSL.
    expect(tsClient!.content).toContain("maxAttempts: 3,");
    // The predicate twin consumption (the retry-arm).
    expect(tsClient!.content).toContain(
      "placeOrderDirectRetryWhen(result) && !placeOrderDirectFailWhen(result)",
    );
    expect(tsClient!.content).toContain("new ResilientPipelineBuilder()");

    // The TS predicate twin is emitted too (the retry-arm consumes it).
    const twin = directUnitEmitted.find((e) =>
      e.path.includes("place-order-direct-resilience-predicates.g.ts"),
    );
    expect(twin).toBeDefined();
  });

  it("a NESTED retry DSL (circuitBreaker(retry(2))) → parseRetryBudget walks the chain to maxAttempts 2", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("NestInput", { customerId: str });
    const outputModel = makeModel("NestOutput", { code: str });
    const op = makeWrappedOp("nestRetry", inputModel, outputModel);

    const commandMap = new Map<object, unknown>([[op, true]]);
    const servedBy = new Map<object, unknown>([[op, "NestFixtures"]]);
    const grpcMethod = new Map<object, unknown>([
      [op, { service: "NestSvc", method: "NestRetry", streaming: "unary" }],
    ]);
    // retry is NESTED under circuitBreaker → parseRetryBudget walks node.inner.
    const resilience = new Map<object, unknown>([
      [op, "circuitBreaker(retry(2))"],
    ]);
    const retryWhen = new Map<object, unknown>([
      [op, 'result.data.code == "PENDING"'],
    ]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_COMMAND_KEY) return commandMap;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_GRPC_METHOD_KEY) return grpcMethod;
      if (key === D2_RESILIENCE_KEY) return resilience;
      if (key === D2_RESILIENCE_RETRY_WHEN_KEY) return retryWhen;
      return new Map();
    });

    await $onEmit(
      makeBaseContext(program, {
        "csharp-namespace": "D2.Test.Ns",
        "csharp-app-namespace-base": "D2.Test.App.Handlers",
        "csharp-clients-namespace": "D2.NestFixtures.Clients",
        "proto-package": "d2.test.v1",
        "proto-csharp-namespace": "D2.Test.Protos.V1",
        "grpc-service-namespace": "D2.Test.Grpc",
      }),
    );

    const tsClient = directUnitEmitted.find((e) =>
      e.path.includes("nest-fixtures-grpc-client.g.ts"),
    );
    expect(tsClient).toBeDefined();
    // The nested retry(2) budget was extracted by walking the chain.
    expect(tsClient!.content).toContain("maxAttempts: 2,");
  });

  it("a no-retry DSL (singleflight()) with a predicate → budget defaults to 3 (no retry node)", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("SfInput", { customerId: str });
    const outputModel = makeModel("SfOutput", { code: str });
    const op = makeWrappedOp("sfOnly", inputModel, outputModel);

    const commandMap = new Map<object, unknown>([[op, true]]);
    const servedBy = new Map<object, unknown>([[op, "SfFixtures"]]);
    const grpcMethod = new Map<object, unknown>([
      [op, { service: "SfSvc", method: "SfOnly", streaming: "unary" }],
    ]);
    // No retry policy in the chain → parseRetryBudget returns undefined → default 3.
    const resilience = new Map<object, unknown>([[op, "singleflight()"]]);
    const retryWhen = new Map<object, unknown>([
      [op, 'result.data.code == "PENDING"'],
    ]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_COMMAND_KEY) return commandMap;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_GRPC_METHOD_KEY) return grpcMethod;
      if (key === D2_RESILIENCE_KEY) return resilience;
      if (key === D2_RESILIENCE_RETRY_WHEN_KEY) return retryWhen;
      return new Map();
    });

    await $onEmit(
      makeBaseContext(program, {
        "csharp-namespace": "D2.Test.Ns",
        "csharp-app-namespace-base": "D2.Test.App.Handlers",
        "csharp-clients-namespace": "D2.SfFixtures.Clients",
        "proto-package": "d2.test.v1",
        "proto-csharp-namespace": "D2.Test.Protos.V1",
        "grpc-service-namespace": "D2.Test.Grpc",
      }),
    );

    const tsClient = directUnitEmitted.find((e) =>
      e.path.includes("sf-fixtures-grpc-client.g.ts"),
    );
    expect(tsClient).toBeDefined();
    // No retry node → undefined budget → the emitter default (3).
    expect(tsClient!.content).toContain("maxAttempts: 3,");
  });

  it("a bare retry() DSL (no explicit count) → maxAttempts is undefined → emitter default 3", async () => {
    const str = makeStringScalar();
    const inputModel = makeModel("BareInput", { customerId: str });
    const outputModel = makeModel("BareOutput", { code: str });
    const op = makeWrappedOp("bareRetry", inputModel, outputModel);

    const commandMap = new Map<object, unknown>([[op, true]]);
    const servedBy = new Map<object, unknown>([[op, "BareFixtures"]]);
    const grpcMethod = new Map<object, unknown>([
      [op, { service: "BareSvc", method: "BareRetry", streaming: "unary" }],
    ]);
    // retry() with no maxAttempts tunable → parseRetryBudget's number-check FALSE arm.
    const resilience = new Map<object, unknown>([[op, "retry()"]]);
    const retryWhen = new Map<object, unknown>([
      [op, 'result.data.code == "PENDING"'],
    ]);

    directUnitOps.push(op);

    const program = makeMockProgram((key: symbol) => {
      if (key === D2_COMMAND_KEY) return commandMap;
      if (key === D2_SERVED_BY_KEY) return servedBy;
      if (key === D2_GRPC_METHOD_KEY) return grpcMethod;
      if (key === D2_RESILIENCE_KEY) return resilience;
      if (key === D2_RESILIENCE_RETRY_WHEN_KEY) return retryWhen;
      return new Map();
    });

    await $onEmit(
      makeBaseContext(program, {
        "csharp-namespace": "D2.Test.Ns",
        "csharp-app-namespace-base": "D2.Test.App.Handlers",
        "csharp-clients-namespace": "D2.BareFixtures.Clients",
        "proto-package": "d2.test.v1",
        "proto-csharp-namespace": "D2.Test.Protos.V1",
        "grpc-service-namespace": "D2.Test.Grpc",
      }),
    );

    const tsClient = directUnitEmitted.find((e) =>
      e.path.includes("bare-fixtures-grpc-client.g.ts"),
    );
    expect(tsClient).toBeDefined();
    expect(tsClient!.content).toContain("maxAttempts: 3,");
  });
});
