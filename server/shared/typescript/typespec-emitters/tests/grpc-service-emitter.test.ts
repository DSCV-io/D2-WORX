// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Direct-unit tests for the gRPC service-impl emitter (emitGrpcService).
//
// Covers: global:: base-class qualification, handler delegation body,
// proto↔DTO short using-aliases, ByteString/byte[] mapper conversion,
// file-header conventions, D2Result envelope population (no throw),
// and absence of phase/step/audit-round identifiers in emitted content.

import { describe, it, expect } from "vitest";
import type {
  FieldInfo,
  NestedEnum,
  NestedModel,
} from "../src/lib/model-walk.js";
import { emitGrpcService } from "../src/lib/grpc-service-emitter.js";
import type { GrpcDelegationTarget } from "../src/lib/grpc-service-emitter.js";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const PROTO_NS = "D2.Services.Protos.Sample.V1";
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
    redactReason: undefined,
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
    redactReason: "SecretInformation",
  };
}

function emitSign() {
  return emitGrpcService(
    "sign",
    "SampleSigner",
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
// Test 1: global:: base-class qualification
// ---------------------------------------------------------------------------

describe("emitGrpcService_BaseClass_GlobalQualified", () => {
  it("service class extends global::<protoNs>.<Service>.<Service>Base", () => {
    const [svc] = emitSign();
    expect(svc.content).toContain(
      ": global::D2.Services.Protos.Sample.V1.SampleSigner.SampleSignerBase",
    );
  });
});

// ---------------------------------------------------------------------------
// Test 2: ctor parameter + method override signature
// ---------------------------------------------------------------------------

describe("emitGrpcService_CtorAndMethodSignature", () => {
  it("ctor takes ISignHandler; method overrides Sign with proto request + ServerCallContext", () => {
    const [svc] = emitSign();
    expect(svc.content).toContain(
      "public sealed class SampleSignerService(ISignHandler handler)",
    );
    expect(svc.content).toContain(
      "public override async Task<SignResponse> Sign(SignRequest request, ServerCallContext context)",
    );
  });
});

// ---------------------------------------------------------------------------
// Test 3: handler delegation body
// ---------------------------------------------------------------------------

describe("emitGrpcService_DelegationBody", () => {
  it("method body calls handler.HandleAsync with mapped input + CancellationToken", () => {
    const [svc] = emitSign();
    expect(svc.content).toContain("SignInput input = request.ToSignInput();");
    expect(svc.content).toContain(
      "var result = await handler.HandleAsync(input, context.CancellationToken).ConfigureAwait(false);",
    );
  });
});

// ---------------------------------------------------------------------------
// Test 4: proto↔DTO short using-aliases present (no Proto*/Dto* prefix needed)
// ---------------------------------------------------------------------------

describe("emitGrpcService_TypeAliases_Disambiguate", () => {
  it("emits short using-aliases for proto message and DTO types (distinct names, no prefix required)", () => {
    const [svc] = emitSign();
    // Proto message names (SignRequest/SignResponse) are distinct from DTO names
    // (SignInput/SignOutput), so no Proto*/Dto* prefixes are needed in the SERVICE file.
    expect(svc.content).toContain(
      "using SignRequest = global::D2.Services.Protos.Sample.V1.SignRequest;",
    );
    expect(svc.content).toContain(
      "using SignResponse = global::D2.Services.Protos.Sample.V1.SignResponse;",
    );
    expect(svc.content).toContain(
      "using SignInput = global::D2.Edge.Tests.TypeSpecDto.Generated.SignInput;",
    );
    expect(svc.content).toContain(
      "using SignOutput = global::D2.Edge.Tests.TypeSpecDto.Generated.SignOutput;",
    );
    // Service file: no old Proto*/Dto*-prefixed using-alias declarations.
    expect(svc.content).not.toContain("using ProtoSignInput");
    expect(svc.content).not.toContain("using DtoSignInput");
    // The service file does not reference the proto data message directly (mapper handles it).
    expect(svc.content).not.toContain("using ProtoSignOutput");
    expect(svc.content).not.toContain("using DtoSignOutput");
  });

  it("mapper file emits standard using-aliases PLUS a ProtoSignOutput disambiguation alias", () => {
    const [, mapper] = emitSign();
    expect(mapper.content).toContain(
      "using SignRequest = global::D2.Services.Protos.Sample.V1.SignRequest;",
    );
    expect(mapper.content).toContain(
      "using SignOutput = global::D2.Edge.Tests.TypeSpecDto.Generated.SignOutput;",
    );
    // The proto data message name (<Op>Output) collides with the DTO name (<Op>Output).
    // The mapper emits a ProtoSignOutput alias to disambiguate.
    expect(mapper.content).toContain(
      "using ProtoSignOutput = global::D2.Services.Protos.Sample.V1.SignOutput;",
    );
    // No DtoSignOutput prefix (the DTO alias keeps the bare SignOutput name).
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
    expect(mapper.content).toContain("extension(D2Result<SignOutput?> result)");
    expect(mapper.content).toContain("extension(SignOutput output)");
    // Must NOT use old `this T` form.
    expect(mapper.content).not.toContain("this SignRequest");
    expect(mapper.content).not.toContain("this SignOutput");
  });

  it("mapper emits D2Result.Grpc + D2.Shared.Result usings for the envelope block", () => {
    const [, mapper] = emitSign();
    expect(mapper.content).toContain("using D2.Shared.Result;");
    expect(mapper.content).toContain("using D2.Shared.Result.Grpc;");
  });

  it("envelope extension block populates result + conditionally sets data", () => {
    const [, mapper] = emitSign();
    expect(mapper.content).toContain("internal SignResponse ToProtoResponse()");
    expect(mapper.content).toContain(
      "var response = new SignResponse { Result = result.ToProto() };",
    );
    expect(mapper.content).toContain(
      "if (result.IsOk && result.Data is not null)",
    );
    expect(mapper.content).toContain(
      "response.Data = result.Data.ToProtoSignOutput();",
    );
    expect(mapper.content).toContain("return response;");
  });

  it("request mapper uses .ToByteArray() for bytes↔byte[] conversion", () => {
    const [, mapper] = emitSign();
    expect(mapper.content).toContain("request.Payload.ToByteArray()");
  });

  it("data-message mapper has no bytes conversion (string field only)", () => {
    const [, mapper] = emitSign();
    expect(mapper.content).toContain("Signature = output.Signature");
    expect(mapper.content).not.toContain(
      "ByteString.CopyFrom(output.Signature)",
    );
  });

  it("data-message mapper with bytes field uses ByteString.CopyFrom", () => {
    // Build a response that has a bytes field.
    const [, mapper] = emitGrpcService(
      "test",
      "Svc",
      "Do",
      PROTO_NS,
      IMPL_NS,
      DTO_NS,
      SOURCE,
      "DoRequest",
      "DoResponse",
      "Req",
      [makeStringField("id")],
      "Resp",
      [makeBytesField("data")],
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
    expect(svc.content).toContain(
      "Generated by the @d2/typespec-emitters TypeSpec emitter.",
    );
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
    expect(mapper.content).toContain(
      "internal static class SignTransportMappers",
    );
  });
});

// ---------------------------------------------------------------------------
// Test 7: envelope population — no throw (§0.5 fidelity)
// ---------------------------------------------------------------------------

describe("emitGrpcService_EnvelopePopulation_NoThrow", () => {
  it("method body ends with result.ToProtoResponse() — no throw, no IsOk branch", () => {
    const [svc] = emitSign();
    // The service delegates the entire result (success OR failure) to the envelope mapper.
    // RpcException is NEVER thrown for business results.
    expect(svc.content).toContain("return result.ToProtoResponse();");
    expect(svc.content).not.toContain("throw new RpcException");
    expect(svc.content).not.toContain("if (!result.IsOk)");
  });

  it("service emits using D2.Shared.Result.Grpc for the ToProtoResponse extension", () => {
    const [svc] = emitSign();
    // Prefer global:: so serviceImplNs containing ".Grpc." cannot shadow Result.Grpc / Grpc.Core.
    expect(svc.content).toMatch(/using (global::)?D2\.Shared\.Result\.Grpc;/);
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
    expect(svc.fileName).toBe("SampleSignerService.g.cs");
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
  it("request with no fields → new DtoReq() in request mapper; empty data-msg → new ProtoEmptyOut()", () => {
    const [, mapper] = emitGrpcService(
      "op",
      "Svc",
      "Do",
      PROTO_NS,
      IMPL_NS,
      DTO_NS,
      SOURCE,
      "DoRequest",
      "DoResponse",
      "EmptyIn",
      [],
      "EmptyOut",
      [],
    );
    // Empty request → parameterless constructor.
    expect(mapper.content).toContain("return new EmptyIn();");
    // Empty data message → parameterless constructor for the proto alias type.
    expect(mapper.content).toContain("return new ProtoEmptyOut();");
    // The envelope Response is NOT hand-constructed here — it goes via ToProtoResponse().
    expect(mapper.content).not.toContain("return new DoResponse();");
  });
});

// ---------------------------------------------------------------------------
// Test 11: façade delegation branch — the re-pointed gRPC service
// ---------------------------------------------------------------------------

describe("emitGrpcService_FacadeDelegation_RePointedService", () => {
  const FACADE_TARGET: GrpcDelegationTarget = {
    kind: "facade",
    typeName: "ISampleSignerFacade",
    methodName: "SignAsync",
    targetNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated.Facade",
  };

  function emitSignWithFacade() {
    return emitGrpcService(
      "sign",
      "SampleSigner",
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
    expect(svc.content).toContain(
      "public sealed class SampleSignerService(ISampleSignerFacade facade)",
    );
    expect(svc.content).not.toContain("ISignHandler handler");
  });

  it("call site uses facade.SignAsync (2-arg transport-neutral, not handler.HandleAsync)", () => {
    const [svc] = emitSignWithFacade();
    expect(svc.content).toContain(
      "var result = await facade.SignAsync(input, context.CancellationToken).ConfigureAwait(false);",
    );
    expect(svc.content).not.toContain("handler.HandleAsync");
  });

  it("adds a using for the façade namespace", () => {
    const [svc] = emitSignWithFacade();
    expect(svc.content).toContain(
      "using D2.Edge.Tests.TypeSpecRoute.Generated.Facade;",
    );
  });

  it("XML doc references the façade type", () => {
    const [svc] = emitSignWithFacade();
    expect(svc.content).toContain(
      'delegating to <see cref="ISampleSignerFacade"/>',
    );
  });

  it("envelope population is unchanged (result.ToProtoResponse(), no throw)", () => {
    const [svc] = emitSignWithFacade();
    // Same envelope pattern regardless of delegation target.
    expect(svc.content).toContain("return result.ToProtoResponse();");
    expect(svc.content).not.toContain("throw new RpcException");
    expect(svc.content).not.toContain("if (!result.IsOk)");
  });

  it("transport mapper emits all three extension blocks regardless of delegation target", () => {
    const [, mapper] = emitSignWithFacade();
    // Mapper uses the same proto↔DTO mapping irrespective of who the service delegates to.
    expect(mapper.content).toContain("extension(SignRequest request)");
    expect(mapper.content).toContain("extension(D2Result<SignOutput?> result)");
    expect(mapper.content).toContain("extension(SignOutput output)");
    expect(mapper.content).toContain("internal SignInput ToSignInput()");
    expect(mapper.content).toContain("internal SignResponse ToProtoResponse()");
    expect(mapper.content).toContain(
      "internal ProtoSignOutput ToProtoSignOutput()",
    );
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
      "SampleSigner",
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
    expect(svcDefault.content).toContain(
      "handler.HandleAsync(input, context.CancellationToken)",
    );
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
      "SampleSigner",
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
      "SampleSigner",
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
      "op",
      "Svc",
      "Do",
      PROTO_NS,
      IMPL_NS,
      DTO_NS,
      SOURCE,
      "DoReq",
      "DoResp",
      "DoIn",
      [],
      "DoOut",
      [],
      facadeTarget,
    );
    expect(svc.content).not.toMatch(/Step\s+\d/);
    expect(svc.content).not.toContain("0019");
    expect(svc.content).not.toMatch(/\bR[0-9]\b/);
    expect(svc.content).not.toMatch(/\bF[0-9]\b/);
  });
});

// ---------------------------------------------------------------------------
// Enum request field — the proto-string ↔ DTO-enum bridge (cross-namespace)
// ---------------------------------------------------------------------------

describe("emitGrpcService_EnumRequestField_ParseBridgeAndAlias", () => {
  const KEY_KIND: NestedEnum = {
    name: "KeyKind",
    members: [
      { csName: "Rsa", wireValue: "Rsa", needsEnumMember: false },
      { csName: "Aes", wireValue: "Aes", needsEnumMember: false },
    ],
  };

  function reqWithEnum(): FieldInfo[] {
    return [
      {
        name: "kid",
        csName: "Kid",
        csType: "string",
        tsName: "kid",
        tsType: "string",
        protoType: "string",
        repeated: false,
        optional: false,
        redactReason: undefined,
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
        redactReason: undefined,
        enumRef: KEY_KIND,
      },
    ];
  }

  it("emits the enum alias (cross-namespace DTO) + ToWire/ParseWire helpers + D2Result<Input> request mapper", () => {
    const [svc, mapper] = emitGrpcService(
      "op",
      "Svc",
      "Do",
      PROTO_NS,
      IMPL_NS,
      DTO_NS, // distinct from IMPL_NS → the enum alias IS emitted
      SOURCE,
      "DoReq",
      "DoResp",
      "DoIn",
      reqWithEnum(),
      "DoOut",
      [],
    );

    // Cross-namespace enum alias (the enumAliasUsings non-empty branch).
    expect(mapper.content).toContain(
      `using KeyKind = global::${DTO_NS}.KeyKind;`,
    );
    expect(mapper.content).toContain("using D2.Shared.I18n;");

    // The fail-loud inbound parse helper + the outbound ToWire helper.
    expect(mapper.content).toContain(
      "internal static D2Result<KeyKind> ParseKeyKindWire(string? value)",
    );
    expect(mapper.content).toContain('KeyKind.Rsa => "Rsa",');
    expect(mapper.content).toContain("internal string ToWire()");
    expect(mapper.content).toContain("TK.Common.Errors.VALIDATION_FAILED");

    // The request mapper returns D2Result<DoIn> (parse can fail).
    expect(mapper.content).toContain("internal D2Result<DoIn> ToDoIn()");
    expect(mapper.content).toContain(
      "var keyKindResult = string.ParseKeyKindWire(request.KeyKind);",
    );
    expect(mapper.content).toContain("if (!keyKindResult.Success)");

    // The service checks .Success and short-circuits to the envelope on failure.
    expect(svc.content).toContain("var inputResult = request.ToDoIn();");
    expect(svc.content).toContain("if (!inputResult.Success)");
    expect(svc.content).toContain("using D2.Shared.Result;");
    expect(svc.content).toContain("DoIn input = inputResult.Data!;");
  });

  it("an enum OUTPUT field maps outbound via .ToWire()", () => {
    const [, mapper] = emitGrpcService(
      "op",
      "Svc",
      "Do",
      PROTO_NS,
      IMPL_NS,
      DTO_NS,
      SOURCE,
      "DoReq",
      "DoResp",
      "DoIn",
      [],
      "DoOut",
      [
        {
          name: "kind",
          csName: "Kind",
          csType: "KeyKind",
          tsName: "kind",
          tsType: "KeyKind",
          protoType: "string",
          repeated: false,
          optional: false,
          redactReason: undefined,
          enumRef: KEY_KIND,
        },
      ],
    );

    expect(mapper.content).toContain("Kind = output.Kind.ToWire(),");
  });
});

// ---------------------------------------------------------------------------
// Nested-model + array-of-model — the per-nested-model sub-mapper recursion
// ---------------------------------------------------------------------------

describe("emitGrpcService_NestedModel_SubMapperRecursion", () => {
  const LINE: NestedModel = {
    name: "PlaceOrderLine",
    fields: [makeStringField("status")],
  };
  const CUSTOMER: NestedModel = {
    name: "PlaceOrderV2Customer",
    fields: [makeStringField("tier")],
  };

  function arrayOfModel(name: string, nested: NestedModel): FieldInfo {
    return {
      name,
      csName: name.charAt(0).toUpperCase() + name.slice(1),
      csType: `IReadOnlyList<${nested.name}>`,
      tsName: name,
      tsType: `readonly ${nested.name}[]`,
      protoType: undefined,
      repeated: true,
      optional: false,
      redactReason: undefined,
      nested,
    };
  }

  function nestedField(
    name: string,
    nested: NestedModel,
    optional = false,
  ): FieldInfo {
    return {
      name,
      csName: name.charAt(0).toUpperCase() + name.slice(1),
      csType: optional ? `${nested.name}?` : nested.name,
      tsName: name,
      tsType: nested.name,
      protoType: undefined,
      repeated: false,
      optional,
      redactReason: undefined,
      nested,
    };
  }

  function emitV2() {
    return emitGrpcService(
      "placeOrderV2",
      "OrdersV2",
      "PlaceOrderV2",
      PROTO_NS,
      IMPL_NS,
      DTO_NS,
      SOURCE,
      "PlaceOrderV2Request",
      "PlaceOrderV2Response",
      "PlaceOrderV2Input",
      [makeStringField("customerId")],
      "PlaceOrderV2Output",
      [
        makeStringField("orderCode"),
        arrayOfModel("lines", LINE),
        nestedField("customer", CUSTOMER, true),
      ],
    );
  }

  it("array-of-model output → collection-init `Field = { …Select(ToProto<Model>()) }` (RepeatedField has no setter)", () => {
    const [, mapper] = emitV2();
    expect(mapper.content).toContain(
      "Lines = { output.Lines.Select(x => x.ToProtoPlaceOrderLine()) },",
    );
    // System.Linq is pulled in for the .Select projection.
    expect(mapper.content).toContain("using System.Linq;");
  });

  it("nullable nested-model output → `Field = source is null ? null : source.ToProto<Model>()`", () => {
    const [, mapper] = emitV2();
    expect(mapper.content).toContain(
      "Customer = output.Customer is null ? null : output.Customer.ToProtoPlaceOrderV2Customer(),",
    );
  });

  it("emits a Proto<Model> alias + DTO alias for each nested model", () => {
    const [, mapper] = emitV2();
    expect(mapper.content).toContain(
      `using ProtoPlaceOrderLine = global::${PROTO_NS}.PlaceOrderLine;`,
    );
    expect(mapper.content).toContain(
      `using ProtoPlaceOrderV2Customer = global::${PROTO_NS}.PlaceOrderV2Customer;`,
    );
    expect(mapper.content).toContain(
      `using PlaceOrderLine = global::${DTO_NS}.PlaceOrderLine;`,
    );
  });

  it("emits BOTH directions of a sub-mapper for each nested model", () => {
    const [, mapper] = emitV2();
    // DTO → proto.
    expect(mapper.content).toContain(
      "internal ProtoPlaceOrderLine ToProtoPlaceOrderLine()",
    );
    expect(mapper.content).toContain(
      "internal ProtoPlaceOrderV2Customer ToProtoPlaceOrderV2Customer()",
    );
    // proto → DTO.
    expect(mapper.content).toContain(
      "internal PlaceOrderLine ToPlaceOrderLine()",
    );
    expect(mapper.content).toContain(
      "internal PlaceOrderV2Customer ToPlaceOrderV2Customer()",
    );
  });

  it("a nested REQUEST field recurses inbound too (proto → DTO ctor arg)", () => {
    const [, mapper] = emitGrpcService(
      "op",
      "Svc",
      "Do",
      PROTO_NS,
      IMPL_NS,
      DTO_NS,
      SOURCE,
      "DoReq",
      "DoResp",
      "DoIn",
      [nestedField("customer", CUSTOMER, false)],
      "DoOut",
      [],
    );
    // Non-optional nested request field still uses the null-guard inbound arm.
    expect(mapper.content).toContain(
      "new DoIn(request.Customer is null ? null : request.Customer.ToPlaceOrderV2Customer())",
    );
    // The sub-mapper for the request-side nested model is emitted.
    expect(mapper.content).toContain(
      "internal PlaceOrderV2Customer ToPlaceOrderV2Customer()",
    );
  });

  it("an empty array-of-model output still emits the collection-init (no NRE on empty)", () => {
    // The non-optional array maps without a null guard (the DTO record requires it
    // non-null); an EMPTY list projects to an empty enumerable → empty repeated field.
    const [, mapper] = emitGrpcService(
      "op",
      "Svc",
      "Do",
      PROTO_NS,
      IMPL_NS,
      DTO_NS,
      SOURCE,
      "DoReq",
      "DoResp",
      "DoIn",
      [],
      "DoOut",
      [arrayOfModel("lines", LINE)],
    );
    expect(mapper.content).toContain(
      "Lines = { output.Lines.Select(x => x.ToProtoPlaceOrderLine()) },",
    );
  });

  it("an OPTIONAL array-of-model output null-coalesces to an empty sequence (no NRE on null)", () => {
    const [, mapper] = emitGrpcService(
      "op",
      "Svc",
      "Do",
      PROTO_NS,
      IMPL_NS,
      DTO_NS,
      SOURCE,
      "DoReq",
      "DoResp",
      "DoIn",
      [],
      "DoOut",
      [
        {
          ...arrayOfModel("lines", LINE),
          csType: "IReadOnlyList<PlaceOrderLine>",
          optional: true,
        },
      ],
    );
    expect(mapper.content).toContain(
      "Lines = { (output.Lines ?? []).Select(x => x.ToProtoPlaceOrderLine()) },",
    );
  });

  it("a nested-model field whose model carries a bytes field maps it via global:: ByteString", () => {
    const BLOB: NestedModel = {
      name: "Blob",
      fields: [makeBytesField("data")],
    };
    const [, mapper] = emitGrpcService(
      "op",
      "Svc",
      "Do",
      PROTO_NS,
      IMPL_NS,
      DTO_NS,
      SOURCE,
      "DoReq",
      "DoResp",
      "DoIn",
      [],
      "DoOut",
      [nestedField("blob", BLOB, false)],
    );
    // The sub-mapper's nested bytes arm uses the fully-qualified ByteString call
    // outbound; inbound it is a positional ctor arg (.ToByteArray()).
    expect(mapper.content).toContain(
      "Data = global::Google.Protobuf.ByteString.CopyFrom(source.Data),",
    );
    expect(mapper.content).toContain(
      "return new Blob(source.Data.ToByteArray());",
    );
  });
});

// ---------------------------------------------------------------------------
// Temporal output field — DateTimeOffset DTO → proto ISO-8601 string
// ---------------------------------------------------------------------------

describe("emitGrpcService_TemporalOutputField_MapsViaRoundTripFormat", () => {
  function makeTemporalField(name: string): FieldInfo {
    return {
      name,
      csName: name.charAt(0).toUpperCase() + name.slice(1),
      csType: "DateTimeOffset",
      tsName: name,
      tsType: "string",
      protoType: "string",
      repeated: false,
      optional: false,
      redactReason: undefined,
    };
  }

  it('a DateTimeOffset output field maps outbound via ToString("O") (ISO-8601 round-trip)', () => {
    const [, mapper] = emitGrpcService(
      "issue",
      "SampleIssuer",
      "Issue",
      PROTO_NS,
      IMPL_NS,
      DTO_NS,
      SOURCE,
      "IssueRequest",
      "IssueResponse",
      "IssueInput",
      [makeBytesField("csrDer")],
      "IssueOutput",
      [makeBytesField("certificateDer"), makeTemporalField("notAfter")],
    );
    expect(mapper.content).toContain(
      'NotAfter = output.NotAfter.ToString("O"),',
    );
    // The non-temporal sibling keeps its own arm (no cross-contamination).
    expect(mapper.content).toContain(
      "CertificateDer = ByteString.CopyFrom(output.CertificateDer),",
    );
  });
});
