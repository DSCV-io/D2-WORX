// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Integration tests: compile inline .tsp through the TypeSpec test-host and
// assert the emitted REST route registration content.
//
// Covers:
//   1. sign op (@route + @post + @d2RequireAnyScope + @d2InProcess) → route emitted
//      with MapPost, RequireAnyScope, façade delegation, rate-tier+csrf markers.
//   2. routed op with no auth intent → D2TSP004 fires (hasError).
//   3. op WITHOUT @route (getJwks) → NO route emitted (skip path).
//   4. op with @route + @d2InProcess → façade delegation in emitted route.
//   5. routed op with unsupported verb (@head) → D2TSP005 fires (hasError).
//   6. allScopes op (@route + @d2RequireAllScopes) → RequireAllScopes in emitted route.
//   7. harmless op (@route + @d2Harmless) → MarkAsD2HarmlessEndpoint in emitted route.

import { describe, it, expect, beforeAll } from "vitest";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";
import { HttpTestLibrary } from "@typespec/http/testing";

// Mount the decorators library.
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

// Mount the emitter package.
const D2EmitterTestLibrary = createTestLibrary({
  name: "@d2/typespec-emitters",
  packageRoot: await findTestPackageRoot(import.meta.url),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

// ---------------------------------------------------------------------------
// Helper: retrieve an emitted file from the in-memory FS by suffix.
// ---------------------------------------------------------------------------

function getEmittedFile(
  host: Awaited<ReturnType<typeof createTestHost>>,
  suffix: string,
): string | undefined {
  const stored = (host as unknown as { fs?: Map<string, string> }).fs;
  if (!(stored instanceof Map)) return undefined;
  const key = [...stored.keys()].find((k) => k.endsWith(suffix));
  return key !== undefined ? stored.get(key) : undefined;
}

// Common tspconfig options for fixture mode (no real app namespace).
const FIXTURE_OPTIONS = {
  "csharp-namespace": "D2.Test.Route",
  "proto-package": "d2.test.v1",
  "proto-csharp-namespace": "D2.Test.Protos.V1",
  "grpc-service-namespace": "D2.Test.Grpc",
};

// ---------------------------------------------------------------------------
// Test 1: sign op with @route + @post + @d2InProcess → route registration
// ---------------------------------------------------------------------------

describe("routeEmitIntegration_Sign_EmitsRouteRegistration", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [
        HttpTestLibrary,
        D2DecoratorTestLibrary,
        D2EmitterTestLibrary,
      ],
    });
  });

  it("sign op with @route/@post/@d2InProcess → MapPost + RequireAnyScope + façade delegation", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      import "@typespec/http";
      using D2;
      using Http;
      namespace D2.Fixtures;

      model SignFixtureInput { @d2Field(1) kid: string; @d2Field(2) @d2Redact("SecretInformation") payload: bytes; }
      model SignFixtureOutput { @d2Field(1) signature: string; }

      @d2Command
      @d2ServedBy("SignFixture")
      @d2InProcess
      @d2GrpcMethod("SignFixtureSigner", "SignFixture")
      @route("/internal/v1/fixtures/sign-fixture")
      @post
      @d2RequireAnyScope("self.write")
      @d2RateLimitTier("Standard")
      @d2Csrf("exempt")
      op signFixture(input: SignFixtureInput): SignFixtureOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: { "@d2/typespec-emitters": FIXTURE_OPTIONS },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // Route registration emitted.
    const routeContent = getEmittedFile(
      host,
      "SignFixtureRouteRegistration.g.cs",
    );
    expect(routeContent).toBeDefined();
    expect(routeContent).toContain("MapPost");
    expect(routeContent).toContain('"/internal/v1/fixtures/sign-fixture"');
    expect(routeContent).toContain("RequireAnyScope");
    expect(routeContent).toContain('"self.write"');
    // Façade delegation: the sign fixture uses @d2InProcess → façade type in ctor.
    expect(routeContent).toContain("Facade");
    expect(routeContent).toContain("SignFixtureAsync");
    // Markers present.
    expect(routeContent).toContain("D2GeneratedRateLimitTier");
    expect(routeContent).toContain("D2GeneratedCsrfPosture");
  });
});

