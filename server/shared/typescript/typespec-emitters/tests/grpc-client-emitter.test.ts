// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Structural coverage for grpc-client-emitter.ts.
//
// Tests are organised by function:
//   - emitGrpcClient (public) — 4-file tuple, guard cases, multi-op
//   - emitClientKeys (public) — keys class, empty / non-empty op names
//   - Mapper body correctness — byte[] CopyFrom / ToByteArray / plain string
//   - DI extension body correctness — AddGrpcClient, AddResilientPipeline, AddTransient
//   - Using / namespace hygiene — no self-using, sorted order, global::
//
// These are unit tests that exercise the pure string-template functions directly.
// Byte-exact parity against committed fixtures is covered in proto-grpc-byte-parity.test.ts.

import { describe, it, expect } from "vitest";
import {
  emitGrpcClient,
  emitClientKeys,
  type GrpcClientOp,
} from "../src/lib/grpc-client-emitter.js";

// ---------------------------------------------------------------------------
// Fixture builders
// ---------------------------------------------------------------------------

const SOURCE = "contracts/typespec/fixtures/sign-shaped.tsp";
const CLIENTS_NS = "D2.Edge.KeyCustodian.Clients";
const PROTO_NS = "D2.Services.Protos.KeyCustodian.V1";

function makeSignOp(overrides: Partial<GrpcClientOp> = {}): GrpcClientOp {
  return {
    opName: "sign",
    grpcService: "KeyCustodianSigner",
    grpcMethod: "Sign",
    protoCsharpNs: PROTO_NS,
    dtoCsharpNs: CLIENTS_NS,
    sourceSpec: SOURCE,
    requestModelName: "SignInput",
    requestFields: [
      {
        name: "kid",
        csName: "Kid",
        csType: "string",
        tsName: "kid",
        tsType: "string",
        protoType: "string",
        repeated: false,
        optional: false,
        redact: false,
      },
      {
        name: "payload",
        csName: "Payload",
        csType: "byte[]",
        tsName: "payload",
        tsType: "Uint8Array",
        protoType: "bytes",
        repeated: false,
        optional: false,
        redact: true,
      },
    ],
    responseModelName: "SignOutput",
    responseFields: [
      {
        name: "signature",
        csName: "Signature",
        csType: "string",
        tsName: "signature",
        tsType: "string",
        protoType: "string",
        repeated: false,
        optional: false,
        redact: false,
      },
    ],
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// emitGrpcClient — guard cases
// ---------------------------------------------------------------------------

describe("emitGrpcClient_Guards", () => {
  it("throws on empty moduleName", () => {
    expect(() => emitGrpcClient("", [makeSignOp()], CLIENTS_NS)).toThrow(
      "moduleName must not be empty",
    );
  });

  it("throws on empty clientsNs", () => {
    expect(() => emitGrpcClient("KeyCustodian", [makeSignOp()], "")).toThrow(
      "clientsNs must not be empty",
    );
  });

  it("returns empty array for empty ops list", () => {
    const files = emitGrpcClient("KeyCustodian", [], CLIENTS_NS);
    expect(files).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// emitGrpcClient — file count and names
// ---------------------------------------------------------------------------

describe("emitGrpcClient_FileNamesAndCount", () => {
  it("returns exactly 4 files for a single-op module", () => {
    const files = emitGrpcClient("KeyCustodian", [makeSignOp()], CLIENTS_NS);
    expect(files).toHaveLength(4);
  });

  it("file[0] is the interface file", () => {
    const [iface] = emitGrpcClient("KeyCustodian", [makeSignOp()], CLIENTS_NS);
    expect(iface!.fileName).toBe("IKeyCustodianGrpcClient.g.cs");
  });

  it("file[1] is the impl file", () => {
    const [, impl] = emitGrpcClient("KeyCustodian", [makeSignOp()], CLIENTS_NS);
    expect(impl!.fileName).toBe("KeyCustodianGrpcClient.g.cs");
  });

  it("file[2] is the mapper file (single-op: <PascalOp>ClientMappers.g.cs)", () => {
    const [, , mapper] = emitGrpcClient(
      "KeyCustodian",
      [makeSignOp()],
      CLIENTS_NS,
    );
    expect(mapper!.fileName).toBe("SignClientMappers.g.cs");
  });

  it("file[3] is the DI-extension file", () => {
    const [, , , di] = emitGrpcClient(
      "KeyCustodian",
      [makeSignOp()],
      CLIENTS_NS,
    );
    expect(di!.fileName).toBe("KeyCustodianGrpcClientsGenerated.g.cs");
  });
});

// ---------------------------------------------------------------------------
// Interface file content
// ---------------------------------------------------------------------------

describe("emitGrpcClient_InterfaceFile", () => {
  function getInterface() {
    const [iface] = emitGrpcClient("KeyCustodian", [makeSignOp()], CLIENTS_NS);
    return iface!.content;
  }

  it("contains namespace before using directives", () => {
    const content = getInterface();
    const nsIdx = content.indexOf(`namespace ${CLIENTS_NS};`);
    const usingIdx = content.indexOf("using D2.Shared.Resilience.Pipeline;");
    expect(nsIdx).toBeGreaterThan(-1);
    expect(usingIdx).toBeGreaterThan(-1);
    expect(nsIdx).toBeLessThan(usingIdx);
  });

  it("contains only the Resilience.Pipeline using (no extras)", () => {
    const content = getInterface();
    const usingLines = content
      .split("\n")
      .filter((l) => l.startsWith("using "));
    expect(usingLines).toHaveLength(1);
    expect(usingLines[0]).toBe("using D2.Shared.Resilience.Pipeline;");
  });

  it("does not use its own namespace", () => {
    const content = getInterface();
    expect(content).not.toContain(`using ${CLIENTS_NS};`);
  });

  it("declares SignAsync with pipelineOverride and CancellationToken", () => {
    const content = getInterface();
    expect(content).toContain("ValueTask<D2Result<SignOutput?>> SignAsync(");
    expect(content).toContain(
      "ResilientPipeline<string, SignOutput?>? pipelineOverride = null,",
    );
    expect(content).toContain("CancellationToken ct = default);");
  });

  it("has auto-generated banner", () => {
    const content = getInterface();
    expect(content).toContain("<auto-generated>");
    expect(content).toContain(SOURCE);
  });

  it("has #nullable enable", () => {
    const content = getInterface();
    expect(content).toContain("#nullable enable");
  });

  it("interface is public (not internal)", () => {
    const content = getInterface();
    expect(content).toMatch(/public interface IKeyCustodianGrpcClient/);
  });
});

// ---------------------------------------------------------------------------
// Impl file content
// ---------------------------------------------------------------------------

describe("emitGrpcClient_ImplFile", () => {
  function getImpl() {
    const [, impl] = emitGrpcClient("KeyCustodian", [makeSignOp()], CLIENTS_NS);
    return impl!.content;
  }

  it("contains namespace before using directives", () => {
    const content = getImpl();
    const nsIdx = content.indexOf(`namespace ${CLIENTS_NS};`);
    const firstUsing = content.indexOf("using D2");
    expect(nsIdx).toBeGreaterThan(-1);
    expect(firstUsing).toBeGreaterThan(-1);
    expect(nsIdx).toBeLessThan(firstUsing);
  });

  it("includes D2.Services.Protos.Common.V1 for D2ResultProto", () => {
    const content = getImpl();
    expect(content).toContain("using D2.Services.Protos.Common.V1;");
  });

  it("includes D2.Shared.Resilience.Pipeline (not just D2.Shared.Resilience)", () => {
    const content = getImpl();
    expect(content).toContain("using D2.Shared.Resilience.Pipeline;");
    expect(content).not.toContain("using D2.Shared.Resilience;");
  });

  it("does not use its own namespace", () => {
    const content = getImpl();
    expect(content).not.toContain(`using ${CLIENTS_NS};`);
  });

  it("usings are in sorted order (SA1210)", () => {
    const content = getImpl();
    const usingLines = content
      .split("\n")
      .filter((l) => l.startsWith("using "))
      .map((l) => l.slice("using ".length).replace(";", ""));
    const sorted = [...usingLines].sort();
    expect(usingLines).toEqual(sorted);
  });

  it("impl is sealed public class", () => {
    const content = getImpl();
    expect(content).toMatch(/public sealed class KeyCustodianGrpcClient\(/);
  });

  it("primary ctor has keyCustodianSignerStub (no r_ prefix)", () => {
    const content = getImpl();
    expect(content).toContain(
      "global::D2.Services.Protos.KeyCustodian.V1.KeyCustodianSigner.KeyCustodianSignerClient keyCustodianSignerStub",
    );
  });

  it("primary ctor has [FromKeyedServices] pipeline (no r_ prefix)", () => {
    const content = getImpl();
    expect(content).toContain(
      "[FromKeyedServices(SignClientKeys.PIPELINE)] ResilientPipeline<string, SignOutput?> signPipeline",
    );
  });

  it("private field r_signPipeline assigned from primary ctor param", () => {
    const content = getImpl();
    expect(content).toContain(
      "private readonly ResilientPipeline<string, SignOutput?> r_signPipeline = signPipeline;",
    );
  });

  it("SignAsync public method dispatches to SignCoreAsync", () => {
    const content = getImpl();
    expect(content).toContain(
      "=> SignCoreAsync(input, pipelineOverride ?? r_signPipeline, ct);",
    );
  });

  it("captures envelope outside the closure (captured-envelope pattern)", () => {
    const content = getImpl();
    expect(content).toContain("D2ResultProto? envelope = null;");
    expect(content).toContain("envelope = response.Result;");
  });

  it("captures the RpcException out of the closure and rethrows it (retry-preserving)", () => {
    const content = getImpl();
    // The transport fault is captured for gRPC-aware remapping AND rethrown so the
    // resilience retry layer still observes the throw (custom IsTransient).
    expect(content).toContain("RpcException? transportFault = null;");
    expect(content).toContain("catch (RpcException ex)");
    expect(content).toContain("transportFault = ex;");
    expect(content).toContain("throw;");
  });

  it("remaps the captured transport fault via ToTransportFaultResult (gRPC-aware 503, not 500)", () => {
    const content = getImpl();
    // After ExecuteAsync, a failure caused by the captured RpcException is remapped via the
    // SHARED D2.Shared.Result.Grpc mapping (HandleAsync's RpcException arm) — Cancelled→Canceled,
    // else→ServiceUnavailable — instead of the pipeline's generic UnhandledException (500).
    expect(content).toContain(
      "if (!pipelineResult.Success && transportFault is not null)",
    );
    expect(content).toContain(
      "return transportFault.ToTransportFaultResult<SignOutput?>();",
    );
  });

  it("reconstructs business D2Result with explicit type arg <SignOutput?>", () => {
    const content = getImpl();
    expect(content).toContain(
      "envelope.ToD2Result<SignOutput?>(pipelineResult.Data)",
    );
  });

  it("falls back to pipelineResult verbatim for non-transport pipeline failures", () => {
    const content = getImpl();
    expect(content).toContain(": pipelineResult;");
  });

  it("does NOT wrap HandleAsync in the pipeline (the no-op retry trap)", () => {
    const content = getImpl();
    // Wrapping HandleAsync inside ExecuteAsync would make retry a silent no-op (it swallows
    // the throw). The op must run the bare throwing stub call instead.
    expect(content).not.toContain(".HandleAsync(");
    expect(content).toContain(
      "await keyCustodianSignerStub.SignAsync(request, cancellationToken: innerCt);",
    );
  });

  it("uses global:: prefix on the stub type", () => {
    const content = getImpl();
    expect(content).toContain(
      "global::D2.Services.Protos.KeyCustodian.V1.KeyCustodianSigner.KeyCustodianSignerClient",
    );
  });
});

// ---------------------------------------------------------------------------
// Mapper file content
// ---------------------------------------------------------------------------

describe("emitGrpcClient_MapperFile", () => {
  function getMapper() {
    const [, , mapper] = emitGrpcClient(
      "KeyCustodian",
      [makeSignOp()],
      CLIENTS_NS,
    );
    return mapper!.content;
  }

  it("contains namespace before using directives", () => {
    const content = getMapper();
    const nsIdx = content.indexOf(`namespace ${CLIENTS_NS};`);
    const usingIdx = content.indexOf("using D2");
    expect(nsIdx).toBeGreaterThan(-1);
    expect(usingIdx).toBeGreaterThan(-1);
    expect(nsIdx).toBeLessThan(usingIdx);
  });

  it("does not use its own namespace", () => {
    const content = getMapper();
    expect(content).not.toContain(`using ${CLIENTS_NS};`);
  });

  it("uses Google.Protobuf for ByteString", () => {
    const content = getMapper();
    expect(content).toContain("using Google.Protobuf;");
  });

  it("maps byte[] → ByteString.CopyFrom (DTO→proto direction)", () => {
    const content = getMapper();
    expect(content).toContain(
      "global::Google.Protobuf.ByteString.CopyFrom(input.Payload)",
    );
  });

  it("maps SignOutput proto → DTO with string field direct copy (no ToByteArray)", () => {
    const content = getMapper();
    // The SignOutput->DTO mapper: signature field is a string, direct copy
    expect(content).toContain(
      "return new global::D2.Edge.KeyCustodian.Clients.SignOutput(data.Signature);",
    );
  });

  it("uses global:: aliases throughout mapper bodies", () => {
    const content = getMapper();
    expect(content).toContain(
      "global::D2.Services.Protos.KeyCustodian.V1.SignRequest",
    );
    expect(content).toContain("global::D2.Edge.KeyCustodian.Clients.SignInput");
    expect(content).toContain(
      "global::D2.Services.Protos.KeyCustodian.V1.SignOutput",
    );
    expect(content).toContain(
      "global::D2.Edge.KeyCustodian.Clients.SignOutput",
    );
  });

  it("mapper class is internal static", () => {
    const content = getMapper();
    expect(content).toMatch(/internal static class SignClientMappers/);
  });

  it("extension blocks use C#14 extension(T x) form", () => {
    const content = getMapper();
    expect(content).toContain(
      "extension(global::D2.Edge.KeyCustodian.Clients.SignInput input)",
    );
    expect(content).toContain(
      "extension(global::D2.Services.Protos.KeyCustodian.V1.SignOutput data)",
    );
  });
});

// ---------------------------------------------------------------------------
// Mapper — byte[] round-trip (request fields: bytes → ByteString; response: ToByteArray)
// ---------------------------------------------------------------------------

describe("emitGrpcClient_MapperBytesField", () => {
  it("response byte[] field maps ToByteArray() in proto→DTO direction", () => {
    // Make an op where the OUTPUT has a bytes field (unusual but valid)
    const op = makeSignOp({
      responseFields: [
        {
          name: "data",
          csName: "Data",
          csType: "byte[]",
          tsName: "data",
          tsType: "Uint8Array",
          protoType: "bytes",
          repeated: false,
          optional: false,
          redact: false,
        },
      ],
      responseModelName: "BlobOutput",
    });
    const [, , mapper] = emitGrpcClient("KeyCustodian", [op], CLIENTS_NS);
    expect(mapper!.content).toContain("data.Data.ToByteArray()");
  });

  it("request non-bytes field passes through directly (no CopyFrom)", () => {
    const [, , mapper] = emitGrpcClient(
      "KeyCustodian",
      [makeSignOp()],
      CLIENTS_NS,
    );
    // Kid is a string — should be a direct assignment, not CopyFrom
    expect(mapper!.content).toContain("Kid = input.Kid,");
  });

  it("empty-response model emits a parameterless DTO ctor in the proto→DTO mapper", () => {
    // A gRPC op whose output model has no fields → the proto data message → DTO mapper
    // returns `new <Dto>()` (no ctor args).
    const op = makeSignOp({
      responseModelName: "EmptyOutput",
      responseFields: [],
    });
    const [, , mapper] = emitGrpcClient("KeyCustodian", [op], CLIENTS_NS);
    expect(mapper!.content).toContain(
      "return new global::D2.Edge.KeyCustodian.Clients.EmptyOutput();",
    );
  });

  it("empty-request model emits a parameterless proto-request ctor in the DTO→proto mapper", () => {
    // A gRPC op whose input model has no fields → the DTO → proto request mapper
    // returns `new <ProtoRequest>()` (no initializer).
    const op = makeSignOp({
      requestModelName: "EmptyInput",
      requestFields: [],
    });
    const [, , mapper] = emitGrpcClient("KeyCustodian", [op], CLIENTS_NS);
    expect(mapper!.content).toContain(
      "return new global::D2.Services.Protos.KeyCustodian.V1.SignRequest();",
    );
  });
});

// ---------------------------------------------------------------------------
// DI extension file content
// ---------------------------------------------------------------------------

describe("emitGrpcClient_DiExtensionFile", () => {
  function getDi() {
    const [, , , di] = emitGrpcClient(
      "KeyCustodian",
      [makeSignOp()],
      CLIENTS_NS,
    );
    return di!.content;
  }

  it("contains namespace before using directives", () => {
    const content = getDi();
    const nsIdx = content.indexOf(`namespace ${CLIENTS_NS};`);
    const usingIdx = content.indexOf("using D2");
    expect(nsIdx).toBeGreaterThan(-1);
    expect(usingIdx).toBeGreaterThan(-1);
    expect(nsIdx).toBeLessThan(usingIdx);
  });

  it("includes Resilience.Pipeline using (not bare Resilience)", () => {
    const content = getDi();
    expect(content).toContain("using D2.Shared.Resilience.Pipeline;");
    expect(content).not.toContain("using D2.Shared.Resilience;");
  });

  it("includes Resilience.Retry for UseRetries", () => {
    const content = getDi();
    expect(content).toContain("using D2.Shared.Resilience.Retry;");
  });

  it("includes Result.Grpc for IsTransientGrpcException", () => {
    const content = getDi();
    expect(content).toContain("using D2.Shared.Result.Grpc;");
  });

  it("includes Grpc.Core for RpcException", () => {
    const content = getDi();
    expect(content).toContain("using Grpc.Core;");
  });

  it("options record has required Uri Address", () => {
    const content = getDi();
    expect(content).toContain(
      "public sealed record KeyCustodianGrpcClientOptions",
    );
    expect(content).toContain("public required Uri Address { get; init; }");
  });

  it("AddGrpcClient uses global:: alias to avoid namespace shadowing", () => {
    const content = getDi();
    expect(content).toContain(
      "services.AddGrpcClient<global::D2.Services.Protos.KeyCustodian.V1.KeyCustodianSigner.KeyCustodianSignerClient>",
    );
  });

  it("AddResilientPipeline uses IsTransientGrpcException", () => {
    const content = getDi();
    expect(content).toContain(
      "IsTransient = ex => ex is RpcException r && ProtoExtensions.IsTransientGrpcException(r),",
    );
  });

  it("AddTransient binds interface to impl", () => {
    const content = getDi();
    expect(content).toContain(
      "services.AddTransient<IKeyCustodianGrpcClient, KeyCustodianGrpcClient>();",
    );
  });

  it("uses C#14 extension(IServiceCollection services) form", () => {
    const content = getDi();
    expect(content).toContain("extension(IServiceCollection services)");
  });

  it("generated DI extension carries no AddD2ServiceIdentity residue (call or guidance)", () => {
    const content = getDi();
    // AddD2ServiceIdentity is the retired client_credentials surface — the auto-wire model
    // replaced it. Neither the body nor the doc-comment may mention it, and the old
    // "host MUST chain" guidance is gone too (the auto-wire IS the chain now).
    expect(content).not.toContain("AddD2ServiceIdentity");
    expect(content).not.toContain("host MUST chain");
  });

  it("auto-wires .AddD2ForwardedJwt().AddD2WorkloadCertificate() onto the gRPC channel", () => {
    const content = getDi();
    // The generated DI registration AUTO-CHAINS the per-channel outbound auth so the host
    // can never forget it (fail-safe). Both extensions must chain off the AddGrpcClient<…>
    // registration, in that order. (The chained CALL — leading-`.` line — is distinct from
    // the docstring's <c>.AddD2ForwardedJwt()</c> mention; assert on the chained statement.)
    const chainedJwt = "\n                .AddD2ForwardedJwt()";
    const chainedCert = "\n                .AddD2WorkloadCertificate();";
    expect(content).toContain(chainedJwt);
    expect(content).toContain(chainedCert);

    const addGrpcIdx = content.indexOf("services.AddGrpcClient<");
    const forwardedJwtIdx = content.indexOf(chainedJwt);
    const workloadCertIdx = content.indexOf(chainedCert);
    expect(addGrpcIdx).toBeGreaterThan(-1);
    expect(forwardedJwtIdx).toBeGreaterThan(addGrpcIdx);
    expect(workloadCertIdx).toBeGreaterThan(forwardedJwtIdx);

    // The AddGrpcClient(...) statement is no longer self-terminated — the chain
    // continues onto the next line (no ";" closing the AddGrpcClient call directly).
    expect(content).toContain("o.Address = options.Address)\n");
    expect(content).not.toContain("o.Address = options.Address);");
  });

  it("includes the D2.Shared.Auth.Outbound.Grpc using for the auto-wired extensions", () => {
    const content = getDi();
    expect(content).toContain("using D2.Shared.Auth.Outbound.Grpc;");
  });
});

// ---------------------------------------------------------------------------
// emitClientKeys — basic output
// ---------------------------------------------------------------------------

describe("emitClientKeys_Structure", () => {
  it("generates SignClientKeys.g.cs", () => {
    const file = emitClientKeys("sign", CLIENTS_NS, SOURCE);
    expect(file.fileName).toBe("SignClientKeys.g.cs");
  });

  it("PIPELINE constant value is <PascalOp>GrpcClientPipeline", () => {
    const file = emitClientKeys("sign", CLIENTS_NS, SOURCE);
    expect(file.content).toContain(
      'public const string PIPELINE = "SignGrpcClientPipeline";',
    );
  });

  it("PIPELINE_KEY constant value is <PascalOp>GrpcClientCall", () => {
    const file = emitClientKeys("sign", CLIENTS_NS, SOURCE);
    expect(file.content).toContain(
      'internal const string PIPELINE_KEY = "SignGrpcClientCall";',
    );
  });

  it("class is public static (accessible for [FromKeyedServices])", () => {
    const file = emitClientKeys("sign", CLIENTS_NS, SOURCE);
    expect(file.content).toMatch(/public static class SignClientKeys/);
  });

  it("namespace is before any usings (no usings in keys file)", () => {
    const file = emitClientKeys("sign", CLIENTS_NS, SOURCE);
    expect(file.content).toContain(`namespace ${CLIENTS_NS};`);
    // Keys file has no usings at all — no ProjectReference needed
    expect(file.content).not.toContain("using ");
  });

  it("has auto-generated banner", () => {
    const file = emitClientKeys("sign", CLIENTS_NS, SOURCE);
    expect(file.content).toContain("<auto-generated>");
    expect(file.content).toContain(SOURCE);
  });

  it("cref uses <c> markup, not <see cref=...> (avoids cref resolution failure)", () => {
    const file = emitClientKeys("sign", CLIENTS_NS, SOURCE);
    expect(file.content).toContain(
      "<c>ResilientPipeline&lt;TKey, TValue&gt;</c>",
    );
    expect(file.content).not.toContain('cref="');
  });
});

describe("emitClientKeys_MultiWordOpName", () => {
  it("camelCase opName is PascalCased in class name and constants", () => {
    const file = emitClientKeys("rotateKey", CLIENTS_NS, SOURCE);
    expect(file.fileName).toBe("RotateKeyClientKeys.g.cs");
    expect(file.content).toContain(
      'public const string PIPELINE = "RotateKeyGrpcClientPipeline";',
    );
    expect(file.content).toContain(
      'internal const string PIPELINE_KEY = "RotateKeyGrpcClientCall";',
    );
  });
});

// ---------------------------------------------------------------------------
// emitGrpcClient — multi-op module
// ---------------------------------------------------------------------------

describe("emitGrpcClient_MultiOp", () => {
  function makeFetchOp(): GrpcClientOp {
    return {
      opName: "fetch",
      grpcService: "KeyCustodianFetcher",
      grpcMethod: "Fetch",
      protoCsharpNs: PROTO_NS,
      dtoCsharpNs: CLIENTS_NS,
      sourceSpec: SOURCE,
      requestModelName: "FetchInput",
      requestFields: [],
      responseModelName: "FetchOutput",
      responseFields: [
        {
          name: "value",
          csName: "Value",
          csType: "string",
          tsName: "value",
          tsType: "string",
          protoType: "string",
          repeated: false,
          optional: false,
          redact: false,
        },
      ],
    };
  }

  it("returns exactly 4 files even for 2 ops", () => {
    const files = emitGrpcClient(
      "KeyCustodian",
      [makeSignOp(), makeFetchOp()],
      CLIENTS_NS,
    );
    expect(files).toHaveLength(4);
  });

  it("interface declares both ops", () => {
    const [iface] = emitGrpcClient(
      "KeyCustodian",
      [makeSignOp(), makeFetchOp()],
      CLIENTS_NS,
    );
    expect(iface!.content).toContain("SignAsync(");
    expect(iface!.content).toContain("FetchAsync(");
  });

  it("mapper file name for multi-op is concatenated ops", () => {
    const [, , mapper] = emitGrpcClient(
      "KeyCustodian",
      [makeSignOp(), makeFetchOp()],
      CLIENTS_NS,
    );
    expect(mapper!.fileName).toBe("SignFetchClientMappers.g.cs");
  });

  it("impl injects two separate pipeline fields for two ops", () => {
    const [, impl] = emitGrpcClient(
      "KeyCustodian",
      [makeSignOp(), makeFetchOp()],
      CLIENTS_NS,
    );
    expect(impl!.content).toContain("r_signPipeline");
    expect(impl!.content).toContain("r_fetchPipeline");
  });

  it("DI ext registers two resilience pipelines", () => {
    const [, , , di] = emitGrpcClient(
      "KeyCustodian",
      [makeSignOp(), makeFetchOp()],
      CLIENTS_NS,
    );
    expect(di!.content).toContain("SignClientKeys.PIPELINE");
    expect(di!.content).toContain("FetchClientKeys.PIPELINE");
  });

  it("two DISTINCT services each get their own auto-wired outbound-auth chain", () => {
    // The byte-parity fixture is single-service, so this is the only place the
    // per-distinct-service auto-wire is proven: each AddGrpcClient<…> channel chains
    // its OWN .AddD2ForwardedJwt().AddD2WorkloadCertificate(). Count the chained
    // STATEMENTS (leading-`.` lines), not the per-method docstring mention.
    const [, , , di] = emitGrpcClient(
      "KeyCustodian",
      [makeSignOp(), makeFetchOp()],
      CLIENTS_NS,
    );
    const channelCount = (di!.content.match(/services\.AddGrpcClient</g) ?? [])
      .length;
    const forwardedJwtCount = (
      di!.content.match(/\n {16}\.AddD2ForwardedJwt\(\)/g) ?? []
    ).length;
    const workloadCertCount = (
      di!.content.match(/\n {16}\.AddD2WorkloadCertificate\(\);/g) ?? []
    ).length;
    expect(channelCount).toBe(2);
    expect(forwardedJwtCount).toBe(2);
    expect(workloadCertCount).toBe(2);
  });

  // Two ops on the SAME gRPC service — exercises the per-service dedup in the impl
  // (one stub field), the mapper, and the DI ext (one AddGrpcClient channel, two
  // AddTransient/pipeline registrations). The op grouping must be per-SERVICE.
  function makeVerifyOpSameService(): GrpcClientOp {
    return {
      opName: "verify",
      grpcService: "KeyCustodianSigner", // SAME service as sign
      grpcMethod: "Verify",
      protoCsharpNs: PROTO_NS,
      dtoCsharpNs: CLIENTS_NS,
      sourceSpec: SOURCE,
      requestModelName: "VerifyInput",
      requestFields: [],
      responseModelName: "VerifyOutput",
      responseFields: [
        {
          name: "valid",
          csName: "Valid",
          csType: "string",
          tsName: "valid",
          tsType: "string",
          protoType: "string",
          repeated: false,
          optional: false,
          redact: false,
        },
      ],
    };
  }

  it("two ops on the SAME service share ONE stub field but get per-op pipelines", () => {
    const [, impl] = emitGrpcClient(
      "KeyCustodian",
      [makeSignOp(), makeVerifyOpSameService()],
      CLIENTS_NS,
    );
    // Exactly ONE stub-field declaration for the shared service (dedup), not two.
    const stubFieldDecls = (
      impl!.content.match(/KeyCustodianSignerClient keyCustodianSignerStub/g) ??
      []
    ).length;
    expect(stubFieldDecls).toBe(1);
    // Both ops keep their own pipeline fields.
    expect(impl!.content).toContain("r_signPipeline");
    expect(impl!.content).toContain("r_verifyPipeline");
  });

  it("two ops on the SAME service register ONE AddGrpcClient channel + two clients", () => {
    const [, , , di] = emitGrpcClient(
      "KeyCustodian",
      [makeSignOp(), makeVerifyOpSameService()],
      CLIENTS_NS,
    );
    const channelCount = (di!.content.match(/services\.AddGrpcClient</g) ?? [])
      .length;
    expect(channelCount).toBe(1);
    expect(di!.content).toContain("SignClientKeys.PIPELINE");
    expect(di!.content).toContain("VerifyClientKeys.PIPELINE");
  });

  it("two ops on the SAME service emit one mapper class per op in one file", () => {
    const [, , mapper] = emitGrpcClient(
      "KeyCustodian",
      [makeSignOp(), makeVerifyOpSameService()],
      CLIENTS_NS,
    );
    expect(mapper!.content).toContain(
      "internal static class SignClientMappers",
    );
    expect(mapper!.content).toContain(
      "internal static class VerifyClientMappers",
    );
  });
});

// ---------------------------------------------------------------------------
// Namespace hygiene: dtoCsharpNs differs from clientsNs (DTO in a different namespace)
// ---------------------------------------------------------------------------

describe("emitGrpcClient_DtoNamespaceNotSelf", () => {
  it("interface aliases the DTO types (global:: rooted) when DTO ns differs from clientsNs", () => {
    const op = makeSignOp({
      dtoCsharpNs: "D2.Edge.SomeOtherModule.Clients",
    });
    const [iface] = emitGrpcClient("KeyCustodian", [op], CLIENTS_NS);
    expect(iface!.content).toContain(
      "using SignInput = global::D2.Edge.SomeOtherModule.Clients.SignInput;",
    );
    expect(iface!.content).toContain(
      "using SignOutput = global::D2.Edge.SomeOtherModule.Clients.SignOutput;",
    );
  });

  it("impl aliases the DTO types (avoids the proto SignOutput collision) when DTO ns differs", () => {
    const op = makeSignOp({
      dtoCsharpNs: "D2.Edge.SomeOtherModule.Clients",
    });
    const [, impl] = emitGrpcClient("KeyCustodian", [op], CLIENTS_NS);
    expect(impl!.content).toContain(
      "using SignOutput = global::D2.Edge.SomeOtherModule.Clients.SignOutput;",
    );
    // The proto SERVICE namespace is NOT bare-imported (the stub is referenced via global::),
    // so the bare SignOutput in the pipeline generic args resolves to the DTO alias only.
    expect(impl!.content).not.toContain(
      "using D2.Services.Protos.KeyCustodian.V1;",
    );
  });

  it("interface emits no DTO alias usings when dtoCsharpNs == clientsNs (DTOs namespace-local)", () => {
    const [iface] = emitGrpcClient("KeyCustodian", [makeSignOp()], CLIENTS_NS);
    expect(iface!.content).not.toContain("global::");
  });

  it("impl file does NOT add clientsNs using when dtoCsharpNs == clientsNs", () => {
    const [, impl] = emitGrpcClient("KeyCustodian", [makeSignOp()], CLIENTS_NS);
    expect(impl!.content).not.toContain(`using ${CLIENTS_NS};`);
  });

  it("mapper file includes dtoCsharpNs using when different from clientsNs", () => {
    const op = makeSignOp({
      dtoCsharpNs: "D2.Edge.SomeOtherModule.Clients",
    });
    const [, , mapper] = emitGrpcClient("KeyCustodian", [op], CLIENTS_NS);
    expect(mapper!.content).toContain("using D2.Edge.SomeOtherModule.Clients;");
  });
});

// ---------------------------------------------------------------------------
// Enum client op — DTO enum → proto string via .ToWire() + the helper blocks
// ---------------------------------------------------------------------------

describe("emitGrpcClient_EnumRequestField_AliasAndToWire", () => {
  const KEY_KIND = {
    name: "KeyKind",
    members: [
      { csName: "Rsa", wireValue: "Rsa", needsEnumMember: false },
      { csName: "Aes", wireValue: "Aes", needsEnumMember: false },
    ],
  };

  function enumOp(): GrpcClientOp {
    return {
      opName: "signWithKind",
      grpcService: "EnumFixturesSigner",
      grpcMethod: "SignWithKind",
      protoCsharpNs: "D2.Services.Protos.EnumFixtures.V1",
      // DTO namespace distinct from the clients namespace → enum alias IS emitted.
      dtoCsharpNs: "D2.Edge.Tests.EnumDto.Generated",
      sourceSpec: SOURCE,
      requestModelName: "SignWithKindInput",
      requestFields: [
        {
          name: "kid",
          csName: "Kid",
          csType: "string",
          tsName: "kid",
          tsType: "string",
          protoType: "string",
          repeated: false,
          optional: false,
          redact: false,
        },
        {
          name: "keyKind",
          csName: "KeyKind",
          csType: "KeyKind",
          tsName: "keyKind",
          tsType: "KeyKind",
          protoType: "string",
          repeated: false,
          optional: false,
          redact: false,
          enumRef: KEY_KIND,
        },
      ],
      responseModelName: "SignWithKindOutput",
      responseFields: [
        {
          name: "signature",
          csName: "Signature",
          csType: "string",
          tsName: "signature",
          tsType: "string",
          protoType: "string",
          repeated: false,
          optional: false,
          redact: false,
        },
      ],
    };
  }

  it("the client mapper emits the enum alias + I18n/Result usings + ToWire on the outbound DTO field", () => {
    const [, , mapper] = emitGrpcClient(
      "EnumFixtures",
      [enumOp()],
      CLIENTS_NS,
    );

    expect(mapper!.content).toContain(
      "using KeyKind = global::D2.Edge.Tests.EnumDto.Generated.KeyKind;",
    );
    expect(mapper!.content).toContain("using D2.Shared.I18n;");
    expect(mapper!.content).toContain("using D2.Shared.Result;");
    // Outbound DTO enum → proto string via .ToWire().
    expect(mapper!.content).toContain("KeyKind = input.KeyKind.ToWire(),");
    // The per-enum helper blocks are emitted (symmetric with the server mapper).
    expect(mapper!.content).toContain("internal string ToWire()");
    expect(mapper!.content).toContain(
      "internal static D2Result<KeyKind> ParseKeyKindWire(string? value)",
    );
  });

  it("two DISTINCT enum fields → one alias each (the dedup .some predicate runs on the 2nd)", () => {
    const ROLE = {
      name: "Role",
      members: [
        { csName: "Admin", wireValue: "admin", needsEnumMember: true },
        { csName: "User", wireValue: "user", needsEnumMember: true },
      ],
    };
    const op = enumOp();
    const opTwoEnums: GrpcClientOp = {
      ...op,
      requestFields: [
        ...op.requestFields,
        {
          name: "role",
          csName: "Role",
          csType: "Role",
          tsName: "role",
          tsType: "Role",
          protoType: "string",
          repeated: false,
          optional: false,
          redact: false,
          enumRef: ROLE,
        },
      ],
    };

    const [, , mapper] = emitGrpcClient("EnumFixtures", [opTwoEnums], CLIENTS_NS);

    expect(mapper!.content).toContain(
      "using KeyKind = global::D2.Edge.Tests.EnumDto.Generated.KeyKind;",
    );
    expect(mapper!.content).toContain(
      "using Role = global::D2.Edge.Tests.EnumDto.Generated.Role;",
    );
  });

  it("the SAME enum referenced twice across fields → one alias (dedup .some returns true)", () => {
    const op = enumOp();
    const dupOp: GrpcClientOp = {
      ...op,
      responseFields: [
        op.responseFields[0]!,
        {
          name: "echoedKind",
          csName: "EchoedKind",
          csType: "KeyKind",
          tsName: "echoedKind",
          tsType: "KeyKind",
          protoType: "string",
          repeated: false,
          optional: false,
          redact: false,
          enumRef: op.requestFields[1]!.enumRef,
        },
      ],
    };

    const [, , mapper] = emitGrpcClient("EnumFixtures", [dupOp], CLIENTS_NS);

    const aliasCount = (
      mapper!.content.match(/using KeyKind = global::/g) ?? []
    ).length;
    expect(aliasCount).toBe(1);
  });
});

// ---------------------------------------------------------------------------
// Response enum — the client proto-string -> DTO-enum inbound parse (symmetric
// with the server request parse): To<Output>() returns D2Result<<Output>> and
// the impl surfaces a client-side parse failure as ValidationFailed.
// ---------------------------------------------------------------------------

describe("emitGrpcClient_EnumResponseField_ParseAndSurface", () => {
  const KEY_KIND = {
    name: "KeyKind",
    members: [
      { csName: "Rsa", wireValue: "Rsa", needsEnumMember: false },
      { csName: "Aes", wireValue: "Aes", needsEnumMember: false },
    ],
  };

  function respEnumOp(): GrpcClientOp {
    return {
      opName: "signWithKind",
      grpcService: "EnumFixturesSigner",
      grpcMethod: "SignWithKind",
      protoCsharpNs: "D2.Services.Protos.EnumFixtures.V1",
      dtoCsharpNs: "D2.Edge.Tests.EnumDto.Generated",
      sourceSpec: SOURCE,
      requestModelName: "SignWithKindInput",
      requestFields: [
        {
          name: "kid",
          csName: "Kid",
          csType: "string",
          tsName: "kid",
          tsType: "string",
          protoType: "string",
          repeated: false,
          optional: false,
          redact: false,
        },
      ],
      responseModelName: "SignWithKindOutput",
      responseFields: [
        {
          name: "signature",
          csName: "Signature",
          csType: "string",
          tsName: "signature",
          tsType: "string",
          protoType: "string",
          repeated: false,
          optional: false,
          redact: false,
        },
        {
          name: "keyKind",
          csName: "KeyKind",
          csType: "KeyKind",
          tsName: "keyKind",
          tsType: "KeyKind",
          protoType: "string",
          repeated: false,
          optional: false,
          redact: false,
          enumRef: KEY_KIND,
        },
      ],
    };
  }

  it("the response mapper returns D2Result<<Output>> and parses the enum fail-loud", () => {
    const [, , mapper] = emitGrpcClient(
      "EnumFixtures",
      [respEnumOp()],
      CLIENTS_NS,
    );

    // Mapper signature now returns D2Result<<Output>> (not a bare <Output>).
    expect(mapper!.content).toContain(
      "internal D2Result<global::D2.Edge.Tests.EnumDto.Generated.SignWithKindOutput> ToSignWithKindOutput()",
    );
    // Inbound parse via the shared Parse<Enum>Wire helper.
    expect(mapper!.content).toContain(
      "var keyKindResult = string.ParseKeyKindWire(data.KeyKind);",
    );
    // Fail-loud short-circuit on an unknown wire value.
    expect(mapper!.content).toContain(
      "return D2Result<global::D2.Edge.Tests.EnumDto.Generated.SignWithKindOutput>.ValidationFailed(",
    );
    // Success constructs the DTO with the parsed enum (and the plain signature).
    expect(mapper!.content).toContain(
      "return D2Result<global::D2.Edge.Tests.EnumDto.Generated.SignWithKindOutput>.Ok(new global::D2.Edge.Tests.EnumDto.Generated.SignWithKindOutput(data.Signature, keyKindResult.Data));",
    );
  });

  it("the impl captures the response-parse failure and surfaces it via BubbleFail", () => {
    const [, impl] = emitGrpcClient("EnumFixtures", [respEnumOp()], CLIENTS_NS);

    // The parse-failure capture local is declared out of the closure.
    expect(impl!.content).toContain(
      "D2Result<SignWithKindOutput>? responseParseFailure = null;",
    );
    // The closure parses then captures + returns default on failure.
    expect(impl!.content).toContain(
      "var dataResult = response.Data.ToSignWithKindOutput();",
    );
    expect(impl!.content).toContain("responseParseFailure = dataResult;");
    expect(impl!.content).toContain("return dataResult.Data;");
    // After the pipeline, a captured parse failure becomes the business result.
    expect(impl!.content).toContain(
      "if (responseParseFailure is not null)",
    );
    expect(impl!.content).toContain(
      "return D2Result<SignWithKindOutput?>.BubbleFail(responseParseFailure);",
    );
  });

  it("an enum-FREE response keeps the bare <Output> mapper (no D2Result, no capture)", () => {
    // The sign op (string-only response) must NOT gain the D2Result response path.
    const [, impl, mapper] = emitGrpcClient(
      "KeyCustodian",
      [makeSignOp()],
      CLIENTS_NS,
    );
    expect(mapper!.content).toContain(
      "internal global::D2.Edge.KeyCustodian.Clients.SignOutput ToSignOutput()",
    );
    expect(mapper!.content).not.toContain("D2Result<");
    expect(impl!.content).not.toContain("responseParseFailure");
    expect(impl!.content).toContain(
      "return response.Data is null ? default : response.Data.ToSignOutput();",
    );
  });
});
