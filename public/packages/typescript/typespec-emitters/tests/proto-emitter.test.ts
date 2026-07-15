// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Direct-unit tests for the proto3 emitter (emitProto).
//
// Covers: service/rpc/message structure, all four streaming modes,
// snake_case field names, author-pinned field numbers (@d2Field), repeated
// fields, nested messages, unmapped scalars (loud failure), unpinned-field
// loud failure (D2TSP009), reserved line emission (numbers + names,
// range-collapse), proto type column (not the C# column), empty messages,
// and the auto-generated banner.

import { describe, it, expect, vi } from "vitest";
import type { FieldInfo, NestedModel } from "../src/lib/model-walk.js";
import { emitProto } from "../src/lib/proto-emitter.js";
import type { NestedMessageDescriptor } from "../src/lib/proto-emitter.js";
import { WIRE_CHANNEL_GRAMMAR } from "../src/lib/wire-channel.js";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

type OnError = (
  code:
    | "unmapped-scalar"
    | "invalid-streaming-mode"
    | "unpinned-proto-field"
    | "duplicate-field-number",
  message: string,
) => void;

function makeStringField(name: string, fieldNumber?: number): FieldInfo {
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
    fieldNumber,
  };
}

function makeBytesField(name: string, fieldNumber?: number): FieldInfo {
  return {
    name,
    csName: name.charAt(0).toUpperCase() + name.slice(1),
    csType: "byte[]",
    tsName: name,
    tsType: "Uint8Array",
    protoType: "bytes",
    repeated: false,
    optional: false,
    redactReason: undefined,
    fieldNumber,
  };
}

function makeDecimalField(name: string, fieldNumber?: number): FieldInfo {
  return {
    name,
    csName: name.charAt(0).toUpperCase() + name.slice(1),
    csType: "decimal",
    tsName: name,
    tsType: "string",
    protoType: "string",
    repeated: false,
    optional: false,
    redactReason: undefined,
    fieldNumber,
  };
}

function makeCollectionField(
  name: string,
  elemCsType: string,
  elemProtoType: string,
  fieldNumber?: number,
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
    redactReason: undefined,
    fieldNumber,
  };
}

function makeNestedField(
  name: string,
  nested: NestedModel,
  fieldNumber?: number,
): FieldInfo {
  return {
    name,
    csName: name.charAt(0).toUpperCase() + name.slice(1),
    csType: nested.name,
    tsName: name,
    tsType: nested.name,
    protoType: undefined,
    repeated: false,
    optional: false,
    redactReason: undefined,
    nested,
    fieldNumber,
  };
}

function nestedDescriptor(model: NestedModel): NestedMessageDescriptor {
  return { model };
}

const SIGN_SOURCE = "contracts/typespec/fixtures/sign-shaped.tsp";
const SIGN_PKG = "d2.sample.v1";
const SIGN_CS_NS = "D2.Services.Protos.Sample.V1";

function buildSignInputFields(): readonly FieldInfo[] {
  return [makeStringField("kid", 1), makeBytesField("payload", 2)];
}

function buildSignOutputFields(): readonly FieldInfo[] {
  return [makeStringField("signature", 1)];
}

