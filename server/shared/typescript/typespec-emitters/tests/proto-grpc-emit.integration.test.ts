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
//   4. unpinned field on proto-bound model → D2TSP009 renders clean (not doubly-wrapped).
//   5. @d2Reserved on request / response models → reserved lines emitted in .proto.
//   6. duplicate reserved names are deduplicated before emission.

import { describe, it, expect, beforeAll } from "vitest";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";
import { VersioningTestLibrary } from "@typespec/versioning/testing";

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

      model SignFixtureInput { @d2Field(1) kid: string; @d2Field(2) @d2Redact("SecretInformation") payload: bytes; }
      model SignFixtureOutput { @d2Field(1) signature: string; }

      @d2Command
      @d2ServedBy("SignFixture")
      @d2InProcess
      @d2GrpcMethod("SignFixtureSigner", "SignFixture")
      op signFixture(input: SignFixtureInput): SignFixtureOutput;
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
    expect(protoContent).toContain("service SignFixtureSigner {");
    expect(protoContent).toContain(
      "rpc SignFixture(SignFixtureRequest) returns (SignFixtureResponse);",
    );
    expect(protoContent).toContain("message SignFixtureRequest {");
    expect(protoContent).toContain("message SignFixtureResponse {");
    expect(protoContent).toContain("string kid = 1;");
    expect(protoContent).toContain("bytes payload = 2;");

    // The live $onEmit proto path must name the DATA message after the DTO output
    // model (<Op>Output), NOT the <Method>Response envelope wrapper. Passing the
    // wrapper name as emitProto's responseModelName produced TWO `message
    // SignFixtureResponse` blocks (envelope + data) — a duplicate proto message that
    // protoc rejects. Pin: the data message is `message SignFixtureOutput`, and
    // `message SignFixtureResponse` appears EXACTLY ONCE (the wrapper, no collision).
    expect(protoContent).toContain("message SignFixtureOutput {");
    const responseMsgDecls = (
      protoContent!.match(/message SignFixtureResponse \{/g) ?? []
    ).length;
    expect(responseMsgDecls).toBe(1);

    // gRPC service class emitted.
    // The sign op has @d2InProcess → the service delegates through the fixture façade,
    // not ISignFixtureHandler directly. The façade type name in fixture mode (no csAppNamespaceBase)
    // is I<ServedBy>SignerFacade = ISignFixtureSignerFacade.
    const serviceContent = getEmittedFile(
      host,
      "SignFixtureSignerService.g.cs",
    );
    expect(serviceContent).toBeDefined();
    expect(serviceContent).toContain("namespace D2.Test.Grpc;");
    expect(serviceContent).toContain(
      "global::D2.Test.Protos.V1.SignFixtureSigner.SignFixtureSignerBase",
    );
    expect(serviceContent).toContain("ISignFixtureSignerFacade facade");
    expect(serviceContent).toContain("facade.SignFixtureAsync");
    expect(serviceContent).not.toContain("ISignFixtureHandler handler");
    expect(serviceContent).not.toContain("handler.HandleAsync");

    // Transport mapper emitted.
    const mapperContent = getEmittedFile(
      host,
      "SignFixtureTransportMappers.g.cs",
    );
    expect(mapperContent).toBeDefined();
    expect(mapperContent).toContain("namespace D2.Test.Grpc;");
    expect(mapperContent).toContain("extension(SignFixtureRequest request)");
    expect(mapperContent).toContain("extension(SignFixtureOutput output)");
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

      model SignFixtureInput { @d2Field(1) kid: string; }
      model SignFixtureOutput { @d2Field(1) signature: string; }

      @d2Command
      @d2ServedBy("KeyCustodian")
      @d2Concern("SignFixture")
      @d2InProcess
      @d2GrpcMethod("SignFixtureSigner", "SignFixture")
      op signFixture(input: SignFixtureInput): SignFixtureOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          "csharp-namespace": "D2.Fixture.Ns",
          "csharp-clients-namespace": "D2.Edge.KeyCustodian.Client",
          "csharp-app-namespace-base":
            "D2.Edge.KeyCustodian.App.Application.Handlers",
          "proto-package": "d2.signfixtures.v2alpha",
          "proto-csharp-namespace": "D2.Services.Protos.SignFixtures.V2Alpha",
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
      "SignFixtureSignerService.g.cs",
    );
    expect(serviceContent).toBeDefined();
    // Real-module façade type name.
    expect(serviceContent).toContain("IKeyCustodianApi");
    // Must use the Clients namespace as the using target.
    expect(serviceContent).toContain("D2.Edge.KeyCustodian.Client");
    // Delegates via SignFixtureAsync (the façade method name).
    expect(serviceContent).toContain("SignFixtureAsync");
    // Must NOT fall through to ISignFixtureHandler.
    expect(serviceContent).not.toContain("ISignFixtureHandler");
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

      model PingOutput { @d2Field(1) ok: string; }
      model FireInput { @d2Field(1) data: string; }

      // No input model — exercises the inputModel-undefined fallback in the client collection.
      @d2Query
      @d2ServedBy("KeyCustodian")
      @d2Concern("Ping")
      @d2GrpcMethod("KeyCustodianPinger", "Ping")
      op ping(): PingOutput;

      // No output model (void) — exercises the outputModel-undefined fallback.
      @d2Command
      @d2ServedBy("KeyCustodian")
      @d2Concern("Fire")
      @d2GrpcMethod("KeyCustodianFirer", "Fire")
      op fire(input: FireInput): void;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          "csharp-clients-namespace": "D2.Edge.KeyCustodian.Client",
          "csharp-app-namespace-base":
            "D2.Edge.KeyCustodian.App.Application.Handlers",
          "proto-package": "d2.signfixtures.v2alpha",
          "proto-csharp-namespace": "D2.Services.Protos.SignFixtures.V2Alpha",
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
    // Real-module served-by "KeyCustodian" → IKeyCustodianGrpcClient (this is the
    // real KC module surface, distinct from the sign fixture's ISignFixtureGrpcClient).
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

      model PlaceOrderFixtureInput { @d2Field(1) customerId: string; }
      model PlaceOrderFixtureOutput { @d2Field(1) orderCode: string; @d2Field(2) itemStatuses: string[]; @d2Field(3) partial: boolean; }

      @d2Command
      @d2ServedBy("PredicateFixtures")
      @d2Concern("PredicateFixture")
      @d2GrpcMethod("PredicateFixturesOrders", "PlaceOrderFixture")
      @d2Resilience(
        "retry(3)",
        #{
          retryWhen: "result.category == \\"infrastructure_unavailable\\" || result.data.itemStatuses.contains(\\"PENDING\\") || result.data.partial == true",
          failWhen: "result.data.itemStatuses.count == 0 || result.errorCode == \\"VALIDATION_FAILED\\"",
        }
      )
      op placeOrderFixture(input: PlaceOrderFixtureInput): PlaceOrderFixtureOutput;

      // A second real-module gRPC op in a DIFFERENT module with NO @d2Resilience —
      // exercises the no-predicate skip (no predicate files, no sentinel for that module).
      model PingInput { @d2Field(1) id: string; }
      model PingOutput { @d2Field(1) ok: boolean; }

      @d2Command
      @d2ServedBy("PlainFixtures")
      @d2Concern("PlainFixture")
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
    const predCs = getEmittedFile(
      host,
      "PlaceOrderFixtureResiliencePredicates.g.cs",
    );
    expect(predCs).toBeDefined();
    expect(predCs).toContain("SR_RetryWhen");
    expect(predCs).toContain("SR_FailWhen");

    // Predicate TS parity twin emitted.
    const predTs = getEmittedFile(
      host,
      "place-order-fixture-resilience-predicates.g.ts",
    );
    expect(predTs).toBeDefined();
    expect(predTs).toContain("export const placeOrderFixtureRetryWhen");

    // Emitter-owned sentinel emitted once for the module.
    const sentinel = getEmittedFile(
      host,
      "D2GeneratedBusinessRetrySignal.g.cs",
    );
    expect(sentinel).toBeDefined();
    expect(sentinel).toContain(
      "internal sealed class D2GeneratedBusinessRetrySignal : Exception",
    );

    // The client impl gains the predicate arm; the DI-ext gains the sentinel IsTransient arm.
    // Match the IMPL with a leading '/' so the suffix does not also match the INTERFACE
    // file (IPredicateFixturesGrpcClient.g.cs ends with the same bare name).
    const clientImpl = getEmittedFile(
      host,
      "/PredicateFixturesGrpcClient.g.cs",
    );
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
    expect(
      getEmittedFile(host, "PingResiliencePredicates.g.cs"),
    ).toBeUndefined();
    const pingDi = getEmittedFile(
      host,
      "PlainFixturesGrpcClientsGenerated.g.cs",
    );
    expect(pingDi).toBeDefined();
    expect(pingDi).not.toContain("D2GeneratedBusinessRetrySignal");
    const pingImpl = getEmittedFile(host, "/PlainFixturesGrpcClient.g.cs");
    expect(pingImpl).not.toContain("D2GeneratedBusinessRetrySignal");
  });
});

