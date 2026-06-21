// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Tests for the OpenAPI x-d2-* extension emitter.
//
// Two layers:
//   1. Integration (TypeSpec test-host) — compiles the real openapi-shaped.tsp
//      fixture, runs emitOpenApiDocuments, and asserts the REAL stock
//      @typespec/openapi3 shape PLUS the four x-d2-* extensions are present-and-
//      correct AND absent-when-undeclared (adversarial), AND the versioned
//      fan-out is genuinely multi-document (one per version, non-vacuous).
//   2. Direct unit (src/** V8 coverage) — exercises injectD2Extensions over
//      synthetic OpenAPI documents (incl. the object-form tier/csrf payloads,
//      the op-not-in-index skip branch, and every scope arm) + the
//      emitOpenApiDocuments record-arm handling via a mocked getOpenAPI3 +
//      getAllHttpServices, so every branch in openapi-emitter.ts is covered.

import { describe, it, expect, beforeAll, vi } from "vitest";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";
import { HttpTestLibrary } from "@typespec/http/testing";
import { VersioningTestLibrary } from "@typespec/versioning/testing";
import type * as CompilerNs from "@typespec/compiler";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

// ===========================================================================
// Integration — real stock @typespec/openapi3 shape + x-d2-* layering.
// ===========================================================================

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

const REPO = join(
  dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "..",
  "..",
  "..",
);

const FIXTURE_TSP = readFileSync(
  join(REPO, "contracts/typespec/fixtures/openapi-shaped.tsp"),
  "utf8",
);

/** Compile the openapi-shaped fixture through the test-host and return the program. */
async function compileFixture(): Promise<
  Awaited<ReturnType<typeof createTestHost>>
> {
  const host = await createTestHost({
    libraries: [D2DecoratorTestLibrary, HttpTestLibrary, VersioningTestLibrary],
  });
  host.addTypeSpecFile("main.tsp", FIXTURE_TSP);
  await host.compile("main.tsp");

  return host;
}