// ---------------------------------------------------------------------------
// Test 2: routed op with no auth intent → D2TSP004
// ---------------------------------------------------------------------------

describe("routeEmitIntegration_MissingAuthIntent_D2TSP004", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [
        HttpTestLibrary,
        D2DecoratorTestLibrary,
        D2EmitterTestLibrary,
      ],
    });
  });

  it("routed op with no @d2RequireAnyScope/@d2RequireAllScopes/@d2Harmless → D2TSP004 error", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      import "@typespec/http";
      using D2;
      using Http;
      namespace D2.Fixtures;

      model NoAuthInput { id: string; }
      model NoAuthOutput { result: string; }

      @d2Command
      @d2ServedBy("Test")
      @d2InProcess
      @route("/test/no-auth")
      @post
      op noAuth(input: NoAuthInput): NoAuthOutput;
      `,
    );

    // diagnose() returns diagnostics without throwing on errors.
    const diagnostics = await host.diagnose("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: { "@d2/typespec-emitters": FIXTURE_OPTIONS },
      outputDir: "testing:/out",
    });

    const d2tsp004 = diagnostics.filter(
      (d) =>
        d.severity === "error" && d.code.includes("route-missing-auth-intent"),
    );
    expect(d2tsp004.length).toBeGreaterThan(0);
  });
});

// ---------------------------------------------------------------------------
// Test 3: op WITHOUT @route → NO route emitted
// ---------------------------------------------------------------------------

describe("routeEmitIntegration_NoRoute_NoRegistrationEmitted", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [
        HttpTestLibrary,
        D2DecoratorTestLibrary,
        D2EmitterTestLibrary,
      ],
    });
  });

  it("getJwks op without @route → no RouteRegistration.g.cs emitted", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.KeyCustodian;

      model Jwk { kid: string; }
      model GetJwksOutput { keys: Jwk[]; }

      @d2Query
      @d2InProcess
      @d2ServedBy("KeyCustodian")
      op getJwks(): GetJwksOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: { "@d2/typespec-emitters": FIXTURE_OPTIONS },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // No route registration emitted.
    const routeContent = getEmittedFile(host, "RouteRegistration.g.cs");
    expect(routeContent).toBeUndefined();

    // DTOs still emitted.
    const inputContent = getEmittedFile(host, "GetJwksInput.g.cs");
    expect(inputContent).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Test 4: @d2InProcess op with @route → façade delegation (not handler)
// ---------------------------------------------------------------------------

describe("routeEmitIntegration_InProcess_FacadeDelegation", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [
        HttpTestLibrary,
        D2DecoratorTestLibrary,
        D2EmitterTestLibrary,
      ],
    });
  });

  it("@d2InProcess op → route delegates through fixture façade, not I<Op>Handler", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      import "@typespec/http";
      using D2;
      using Http;
      namespace D2.Fixtures;

      model PingInput { id: string; }
      model PingOutput { pong: string; }

      @d2Command
      @d2ServedBy("MyModule")
      @d2InProcess
      @route("/internal/ping")
      @post
      @d2RequireAnyScope("self.write")
      op ping(input: PingInput): PingOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: { "@d2/typespec-emitters": FIXTURE_OPTIONS },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    const routeContent = getEmittedFile(host, "PingRouteRegistration.g.cs");
    expect(routeContent).toBeDefined();
    // Façade delegation — not IHandler.
    expect(routeContent).toContain("Facade");
    expect(routeContent).toContain("PingAsync");
    expect(routeContent).not.toContain("IPingHandler handler");
    expect(routeContent).not.toContain("HandleAsync");
  });
});

// ---------------------------------------------------------------------------
// Test 5: op with @head (unsupported verb) → D2TSP005
// ---------------------------------------------------------------------------

describe("routeEmitIntegration_UnsupportedVerb_D2TSP005", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [
        HttpTestLibrary,
        D2DecoratorTestLibrary,
        D2EmitterTestLibrary,
      ],
    });
  });

  it("op with @head verb → D2TSP005 error (unsupported-http-verb)", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      import "@typespec/http";
      using D2;
      using Http;
      namespace D2.Fixtures;

      model HeadInput { id: string; }
      model HeadOutput { exists: boolean; }

      @d2Command
      @d2ServedBy("Test")
      @d2InProcess
      @route("/test/head-check")
      @head
      @d2RequireAnyScope("self.read")
      op headCheck(input: HeadInput): HeadOutput;
      `,
    );

    // diagnose() returns diagnostics without throwing on errors.
    const diagnostics = await host.diagnose("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: { "@d2/typespec-emitters": FIXTURE_OPTIONS },
      outputDir: "testing:/out",
    });

    const d2tsp005 = diagnostics.filter(
      (d) => d.severity === "error" && d.code.includes("unsupported-http-verb"),
    );
    expect(d2tsp005.length).toBeGreaterThan(0);
  });
});

