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

type OnError = (code: "unmapped-scalar" | "invalid-streaming-mode", message: string) => void;

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

function makeCollectionField(name: string, elemCsType: string, elemProtoType: string): FieldInfo {
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
    "SignResponse",
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
    expect(result!.content).toContain("rpc Sign(SignRequest) returns (SignResponse);");
  });

  it("emits request and response message declarations", () => {
    const { result } = emitSignProto();
    expect(result!.content).toContain("message SignRequest {");
    expect(result!.content).toContain("message SignResponse {");
  });

  it("emits correct field types", () => {
    const { result } = emitSignProto();
    expect(result!.content).toContain("string kid = 1;");
    expect(result!.content).toContain("bytes payload = 2;");
    expect(result!.content).toContain("string signature = 1;");
  });
});

// ---------------------------------------------------------------------------
// Test 3-6: rpc form per streaming mode (4 cases)
// ---------------------------------------------------------------------------

describe("emitProto_StreamingMode_CorrectRpcForm", () => {
  it("unary → rpc M(Req) returns (Resp)", () => {
    const { result } = emitSignProto("unary");
    expect(result!.content).toContain("rpc Sign(SignRequest) returns (SignResponse);");
    expect(result!.content).not.toContain("stream");
  });

  it("serverStream → rpc M(Req) returns (stream Resp)", () => {
    const errors: string[] = [];
    const result = emitProto(
      "sign", "Svc", "Method", "serverStream", SIGN_PKG, SIGN_CS_NS, SIGN_SOURCE,
      "Req", [], "Resp", [], [], (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(result!.content).toContain("rpc Method(Req) returns (stream Resp);");
  });

  it("clientStream → rpc M(stream Req) returns (Resp)", () => {
    const errors: string[] = [];
    const result = emitProto(
      "sign", "Svc", "Method", "clientStream", SIGN_PKG, SIGN_CS_NS, SIGN_SOURCE,
      "Req", [], "Resp", [], [], (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(result!.content).toContain("rpc Method(stream Req) returns (Resp);");
  });

  it("bidiStream → rpc M(stream Req) returns (stream Resp)", () => {
    const errors: string[] = [];
    const result = emitProto(
      "sign", "Svc", "Method", "bidiStream", SIGN_PKG, SIGN_CS_NS, SIGN_SOURCE,
      "Req", [], "Resp", [], [], (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(result!.content).toContain("rpc Method(stream Req) returns (stream Resp);");
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
      "test", "MyService", "DoThing", "unary", SIGN_PKG, SIGN_CS_NS, SIGN_SOURCE,
      "MyInput", fields, "MyOutput", [], [], (_, m) => errors.push(m),
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
      "test", "Svc", "Do", "unary", SIGN_PKG, SIGN_CS_NS, SIGN_SOURCE,
      "Multi", fields, "Out", [], [], (_, m) => errors.push(m),
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
    const fields: readonly FieldInfo[] = [makeCollectionField("items", "string", "string")];
    const errors: string[] = [];
    const result = emitProto(
      "test", "Svc", "Do", "unary", SIGN_PKG, SIGN_CS_NS, SIGN_SOURCE,
      "Req", fields, "Resp", [], [], (_, m) => errors.push(m),
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
      "test", "Svc", "Do", "unary", SIGN_PKG, SIGN_CS_NS, SIGN_SOURCE,
      "Req", [nestedField], "Resp", [], [nestedModel], (_, m) => errors.push(m),
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
      name: "createdAt",
      csName: "CreatedAt",
      csType: "DateTimeOffset", // not in the cs-to-proto map
      tsName: "createdAt",
      tsType: "string",
      protoType: undefined, // no proto column (would fail)
      repeated: false,
      optional: false,
      redact: false,
    };
    const onError = vi.fn();
    const result = emitProto(
      "test", "Svc", "Do", "unary", SIGN_PKG, SIGN_CS_NS, SIGN_SOURCE,
      "Req", [badField], "Resp", [], [], onError,
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
      name: "timestamps",
      csName: "Timestamps",
      csType: "IReadOnlyList<DateTimeOffset>", // not in map
      tsName: "timestamps",
      tsType: "readonly string[]",
      protoType: undefined,
      repeated: true,
      optional: false,
      redact: false,
    };
    const onError = vi.fn();
    const result = emitProto(
      "test", "Svc", "Do", "unary", SIGN_PKG, SIGN_CS_NS, SIGN_SOURCE,
      "Req", [badField], "Resp", [], [], onError,
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
    expect(result!.content).toContain("Generated by the @d2/typespec-emitters TypeSpec emitter.");
    expect(result!.content).toContain("Manual edits will be lost on rebuild.");
    expect(result!.content).toContain("syntax = \"proto3\";");
    expect(result!.content).toContain("package d2.keycustodian.v1;");
    expect(result!.content).toContain("option csharp_namespace = \"D2.Services.Protos.KeyCustodian.V1\";");
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
      "test", "Svc", "Do", "unary", SIGN_PKG, SIGN_CS_NS, SIGN_SOURCE,
      "Req", fields, "Resp", [], [], (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(errors).toHaveLength(0);
    expect(result!.content).toContain("string amount = 1;");
  });
});

// ---------------------------------------------------------------------------
// Test 16: empty message (no fields) → well-formed `message Name {}`
// ---------------------------------------------------------------------------

describe("emitProto_EmptyMessage_WellFormed", () => {
  it("op with no input fields → message Name {} (well-formed empty message)", () => {
    const errors: string[] = [];
    const result = emitProto(
      "test", "Svc", "Do", "unary", SIGN_PKG, SIGN_CS_NS, SIGN_SOURCE,
      "EmptyIn", [], "EmptyOut", [], [], (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(errors).toHaveLength(0);
    expect(result!.content).toContain("message EmptyIn {}");
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
      "test", "Svc", "Do", "bidirectional" /* not a valid mode */, SIGN_PKG, SIGN_CS_NS, SIGN_SOURCE,
      "Req", [], "Resp", [], [], onError,
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
      name: "when",
      csName: "When",
      csType: "DateTimeOffset", // not in the cs-to-proto map
      tsName: "when",
      tsType: "string",
      protoType: undefined,
      repeated: false,
      optional: false,
      redact: false,
    };
    const onError = vi.fn();
    const result = emitProto(
      "test", "Svc", "Do", "unary", SIGN_PKG, SIGN_CS_NS, SIGN_SOURCE,
      "Req", [], "Resp", [badRespField], [], onError,
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
      name: "when",
      csName: "When",
      csType: "DateTimeOffset",
      tsName: "when",
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
      "test", "Svc", "Do", "unary", SIGN_PKG, SIGN_CS_NS, SIGN_SOURCE,
      "Req", [nestedField], "Resp", [], [nestedModel], onError,
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
      "test", "Svc", "Do", "unary", SIGN_PKG, SIGN_CS_NS, SIGN_SOURCE,
      "Req", [collectionField], "Resp", [], [nestedModel], (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(errors).toHaveLength(0);
    expect(result!.content).toContain("repeated Item items = 1;");
    expect(result!.content).toContain("message Item {");
    expect(result!.content).toContain("string id = 1;");
  });
});