describe("openApiEmitter_Integration_StockShapePlusExtensions", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;
  // The unversioned-service document and the two versioned documents.
  let unversioned: Record<string, unknown>;

  beforeAll(async () => {
    host = await compileFixture();
    const { emitOpenApiDocuments } =
      await import("../src/lib/openapi-emitter.js");
    const files = await emitOpenApiDocuments(host.program);
    const unversionedFile = files.find(
      (f) => f.fileName === "open-api-fixtures.openapi.g.json",
    );
    if (unversionedFile === undefined)
      throw new Error("unversioned OpenAPI doc not emitted");

    unversioned = JSON.parse(unversionedFile.content) as Record<
      string,
      unknown
    >;
  });

  it("compiles the fixture with zero error diagnostics", () => {
    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);
  });

  it("emits the genuine stock OpenAPI 3.0 shape (paths + components + requestBody + $ref responses)", () => {
    // Structural facts only the real @typespec/openapi3 emitter produces — proof
    // the HTTP shape was NOT reimplemented.
    expect(unversioned["openapi"]).toBe("3.0.0");
    expect((unversioned["info"] as Record<string, unknown>)["title"]).toBe(
      "OpenApi Fixtures",
    );

    const paths = unversioned["paths"] as Record<string, unknown>;
    const signPost = (paths["/v1/openapi/sign"] as Record<string, unknown>)[
      "post"
    ] as Record<string, unknown>;
    expect(signPost["operationId"]).toBe("sign");

    // Stock request-body wrapping the named param + $ref into components/schemas.
    const requestBody = signPost["requestBody"] as Record<string, unknown>;
    const requestSchema = (
      (requestBody["content"] as Record<string, unknown>)[
        "application/json"
      ] as Record<string, unknown>
    )["schema"] as Record<string, unknown>;
    const inputRef = (
      (requestSchema["properties"] as Record<string, unknown>)[
        "input"
      ] as Record<string, unknown>
    )["$ref"];
    expect(inputRef).toBe("#/components/schemas/OpenApiSignInput");

    // Stock components/schemas carry the model definitions.
    const components = unversioned["components"] as Record<string, unknown>;
    const schemas = components["schemas"] as Record<string, unknown>;
    expect(schemas["OpenApiSignInput"]).toBeDefined();
    expect(schemas["OpenApiSignOutput"]).toBeDefined();
  });

  it("injects ALL FOUR x-d2-* extensions, present-and-correct, on the fully-decorated op", () => {
    const paths = unversioned["paths"] as Record<string, unknown>;
    const signPost = (paths["/v1/openapi/sign"] as Record<string, unknown>)[
      "post"
    ] as Record<string, unknown>;

    expect(signPost["x-d2-scope"]).toEqual({
      mode: "any",
      scopes: ["self.write"],
    });
    expect(signPost["x-d2-tier"]).toBe("Standard");
    expect(signPost["x-d2-audience"]).toBe("d2-edge");
    expect(signPost["x-d2-csrf"]).toBe("exempt");
  });

  it("ADVERSARIAL: an op with only the required auth intent carries x-d2-scope but NOT tier/csrf/audience", () => {
    const paths = unversioned["paths"] as Record<string, unknown>;
    const minimalPost = (
      paths["/v1/openapi/sign-minimal"] as Record<string, unknown>
    )["post"] as Record<string, unknown>;

    expect(minimalPost["x-d2-scope"]).toEqual({
      mode: "any",
      scopes: ["self.write"],
    });
    // The three optional extensions are NOT vacuously stamped.
    expect("x-d2-tier" in minimalPost).toBe(false);
    expect("x-d2-csrf" in minimalPost).toBe(false);
    expect("x-d2-audience" in minimalPost).toBe(false);
  });

  it("encodes the all-scopes arm as x-d2-scope { mode: 'all', scopes }", () => {
    const paths = unversioned["paths"] as Record<string, unknown>;
    const auditGet = (paths["/v1/openapi/audit"] as Record<string, unknown>)[
      "get"
    ] as Record<string, unknown>;
    expect(auditGet["x-d2-scope"]).toEqual({
      mode: "all",
      scopes: ["self.read", "auth.password.change"],
    });
  });

  it("encodes the harmless arm as x-d2-scope { mode: 'harmless' }", () => {
    const paths = unversioned["paths"] as Record<string, unknown>;
    const healthGet = (paths["/v1/openapi/health"] as Record<string, unknown>)[
      "get"
    ] as Record<string, unknown>;
    expect(healthGet["x-d2-scope"]).toEqual({ mode: "harmless" });
  });

  it("emits a document-level x-d2-generated-by traceability marker", () => {
    const marker = unversioned["x-d2-generated-by"] as Record<string, unknown>;
    expect(marker["emitter"]).toBe("@d2/typespec-emitters");
    expect(typeof marker["note"]).toBe("string");
  });

  it("NO PII LEAK: the emitted document never contains a @d2Redact field VALUE or payload content", () => {
    // An OpenAPI document is schema-only: it describes types (type/format/
    // description/$ref) but never carries payload instance values. The leak
    // vectors — `example`, `examples`, and `default` on schema objects — must
    // be absent from every schema in components/schemas, since the fixture
    // carries a @d2Redact payload field whose instance value must never surface.
    const instanceValueKeys = new Set(["example", "examples", "default"]);

    const schemas = (unversioned["components"] as Record<string, unknown>)[
      "schemas"
    ] as Record<string, unknown>;

    for (const [schemaName, schema] of Object.entries(schemas)) {
      const schemaObj = schema as Record<string, unknown>;
      for (const leakKey of instanceValueKeys) {
        expect(
          leakKey in schemaObj,
          `schema "${schemaName}" must not carry instance-value key "${leakKey}"`,
        ).toBe(false);
      }

      // Also walk schema properties for per-property instance values.
      const properties = schemaObj["properties"] as
        | Record<string, Record<string, unknown>>
        | undefined;
      if (properties !== undefined) {
        for (const [propName, propSchema] of Object.entries(properties)) {
          for (const leakKey of instanceValueKeys) {
            expect(
              leakKey in propSchema,
              `schema "${schemaName}.${propName}" must not carry instance-value key "${leakKey}"`,
            ).toBe(false);
          }
        }
      }
    }
  });
});