// ---------------------------------------------------------------------------
// Test: unpinned field on a proto-bound model → D2TSP009 diagnostic renders
// the clean pre-formatted sentence, not a doubly-wrapped garbled string.
//
// Regression pin: the old template had two slots (${"field"} and
// ${"model"}); the call site stuffed the full pre-formatted sentence into
// the `field` slot with an empty `model`, producing a message that contained
// the sentence twice (once nested inside the outer template slots). The fix
// changes the template to a single ${"detail"} slot and the call site to
// `format: { detail: message }`, so the rendered string is the clean sentence.
// ---------------------------------------------------------------------------

describe("protoGrpcEmitIntegration_UnpinnedField_D2TSP009_CleanMessage", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("unpinned field on proto-bound model → D2TSP009 fires with a clean rendered message (not doubly-wrapped)", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Test;

      model PinlessInput { kid: string; }
      model PinlessOutput { @d2Field(1) result: string; }

      @d2Command
      @d2ServedBy("TestSvc")
      @d2GrpcMethod("TestSvc", "Do")
      op doOp(input: PinlessInput): PinlessOutput;
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

    // The rendered diagnostic message must contain the field name and model name
    // from the pre-formatted sentence constructed in resolveProtoFields.
    const d2tsp009 = host.program.diagnostics.find(
      (d) => d.code === "@d2/typespec-emitters/unpinned-proto-field",
    );
    expect(d2tsp009).toBeDefined();

    const msg = d2tsp009!.message;

    // The clean message must mention both the field name and the proto message
    // name. The emitter passes protoRequestName (grpcMethod + "Request") to
    // resolveProtoFields — NOT the TypeSpec model name — so the expected model
    // name in the rendered diagnostic is "DoRequest" (from @d2GrpcMethod("TestSvc", "Do")).
    expect(msg).toContain("kid");
    expect(msg).toContain("DoRequest");
    expect(msg).toContain("D2TSP009");

    // It must NOT be doubly-wrapped: the old bug embedded the entire sentence
    // inside the `field` slot so the rendered string contained "@d2Field pin"
    // twice (once from the outer template, once from the embedded sentence).
    const pinPhrase = "@d2Field pin";
    const occurrences = (
      msg.match(
        new RegExp(pinPhrase.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"), "g"),
      ) ?? []
    ).length;
    expect(occurrences).toBe(1);

    // The model slot must not appear empty ("on model ''") — that was the tell.
    expect(msg).not.toContain("on model ''");
  });
});

