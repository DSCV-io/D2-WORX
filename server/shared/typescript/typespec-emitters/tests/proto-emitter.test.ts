// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Direct-unit tests for the proto3 emitter (emitProto).
//
// Covers: service/rpc/message structure, all four streaming modes,
// snake_case field names, field numbering, repeated fields, nested
// messages, unmapped scalars (loud failure), proto type column
// (not the C# column), empty messages, and the auto-generated banner.

import { describe, it, expect, vi } from "vitest";
import type { FieldInfo, NestedModel } from "../src/lib/model-walk.js";
import { emitProto } from "../src/lib/proto-emitter.js";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

type OnError = (
  code: "unmapped-scalar" | "invalid-streaming-mode",
  message: string,
) => void;

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
    redact: false,
  };
}

function makeDecimalField(name: string): FieldInfo {
  return {
    name,
    csName: name.charAt(0).toUpperCase() + name.slice(1),
    csType: "decimal",
    tsName: name,
    tsType: "string",
    protoType: "string",
    repeated: false,
    optional: false,
    redact: false,
  };
}

function makeCollectionField(
  name: string,
  elemCsType: string,
  elemProtoType: string,
): FieldInfo {
  return {
    name,
    csName: name.charAt(0).toUpperCase() + name.slice(1),
    csType: `IReadOnlyList<${elemCsType}>`,
    tsName: name,
    tsType: `readonly ${elemCsType}[]`,
    protoType: elemProtoType,
    repeated: true,
    optional: false,
    redact: false,
  };
}

function makeNestedField(name: string, nested: NestedModel): FieldInfo {
  return {
    name,
    csName: name.charAt(0).toUpperCase() + name.slice(1),
    csType: nested.name,
    tsName: name,
    tsType: nested.name,
    protoType: undefined,
    repeated: false,
    optional: false,
    redact: false,
    nested,
  };
}

const SIGN_SOURCE = "contracts/typespec/fixtures/sign-shaped.tsp";
const SIGN_PKG = "d2.keycustodian.v1";
const SIGN_CS_NS = "D2.Services.Protos.KeyCustodian.V1";

function buildSignInputFields(): readonly FieldInfo[] {
  return [makeStringField("kid"), makeBytesField("payload")];
}

function buildSignOutputFields(): readonly FieldInfo[] {
  return [makeStringField("signature")];
}

function emitSignProto(streaming = "unary", onErr?: OnError) {
  const errors: string[] = [];
  const onError: OnError = onErr ?? ((_, m) => errors.push(m));
  const result = emitProto(
    "sign",
    "KeyCustodianSigner",
    "Sign",
    streaming,
    SIGN_PKG,
    SIGN_CS_NS,
    SIGN_SOURCE,
    "SignRequest",
    buildSignInputFields(),
    "SignOutput", // data message name — wrapper is always <grpcMethod>Response
    buildSignOutputFields(),
    [],
    onError,
  );
  return { result, errors };
}

// ---------------------------------------------------------------------------
// Tests 1-2: sign shape → service / rpc / message structure
// ---------------------------------------------------------------------------

describe("emitProto_SignShape_EmitsServiceRpcMessages", () => {
  it("emits proto service declaration", () => {
    const { result } = emitSignProto();
    expect(result).toBeDefined();
    expect(result!.content).toContain("service KeyCustodianSigner {");
    expect(result!.content).toContain(
      "rpc Sign(SignRequest) returns (SignResponse);",
    );
  });

  it("emits request message, envelope wrapper, and data message declarations", () => {
    const { result } = emitSignProto();
    expect(result!.content).toContain("message SignRequest {");
    // Envelope wrapper carries D2ResultProto (field 1) + typed data (field 2).
    expect(result!.content).toContain("message SignResponse {");
    // Separate data message carries the DTO fields.
    expect(result!.content).toContain("message SignOutput {");
  });

  it("emits the D2ResultProto import after syntax", () => {
    const { result } = emitSignProto();
    expect(result!.content).toContain('import "common/v1/d2_result.proto";');
  });

  it("emits correct field types: request fields + envelope fields + data message fields", () => {
    const { result } = emitSignProto();
    // Request fields.
    expect(result!.content).toContain("string kid = 1;");
    expect(result!.content).toContain("bytes payload = 2;");
    // Envelope wrapper fields.
    expect(result!.content).toContain("d2.common.v1.D2ResultProto result = 1;");
    expect(result!.content).toContain("SignOutput data = 2;");
    // Data message field (signature lives in SignOutput, not SignResponse).
    expect(result!.content).toContain("string signature = 1;");
  });
});