describe("openApiEmitter_Integration_VersionedFanOutIsReal", () => {
  let unversionedCount: number;
  let v1: Record<string, unknown>;
  let v2: Record<string, unknown>;
  let fileNames: string[];

  beforeAll(async () => {
    const host = await compileFixture();
    const { emitOpenApiDocuments } =
      await import("../src/lib/openapi-emitter.js");
    const files = await emitOpenApiDocuments(host.program);
    fileNames = files.map((f) => f.fileName);
    unversionedCount = files.filter((f) =>
      f.fileName.startsWith("open-api-fixtures"),
    ).length;

    const v1File = files.find(
      (f) => f.fileName === "open-api-versioned-fixtures.1-0.openapi.g.json",
    );
    const v2File = files.find(
      (f) => f.fileName === "open-api-versioned-fixtures.2-0.openapi.g.json",
    );
    if (v1File === undefined || v2File === undefined)
      throw new Error("versioned OpenAPI docs not emitted");

    v1 = JSON.parse(v1File.content) as Record<string, unknown>;
    v2 = JSON.parse(v2File.content) as Record<string, unknown>;
  });

  it("emits one file per (service × version): 1 unversioned + 2 versions = 3 documents", () => {
    expect(fileNames).toHaveLength(3);
    expect(unversionedCount).toBe(1);
    expect(fileNames).toContain(
      "open-api-versioned-fixtures.1-0.openapi.g.json",
    );
    expect(fileNames).toContain(
      "open-api-versioned-fixtures.2-0.openapi.g.json",
    );
  });

  it("NON-VACUOUS: v1 and v2 documents genuinely differ (exportReport is @added in v2 only)", () => {
    expect((v1["info"] as Record<string, unknown>)["version"]).toBe("1.0");
    expect((v2["info"] as Record<string, unknown>)["version"]).toBe("2.0");

    const v1Paths = Object.keys(v1["paths"] as Record<string, unknown>);
    const v2Paths = Object.keys(v2["paths"] as Record<string, unknown>);

    // v1 has only /report; v2 adds /report/export — a real per-version delta.
    expect(v1Paths).toEqual(["/v1/openapi/report"]);
    expect(v2Paths).toContain("/v1/openapi/report");
    expect(v2Paths).toContain("/v1/openapi/report/export");
    expect(v2Paths.length).toBeGreaterThan(v1Paths.length);
  });

  it("each version document carries the x-d2-* extensions on its ops", () => {
    const v1Report = (
      (v1["paths"] as Record<string, unknown>)["/v1/openapi/report"] as Record<
        string,
        unknown
      >
    )["get"] as Record<string, unknown>;
    expect(v1Report["x-d2-scope"]).toEqual({
      mode: "any",
      scopes: ["self.read"],
    });
    expect(v1Report["x-d2-tier"]).toBe("Standard");

    const v2Export = (
      (v2["paths"] as Record<string, unknown>)[
        "/v1/openapi/report/export"
      ] as Record<string, unknown>
    )["post"] as Record<string, unknown>;
    expect(v2Export["x-d2-scope"]).toEqual({
      mode: "any",
      scopes: ["self.write"],
    });
  });
});

// ===========================================================================
// Direct unit tests — full src/** branch coverage of the pure injection +
// orchestration logic via synthetic documents + mocked compiler seams.
// ===========================================================================

/**
 * A stub Program whose stateMap(key).get(op) returns values from a per-op,
 * per-key lookup. Only the surface injectD2Extensions touches is implemented.
 */
function makeStubProgram(stateByOp: Map<object, Map<symbol, unknown>>): {
  stateMap: (key: symbol) => { get: (op: object) => unknown };
} {
  return {
    stateMap(key: symbol) {
      return {
        get(op: object): unknown {
          return stateByOp.get(op)?.get(key);
        },
      };
    },
  };
}

/** A minimal OpenAPI 3.0 path-item operation object. */
function makeOperation(): Record<string, unknown> {
  return { operationId: "op", parameters: [], responses: {} };
}

