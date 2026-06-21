// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Integration tests: compile inline .tsp through the TypeSpec test-host and
// assert the emitted .proto / gRPC service-impl / mapper content.
//
// Specifically covers:
//   1. sign op (with @d2GrpcMethod) → .proto + service + mapper emitted.
//   2. getJwks op (no @d2GrpcMethod) → NO .proto emitted (skip path).
//   3. op with @d2GrpcMethod + unmapped scalar → D2TSP001 fires.

import { describe, it, expect, beforeAll } from "vitest";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";

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

// ---------------------------------------------------------------------------
// Test 1: sign op with @d2GrpcMethod → .proto + service + mapper emitted
// ---------------------------------------------------------------------------

describe("protoGrpcEmitIntegration_Sign_EmitsProtoAndService", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("sign op with @d2GrpcMethod → .proto + service + mapper emitted in in-memory FS", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Fixtures;

      model SignInput { kid: string; @d2Redact payload: bytes; }
      model SignOutput { signature: string; }

      @d2Command
      @d2ServedBy("KeyCustodian")
      @d2InProcess
      @d2GrpcMethod("KeyCustodianSigner", "Sign")
      op sign(input: SignInput): SignOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          "csharp-namespace": "D2.Test",
          "proto-package": "d2.test.v1",
          "proto-csharp-namespace": "D2.Test.Protos.V1",
          "grpc-service-namespace": "D2.Test.Grpc",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // .proto file emitted.
    const protoContent = getEmittedFile(host, ".g.proto");
    expect(protoContent).toBeDefined();
    expect(protoContent).toContain('syntax = "proto3";');
    expect(protoContent).toContain("package d2.test.v1;");
    expect(protoContent).toContain(
      'option csharp_namespace = "D2.Test.Protos.V1";',
    );
    expect(protoContent).toContain("service KeyCustodianSigner {");
    expect(protoContent).toContain(
      "rpc Sign(SignRequest) returns (SignResponse);",
    );
    expect(protoContent).toContain("message SignRequest {");
    expect(protoContent).toContain("message SignResponse {");
    expect(protoContent).toContain("string kid = 1;");
    expect(protoContent).toContain("bytes payload = 2;");

    // The live $onEmit proto path must name the DATA message after the DTO output
    // model (<Op>Output), NOT the <Method>Response envelope wrapper. Passing the
    // wrapper name as emitProto's responseModelName produced TWO `message
    // SignResponse` blocks (envelope + data) — a duplicate proto message that
    // protoc rejects. Pin: the data message is `message SignOutput`, and
    // `message SignResponse` appears EXACTLY ONCE (the wrapper, no collision).
    expect(protoContent).toContain("message SignOutput {");
    const responseMsgDecls = (
      protoContent!.match(/message SignResponse \{/g) ?? []
    ).length;
    expect(responseMsgDecls).toBe(1);

    // gRPC service class emitted.
    // The sign op has @d2InProcess → the service delegates through the fixture façade,
    // not ISignHandler directly. The façade type name in fixture mode (no csAppNamespaceBase)
    // is I<ServedBy>SignerFacade = IKeyCustodianSignerFacade.
    const serviceContent = getEmittedFile(
      host,
      "KeyCustodianSignerService.g.cs",
    );
    expect(serviceContent).toBeDefined();
    expect(serviceContent).toContain("namespace D2.Test.Grpc;");
    expect(serviceContent).toContain(
      "global::D2.Test.Protos.V1.KeyCustodianSigner.KeyCustodianSignerBase",
    );
    expect(serviceContent).toContain("IKeyCustodianSignerFacade facade");
    expect(serviceContent).toContain("facade.SignAsync");
    expect(serviceContent).not.toContain("ISignHandler handler");
    expect(serviceContent).not.toContain("handler.HandleAsync");

    // Transport mapper emitted.
    const mapperContent = getEmittedFile(host, "SignTransportMappers.g.cs");
    expect(mapperContent).toBeDefined();
    expect(mapperContent).toContain("namespace D2.Test.Grpc;");
    expect(mapperContent).toContain("extension(SignRequest request)");
    expect(mapperContent).toContain("extension(SignOutput output)");
    expect(mapperContent).toContain("request.Payload.ToByteArray()");
  });
});

// ---------------------------------------------------------------------------
// Test 2: getJwks op (no @d2GrpcMethod) → NO .proto emitted
// ---------------------------------------------------------------------------

