// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Direct-unit tests for the gRPC service-impl emitter (emitGrpcService).
//
// Covers: global:: base-class qualification, handler delegation body,
// proto↔DTO short using-aliases, ByteString/byte[] mapper conversion,
// file-header conventions, failure-mapping body, and absence of
// phase/step/audit-round identifiers in emitted content.

import { describe, it, expect } from "vitest";
import type { FieldInfo } from "../src/lib/model-walk.js";
import { emitGrpcService } from "../src/lib/grpc-service-emitter.js";
import type { GrpcDelegationTarget } from "../src/lib/grpc-service-emitter.js";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const PROTO_NS = "D2.Services.Protos.KeyCustodian.V1";
const IMPL_NS = "D2.Edge.Tests.TypeSpecGrpc.Generated";
const DTO_NS = "D2.Edge.Tests.TypeSpecDto.Generated";
const SOURCE = "contracts/typespec/fixtures/sign-shaped.tsp";

function makeStringField(name: string): FieldInfo {
  return {
    name,
    csName: name.charAt(0).toUpperCase() + name.slice(1),
    csType: "string",
    tsName: name,
    tsType: "string",
    protoType: "string",
    repeated: false,
    optional: false,
    redact: false,
  };
}

function makeBytesField(name: string): FieldInfo {
  return {
    name,
    csName: name.charAt(0).toUpperCase() + name.slice(1),
    csType: "byte[]",
    tsName: name,
    tsType: "Uint8Array",
    protoType: "bytes",
    repeated: false,
    optional: false,
    redact: true,
  };
}

function emitSign() {
  return emitGrpcService(
    "sign",
    "KeyCustodianSigner",
    "Sign",
    PROTO_NS,
    IMPL_NS,
    DTO_NS,
    SOURCE,
    "SignRequest",
    "SignResponse",
    "SignInput",
    [makeStringField("kid"), makeBytesField("payload")],
    "SignOutput",
    [makeStringField("signature")],
  );
}

// ---------------------------------------------------------------------------
// Test 1: global:: base-class qualification (SC1 lesson)
// ---------------------------------------------------------------------------

describe("emitGrpcService_BaseClass_GlobalQualified", () => {
  it("service class extends global::<protoNs>.<Service>.<Service>Base", () => {
    const [svc] = emitSign();
    expect(svc.content).toContain(
      ": global::D2.Services.Protos.KeyCustodian.V1.KeyCustodianSigner.KeyCustodianSignerBase",
    );
  });
});

// ---------------------------------------------------------------------------
// Test 2: ctor parameter + method override signature
// ---------------------------------------------------------------------------

describe("emitGrpcService_CtorAndMethodSignature", () => {
  it("ctor takes ISignHandler; method overrides Sign with proto request + ServerCallContext", () => {
    const [svc] = emitSign();
    expect(svc.content).toContain("public sealed class KeyCustodianSignerService(ISignHandler handler)");
    expect(svc.content).toContain("public override async Task<SignResponse> Sign(SignRequest request, ServerCallContext context)");
  });
});

// ---------------------------------------------------------------------------
// Test 3: handler delegation body
// ---------------------------------------------------------------------------

describe("emitGrpcService_DelegationBody", () => {
  it("method body calls handler.HandleAsync with mapped input + CancellationToken", () => {
    const [svc] = emitSign();
    expect(svc.content).toContain("SignInput input = request.ToSignInput();");
    expect(svc.content).toContain("var result = await handler.HandleAsync(input, context.CancellationToken).ConfigureAwait(false);");
  });
});

// ---------------------------------------------------------------------------
// Test 4: proto↔DTO short using-aliases present (no Proto*/Dto* prefix needed)
// ---------------------------------------------------------------------------

describe("emitGrpcService_TypeAliases_Disambiguate", () => {
  it("emits short using-aliases for proto message and DTO types (distinct names, no prefix required)", () => {
    const [svc] = emitSign();
    // Proto message names (SignRequest/SignResponse) are distinct from DTO names
    // (SignInput/SignOutput), so no Proto*/Dto* prefixes are needed.
    expect(svc.content).toContain("using SignRequest = global::D2.Services.Protos.KeyCustodian.V1.SignRequest;");
    expect(svc.content).toContain("using SignResponse = global::D2.Services.Protos.KeyCustodian.V1.SignResponse;");
    expect(svc.content).toContain("using SignInput = global::D2.Edge.Tests.TypeSpecDto.Generated.SignInput;");
    expect(svc.content).toContain("using SignOutput = global::D2.Edge.Tests.TypeSpecDto.Generated.SignOutput;");
    // Must NOT emit old Proto*/Dto*-prefixed using-alias declarations.
    expect(svc.content).not.toContain("using ProtoSignInput");
    expect(svc.content).not.toContain("using DtoSignInput");
    expect(svc.content).not.toContain("using ProtoSignOutput");
    expect(svc.content).not.toContain("using DtoSignOutput");
  });

  it("mapper file also emits the short using-aliases", () => {
    const [, mapper] = emitSign();
    expect(mapper.content).toContain("using SignRequest = global::D2.Services.Protos.KeyCustodian.V1.SignRequest;");
    expect(mapper.content).toContain("using SignOutput = global::D2.Edge.Tests.TypeSpecDto.Generated.SignOutput;");
    // No Proto*/Dto*-prefixed using-alias declarations in mapper either.
    expect(mapper.content).not.toContain("using ProtoSignInput");
    expect(mapper.content).not.toContain("using DtoSignOutput");
  });
});

