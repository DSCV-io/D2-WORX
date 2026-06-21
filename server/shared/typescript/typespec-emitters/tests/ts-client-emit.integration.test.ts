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

      model SignInput { kid: string; @d2Redact payload: bytes; }
      model SignOutput { signature: string; }

      @d2Command
      @d2ServedBy("KeyCustodian")
      @d2InProcess
      @d2GrpcMethod("KeyCustodianSigner", "Sign")
      @route("/internal/v1/kc/sign")
      @post
      @d2RequireAnyScope("self.write")
      @d2Idempotent("header", 86400)
      op sign(input: SignInput): SignOutput;
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
    const grpc = getEmittedFile(host, "key-custodian-grpc-client.g.ts");
    expect(grpc).toBeDefined();
    expect(grpc).toContain("export interface KeyCustodianGrpcClient {");
    expect(grpc).toContain("sign(input: SignInput");
    expect(grpc).toContain("createKeyCustodianGrpcClient");
    expect(grpc).toContain('from "@d2/grpc-client"');

    // TS REST client (browser surface) — @route.
    const rest = getEmittedFile(host, "key-custodian-rest-client.g.ts");
    expect(rest).toBeDefined();
    expect(rest).toContain("export interface KeyCustodianRestClient {");
    expect(rest).toContain('apiCall<SignOutput>("/internal/v1/kc/sign"');
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

      model PlaceOrderInput { customerId: string; }
      model PlaceOrderOutput { orderCode: string; itemStatuses: string[]; partial: boolean; }

      @d2Command
      @d2ServedBy("PredicateFixtures")
      @d2GrpcMethod("PredicateFixturesOrders", "PlaceOrder")
      @d2Resilience(
        "retry(3)",
        #{
          retryWhen: "result.category == \\"infrastructure_unavailable\\" || result.data.itemStatuses.contains(\\"PENDING\\") || result.data.partial == true",
          failWhen: "result.data.itemStatuses.count == 0 || result.errorCode == \\"VALIDATION_FAILED\\"",
        }
      )
      op placeOrder(input: PlaceOrderInput): PlaceOrderOutput;
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
    // The predicate retry-arm is folded in: imports the C-5 twin + builds a pipeline.
    expect(grpc).toContain(
      'import { placeOrderRetryWhen, placeOrderFailWhen } from "./place-order-resilience-predicates.js";',
    );
    expect(grpc).toContain("new ResilientPipelineBuilder()");
    expect(grpc).toContain(
      "placeOrderRetryWhen(result) && !placeOrderFailWhen(result)",
    );
    expect(grpc).toContain("maxAttempts: 3,");

    // The C-5 TS predicate twin itself is also emitted (the retry-arm consumes it).
    const twin = getEmittedFile(host, "place-order-resilience-predicates.g.ts");
    expect(twin).toBeDefined();
    expect(twin).toContain("export const placeOrderRetryWhen");

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

      model BareInput { customerId: string; }
      model BareOutput { orderCode: string; partial: boolean; }

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