describe("openApiEmitter_Unit_injectD2Extensions", () => {
  it("injects the doc-level x-d2-generated-by marker even with empty paths", async () => {
    // No HTTP services → empty op index; the marker is still added.
    vi.resetModules();
    vi.doMock("@typespec/http", () => ({
      getAllHttpServices: () => [[], []],
    }));
    const { injectD2Extensions } =
      await import("../src/lib/openapi-emitter.js");

    const document = { openapi: "3.0.0", info: {}, paths: {} } as never;
    const program = makeStubProgram(new Map()) as never;

    const result = injectD2Extensions(program, document) as unknown as Record<
      string,
      unknown
    >;
    expect(result["x-d2-generated-by"]).toEqual({
      emitter: "@d2/typespec-emitters",
      note: expect.stringContaining("@typespec/openapi3"),
    });
    vi.doUnmock("@typespec/http");
  });

  it("SKIP branch: a path-item op with no matching TypeSpec op gets no x-d2-* extensions", async () => {
    vi.resetModules();
    // getAllHttpServices returns NO operations → the (verb,path) lookup misses.
    vi.doMock("@typespec/http", () => ({
      getAllHttpServices: () => [[{ operations: [] }], []],
    }));
    const { injectD2Extensions } =
      await import("../src/lib/openapi-emitter.js");

    const op = makeOperation();
    const document = {
      openapi: "3.0.0",
      info: {},
      paths: { "/x": { post: op } },
    } as never;
    const program = makeStubProgram(new Map()) as never;

    injectD2Extensions(program, document);
    expect("x-d2-scope" in op).toBe(false);
    expect("x-d2-tier" in op).toBe(false);
    vi.doUnmock("@typespec/http");
  });

  it("covers every scope arm + tier/csrf object-form payloads + audience + the undefined-operation skip", async () => {
    vi.resetModules();

    // Four distinct TypeSpec op sentinels — keyed into the op index by (verb,path).
    const opAny = { name: "any" };
    const opAll = { name: "all" };
    const opHarmless = { name: "harmless" };
    const opNone = { name: "none" };

    vi.doMock("@typespec/http", () => ({
      getAllHttpServices: () => [
        [
          {
            operations: [
              { verb: "post", path: "/any", operation: opAny },
              { verb: "get", path: "/all", operation: opAll },
              { verb: "get", path: "/harmless", operation: opHarmless },
              { verb: "post", path: "/none", operation: opNone },
            ],
          },
        ],
        [],
      ],
    }));
    const { injectD2Extensions } =
      await import("../src/lib/openapi-emitter.js");
    const {
      D2_REQUIRE_ANY_SCOPE_KEY,
      D2_REQUIRE_ALL_SCOPES_KEY,
      D2_HARMLESS_KEY,
      D2_RATE_LIMIT_TIER_KEY,
      D2_AUDIENCE_KEY,
      D2_CSRF_KEY,
    } = await import("@d2/typespec-decorators");

    const stateByOp = new Map<object, Map<symbol, unknown>>();
    // any-scope op: also OBJECT-form tier + OBJECT-form csrf + audience.
    stateByOp.set(
      opAny,
      new Map<symbol, unknown>([
        [D2_REQUIRE_ANY_SCOPE_KEY, ["s.write"]],
        [D2_RATE_LIMIT_TIER_KEY, { tier: "Burst" }],
        [D2_CSRF_KEY, { posture: "required" }],
        [D2_AUDIENCE_KEY, "Files"],
      ]),
    );
    // all-scopes op.
    stateByOp.set(
      opAll,
      new Map<symbol, unknown>([
        [D2_REQUIRE_ALL_SCOPES_KEY, ["a.read", "b.read"]],
      ]),
    );
    // harmless op.
    stateByOp.set(
      opHarmless,
      new Map<symbol, unknown>([[D2_HARMLESS_KEY, true]]),
    );
    // none op: no auth intent at all → x-d2-scope omitted (defensive branch).
    stateByOp.set(opNone, new Map<symbol, unknown>());

    const anyOp = makeOperation();
    const allOp = makeOperation();
    const harmlessOp = makeOperation();
    const noneOp = makeOperation();

    const document = {
      openapi: "3.0.0",
      info: {},
      paths: {
        "/any": { post: anyOp, get: undefined },
        "/all": { get: allOp },
        "/harmless": { get: harmlessOp },
        "/none": { post: noneOp },
      },
    } as never;
    const program = makeStubProgram(stateByOp) as never;

    injectD2Extensions(program, document);

    // any-scope + object-form tier/csrf + audience.
    expect(anyOp["x-d2-scope"]).toEqual({ mode: "any", scopes: ["s.write"] });
    expect(anyOp["x-d2-tier"]).toBe("Burst");
    expect(anyOp["x-d2-csrf"]).toBe("required");
    expect(anyOp["x-d2-audience"]).toBe("Files");

    // all-scopes.
    expect(allOp["x-d2-scope"]).toEqual({
      mode: "all",
      scopes: ["a.read", "b.read"],
    });
    expect("x-d2-tier" in allOp).toBe(false);

    // harmless.
    expect(harmlessOp["x-d2-scope"]).toEqual({ mode: "harmless" });

    // none: defensive — no x-d2-scope (compile would already have failed D2TSP004).
    expect("x-d2-scope" in noneOp).toBe(false);

    vi.doUnmock("@typespec/http");
  });
});