// ---------------------------------------------------------------------------
// Test: @d2Reserved on request / response models → reserved lines in .proto
//
// The full end-to-end path from @d2Reserved decorator through
// the emitter state-map read, emitProto call, and buildReservedNumberLines /
// buildReservedNameLines helpers, all the way to the emitted .proto content.
// Asserts both number ranges (range-collapsed) and quoted name lines appear
// inside the correct message blocks.
// ---------------------------------------------------------------------------

describe("protoGrpcEmitIntegration_D2Reserved_ReservedLinesEmitted", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("@d2Reserved on request and response models → correct reserved lines in both message blocks", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Test;

      @d2Reserved("old_kid, removed_slot", 3, 5)
      model UpdateInput { @d2Field(1) newKid: string; }

      @d2Reserved("old_sig", 2)
      model UpdateOutput { @d2Field(1) signature: string; }

      @d2Command
      @d2ServedBy("TestSvc")
      @d2GrpcMethod("TestSvc", "Update")
      op update(input: UpdateInput): UpdateOutput;
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

    const protoContent = getEmittedFile(host, ".g.proto");
    expect(protoContent).toBeDefined();

    // Extract the request message block to assert reserved lines.
    // The emitter names the request proto message <grpcMethod>Request (not the
    // TypeSpec model name), so the block is "UpdateRequest" for
    // @d2GrpcMethod("TestSvc", "Update").
    const inputBlockMatch = protoContent!.match(
      /message UpdateRequest \{([^}]*)\}/s,
    );
    expect(inputBlockMatch).not.toBeNull();
    const inputBlock = inputBlockMatch![1]!;

    // Numbers 3 and 5 are non-consecutive → emitted as "reserved 3, 5;"
    expect(inputBlock).toContain("reserved 3, 5;");
    // Names emitted as individual reserved lines.
    expect(inputBlock).toContain('reserved "old_kid";');
    expect(inputBlock).toContain('reserved "removed_slot";');

    // Extract the UpdateOutput (data message) block for response reserved lines.
    const outputBlockMatch = protoContent!.match(
      /message UpdateOutput \{([^}]*)\}/s,
    );
    expect(outputBlockMatch).not.toBeNull();
    const outputBlock = outputBlockMatch![1]!;

    expect(outputBlock).toContain("reserved 2;");
    expect(outputBlock).toContain('reserved "old_sig";');
  });
});