// ---------------------------------------------------------------------------
// Test 6: @d2RequireAllScopes op → RequireAllScopes in emitted route
// ---------------------------------------------------------------------------

describe("routeEmitIntegration_AllScopes_RequireAllScopesEmitted", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [
        HttpTestLibrary,
        D2DecoratorTestLibrary,
        D2EmitterTestLibrary,
      ],
    });
  });

  it("@d2RequireAllScopes op → RequireAllScopes(first, ...rest) in route", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      import "@typespec/http";
      using D2;
      using Http;
      namespace D2.Fixtures;

      model AllScopesInput { id: string; }
      model AllScopesOutput { data: string; }

      @d2Command
      @d2ServedBy("KeyCustodian")
      @d2InProcess
      @route("/internal/v1/fixtures/all-scopes")
      @get
      @d2RequireAllScopes("self.read", "self.write")
      op allScopes(input: AllScopesInput): AllScopesOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: { "@d2/typespec-emitters": FIXTURE_OPTIONS },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    const routeContent = getEmittedFile(
      host,
      "AllScopesRouteRegistration.g.cs",
    );
    expect(routeContent).toBeDefined();
    expect(routeContent).toContain("RequireAllScopes");
    expect(routeContent).toContain('"self.read"');
    expect(routeContent).toContain('"self.write"');
    expect(routeContent).not.toContain("RequireAnyScope");
  });
});

// ---------------------------------------------------------------------------
// Test 7: @d2Harmless op → MarkAsD2HarmlessEndpoint in emitted route
// ---------------------------------------------------------------------------

describe("routeEmitIntegration_Harmless_MarkAsHarmlessEmitted", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [
        HttpTestLibrary,
        D2DecoratorTestLibrary,
        D2EmitterTestLibrary,
      ],
    });
  });

  it("@d2Harmless op → MarkAsD2HarmlessEndpoint() in route", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      import "@typespec/http";
      using D2;
      using Http;
      namespace D2.Fixtures;

      model HealthInput {}
      model HealthOutput { status: string; }

      @d2Query
      @d2ServedBy("Edge")
      @d2InProcess
      @route("/healthz")
      @get
      @d2Harmless
      op health(input: HealthInput): HealthOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: { "@d2/typespec-emitters": FIXTURE_OPTIONS },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    const routeContent = getEmittedFile(host, "HealthRouteRegistration.g.cs");
    expect(routeContent).toBeDefined();
    expect(routeContent).toContain("MarkAsD2HarmlessEndpoint");
    expect(routeContent).not.toContain("RequireAnyScope");
    expect(routeContent).not.toContain("RequireAllScopes");
  });
});

// ---------------------------------------------------------------------------
// Test 8: @d2InProcess + @d2GrpcMethod → both surfaces delegate through the façade
// ---------------------------------------------------------------------------