function emitSignProto(streaming = "unary", onErr?: OnError) {
  const errors: string[] = [];
  const onError: OnError = onErr ?? ((_, m) => errors.push(m));
  const result = emitProto(
    "sign",
    "SampleSigner",
    "Sign",
    streaming,
    SIGN_PKG,
    SIGN_CS_NS,
    SIGN_SOURCE,
    "SignRequest",
    buildSignInputFields(),
    undefined,
    "SignOutput", // data message name — wrapper is always <grpcMethod>Response
    buildSignOutputFields(),
    undefined,
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
    expect(result!.content).toContain("service SampleSigner {");
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
    // Request fields (author-pinned: kid=1, payload=2).
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
      undefined,
      "Resp",
      [],
      undefined,
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
      undefined,
      "Resp",
      [],
      undefined,
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
      undefined,
      "Resp",
      [],
      undefined,
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
        redactReason: undefined,
        fieldNumber: 1,
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
      undefined,
      "MyOutput",
      [],
      undefined,
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
// Test 8: author-pinned field numbers are honored (not positional)
// ---------------------------------------------------------------------------

describe("emitProto_FieldNumbering_AuthorPinned", () => {
  it("fields with non-sequential pins → numbers used verbatim", () => {
    const fields: readonly FieldInfo[] = [
      makeStringField("alpha", 1),
      makeBytesField("beta", 5),
      makeStringField("gamma", 10),
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
      undefined,
      "Out",
      [],
      undefined,
      [],
      (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(result!.content).toContain("string alpha = 1;");
    // Pin 5 is used verbatim — no positional counter.
    expect(result!.content).toContain("bytes beta = 5;");
    expect(result!.content).toContain("string gamma = 10;");
  });

  it("three-field message pinned 1-2-3 → same output as legacy positional (byte-neutral)", () => {
    const fields: readonly FieldInfo[] = [
      makeStringField("alpha", 1),
      makeBytesField("beta", 2),
      makeStringField("gamma", 3),
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
      undefined,
      "Out",
      [],
      undefined,
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
// Test 8b: unpinned field → loud D2TSP009 failure
// ---------------------------------------------------------------------------

describe("emitProto_UnpinnedField_LoudFailure", () => {
  it("a field without @d2Field (fieldNumber undefined) → D2TSP009 + returns undefined", () => {
    const unpinned: FieldInfo = {
      name: "kid",
      csName: "Kid",
      csType: "string",
      tsName: "kid",
      tsType: "string",
      protoType: "string",
      repeated: false,
      optional: false,
      redactReason: undefined,
      // fieldNumber intentionally absent — simulates missing @d2Field
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
      [unpinned],
      undefined,
      "Resp",
      [],
      undefined,
      [],
      onError,
    );
    expect(result).toBeUndefined();
    expect(onError).toHaveBeenCalledOnce();
    expect(onError.mock.calls[0]![0]).toBe("unpinned-proto-field");
    expect(onError.mock.calls[0]![1]).toContain("D2TSP009");
    expect(onError.mock.calls[0]![1]).toContain("kid");
  });

  it("unpinned response field → D2TSP009 (emitter checks both request and response)", () => {
    const unpinned: FieldInfo = {
      name: "signature",
      csName: "Signature",
      csType: "string",
      tsName: "signature",
      tsType: "string",
      protoType: "string",
      repeated: false,
      optional: false,
      redactReason: undefined,
      // fieldNumber intentionally absent
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
      [], // empty request — passes
      undefined,
      "Resp",
      [unpinned],
      undefined,
      [],
      onError,
    );
    expect(result).toBeUndefined();
    expect(onError).toHaveBeenCalledOnce();
    expect(onError.mock.calls[0]![0]).toBe("unpinned-proto-field");
  });

  it("pinned fields pass (no unpinned-proto-field error)", () => {
    const { result, errors } = emitSignProto();
    expect(result).toBeDefined();
    expect(errors).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Test 8c: duplicate @d2Field pin → loud D2TSP011 failure
// ---------------------------------------------------------------------------

describe("emitProto_DuplicateFieldNumber_LoudFailure", () => {
  it("two request fields with the same pin → D2TSP011 + returns undefined", () => {
    const fieldA = makeStringField("kid", 1);
    const fieldB = makeBytesField("payload", 1); // same pin as kid
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
      [fieldA, fieldB],
      undefined,
      "Resp",
      [],
      undefined,
      [],
      onError,
    );
    expect(result).toBeUndefined();
    expect(onError).toHaveBeenCalledOnce();
    expect(onError.mock.calls[0]![0]).toBe("duplicate-field-number");
    expect(onError.mock.calls[0]![1]).toContain("D2TSP011");
    expect(onError.mock.calls[0]![1]).toContain("payload");
    expect(onError.mock.calls[0]![1]).toContain("1");
  });

  it("two response fields with the same pin → D2TSP011 + returns undefined", () => {
    const reqField = makeStringField("kid", 1);
    const respA = makeStringField("status", 1);
    const respB = makeStringField("message", 1); // same pin as status
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
      [reqField],
      undefined,
      "Resp",
      [respA, respB],
      undefined,
      [],
      onError,
    );
    expect(result).toBeUndefined();
    expect(onError).toHaveBeenCalledOnce();
    expect(onError.mock.calls[0]![0]).toBe("duplicate-field-number");
    expect(onError.mock.calls[0]![1]).toContain("D2TSP011");
    expect(onError.mock.calls[0]![1]).toContain("message");
  });

  it("same pin used in request AND response is not a collision (different message scopes)", () => {
    // Field numbers are per-message in proto3 — the same number can appear in
    // two distinct messages. Duplicate detection is scoped per resolveProtoFields call.
    const reqField = makeStringField("kid", 1);
    const respField = makeBytesField("signature", 1); // same pin, different message
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
      [reqField],
      undefined,
      "Resp",
      [respField],
      undefined,
      [],
      (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(errors).toHaveLength(0);
  });

  it("three fields with two sharing a pin → D2TSP011 fires on the second duplicate", () => {
    const fieldA = makeStringField("alpha", 1);
    const fieldB = makeStringField("beta", 2);
    const fieldC = makeStringField("gamma", 1); // collides with alpha
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
      [fieldA, fieldB, fieldC],
      undefined,
      "Resp",
      [],
      undefined,
      [],
      onError,
    );
    expect(result).toBeUndefined();
    expect(onError).toHaveBeenCalledOnce();
    expect(onError.mock.calls[0]![0]).toBe("duplicate-field-number");
    expect(onError.mock.calls[0]![1]).toContain("gamma");
  });
});

// ---------------------------------------------------------------------------
// Test 9: `repeated` for collection fields
// ---------------------------------------------------------------------------

describe("emitProto_CollectionField_RepeatedPrefix", () => {
  it("IReadOnlyList<string> field → repeated string tag", () => {
    const fields: readonly FieldInfo[] = [
      makeCollectionField("items", "string", "string", 1),
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
      undefined,
      "Resp",
      [],
      undefined,
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
      fields: [makeStringField("id", 1)],
    };
    const nestedField = makeNestedField("item", nestedModel, 1);

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
      undefined,
      "Resp",
      [],
      undefined,
      [nestedDescriptor(nestedModel)],
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
      redactReason: undefined,
      fieldNumber: 1, // pinned — passes the pin check; fails at scalar resolution
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
      undefined,
      "Resp",
      [],
      undefined,
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
      redactReason: undefined,
      fieldNumber: 1,
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
      undefined,
      "Resp",
      [],
      undefined,
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
      "Generated by the @dcsv-io/d2-typespec-emitters TypeSpec emitter.",
    );
    expect(result!.content).toContain("Manual edits will be lost on rebuild.");
    expect(result!.content).toContain('syntax = "proto3";');
    expect(result!.content).toContain("package d2.sample.v1;");
    expect(result!.content).toContain(
      'option csharp_namespace = "D2.Services.Protos.Sample.V1";',
    );
  });
});

// ---------------------------------------------------------------------------
// Test 15: decimal scalar → proto string (registry proto column, not cs column)
// ---------------------------------------------------------------------------

describe("emitProto_DecimalScalar_MapsToProtoString", () => {
  it("decimal C# type → string in proto (lossless-wire mapping from registry proto column)", () => {
    const fields: readonly FieldInfo[] = [makeDecimalField("amount", 1)];
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
      undefined,
      "Resp",
      [],
      undefined,
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
      redactReason: undefined,
      fieldNumber: 1,
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
      undefined,
      "Resp",
      [],
      undefined,
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
      redactReason: undefined,
      fieldNumber: 1,
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
      undefined,
      "Resp",
      [],
      undefined,
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
      undefined,
      "EmptyOut",
      [],
      undefined,
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
      undefined,
      "Resp",
      [],
      undefined,
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
  it("service SampleSigner + method Sign → sample_signer_sign.g.proto", () => {
    const { result } = emitSignProto();
    expect(result!.fileName).toBe("sample_signer_sign.g.proto");
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
      redactReason: undefined,
      fieldNumber: 1,
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
      undefined,
      "Resp",
      [badRespField],
      undefined,
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
      redactReason: undefined,
      fieldNumber: 1,
    };
    const nestedModel: NestedModel = {
      name: "BadNested",
      fields: [badNestedField],
    };
    const nestedField = makeNestedField("item", nestedModel, 1);
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
      undefined,
      "Resp",
      [],
      undefined,
      [nestedDescriptor(nestedModel)],
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
      fields: [makeStringField("id", 1)],
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
      redactReason: undefined,
      nested: nestedModel,
      fieldNumber: 1,
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
      undefined,
      "Resp",
      [],
      undefined,
      [nestedDescriptor(nestedModel)],
      (_, m) => errors.push(m),
    );
    expect(result).toBeDefined();
    expect(errors).toHaveLength(0);
    expect(result!.content).toContain("repeated Item items = 1;");
    expect(result!.content).toContain("message Item {");
    expect(result!.content).toContain("string id = 1;");
  });
});

// ---------------------------------------------------------------------------
// Enum field → proto `string` field (the cross-language enum wire is a string)
// ---------------------------------------------------------------------------

describe("emitProto_EnumField_EmitsStringField", () => {
  const KEY_KIND = {
    name: "KeyKind",
    members: [
      { csName: "Rsa", wireValue: "Rsa", needsEnumMember: false },
      { csName: "Aes", wireValue: "Aes", needsEnumMember: false },
    ],
  };

  function enumField(
    name: string,
    repeated = false,
    fieldNumber?: number,
  ): FieldInfo {
    return {
      name,
      csName: name.charAt(0).toUpperCase() + name.slice(1),
      csType: repeated ? "IReadOnlyList<KeyKind>" : "KeyKind",
      tsName: name,
      tsType: repeated ? "readonly KeyKind[]" : "KeyKind",
      protoType: "string",
      repeated,
      optional: false,
      redactReason: undefined,
      enumRef: KEY_KIND,
      fieldNumber,
    };
  }

  it("a non-array enum field → `string <name> = N;` (NOT a proto enum decl)", () => {
    const errors: string[] = [];
    const result = emitProto(
      "op",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [makeStringField("kid", 1), enumField("keyKind", false, 2)],
      undefined,
      "Resp",
      [makeStringField("signature", 1)],
      undefined,
      [],
      (_, m) => errors.push(m),
    );

    expect(errors).toHaveLength(0);
    expect(result!.content).toContain("string key_kind = 2;");
    // No proto `enum` declaration + no _UNSPECIFIED sentinel.
    expect(result!.content).not.toContain("enum KeyKind");
    expect(result!.content).not.toContain("UNSPECIFIED");
  });

  it("a repeated enum field → `repeated string <name> = N;`", () => {
    const errors: string[] = [];
    const result = emitProto(
      "op",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [enumField("kinds", true, 1)],
      undefined,
      "Resp",
      [makeStringField("signature", 1)],
      undefined,
      [],
      (_, m) => errors.push(m),
    );

    expect(errors).toHaveLength(0);
    expect(result!.content).toContain("repeated string kinds = 1;");
  });

  it("an enum field with an absent protoType falls back to `string` (defensive `?? string`)", () => {
    const errors: string[] = [];
    const field: FieldInfo = {
      name: "keyKind",
      csName: "KeyKind",
      csType: "KeyKind",
      tsName: "keyKind",
      tsType: "KeyKind",
      // protoType deliberately omitted — the resolver must fall back to "string".
      repeated: false,
      optional: false,
      redactReason: undefined,
      enumRef: KEY_KIND,
      fieldNumber: 1,
    };
    const result = emitProto(
      "op",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [field],
      undefined,
      "Resp",
      [makeStringField("signature", 1)],
      undefined,
      [],
      (_, m) => errors.push(m),
    );

    expect(errors).toHaveLength(0);
    expect(result!.content).toContain("string key_kind = 1;");
  });
});

// ---------------------------------------------------------------------------
// Nested-model wire shape — nullable nested (no `optional`), array-of-model,
// depth-N message emission, two-distinct + dedup.
// ---------------------------------------------------------------------------

describe("emitProto_NestedModel_WireShape", () => {
  it("a nullable nested-model field → a BARE message field (proto3 implicit presence, NO `optional`)", () => {
    const customer: NestedModel = {
      name: "Customer",
      fields: [makeStringField("tier", 1)],
    };
    const field: FieldInfo = {
      name: "customer",
      csName: "Customer",
      csType: "Customer?",
      tsName: "customer",
      tsType: "Customer",
      protoType: undefined,
      repeated: false,
      optional: true,
      redactReason: undefined,
      nested: customer,
      fieldNumber: 1,
    };
    const errors: string[] = [];
    const result = emitProto(
      "op",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [],
      undefined,
      "Out",
      [field],
      undefined,
      [nestedDescriptor(customer)],
      (_, m) => errors.push(m),
    );
    expect(errors).toHaveLength(0);
    // Bare message field — no `optional` keyword (message fields carry implicit presence).
    expect(result!.content).toContain("  Customer customer = 1;");
    expect(result!.content).not.toContain("optional Customer");
    expect(result!.content).toContain("message Customer {");
  });

  it("array-of-model field → `repeated <Message>` + the element message", () => {
    const line: NestedModel = {
      name: "Line",
      fields: [makeStringField("status", 1)],
    };
    const field: FieldInfo = {
      name: "lines",
      csName: "Lines",
      csType: "IReadOnlyList<Line>",
      tsName: "lines",
      tsType: "readonly Line[]",
      protoType: undefined,
      repeated: true,
      optional: false,
      redactReason: undefined,
      nested: line,
      fieldNumber: 1,
    };
    const errors: string[] = [];
    const result = emitProto(
      "op",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [],
      undefined,
      "Out",
      [field],
      undefined,
      [nestedDescriptor(line)],
      (_, m) => errors.push(m),
    );
    expect(errors).toHaveLength(0);
    expect(result!.content).toContain("  repeated Line lines = 1;");
    expect(result!.content).toContain("message Line {");
  });

  it("depth-N: a nested model referencing a deeper model → a message at EVERY level + repeated-inside-nested", () => {
    // The caller passes the FULL transitive nested list (the walker dedups it);
    // the proto emitter emits a message per entry, and a nested-model field INSIDE
    // a nested message resolves to that model's name (incl. `repeated` for arrays).
    const part: NestedModel = {
      name: "Part",
      fields: [makeStringField("code", 1)],
    };
    const widget: NestedModel = {
      name: "Widget",
      fields: [
        makeStringField("name", 1),
        {
          name: "parts",
          csName: "Parts",
          csType: "IReadOnlyList<Part>",
          tsName: "parts",
          tsType: "readonly Part[]",
          protoType: undefined,
          repeated: true,
          optional: false,
          redactReason: undefined,
          nested: part,
          fieldNumber: 2,
        },
      ],
    };
    const outputField: FieldInfo = {
      name: "widget",
      csName: "Widget",
      csType: "Widget?",
      tsName: "widget",
      tsType: "Widget",
      protoType: undefined,
      repeated: false,
      optional: true,
      redactReason: undefined,
      nested: widget,
      fieldNumber: 2,
    };
    const errors: string[] = [];
    const result = emitProto(
      "op",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [],
      undefined,
      "Out",
      [makeStringField("id", 1), outputField],
      undefined,
      [nestedDescriptor(widget), nestedDescriptor(part)],
      (_, m) => errors.push(m),
    );
    expect(errors).toHaveLength(0);
    // Top output references the depth-2 message.
    expect(result!.content).toContain("  Widget widget = 2;");
    // The depth-2 message references the depth-3 message via `repeated`.
    expect(result!.content).toContain("message Widget {");
    expect(result!.content).toContain("  repeated Part parts = 2;");
    // The depth-3 leaf message is emitted.
    expect(result!.content).toContain("message Part {");
    expect(result!.content).toContain("  string code = 1;");
  });

  it("two DISTINCT nested models in one output → both messages emitted", () => {
    const a: NestedModel = { name: "Alpha", fields: [makeStringField("x", 1)] };
    const b: NestedModel = { name: "Beta", fields: [makeStringField("y", 1)] };
    const errors: string[] = [];
    const result = emitProto(
      "op",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [],
      undefined,
      "Out",
      [makeNestedField("alpha", a, 1), makeNestedField("beta", b, 2)],
      undefined,
      [nestedDescriptor(a), nestedDescriptor(b)],
      (_, m) => errors.push(m),
    );
    expect(errors).toHaveLength(0);
    expect(result!.content).toContain("message Alpha {");
    expect(result!.content).toContain("message Beta {");
    expect(result!.content).toContain("  Alpha alpha = 1;");
    expect(result!.content).toContain("  Beta beta = 2;");
  });
});

// ---------------------------------------------------------------------------
// Reserved lines — number emission, range collapse, name emission
// ---------------------------------------------------------------------------

describe("emitProto_Reserved_NumbersAndNames", () => {
  it("reserved single number → `reserved N;` inside the message", () => {
    const errors: string[] = [];
    const result = emitProto(
      "op",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [makeStringField("kid", 1)],
      { numbers: [3], names: [] },
      "Resp",
      [],
      undefined,
      [],
      (_, m) => errors.push(m),
    );
    expect(errors).toHaveLength(0);
    expect(result!.content).toContain("reserved 3;");
  });

  it("reserved consecutive numbers → range-collapsed `reserved N to M;`", () => {
    const errors: string[] = [];
    const result = emitProto(
      "op",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [makeStringField("kid", 1)],
      { numbers: [3, 4, 5], names: [] },
      "Resp",
      [],
      undefined,
      [],
      (_, m) => errors.push(m),
    );
    expect(errors).toHaveLength(0);
    expect(result!.content).toContain("reserved 3 to 5;");
  });

  it("reserved mixed numbers → combined range+singles on one line", () => {
    const errors: string[] = [];
    const result = emitProto(
      "op",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [makeStringField("kid", 1)],
      { numbers: [2, 3, 7], names: [] },
      "Resp",
      [],
      undefined,
      [],
      (_, m) => errors.push(m),
    );
    expect(errors).toHaveLength(0);
    // 2+3 → range; 7 → single.
    expect(result!.content).toContain("reserved 2 to 3, 7;");
  });

  it('reserved names → `reserved "name";` lines', () => {
    const errors: string[] = [];
    const result = emitProto(
      "op",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [makeStringField("kid", 1)],
      { numbers: [], names: ["old_field", "removed_field"] },
      "Resp",
      [],
      undefined,
      [],
      (_, m) => errors.push(m),
    );
    expect(errors).toHaveLength(0);
    expect(result!.content).toContain('reserved "old_field";');
    expect(result!.content).toContain('reserved "removed_field";');
  });

  it("reserved numbers are deduplicated before emission", () => {
    const errors: string[] = [];
    const result = emitProto(
      "op",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [makeStringField("kid", 1)],
      { numbers: [3, 3, 5, 5], names: [] },
      "Resp",
      [],
      undefined,
      [],
      (_, m) => errors.push(m),
    );
    expect(errors).toHaveLength(0);
    // Deduplicated: 3, 5.
    expect(result!.content).toContain("reserved 3, 5;");
    // Only one occurrence.
    const count = (result!.content.match(/reserved 3, 5;/g) ?? []).length;
    expect(count).toBe(1);
  });

  it("reserved names are deduplicated before emission", () => {
    const errors: string[] = [];
    const result = emitProto(
      "op",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [makeStringField("kid", 1)],
      // A repeated name — exercises the seen-set skip arm of buildReservedNameLines.
      { numbers: [], names: ["dropped_field", "dropped_field"] },
      "Resp",
      [],
      undefined,
      [],
      (_, m) => errors.push(m),
    );
    expect(errors).toHaveLength(0);
    expect(result!.content).toContain('reserved "dropped_field";');
    // Only one occurrence — the duplicate was skipped by the dedup guard.
    const count = (result!.content.match(/reserved "dropped_field";/g) ?? [])
      .length;
    expect(count).toBe(1);
  });

  it("response reserved payload is emitted in the data message block", () => {
    const errors: string[] = [];
    const result = emitProto(
      "op",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [],
      undefined,
      "Out",
      [makeStringField("signature", 1)],
      { numbers: [2], names: ["old_sig"] },
      [],
      (_, m) => errors.push(m),
    );
    expect(errors).toHaveLength(0);
    // The reserved lines appear inside the `Out` data message (not in `DoResponse`).
    const outBlock = result!.content.slice(
      result!.content.indexOf("message Out {"),
    );
    expect(outBlock).toContain("reserved 2;");
    expect(outBlock).toContain('reserved "old_sig";');
  });

  it("nested message reserved payload is emitted inside that nested message block", () => {
    const nestedModel: NestedModel = {
      name: "Item",
      fields: [makeStringField("id", 1)],
    };
    const errors: string[] = [];
    const result = emitProto(
      "op",
      "Svc",
      "Do",
      "unary",
      SIGN_PKG,
      SIGN_CS_NS,
      SIGN_SOURCE,
      "Req",
      [],
      undefined,
      "Out",
      [makeNestedField("item", nestedModel, 1)],
      undefined,
      [
        {
          model: nestedModel,
          reserved: { numbers: [4, 5, 6], names: ["removed"] },
        },
      ],
      (_, m) => errors.push(m),
    );
    expect(errors).toHaveLength(0);
    // Reserved lines inside the nested Item message.
    const itemBlock = result!.content.slice(
      result!.content.indexOf("message Item {"),
    );
    expect(itemBlock).toContain("reserved 4 to 6;");
    expect(itemBlock).toContain('reserved "removed";');
  });
});

// ---------------------------------------------------------------------------
// Structural guard: proto package is well-formed.
// Two-part guard: (1) WIRE_CHANNEL_GRAMMAR regex asserts the package SHAPE
// (d2.<svc>.v<N>(alpha|beta)?) — it does NOT structurally exclude v1 because
// v1 is syntactically valid; (2) the identity assertion `expect(SIGN_PKG).
// not.toBe("d2.sample.v1alpha")` is the real guard against accidental alpha
// drift (SIGN_PKG is the pinned v1 stable value for the sample fixture).
// ---------------------------------------------------------------------------

describe("emitProto_ProtoPackage_ChannelGrammar", () => {
  it("SIGN_PKG matches the channel grammar d2.<svc>.v<N>(alpha|beta)?", () => {
    expect(WIRE_CHANNEL_GRAMMAR.test(SIGN_PKG)).toBe(true);
  });

  it("SIGN_PKG is the stable v1 value (not an alpha/beta prerelease)", () => {
    // Non-vacuous: the channel grammar accepts v1alpha too (syntactically valid).
    // The real guard is that SIGN_PKG is the pinned stable value.
    expect(SIGN_PKG).not.toBe("d2.sample.v1alpha");
  });

  it("emitted proto content carries the channel-grammar package", () => {
    const { result } = emitSignProto();
    expect(result).toBeDefined();
    const packageLine = result!.content
      .split("\n")
      .find((l) => l.startsWith("package "));
    expect(packageLine).toBeDefined();
    const pkg = packageLine!.replace("package ", "").replace(";", "").trim();
    expect(WIRE_CHANNEL_GRAMMAR.test(pkg)).toBe(true);
  });

  it("stable v2 package (no alpha/beta) matches channel grammar", () => {
    // Confirms the grammar accepts v2, v3, etc.
    expect(WIRE_CHANNEL_GRAMMAR.test("d2.sample.v2")).toBe(true);
  });

  it("v2beta package matches channel grammar", () => {
    expect(WIRE_CHANNEL_GRAMMAR.test("d2.sample.v2beta")).toBe(true);
  });

  it("malformed packages do NOT match (e.g. uppercase, missing v, extra dots)", () => {
    expect(WIRE_CHANNEL_GRAMMAR.test("D2.sample.v2alpha")).toBe(false);
    expect(WIRE_CHANNEL_GRAMMAR.test("d2.sample.2alpha")).toBe(false);
    expect(WIRE_CHANNEL_GRAMMAR.test("d2.sample.v2.alpha")).toBe(false);
    expect(WIRE_CHANNEL_GRAMMAR.test("d2.Sample.v2alpha")).toBe(false);
    // malformed channel suffixes: gamma is not a valid stability channel (only alpha/beta)
    expect(WIRE_CHANNEL_GRAMMAR.test("d2.sample.v2gamma")).toBe(false);
    // "valpha" is not a valid version segment — must be v<N>(alpha|beta)?
    expect(WIRE_CHANNEL_GRAMMAR.test("d2.sample.valpha")).toBe(false);
  });
});
