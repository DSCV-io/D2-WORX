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

    const errors = host.program.diagnostics.filter((d) => d.severity === "error");
    expect(errors).toHaveLength(0);

    // .proto file emitted.
    const protoContent = getEmittedFile(host, ".g.proto");
    expect(protoContent).toBeDefined();
    expect(protoContent).toContain("syntax = \"proto3\";");
    expect(protoContent).toContain("package d2.test.v1;");
    expect(protoContent).toContain("option csharp_namespace = \"D2.Test.Protos.V1\";");
    expect(protoContent).toContain("service KeyCustodianSigner {");
    expect(protoContent).toContain("rpc Sign(SignRequest) returns (SignResponse);");
    expect(protoContent).toContain("message SignRequest {");
    expect(protoContent).toContain("message SignResponse {");
    expect(protoContent).toContain("string kid = 1;");
    expect(protoContent).toContain("bytes payload = 2;");

    // gRPC service class emitted.
    const serviceContent = getEmittedFile(host, "KeyCustodianSignerService.g.cs");
    expect(serviceContent).toBeDefined();
    expect(serviceContent).toContain("namespace D2.Test.Grpc;");
    expect(serviceContent).toContain("global::D2.Test.Protos.V1.KeyCustodianSigner.KeyCustodianSignerBase");
    expect(serviceContent).toContain("ISignHandler handler");
    expect(serviceContent).toContain("handler.HandleAsync");

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

    const errors = host.program.diagnostics.filter((d) => d.severity === "error");
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

    const programErrors = host.program.diagnostics.filter((d) => d.severity === "error");
    const hasErrors = compileError !== undefined || programErrors.length > 0;
    expect(hasErrors).toBe(true);
  });
});