// ---------------------------------------------------------------------------
// Test 5: transport mapper — extension block form + bytes conversion
// ---------------------------------------------------------------------------

describe("emitGrpcService_TransportMapper_ExtensionBlockForm", () => {
  it("emits C# 14 block-form extension members (not this T parameter style)", () => {
    const [, mapper] = emitSign();
    expect(mapper.content).toContain("extension(SignRequest request)");
    expect(mapper.content).toContain("extension(SignOutput output)");
    // Must NOT use old `this T` form.
    expect(mapper.content).not.toContain("this SignRequest");
    expect(mapper.content).not.toContain("this SignOutput");
  });

  it("request mapper uses .ToByteArray() for bytes↔byte[] conversion", () => {
    const [, mapper] = emitSign();
    expect(mapper.content).toContain("request.Payload.ToByteArray()");
  });

  it("response mapper has no bytes conversion (string field only)", () => {
    const [, mapper] = emitSign();
    expect(mapper.content).toContain("Signature = output.Signature");
    expect(mapper.content).not.toContain("ByteString.CopyFrom(output.Signature)");
  });

  it("response mapper with bytes field uses ByteString.CopyFrom", () => {
    // Build a response that has a bytes field.
    const [, mapper] = emitGrpcService(
      "test", "Svc", "Do", PROTO_NS, IMPL_NS, DTO_NS, SOURCE,
      "DoRequest", "DoResponse",
      "Req", [makeStringField("id")],
      "Resp", [makeBytesField("data")],
    );
    expect(mapper.content).toContain("using Google.Protobuf;");
    expect(mapper.content).toContain("ByteString.CopyFrom(output.Data)");
  });
});

// ---------------------------------------------------------------------------
// Test 6: file-header conventions
// ---------------------------------------------------------------------------

describe("emitGrpcService_FileHeaderConventions", () => {
  it("service file has auto-generated banner", () => {
    const [svc] = emitSign();
    expect(svc.content).toContain("// <auto-generated>");
    expect(svc.content).toContain("Generated by the @d2/typespec-emitters TypeSpec emitter.");
  });

  it("service file has #nullable enable", () => {
    const [svc] = emitSign();
    expect(svc.content).toContain("#nullable enable");
  });

  it("service file namespace-before-using ordering", () => {
    const [svc] = emitSign();
    const nsIdx = svc.content.indexOf("namespace ");
    const usingIdx = svc.content.indexOf("\nusing ");
    expect(nsIdx).toBeLessThan(usingIdx);
  });

  it("service class is sealed", () => {
    const [svc] = emitSign();
    expect(svc.content).toContain("public sealed class");
  });

  it("mapper class is internal static", () => {
    const [, mapper] = emitSign();
    expect(mapper.content).toContain("internal static class SignTransportMappers");
  });
});

// ---------------------------------------------------------------------------
// Test 7: failure mapping body present
// ---------------------------------------------------------------------------

describe("emitGrpcService_FailureMapping_RpcExceptionPresent", () => {
  it("on failure IsSuccess false → throws RpcException with Status.Internal empty detail", () => {
    const [svc] = emitSign();
    expect(svc.content).toContain("if (!result.IsOk)");
    expect(svc.content).toContain("throw new RpcException(new Status(StatusCode.Internal, string.Empty));");
  });

  it("success path maps result.Data! to proto response", () => {
    const [svc] = emitSign();
    expect(svc.content).toContain("return result.Data!.ToProtoSignOutput();");
  });
});

// ---------------------------------------------------------------------------
// Test 8: no phase/step/audit-round identifiers
// ---------------------------------------------------------------------------