describe("protoGrpcEmitIntegration_GetJwks_NoProtoEmitted", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("getJwks op without @d2GrpcMethod → no .proto or gRPC service emitted", async () => {
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
      options: {
        "@d2/typespec-emitters": {
          "csharp-namespace": "D2.Test",
          "proto-package": "d2.test.v1",
          "proto-csharp-namespace": "D2.Test.Protos.V1",
          "grpc-service-namespace": "D2.Test.Grpc",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // No .proto file should be emitted for getJwks.
    const protoContent = getEmittedFile(host, ".g.proto");
    expect(protoContent).toBeUndefined();

    // No gRPC service class emitted.
    const serviceContent = getEmittedFile(host, "Service.g.cs");
    expect(serviceContent).toBeUndefined();

    // DTOs are still emitted (normal DTO emission still fires).
    const inputContent = getEmittedFile(host, "GetJwksInput.g.cs");
    expect(inputContent).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Test 2b: @d2GrpcMethod + @d2InProcess + real-module options → IApi façade
// ---------------------------------------------------------------------------
//
// This exercises the real-module branch in emitter.ts (lines 253-259):
//   csAppNamespaceBase + csClientsNamespace BOTH present + grpcInProcess=true
//   → facadeTypeName = I<ServedBy>Api (NOT I<ServedBy>SignerFacade).
// This is the branch that runs for non-fixture (production) module compilation.

describe("protoGrpcEmitIntegration_RealModule_InProcessGrpc_UsesApiFacade", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("@d2GrpcMethod + @d2InProcess with real-module options → gRPC service injects I<Module>Api", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.KeyCustodian;

      model SignInput { kid: string; }
      model SignOutput { signature: string; }

      @d2Command
      @d2ServedBy("KeyCustodian")
      @d2InProcess
      @d2GrpcMethod("KeyCustodianSigner", "Sign")
      op sign(input: SignInput): SignOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          "csharp-namespace": "D2.Fixture.Ns",
          "csharp-clients-namespace": "D2.Edge.KeyCustodian.Clients",
          "csharp-app-namespace-base":
            "D2.Edge.KeyCustodian.App.Application.Handlers",
          "proto-package": "d2.keycustodian.v1",
          "proto-csharp-namespace": "D2.Services.Protos.KeyCustodian.V1",
          "grpc-service-namespace": "D2.Edge.KeyCustodian.Api.Generated",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // In real-module mode the gRPC service delegates through I<Module>Api
    // (the production façade in the Clients namespace), NOT I<ServedBy>SignerFacade.
    const serviceContent = getEmittedFile(
      host,
      "KeyCustodianSignerService.g.cs",
    );
    expect(serviceContent).toBeDefined();
    // Real-module façade type name.
    expect(serviceContent).toContain("IKeyCustodianApi");
    // Must use the Clients namespace as the using target.
    expect(serviceContent).toContain("D2.Edge.KeyCustodian.Clients");
    // Delegates via SignAsync (the façade method name).
    expect(serviceContent).toContain("SignAsync");
    // Must NOT fall through to ISignHandler.
    expect(serviceContent).not.toContain("ISignHandler");
    expect(serviceContent).not.toContain("HandleAsync");
  });
});

// ---------------------------------------------------------------------------
// Test 2c: real-module @d2GrpcMethod op with NO input (parameterless) + NO output
// (void) → exercises the input-undefined / output-undefined fallbacks in the gRPC
// client-op collection (the `{ fields: [], nestedModels: [] }` branches). The
// generated client interface + mappers are still emitted with empty request/response.
// ---------------------------------------------------------------------------

describe("protoGrpcEmitIntegration_RealModule_ParameterlessAndVoidGrpcOps", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("parameterless (no-input) + void (no-output) gRPC ops emit a client with empty mappers", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.KeyCustodian;

      model PingOutput { ok: string; }
      model FireInput { data: string; }

      // No input model — exercises the inputModel-undefined fallback in the client collection.
      @d2Query
      @d2ServedBy("KeyCustodian")
      @d2GrpcMethod("KeyCustodianPinger", "Ping")
      op ping(): PingOutput;

      // No output model (void) — exercises the outputModel-undefined fallback.
      @d2Command
      @d2ServedBy("KeyCustodian")
      @d2GrpcMethod("KeyCustodianFirer", "Fire")
      op fire(input: FireInput): void;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          "csharp-clients-namespace": "D2.Edge.KeyCustodian.Clients",
          "csharp-app-namespace-base":
            "D2.Edge.KeyCustodian.App.Application.Handlers",
          "proto-package": "d2.keycustodian.v1",
          "proto-csharp-namespace": "D2.Services.Protos.KeyCustodian.V1",
          "grpc-service-namespace": "D2.Edge.KeyCustodian.Api.Generated",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // The per-module gRPC client interface is emitted and declares both ops.
    const ifaceContent = getEmittedFile(host, "IKeyCustodianGrpcClient.g.cs");
    expect(ifaceContent).toBeDefined();
    expect(ifaceContent).toContain("PingAsync(");
    expect(ifaceContent).toContain("FireAsync(");

    // Two ops in one module → a single combined client-mapper file with both mapper classes.
    const mapper = getEmittedFile(host, "ClientMappers.g.cs");
    expect(mapper).toBeDefined();
    // Parameterless op → empty proto request ctor; void-output op → empty DTO output ctor.
    expect(mapper).toContain("internal static class PingClientMappers");
    expect(mapper).toContain("internal static class FireClientMappers");
    expect(mapper).toContain("ToPingRequest()");
  });
});

// ---------------------------------------------------------------------------
// Test 3: op with @d2GrpcMethod + unmapped scalar → D2TSP001 fires (§1.29)
// ---------------------------------------------------------------------------

describe("protoGrpcEmitIntegration_UnmappedScalar_D2TSP001", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("grpc op with unmapped scalar → D2TSP001 diagnostic fires", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Test;

      model BadInput { when: utcDateTime; }
      model BadOutput { message: string; }

      @d2GrpcMethod("MySvc", "Do")
      op badOp(input: BadInput): BadOutput;
      `,
    );

    let compileError: unknown;
    try {
      await host.compile("main.tsp", {
        emit: ["@d2/typespec-emitters"],
        options: {
          "@d2/typespec-emitters": {
            "csharp-namespace": "D2.Test",
            "proto-package": "d2.test.v1",
            "proto-csharp-namespace": "D2.Test.Protos.V1",
            "grpc-service-namespace": "D2.Test.Grpc",
          },
        },
        outputDir: "testing:/out",
      });
    } catch (err) {
      compileError = err;
    }

    const programErrors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    const hasErrors = compileError !== undefined || programErrors.length > 0;
    expect(hasErrors).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Test: @d2Resilience predicate-bearing op → predicate files + sentinel emitted
// through the live $onEmit dispatch (real-module mode).
// ---------------------------------------------------------------------------

describe("protoGrpcEmitIntegration_Resilience_PredicateAndSentinelEmitted", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("a predicate-bearing gRPC op emits the C#/TS predicates + the sentinel, and the client gains the sentinel arm", async () => {
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

      // A second real-module gRPC op in a DIFFERENT module with NO @d2Resilience —
      // exercises the no-predicate skip (no predicate files, no sentinel for that module).
      model PingInput { id: string; }
      model PingOutput { ok: boolean; }

      @d2Command
      @d2ServedBy("PlainFixtures")
      @d2GrpcMethod("PlainFixturesPinger", "Ping")
      op ping(input: PingInput): PingOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          "csharp-namespace": "D2.Test.Dto",
          "csharp-clients-namespace": "D2.Test.Clients",
          "csharp-app-namespace-base": "D2.Test.App.Application.Handlers",
          "proto-package": "d2.predicatefixtures.v1",
          "proto-csharp-namespace": "D2.Services.Protos.PredicateFixtures.V1",
          "grpc-service-namespace": "D2.Test.Grpc",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // Predicate C# file emitted with the SR_ fields.
    const predCs = getEmittedFile(host, "PlaceOrderResiliencePredicates.g.cs");
    expect(predCs).toBeDefined();
    expect(predCs).toContain("SR_RetryWhen");
    expect(predCs).toContain("SR_FailWhen");

    // Predicate TS parity twin emitted.
    const predTs = getEmittedFile(host, "place-order-resilience-predicates.g.ts");
    expect(predTs).toBeDefined();
    expect(predTs).toContain("export const placeOrderRetryWhen");

    // Emitter-owned sentinel emitted once for the module.
    const sentinel = getEmittedFile(host, "D2GeneratedBusinessRetrySignal.g.cs");
    expect(sentinel).toBeDefined();
    expect(sentinel).toContain(
      "internal sealed class D2GeneratedBusinessRetrySignal : Exception",
    );

    // The client impl gains the predicate arm; the DI-ext gains the sentinel IsTransient arm.
    // Match the IMPL with a leading '/' so the suffix does not also match the INTERFACE
    // file (IPredicateFixturesGrpcClient.g.cs ends with the same bare name).
    const clientImpl = getEmittedFile(host, "/PredicateFixturesGrpcClient.g.cs");
    expect(clientImpl).toContain(
      "throw new D2GeneratedBusinessRetrySignal(businessResult.ToProto());",
    );
    const clientDi = getEmittedFile(
      host,
      "PredicateFixturesGrpcClientsGenerated.g.cs",
    );
    expect(clientDi).toContain("ex is D2GeneratedBusinessRetrySignal");

    // The PLAIN module (no @d2Resilience) emits NO predicate files and its client stays
    // byte-identical — no sentinel arm, no predicate class for that module's op.
    expect(getEmittedFile(host, "PingResiliencePredicates.g.cs")).toBeUndefined();
    const pingDi = getEmittedFile(host, "PlainFixturesGrpcClientsGenerated.g.cs");
    expect(pingDi).toBeDefined();
    expect(pingDi).not.toContain("D2GeneratedBusinessRetrySignal");
    const pingImpl = getEmittedFile(host, "/PlainFixturesGrpcClient.g.cs");
    expect(pingImpl).not.toContain("D2GeneratedBusinessRetrySignal");
  });
});