describe("routeEmitIntegration_GrpcRePoint_BothSurfacesDelegateThroughFacade", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [
        HttpTestLibrary,
        D2DecoratorTestLibrary,
        D2EmitterTestLibrary,
      ],
    });
  });

  it("@d2InProcess + @d2GrpcMethod op → both the route AND gRPC service delegate through the façade", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      import "@typespec/http";
      using D2;
      using Http;
      namespace D2.Fixtures;

      model SignFixtureInput { @d2Field(1) kid: string; @d2Field(2) @d2Redact("SecretInformation") payload: bytes; }
      model SignFixtureOutput { @d2Field(1) signature: string; }

      @d2Command
      @d2ServedBy("SignFixture")
      @d2InProcess
      @d2GrpcMethod("SignFixtureSigner", "SignFixture")
      @route("/internal/v1/fixtures/sign-fixture")
      @post
      @d2RequireAnyScope("self.write")
      @d2RateLimitTier("Standard")
      @d2Csrf("exempt")
      op signFixture(input: SignFixtureInput): SignFixtureOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: { "@d2/typespec-emitters": FIXTURE_OPTIONS },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // Route delegates through the façade.
    const routeContent = getEmittedFile(
      host,
      "SignFixtureRouteRegistration.g.cs",
    );
    expect(routeContent).toBeDefined();
    expect(routeContent).toContain("Facade");
    expect(routeContent).toContain("SignFixtureAsync");
    expect(routeContent).not.toContain("HandleAsync");

    // gRPC service also delegates through the façade (the re-point).
    const grpcContent = getEmittedFile(host, "SignFixtureSignerService.g.cs");
    expect(grpcContent).toBeDefined();
    expect(grpcContent).toContain("SignerFacade");
    expect(grpcContent).toContain("SignFixtureAsync");
    expect(grpcContent).not.toContain("HandleAsync");
    expect(grpcContent).not.toContain("ISignFixtureHandler");
  });
});

// ---------------------------------------------------------------------------
// Test 9: @d2Idempotent on an op WITHOUT @route → D2TSP006
// ---------------------------------------------------------------------------

describe("routeEmitIntegration_IdempotentWithoutRoute_D2TSP006", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [
        HttpTestLibrary,
        D2DecoratorTestLibrary,
        D2EmitterTestLibrary,
      ],
    });
  });

  it("@d2Idempotent on op with no @route → D2TSP006 error (idempotent-requires-route)", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Fixtures;

      model IdempInput { kid: string; }
      model IdempOutput { result: string; }

      @d2Command
      @d2ServedBy("KeyCustodian")
      @d2InProcess
      @d2Idempotent("header", 86400)
      op idempNoRoute(input: IdempInput): IdempOutput;
      `,
    );

    const diagnostics = await host.diagnose("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: { "@d2/typespec-emitters": FIXTURE_OPTIONS },
      outputDir: "testing:/out",
    });

    const d2tsp006 = diagnostics.filter(
      (d) =>
        d.severity === "error" && d.code.includes("idempotent-requires-route"),
    );
    expect(d2tsp006.length).toBeGreaterThan(0);
  });
});

// ---------------------------------------------------------------------------
// Test 10: @d2Idempotent (header) + @route → seam + gated route emitted
// ---------------------------------------------------------------------------

describe("routeEmitIntegration_Idempotent_Header_GatedRouteEmitted", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [
        HttpTestLibrary,
        D2DecoratorTestLibrary,
        D2EmitterTestLibrary,
      ],
    });
  });

  it("@d2Idempotent('header', 86400) + @route → route includes store param + gate lines + seam emitted", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      import "@typespec/http";
      using D2;
      using Http;
      namespace D2.Fixtures;

      model SignFixtureInput { kid: string; }
      model SignFixtureOutput { signature: string; }

      @d2Command
      @d2ServedBy("SignFixture")
      @d2InProcess
      @route("/internal/v1/fixtures/sign-fixture")
      @post
      @d2RequireAnyScope("self.write")
      @d2Idempotent("header", 86400)
      op signFixture(input: SignFixtureInput): SignFixtureOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: { "@d2/typespec-emitters": FIXTURE_OPTIONS },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // Route registration contains the store parameter and gate calls.
    const routeContent = getEmittedFile(
      host,
      "SignFixtureRouteRegistration.g.cs",
    );
    expect(routeContent).toBeDefined();
    expect(routeContent).toContain("D2GeneratedIdempotencyStore store");
    expect(routeContent).toContain("Idempotency-Key");
    expect(routeContent).toContain("TryGetAsync");
    expect(routeContent).toContain("StoreAsync");
    expect(routeContent).toContain("ValidationFailed");

    // Idempotency store seam emitted once for this namespace.
    const seamContent = getEmittedFile(
      host,
      "D2GeneratedIdempotencyStore.g.cs",
    );
    expect(seamContent).toBeDefined();
    expect(seamContent).toContain("D2GeneratedIdempotencyStore");
    expect(seamContent).toContain("TryGetAsync");
    expect(seamContent).toContain("StoreAsync");
  });
});
