// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Integration tests for the TS client emitters via the $onEmit dispatch.
//
// Compiles inline .tsp through the TypeSpec test-host with the emitter in the
// emit list and asserts the in-memory FS contains the emitted TS client files:
//   - <module>-grpc-client.g.ts for a @d2GrpcMethod op (real-module mode).
//   - <module>-rest-client.g.ts for a @route op.
//   - an op carrying BOTH @route AND @d2GrpcMethod appears in BOTH surfaces.
//   - a @d2Resilience gRPC op's TS client folds in the predicate retry-arm.
//
// This proves the DISPATCH wiring in emitter.ts (collection + after-walk loops),
// not just the pure emitter functions (covered by the unit + byte-parity tests).

import { describe, it, expect, beforeAll } from "vitest";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";
import { HttpTestLibrary } from "@typespec/http/testing";

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

const D2EmitterTestLibrary = createTestLibrary({
  name: "@d2/typespec-emitters",
  packageRoot: await findTestPackageRoot(import.meta.url),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

function getEmittedFile(
  host: Awaited<ReturnType<typeof createTestHost>>,
  suffix: string,
): string | undefined {
  const stored = (host as unknown as { fs?: Map<string, string> }).fs;
  if (!(stored instanceof Map)) return undefined;
  const key = [...stored.keys()].find((k) => k.endsWith(suffix));
  return key !== undefined ? stored.get(key) : undefined;
}

// Real-module options — csClientsNamespace gates gRPC-client collection; the TS
// gRPC client reuses that collection. The REST client collects unconditionally.
const REAL_MODULE_OPTIONS = {
  "csharp-namespace": "D2.Test",
  "csharp-clients-namespace": "D2.Edge.KeyCustodian.Clients",
  "csharp-app-namespace-base": "D2.Edge.KeyCustodian.App.Application.Handlers",
};

describe("tsClientEmitIntegration_GrpcClient_DispatchedForGrpcOp", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [
        D2DecoratorTestLibrary,
        D2EmitterTestLibrary,
        HttpTestLibrary,
      ],
    });
  });

  it("a @route + @d2GrpcMethod op (sign) emits BOTH a TS gRPC client AND a TS REST client", async () => {
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
      @d2Idempotent("header", 86400)
      op signFixture(input: SignFixtureInput): SignFixtureOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: { "@d2/typespec-emitters": REAL_MODULE_OPTIONS },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // TS gRPC client (server surface) — @d2GrpcMethod.
    const grpc = getEmittedFile(host, "sign-fixture-grpc-client.g.ts");
    expect(grpc).toBeDefined();
    expect(grpc).toContain("export interface SignFixtureGrpcClient {");
    expect(grpc).toContain("signFixture(input: SignFixtureInput");
    expect(grpc).toContain("createSignFixtureGrpcClient");
    expect(grpc).toContain('from "@d2/grpc-client"');

    // TS REST client (browser surface) — @route.
    const rest = getEmittedFile(host, "sign-fixture-rest-client.g.ts");
    expect(rest).toBeDefined();
    expect(rest).toContain("export interface SignFixtureRestClient {");
    expect(rest).toContain(
      'apiCall<SignFixtureOutput>("/internal/v1/fixtures/sign-fixture"',
    );
    expect(rest).toContain("idempotencyKey: opts?.idempotencyKey,");
  });
});