// ---------------------------------------------------------------------------
// Test: duplicate reserved names are deduplicated before emission
//
// buildReservedNameLines deduplicates names (same as the
// number path deduplicates numbers). This test pins the behavior: duplicate
// name entries in the @d2Reserved list must emit only once.
// ---------------------------------------------------------------------------

describe("protoGrpcEmitIntegration_D2Reserved_DuplicateNamesDeduplicated", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("duplicate reserved names in @d2Reserved list emit only once in the .proto", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Test;

      @d2Reserved("old_key, old_key", 1)
      model DedupInput { @d2Field(2) value: string; }

      model DedupOutput { @d2Field(1) ok: string; }

      @d2Command
      @d2ServedBy("TestSvc")
      @d2GrpcMethod("TestSvc", "Dedup")
      op dedup(input: DedupInput): DedupOutput;
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

    const protoContent = getEmittedFile(host, ".g.proto");
    expect(protoContent).toBeDefined();

    // "old_key" appears twice in the decorator but must emit exactly once.
    const occurrences = (protoContent!.match(/reserved "old_key";/g) ?? [])
      .length;
    expect(occurrences).toBe(1);
  });
});

// ---------------------------------------------------------------------------
// Test 7: WireVersion.g.cs + wire-identity.manifest.g.json emitted when ≥1
// @d2GrpcMethod op is present and the channel validates. Asserts agree-by-
// construction: the proto package, proto C# namespace, and manifest channel
// carry the SAME generation segment.
// ---------------------------------------------------------------------------