describe("emitGrpcService_NoPhaseAuditIdentifiers", () => {
  it("emitted service file contains no phase/step/deliverable/audit-round identifiers", () => {
    const [svc] = emitSign();
    // Patterns: Step N, 0019, R0-R9, F0-F9.
    expect(svc.content).not.toMatch(/Step\s+\d/);
    expect(svc.content).not.toContain("0019");
    expect(svc.content).not.toMatch(/\bR[0-9]\b/);
    expect(svc.content).not.toMatch(/\bF[0-9]\b/);
  });

  it("emitted mapper file contains no phase/step/deliverable/audit-round identifiers", () => {
    const [, mapper] = emitSign();
    expect(mapper.content).not.toMatch(/Step\s+\d/);
    expect(mapper.content).not.toContain("0019");
  });
});

// ---------------------------------------------------------------------------
// Test 9: file names
// ---------------------------------------------------------------------------

describe("emitGrpcService_FileNames", () => {
  it("service file named <Service>Service.g.cs", () => {
    const [svc] = emitSign();
    expect(svc.fileName).toBe("KeyCustodianSignerService.g.cs");
  });

  it("mapper file named <PascalOp>TransportMappers.g.cs", () => {
    const [, mapper] = emitSign();
    expect(mapper.fileName).toBe("SignTransportMappers.g.cs");
  });
});

// ---------------------------------------------------------------------------
// Test 10: empty request fields → parameterless constructor
// ---------------------------------------------------------------------------

describe("emitGrpcService_EmptyRequest_ParameterlessConstructor", () => {
  it("request with no fields → new DtoReq() call in mapper", () => {
    const [, mapper] = emitGrpcService(
      "op", "Svc", "Do", PROTO_NS, IMPL_NS, DTO_NS, SOURCE,
      "DoRequest", "DoResponse",
      "EmptyIn", [],
      "EmptyOut", [],
    );
    expect(mapper.content).toContain("return new EmptyIn();");
    expect(mapper.content).toContain("return new DoResponse();");
  });
});

// ---------------------------------------------------------------------------
// Test 11: façade delegation branch — the re-pointed gRPC service
// ---------------------------------------------------------------------------

describe("emitGrpcService_FacadeDelegation_RePointedService", () => {
  const FACADE_TARGET: GrpcDelegationTarget = {
    kind: "facade",
    typeName: "IKeyCustodianSignerFacade",
    methodName: "SignAsync",
    targetNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated.Facade",
  };

  function emitSignWithFacade() {
    return emitGrpcService(
      "sign",
      "KeyCustodianSigner",
      "Sign",
      PROTO_NS,
      IMPL_NS,
      DTO_NS,
      SOURCE,
      "SignRequest",
      "SignResponse",
      "SignInput",
      [makeStringField("kid"), makeBytesField("payload")],
      "SignOutput",
      [makeStringField("signature")],
      FACADE_TARGET,
    );
  }

  it("ctor injects the façade type (not ISignHandler)", () => {
    const [svc] = emitSignWithFacade();
    expect(svc.content).toContain("public sealed class KeyCustodianSignerService(IKeyCustodianSignerFacade facade)");
    expect(svc.content).not.toContain("ISignHandler handler");
  });

  it("call site uses facade.SignAsync (2-arg transport-neutral, not handler.HandleAsync)", () => {
    const [svc] = emitSignWithFacade();
    expect(svc.content).toContain("var result = await facade.SignAsync(input, context.CancellationToken).ConfigureAwait(false);");
    expect(svc.content).not.toContain("handler.HandleAsync");
  });

  it("adds a using for the façade namespace", () => {
    const [svc] = emitSignWithFacade();
    expect(svc.content).toContain("using D2.Edge.Tests.TypeSpecRoute.Generated.Facade;");
  });

  it("XML doc references the façade type", () => {
    const [svc] = emitSignWithFacade();
    expect(svc.content).toContain('delegating to <see cref="IKeyCustodianSignerFacade"/>');
  });

  it("failure-mapping is unchanged (RpcException Internal, empty detail)", () => {
    const [svc] = emitSignWithFacade();
    expect(svc.content).toContain("if (!result.IsOk)");
    expect(svc.content).toContain("throw new RpcException(new Status(StatusCode.Internal, string.Empty));");
  });

  it("success path maps result.Data! to proto response (unchanged)", () => {
    const [svc] = emitSignWithFacade();
    expect(svc.content).toContain("return result.Data!.ToProtoSignOutput();");
  });

  it("transport mapper is UNCHANGED regardless of delegation target", () => {
    const [, mapper] = emitSignWithFacade();
    // Mapper uses the same proto↔DTO mapping irrespective of who the service delegates to.
    expect(mapper.content).toContain("extension(SignRequest request)");
    expect(mapper.content).toContain("extension(SignOutput output)");
    expect(mapper.content).toContain("internal SignInput ToSignInput()");
    expect(mapper.content).toContain("internal SignResponse ToProtoSignOutput()");
  });
});