describe("openApiEmitter_Unit_emitOpenApiDocuments", () => {
  /** Mock @typespec/compiler so listServices reports `count` synthetic services. */
  async function mockListServices(count: number): Promise<void> {
    const actual =
      await vi.importActual<typeof CompilerNs>("@typespec/compiler");
    vi.doMock("@typespec/compiler", () => ({
      ...actual,
      listServices: () => new Array(count).fill({}),
    }));
  }

  it("returns no files when the program declares no @service namespace (listServices empty)", async () => {
    vi.resetModules();
    await mockListServices(0);
    // getOpenAPI3 must NOT be called when there is no @service — make it throw
    // so a regression that drops the guard surfaces immediately.
    vi.doMock("@typespec/openapi3", () => ({
      getOpenAPI3: () => {
        throw new Error("getOpenAPI3 must not run without a @service");
      },
    }));
    vi.doMock("@typespec/http", () => ({
      getAllHttpServices: () => [[], []],
    }));
    const { emitOpenApiDocuments } =
      await import("../src/lib/openapi-emitter.js");

    const files = await emitOpenApiDocuments({} as never);
    expect(files).toHaveLength(0);

    vi.doUnmock("@typespec/compiler");
    vi.doUnmock("@typespec/openapi3");
    vi.doUnmock("@typespec/http");
  });

  it("handles the unversioned record arm + an unnamed service (filename fallback)", async () => {
    vi.resetModules();
    await mockListServices(1);
    vi.doMock("@typespec/openapi3", () => ({
      getOpenAPI3: () =>
        Promise.resolve([
          {
            versioned: false,
            service: { type: { name: undefined } },
            document: { openapi: "3.0.0", info: {}, paths: {} },
            diagnostics: [],
          },
        ]),
    }));
    vi.doMock("@typespec/http", () => ({
      getAllHttpServices: () => [[], []],
    }));
    const { emitOpenApiDocuments } =
      await import("../src/lib/openapi-emitter.js");

    const files = await emitOpenApiDocuments({} as never);
    expect(files).toHaveLength(1);
    // Unnamed service → "service" fallback; trailing newline present.
    expect(files[0]!.fileName).toBe("service.openapi.g.json");
    expect(files[0]!.content.endsWith("\n")).toBe(true);
    const doc = JSON.parse(files[0]!.content) as Record<string, unknown>;
    expect(doc["x-d2-generated-by"]).toBeDefined();

    vi.doUnmock("@typespec/compiler");
    vi.doUnmock("@typespec/openapi3");
    vi.doUnmock("@typespec/http");
  });

  it("handles the versioned record arm + an empty version string (no version segment)", async () => {
    vi.resetModules();
    await mockListServices(1);
    vi.doMock("@typespec/openapi3", () => ({
      getOpenAPI3: () =>
        Promise.resolve([
          {
            versioned: true,
            service: { type: { name: "MyService" } },
            versions: [
              {
                version: "3.0",
                service: { type: { name: "MyService" } },
                document: { openapi: "3.0.0", info: {}, paths: {} },
                diagnostics: [],
              },
              {
                // Empty version string → filename has no version segment.
                version: "",
                service: { type: { name: "MyService" } },
                document: { openapi: "3.0.0", info: {}, paths: {} },
                diagnostics: [],
              },
            ],
          },
        ]),
    }));
    vi.doMock("@typespec/http", () => ({
      getAllHttpServices: () => [[], []],
    }));
    const { emitOpenApiDocuments } =
      await import("../src/lib/openapi-emitter.js");

    const files = await emitOpenApiDocuments({} as never);
    expect(files).toHaveLength(2);
    expect(files.map((f) => f.fileName)).toEqual([
      "my-service.3-0.openapi.g.json",
      "my-service.openapi.g.json",
    ]);

    vi.doUnmock("@typespec/compiler");
    vi.doUnmock("@typespec/openapi3");
    vi.doUnmock("@typespec/http");
  });
});