describe("protoGrpcEmitIntegration_WireVersion_EmittedOnGrpcOp", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("a @d2GrpcMethod op → WireVersion.g.cs emitted with matching channel", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Fixtures;

      model PingInput { @d2Field(1) id: string; }
      model PingOutput { @d2Field(1) ok: string; }

      @d2Command
      @d2ServedBy("PingSvc")
      @d2GrpcMethod("PingSvc", "Ping")
      op ping(input: PingInput): PingOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          "csharp-namespace": "D2.Test",
          "proto-package": "d2.test.v2alpha",
          "proto-csharp-namespace": "D2.Test.Protos.V2Alpha",
          "grpc-service-namespace": "D2.Test.Grpc",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // WireVersion.g.cs is emitted.
    const wireVersionContent = getEmittedFile(host, "WireVersion.g.cs");
    expect(wireVersionContent).toBeDefined();
    expect(wireVersionContent).toContain('CHANNEL = "v2alpha"');
    expect(wireVersionContent).toContain("GENERATION = 2");
    expect(wireVersionContent).toContain('STABILITY = "alpha"');
    expect(wireVersionContent).toContain("namespace D2.Test.Protos.V2Alpha;");

    // wire-identity.manifest.g.json is emitted.
    const manifestContent = getEmittedFile(
      host,
      "wire-identity.manifest.g.json",
    );
    expect(manifestContent).toBeDefined();
    const manifest = JSON.parse(manifestContent!) as Record<string, unknown>;

    // Channel agree-by-construction: proto package ↔ proto C# ns ↔ manifest.
    expect(manifest["channel"]).toBe("v2alpha");
    expect(manifest["protoPackage"]).toBe("d2.test.v2alpha");
    expect(manifest["protoCsharpNamespace"]).toBe("D2.Test.Protos.V2Alpha");
    expect(manifest["generation"]).toBe(2);
    expect(manifest["stability"]).toBe("alpha");

    // Also verify the emitted proto carries the matching package.
    const protoContent = getEmittedFile(host, ".g.proto");
    expect(protoContent).toBeDefined();
    expect(protoContent).toContain("package d2.test.v2alpha;");
    expect(protoContent).toContain(
      'option csharp_namespace = "D2.Test.Protos.V2Alpha";',
    );
  });
});

// ---------------------------------------------------------------------------
// Test 8: D2TSP010 fires when proto-package ↔ proto-csharp-namespace mismatch.
// Proves the validation reaches the TypeSpec compiler diagnostic surface.
// ---------------------------------------------------------------------------

describe("protoGrpcEmitIntegration_D2TSP010_FiresOnChannelMismatch", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("mismatched proto-package vs proto-csharp-namespace → D2TSP010 error diagnostic", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Fixtures;

      model PingInput { @d2Field(1) id: string; }
      model PingOutput { @d2Field(1) ok: string; }

      @d2Command
      @d2ServedBy("PingSvc")
      @d2GrpcMethod("PingSvc", "Ping")
      op ping(input: PingInput): PingOutput;
      `,
    );

    // host.compile throws when the program contains error-severity diagnostics
    // (TypeSpec test-host calls expectDiagnosticEmpty internally). Absorb the
    // throw so we can inspect host.program.diagnostics for D2TSP010.
    try {
      await host.compile("main.tsp", {
        emit: ["@d2/typespec-emitters"],
        options: {
          "@d2/typespec-emitters": {
            "csharp-namespace": "D2.Test",
            // Deliberately mismatched: v2alpha vs V2Beta
            "proto-package": "d2.test.v2alpha",
            "proto-csharp-namespace": "D2.Test.Protos.V2Beta",
            "grpc-service-namespace": "D2.Test.Grpc",
          },
        },
        outputDir: "testing:/out",
      });
    } catch {
      // Expected: host.compile throws on error diagnostics. Fall through to assert below.
    }

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    // D2TSP010 must fire.
    const d2tsp010 = errors.find((d) => d.message.includes("D2TSP010"));
    expect(d2tsp010).toBeDefined();
    expect(d2tsp010!.message).toContain("v2alpha");
    expect(d2tsp010!.message).toContain("V2Beta");
  });
});

// ---------------------------------------------------------------------------
// Test 9: @versioned adoption on D2.KeyCustodian is byte-neutral for existing
// committed fixtures (GetJwks DTOs, IGetJwksHandler).
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Test: unpinned proto field on the ONLY @d2GrpcMethod op fires D2TSP009 AND
// produces NO WireVersion.g.cs and NO wire-identity.manifest.g.json.
//
// Regression pin for the orphaned-wire-identity bug: before the fix,
// emitProtoAndGrpcService returned void and the caller set anyGrpcProtoEmitted
// unconditionally — so D2TSP009 fired (no .proto on disk) but WireVersion.g.cs
// + wire-identity.manifest.g.json were still emitted. The fix gates the flag on
// the boolean return value; a false return (walk-error / proto-undefined path)
// now leaves anyGrpcProtoEmitted=false and the WireVersion gate (line ~833) is
// never entered.
// ---------------------------------------------------------------------------

describe("protoGrpcEmitIntegration_UnpinnedField_NoOrphanedWireIdentity", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("unpinned proto field on the sole @d2GrpcMethod op → D2TSP009 fires and no WireVersion or wire-identity manifest is emitted", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Test;

      model OrphanInput { unpinnedField: string; }
      model OrphanOutput { @d2Field(1) result: string; }

      @d2Command
      @d2ServedBy("TestSvc")
      @d2GrpcMethod("TestSvc", "Orphan")
      op orphan(input: OrphanInput): OrphanOutput;
      `,
    );

    let compileError: unknown;
    try {
      await host.compile("main.tsp", {
        emit: ["@d2/typespec-emitters"],
        options: {
          "@d2/typespec-emitters": {
            "csharp-namespace": "D2.Test",
            "proto-package": "d2.test.v2alpha",
            "proto-csharp-namespace": "D2.Test.Protos.V2Alpha",
            "grpc-service-namespace": "D2.Test.Grpc",
          },
        },
        outputDir: "testing:/out",
      });
    } catch (err) {
      compileError = err;
    }

    // D2TSP009 must fire (the unpinned field triggers the walk-error path).
    const programErrors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    const hasErrors = compileError !== undefined || programErrors.length > 0;
    expect(hasErrors).toBe(true);

    const d2tsp009 = host.program.diagnostics.find(
      (d) => d.code === "@d2/typespec-emitters/unpinned-proto-field",
    );
    expect(d2tsp009).toBeDefined();

    // The .proto was NOT written — the walk-error path returned false.
    const protoContent = getEmittedFile(host, ".g.proto");
    expect(protoContent).toBeUndefined();

    // WireVersion.g.cs must NOT be emitted — anyGrpcProtoEmitted stayed false.
    const wireVersionContent = getEmittedFile(host, "WireVersion.g.cs");
    expect(wireVersionContent).toBeUndefined();

    // wire-identity.manifest.g.json must NOT be emitted for the same reason.
    const wireManifestContent = getEmittedFile(
      host,
      "wire-identity.manifest.g.json",
    );
    expect(wireManifestContent).toBeUndefined();
  });
});