// ---------------------------------------------------------------------------
// Test 12: handler delegation branch (no delegationTarget — backward-compat default)
// ---------------------------------------------------------------------------

describe("emitGrpcService_HandlerDelegation_Default", () => {
  it("no delegationTarget supplied → defaults to I<PascalOp>Handler.HandleAsync", () => {
    // The backward-compatible default (delegationTarget omitted) must produce
    // the same handler-delegation output as passing an explicit handler target.
    const [svcDefault] = emitGrpcService(
      "sign",
      "KeyCustodianSigner",
      "Sign",
      PROTO_NS,
      IMPL_NS,
      DTO_NS,
      SOURCE,
      "SignRequest",
      "SignResponse",
      "SignInput",
      [makeStringField("kid"), makeBytesField("payload")],
      "SignOutput",
      [makeStringField("signature")],
      // No delegationTarget — falls back to handler.
    );
    expect(svcDefault.content).toContain("ISignHandler handler");
    expect(svcDefault.content).toContain("handler.HandleAsync(input, context.CancellationToken)");
    expect(svcDefault.content).not.toContain("facade");
  });

  it("explicit handler target produces the same result as the omitted default", () => {
    const handlerTarget: GrpcDelegationTarget = {
      kind: "handler",
      typeName: "ISignHandler",
      methodName: "HandleAsync",
    };
    const [svcExplicit] = emitGrpcService(
      "sign",
      "KeyCustodianSigner",
      "Sign",
      PROTO_NS,
      IMPL_NS,
      DTO_NS,
      SOURCE,
      "SignRequest",
      "SignResponse",
      "SignInput",
      [makeStringField("kid"), makeBytesField("payload")],
      "SignOutput",
      [makeStringField("signature")],
      handlerTarget,
    );
    const [svcDefault] = emitGrpcService(
      "sign",
      "KeyCustodianSigner",
      "Sign",
      PROTO_NS,
      IMPL_NS,
      DTO_NS,
      SOURCE,
      "SignRequest",
      "SignResponse",
      "SignInput",
      [makeStringField("kid"), makeBytesField("payload")],
      "SignOutput",
      [makeStringField("signature")],
    );
    expect(svcExplicit.content).toBe(svcDefault.content);
  });
});

// ---------------------------------------------------------------------------
// Test 13: façade target with targetNamespace === serviceImplNs → no extra using
// ---------------------------------------------------------------------------

describe("emitGrpcService_FacadeDelegation_SameNamespaceNoExtraUsing", () => {
  it("façade targetNamespace === serviceImplNs → no duplicate using added", () => {
    const sameNsTarget: GrpcDelegationTarget = {
      kind: "facade",
      typeName: "ISomeFacade",
      methodName: "DoAsync",
      targetNamespace: IMPL_NS, // same as serviceImplNs → no extra using
    };
    const [svc] = emitGrpcService(
      "doIt",
      "Svc",
      "Do",
      PROTO_NS,
      IMPL_NS,
      DTO_NS,
      SOURCE,
      "DoRequest",
      "DoResponse",
      "DoIn",
      [],
      "DoOut",
      [],
      sameNsTarget,
    );
    // The façade is in the same namespace — no extra using should be added.
    const usingCount = (svc.content.match(/^using /gm) ?? []).length;
    // Only the standard usings (proto aliases + Grpc.Core) — no duplicate.
    expect(svc.content).not.toContain(`using ${IMPL_NS};`);
    // Ctor uses the façade type.
    expect(svc.content).toContain("ISomeFacade facade");
    expect(usingCount).toBeGreaterThanOrEqual(1);
  });
});

// ---------------------------------------------------------------------------
// Test 14: no phase/step/audit identifiers — delegation target shapes (§5)
// ---------------------------------------------------------------------------

describe("emitGrpcService_FacadeDelegation_NoPhaseAuditIdentifiers", () => {
  it("façade-delegation service contains no phase/step/deliverable identifiers", () => {
    const facadeTarget: GrpcDelegationTarget = {
      kind: "facade",
      typeName: "IMyFacade",
      methodName: "OpAsync",
      targetNamespace: "My.Ns.Clients",
    };
    const [svc] = emitGrpcService(
      "op", "Svc", "Do", PROTO_NS, IMPL_NS, DTO_NS, SOURCE,
      "DoReq", "DoResp",
      "DoIn", [],
      "DoOut", [],
      facadeTarget,
    );
    expect(svc.content).not.toMatch(/Step\s+\d/);
    expect(svc.content).not.toContain("0019");
    expect(svc.content).not.toMatch(/\bR[0-9]\b/);
    expect(svc.content).not.toMatch(/\bF[0-9]\b/);
  });
});