describe("tsClientEmitIntegration_PredicateRetryArm_FoldedIn", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("a @d2Resilience gRPC op (placeOrder) emits a TS gRPC client that consumes the predicate twin", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Fixtures;

      model PlaceOrderFixtureInput { @d2Field(1) customerId: string; }
      model PlaceOrderFixtureOutput { @d2Field(1) orderCode: string; @d2Field(2) itemStatuses: string[]; @d2Field(3) partial: boolean; }

      @d2Command
      @d2ServedBy("PredicateFixtures")
      @d2GrpcMethod("PredicateFixturesOrders", "PlaceOrderFixture")
      @d2Resilience(
        "retry(3)",
        #{
          retryWhen: "result.category == \\"infrastructure_unavailable\\" || result.data.itemStatuses.contains(\\"PENDING\\") || result.data.partial == true",
          failWhen: "result.data.itemStatuses.count == 0 || result.errorCode == \\"VALIDATION_FAILED\\"",
        }
      )
      op placeOrderFixture(input: PlaceOrderFixtureInput): PlaceOrderFixtureOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          ...REAL_MODULE_OPTIONS,
          "csharp-clients-namespace": "D2.Edge.PredicateFixtures.Clients",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    const grpc = getEmittedFile(host, "predicate-fixtures-grpc-client.g.ts");
    expect(grpc).toBeDefined();
    // The predicate retry-arm is folded in: imports the predicate twin + builds a pipeline.
    expect(grpc).toContain(
      'import { placeOrderFixtureRetryWhen, placeOrderFixtureFailWhen } from "./place-order-fixture-resilience-predicates.js";',
    );
    expect(grpc).toContain("new ResilientPipelineBuilder()");
    expect(grpc).toContain(
      "placeOrderFixtureRetryWhen(result) && !placeOrderFixtureFailWhen(result)",
    );
    expect(grpc).toContain("maxAttempts: 3,");

    // The TS predicate twin itself is also emitted (the retry-arm consumes it).
    const twin = getEmittedFile(
      host,
      "place-order-fixture-resilience-predicates.g.ts",
    );
    expect(twin).toBeDefined();
    expect(twin).toContain("export const placeOrderFixtureRetryWhen");

    // placeOrder is @d2GrpcMethod-only (no @route) → NO REST client for the module.
    const rest = getEmittedFile(host, "predicate-fixtures-rest-client.g.ts");
    expect(rest).toBeUndefined();
  });

  it("a bare retry() predicate op (no explicit count) defaults the pipeline budget to 3", async () => {
    const bareHost = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
    bareHost.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Fixtures;

      model BareInput { @d2Field(1) customerId: string; }
      model BareOutput { @d2Field(1) orderCode: string; @d2Field(2) partial: boolean; }

      @d2Command
      @d2ServedBy("BareFixtures")
      @d2GrpcMethod("BareFixturesOrders", "BarePlace")
      @d2Resilience(
        "retry()",
        #{
          retryWhen: "result.data.partial == true",
          failWhen: "result.errorCode == \\"VALIDATION_FAILED\\"",
        }
      )
      op barePlace(input: BareInput): BareOutput;
      `,
    );

    await bareHost.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          ...REAL_MODULE_OPTIONS,
          "csharp-clients-namespace": "D2.Edge.BareFixtures.Clients",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = bareHost.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    const grpc = getEmittedFile(bareHost, "bare-fixtures-grpc-client.g.ts");
    expect(grpc).toBeDefined();
    // No explicit count in the DSL → the emitter defaults maxAttempts to 3.
    expect(grpc).toContain("maxAttempts: 3,");
  });
});

describe("tsClientEmitIntegration_RestOnlyOp_NoGrpcClient", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [
        D2DecoratorTestLibrary,
        D2EmitterTestLibrary,
        HttpTestLibrary,
      ],
    });
  });

  it("a @route-only op emits a TS REST client but NO TS gRPC client", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      import "@typespec/http";
      using D2;
      using Http;
      namespace D2.Fixtures;

      model ProfileInput { userId: string; }
      model ProfileOutput { displayName: string; }

      @d2Query
      @d2ServedBy("Accounts")
      @route("/v1/accounts/profile")
      @get
      @d2RequireAnyScope("self.read")
      op getProfile(input: ProfileInput): ProfileOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          ...REAL_MODULE_OPTIONS,
          "csharp-clients-namespace": "D2.Edge.Accounts.Clients",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    const rest = getEmittedFile(host, "accounts-rest-client.g.ts");
    expect(rest).toBeDefined();
    // A GET op binds the input as a query string (no body).
    expect(rest).toContain('withQuery("/v1/accounts/profile", input)');
    expect(rest).toContain('method: "GET"');

    const grpc = getEmittedFile(host, "accounts-grpc-client.g.ts");
    expect(grpc).toBeUndefined();
  });
});