// ---------------------------------------------------------------------------
// Test 3-6: rpc form per streaming mode (4 cases)
// ---------------------------------------------------------------------------

describe("emitProto_StreamingMode_CorrectRpcForm", () => {
  it("unary → rpc M(Req) returns (Resp)", () => {
    const { result } = emitSignProto("unary");
    expect(result!.content).toContain(
      "rpc Sign(SignRequest) returns (SignResponse);",
    );
    expect(result!.content).not.toContain("stream");
  });

  it("serverStream → rpc M(Req) returns (stream MethodResponse)", () => {
    const errors: string[] = [];
    const result = emitProto(
      "sign",
      "Svc",
      "Method",
      "serverStream",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [],
      "Resp",
      [],
      [],
      (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    // The response wrapper is always <grpcMethod>Response ("MethodResponse"), not the data model name.
    expect(result!.content).toContain(
      "rpc Method(Req) returns (stream MethodResponse);",
    );
  });

  it("clientStream → rpc M(stream Req) returns (MethodResponse)", () => {
    const errors: string[] = [];
    const result = emitProto(
      "sign",
      "Svc",
      "Method",
      "clientStream",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [],
      "Resp",
      [],
      [],
      (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(result!.content).toContain(
      "rpc Method(stream Req) returns (MethodResponse);",
    );
  });

  it("bidiStream → rpc M(stream Req) returns (stream MethodResponse)", () => {
    const errors: string[] = [];
    const result = emitProto(
      "sign",
      "Svc",
      "Method",
      "bidiStream",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [],
      "Resp",
      [],
      [],
      (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(result!.content).toContain(
      "rpc Method(stream Req) returns (stream MethodResponse);",
    );
  });
});

// ---------------------------------------------------------------------------
// Test 7: snake_case field names + PascalCase message names
// ---------------------------------------------------------------------------

describe("emitProto_FieldNaming_SnakeCaseFields_PascalCaseMessages", () => {
  it("multiWordField → multi_word_field in proto, PascalCase message name preserved", () => {
    const fields: readonly FieldInfo[] = [
      {
        name: "multiWordField",
        csName: "MultiWordField",
        csType: "string",
        tsName: "multiWordField",
        tsType: "string",
        protoType: "string",
        repeated: false,
        optional: false,
        redact: false,
      },
    ];
    const errors: string[] = [];
    const result = emitProto(
      "test",
      "MyService",
      "DoThing",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "MyInput",
      fields,
      "MyOutput",
      [],
      [],
      (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(result!.content).toContain("string multi_word_field = 1;");
    expect(result!.content).toContain("message MyInput {");
    expect(result!.content).toContain("message MyOutput {}");
  });
});

// ---------------------------------------------------------------------------
// Test 8: field numbering in declaration order
// ---------------------------------------------------------------------------

describe("emitProto_FieldNumbering_OneBased_InDeclarationOrder", () => {
  it("three-field message → fields numbered 1, 2, 3 in order", () => {
    const fields: readonly FieldInfo[] = [
      makeStringField("alpha"),
      makeBytesField("beta"),
      makeStringField("gamma"),
    ];
    const errors: string[] = [];
    const result = emitProto(
      "test",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Multi",
      fields,
      "Out",
      [],
      [],
      (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(result!.content).toContain("string alpha = 1;");
    expect(result!.content).toContain("bytes beta = 2;");
    expect(result!.content).toContain("string gamma = 3;");
  });
});

// ---------------------------------------------------------------------------
// Test 9: `repeated` for collection fields
// ---------------------------------------------------------------------------

describe("emitProto_CollectionField_RepeatedPrefix", () => {
  it("IReadOnlyList<string> field → repeated string tag", () => {
    const fields: readonly FieldInfo[] = [
      makeCollectionField("items", "string", "string"),
    ];
    const errors: string[] = [];
    const result = emitProto(
      "test",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      fields,
      "Resp",
      [],
      [],
      (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(result!.content).toContain("repeated string items = 1;");
  });
});

// ---------------------------------------------------------------------------
// Test 10-11: nested message dedup + field type uses PascalCase model name
// ---------------------------------------------------------------------------

describe("emitProto_NestedMessage_EmittedAndFieldUsesModelName", () => {
  it("nested model → nested message emitted, field type is PascalCase model name", () => {
    const nestedModel: NestedModel = {
      name: "Item",
      fields: [makeStringField("id")],
    };
    const nestedField = makeNestedField("item", nestedModel);

    const errors: string[] = [];
    const result = emitProto(
      "test",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [nestedField],
      "Resp",
      [],
      [nestedModel],
      (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    // Field in message uses PascalCase model name as proto type.
    expect(result!.content).toContain("Item item = 1;");
    // Nested message itself is emitted.
    expect(result!.content).toContain("message Item {");
    expect(result!.content).toContain("string id = 1;");
  });
});

// ---------------------------------------------------------------------------
// Test 12: unmapped scalar → loud failure, returns undefined (§1.29)
// ---------------------------------------------------------------------------

describe("emitProto_UnmappedScalar_LoudFailure", () => {
  it("field with unmapped C# type → onError called + returns undefined", () => {
    const badField: FieldInfo = {
      name: "correlationId",
      csName: "CorrelationId",
      csType: "Guid", // not in the cs-to-proto map (a genuinely-unmapped C# type)
      tsName: "correlationId",
      tsType: "string",
      protoType: undefined, // no proto column (would fail)
      repeated: false,
      optional: false,
      redact: false,
    };
    const onError = vi.fn();
    const result = emitProto(
      "test",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [badField],
      "Resp",
      [],
      [],
      onError,
    );
    expect(result).toBeUndefined();
    expect(onError).toHaveBeenCalledOnce();
    expect(onError.mock.calls[0]![0]).toBe("unmapped-scalar");
    expect(onError.mock.calls[0]![1]).toContain("D2TSP001");
  });
});

// ---------------------------------------------------------------------------
// Test 13: unmapped array-element scalar → loud failure
// ---------------------------------------------------------------------------

describe("emitProto_UnmappedArrayElementScalar_LoudFailure", () => {
  it("repeated field with unmapped element type → onError called + returns undefined", () => {
    const badField: FieldInfo = {
      name: "ids",
      csName: "Ids",
      csType: "IReadOnlyList<Guid>", // not in map (genuinely-unmapped element type)
      tsName: "ids",
      tsType: "readonly string[]",
      protoType: undefined,
      repeated: true,
      optional: false,
      redact: false,
    };
    const onError = vi.fn();
    const result = emitProto(
      "test",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [badField],
      "Resp",
      [],
      [],
      onError,
    );
    expect(result).toBeUndefined();
    expect(onError).toHaveBeenCalledOnce();
    expect(onError.mock.calls[0]![0]).toBe("unmapped-scalar");
  });
});

// ---------------------------------------------------------------------------
// Test 14: banner + syntax + package + csharp_namespace
// ---------------------------------------------------------------------------

describe("emitProto_Banner_SyntaxPackageNamespace", () => {
  it("emits auto-generated banner + syntax + package + option csharp_namespace", () => {
    const { result } = emitSignProto();
    expect(result!.content).toContain("// <auto-generated>");
    expect(result!.content).toContain(
      "Generated by the @d2/typespec-emitters TypeSpec emitter.",
    );
    expect(result!.content).toContain("Manual edits will be lost on rebuild.");
    expect(result!.content).toContain('syntax = "proto3";');
    expect(result!.content).toContain("package d2.keycustodian.v1;");
    expect(result!.content).toContain(
      'option csharp_namespace = "D2.Services.Protos.KeyCustodian.V1";',
    );
  });
});

// ---------------------------------------------------------------------------
// Test 15: decimal scalar → proto string (registry proto column, not cs column)
// ---------------------------------------------------------------------------

describe("emitProto_DecimalScalar_MapsToProtoString", () => {
  it("decimal C# type → string in proto (lossless-wire mapping from registry proto column)", () => {
    const fields: readonly FieldInfo[] = [makeDecimalField("amount")];
    const errors: string[] = [];
    const result = emitProto(
      "test",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      fields,
      "Resp",
      [],
      [],
      (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(errors).toHaveLength(0);
    expect(result!.content).toContain("string amount = 1;");
  });
});

// ---------------------------------------------------------------------------
// Test 15b: temporal C# type (DateTimeOffset) → proto string via reverse-map
// ---------------------------------------------------------------------------

describe("emitProto_DateTimeOffset_MapsToProtoString", () => {
  it("DateTimeOffset (required) → string in proto (utcDateTime / offsetDateTime wire)", () => {
    const field: FieldInfo = {
      name: "createdAt",
      csName: "CreatedAt",
      csType: "DateTimeOffset",
      tsName: "createdAt",
      tsType: "string",
      protoType: "string",
      repeated: false,
      optional: false,
      redact: false,
    };
    const errors: string[] = [];
    const result = emitProto(
      "test",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [field],
      "Resp",
      [],
      [],
      (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(errors).toHaveLength(0);
    expect(result!.content).toContain("string created_at = 1;");
  });

  it("DateTimeOffset? (optional) → string in proto (the ?-strip covers optional)", () => {
    const field: FieldInfo = {
      name: "nextFireUtc",
      csName: "NextFireUtc",
      csType: "DateTimeOffset?",
      tsName: "nextFireUtc",
      tsType: "string",
      protoType: "string",
      repeated: false,
      optional: true,
      redact: false,
    };
    const errors: string[] = [];
    const result = emitProto(
      "test",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [field],
      "Resp",
      [],
      [],
      (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(errors).toHaveLength(0);
    expect(result!.content).toContain("string next_fire_utc = 1;");
  });
});

// ---------------------------------------------------------------------------
// Test 16: empty message (no fields) → well-formed `message Name {}`
// ---------------------------------------------------------------------------

describe("emitProto_EmptyMessage_WellFormed", () => {
  it("op with no input fields → request is well-formed empty; data message is well-formed empty", () => {
    const errors: string[] = [];
    const result = emitProto(
      "test",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "EmptyIn",
      [],
      "EmptyOut",
      [],
      [],
      (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(errors).toHaveLength(0);
    // Request message: empty → well-formed `message EmptyIn {}`.
    expect(result!.content).toContain("message EmptyIn {}");
    // The envelope wrapper is always `message DoResponse { ... }` (not EmptyOut).
    expect(result!.content).toContain("message DoResponse {");
    expect(result!.content).toContain("d2.common.v1.D2ResultProto result = 1;");
    expect(result!.content).toContain("EmptyOut data = 2;");
    // The data message with no DTO fields emits as a well-formed empty message.
    expect(result!.content).toContain("message EmptyOut {}");
  });
});

// ---------------------------------------------------------------------------
// Test 17: unknown streaming mode → loud failure, returns undefined
// ---------------------------------------------------------------------------

describe("emitProto_UnknownStreamingMode_LoudFailure", () => {
  it("unknown streaming mode → onError called + returns undefined", () => {
    const onError = vi.fn();
    const result = emitProto(
      "test",
      "Svc",
      "Do",
      "bidirectional" /* not a valid mode */,
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [],
      "Resp",
      [],
      [],
      onError,
    );
    expect(result).toBeUndefined();
    expect(onError).toHaveBeenCalledOnce();
    expect(onError.mock.calls[0]![0]).toBe("invalid-streaming-mode");
    expect(onError.mock.calls[0]![1]).toContain("D2TSP003");
  });
});

// ---------------------------------------------------------------------------
// Test 18: file name derived from service + method
// ---------------------------------------------------------------------------

describe("emitProto_FileName_DerivedFromServiceMethod", () => {
  it("service KeyCustodianSigner + method Sign → key_custodian_signer_sign.g.proto", () => {
    const { result } = emitSignProto();
    expect(result!.fileName).toBe("key_custodian_signer_sign.g.proto");
  });
});

// ---------------------------------------------------------------------------
// Test 19: unmapped response field → early return on response walk (covers line 87 branch)
// ---------------------------------------------------------------------------

describe("emitProto_UnmappedResponseField_EarlyReturn", () => {
  it("response model with unmapped C# type → onError called + returns undefined", () => {
    const badRespField: FieldInfo = {
      name: "owner",
      csName: "Owner",
      csType: "Guid", // not in the cs-to-proto map (genuinely-unmapped C# type)
      tsName: "owner",
      tsType: "string",
      protoType: undefined,
      repeated: false,
      optional: false,
      redact: false,
    };
    const onError = vi.fn();
    const result = emitProto(
      "test",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [],
      "Resp",
      [badRespField],
      [],
      onError,
    );
    expect(result).toBeUndefined();
    expect(onError).toHaveBeenCalledOnce();
    expect(onError.mock.calls[0]![0]).toBe("unmapped-scalar");
  });
});

// ---------------------------------------------------------------------------
// Test 20: nested model with unmapped field → early return (covers line 93 branch)
// ---------------------------------------------------------------------------

describe("emitProto_NestedModel_UnmappedField_EarlyReturn", () => {
  it("nested model containing unmapped scalar → onError called + returns undefined", () => {
    const badNestedField: FieldInfo = {
      name: "owner",
      csName: "Owner",
      csType: "Guid", // genuinely-unmapped C# type
      tsName: "owner",
      tsType: "string",
      protoType: undefined,
      repeated: false,
      optional: false,
      redact: false,
    };
    const nestedModel: NestedModel = {
      name: "BadNested",
      fields: [badNestedField],
    };
    const nestedField = makeNestedField("item", nestedModel);
    const onError = vi.fn();
    const result = emitProto(
      "test",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [nestedField],
      "Resp",
      [],
      [nestedModel],
      onError,
    );
    expect(result).toBeUndefined();
    expect(onError).toHaveBeenCalledOnce();
    expect(onError.mock.calls[0]![0]).toBe("unmapped-scalar");
  });
});

// ---------------------------------------------------------------------------
// Test 21: model-typed collection field → repeated with model name as proto type (line 216 true branch)
// ---------------------------------------------------------------------------

describe("emitProto_ModelTypedCollection_RepeatedWithModelName", () => {
  it("IReadOnlyList<Item> field with nested set → repeated Item tag", () => {
    const nestedModel: NestedModel = {
      name: "Item",
      fields: [makeStringField("id")],
    };
    const collectionField: FieldInfo = {
      name: "items",
      csName: "Items",
      csType: "IReadOnlyList<Item>",
      tsName: "items",
      tsType: "readonly Item[]",
      protoType: undefined, // model type — no scalar proto type
      repeated: true,
      optional: false,
      redact: false,
      nested: nestedModel,
    };
    const errors: string[] = [];
    const result = emitProto(
      "test",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [collectionField],
      "Resp",
      [],
      [nestedModel],
      (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(errors).toHaveLength(0);
    expect(result!.content).toContain("repeated Item items = 1;");
    expect(result!.content).toContain("message Item {");
    expect(result!.content).toContain("string id = 1;");
  });
});