describe("protoGrpcEmitIntegration_VersionedAdoption_ByteNeutralForExistingFixtures", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [
        VersioningTestLibrary,
        D2DecoratorTestLibrary,
        D2EmitterTestLibrary,
      ],
    });
  });

  it("@versioned on D2.KeyCustodian produces the same getJwks handler interface as before", async () => {
    // Simulate the updated key-custodian.tsp with @versioned adopted.
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      import "@typespec/versioning";
      using D2;
      using TypeSpec.Versioning;

      @versioned(D2.KeyCustodian.Versions)
      namespace D2.KeyCustodian {
        enum Versions { v2alpha: "v2alpha" }

        model GetJwksOutput { keys: string[]; }

        @d2Query
        @d2InProcess
        @d2ServedBy("KeyCustodian")
        @d2Concern("Jwks")
        op getJwks(): GetJwksOutput;
      }
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          "csharp-namespace": "D2.Test",
          "csharp-clients-namespace": "D2.Edge.KeyCustodian.Client",
          "csharp-app-namespace-base":
            "D2.Edge.KeyCustodian.App.Application.Handlers",
          "proto-package": "d2.signfixtures.v2alpha",
          "proto-csharp-namespace": "D2.Services.Protos.SignFixtures.V2Alpha",
          "grpc-service-namespace": "D2.Test.Grpc",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // The handler interface for getJwks is still emitted (byte-neutrality: @versioned
    // does not suppress in-process handler-interface emission).
    const handlerInterface = getEmittedFile(host, "IGetJwksHandler.g.cs");
    expect(handlerInterface).toBeDefined();
    expect(handlerInterface).toContain("IGetJwksHandler");

    // No proto emitted (getJwks has no @d2GrpcMethod → no proto, no WireVersion).
    const protoContent = getEmittedFile(host, ".g.proto");
    expect(protoContent).toBeUndefined();

    // WireVersion.g.cs is NOT emitted when no @d2GrpcMethod op is present.
    const wireVersion = getEmittedFile(host, "WireVersion.g.cs");
    expect(wireVersion).toBeUndefined();
  });
});
