// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// proto3 emitter — pure string-template emission of a `.proto` file.
//
// For each operation decorated with `@d2GrpcMethod`, emits a proto3 file:
//   <Service>_<method>.g.proto   — proto3 service + rpc + message declarations
//
// Conventions:
//   - syntax = "proto3" with package and csharp_namespace from tspconfig options.
//   - service name from @d2GrpcMethod `service` arg; rpc name from `method` arg.
//   - Message names = TypeSpec model names verbatim (e.g. SignInput, SignOutput).
//   - Field names are lower_snake_case; field numbers assigned 1-based in order.
//   - `repeated` for collection fields (IReadOnlyList<T> / readonly T[] in C#/TS).
//   - Enum / string-literal-union field → a proto `string` field carrying the
//     member-name wire string (the cross-language enum wire is always a string;
//     NO proto `enum` declaration, NO `_UNSPECIFIED` sentinel). The proto↔DTO
//     transport mapper parses the string back to the C# enum, failing loud on an
//     unknown value (matching the JSON deserialization policy). A repeated enum
//     field is `repeated string`.
//   - Auto-generated banner (not the copyright header) per §26.5.
//   - Unmapped scalar → D2TSP001 loud failure; returns undefined (no partial file).
//   - Unknown streaming mode → loud failure (belt-and-suspenders for stale state maps).
//   - No phase/step/deliverable/audit-round identifiers in emitted content.
//   - Response message carries the D2ResultProto envelope: field 1 is the result
//     envelope (d2.common.v1.D2ResultProto), field 2 is the typed output data
//     (<Op>Output). The import "common/v1/d2_result.proto" is emitted after syntax.
//   - Request message is UNCHANGED (no envelope on requests).
//   - The <Op>Output data message is emitted as a distinct message block; its fields
//     are the response DTO fields. The Response wrapper has only the two envelope fields.

import { buildBanner } from "./banner.js";
import { toSnake } from "./name-transforms.js";
import type { FieldInfo, NestedModel } from "./model-walk.js";
import type { EmittedFile } from "./csharp-dto-emitter.js";

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/** Descriptor for one proto field (derived from FieldInfo by the emitter). */
export interface ProtoFieldInfo {
  /** lower_snake_case proto field name. */
  readonly protoName: string;
  /** proto3 type string (e.g. "string", "bytes", "int32"). */
  readonly protoType: string;
  /** 1-based field number (assigned by the emitter in declaration order). */
  readonly fieldNumber: number;
  /** True when the field is a collection (emits `repeated` prefix). */
  readonly repeated: boolean;
}

/** Streaming mode values accepted by @d2GrpcMethod. */
export type StreamingMode =
  | "unary"
  | "serverStream"
  | "clientStream"
  | "bidiStream";

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Emit a proto3 `.proto` file for one gRPC operation. Pure function — no I/O.
 *
 * The Response message carries the D2ResultProto envelope:
 *   `{ d2.common.v1.D2ResultProto result = 1; <responseModelName> data = 2; }`
 * The `<responseModelName>` data message is emitted as a separate message block
 * with the response DTO fields. The Request message is UNCHANGED.
 *
 * @param opName            - lowerCamelCase op name (for banner context only).
 * @param grpcService       - gRPC service name from @d2GrpcMethod (e.g. "KeyCustodianSigner").
 * @param grpcMethod        - gRPC method name from @d2GrpcMethod (e.g. "Sign").
 * @param streaming         - Streaming mode from @d2GrpcMethod.
 * @param protoPackage      - Proto3 package string (e.g. "d2.keycustodian.v1").
 * @param protoCsharpNs     - C# namespace option (e.g. "D2.Services.Protos.KeyCustodian.V1").
 * @param sourceSpec        - Relative spec path for the banner.
 * @param requestModelName  - Proto message name for the request (e.g. "SignRequest").
 * @param requestFields     - Resolved field list for the request model.
 * @param responseModelName - TypeSpec model name for the response DTO (e.g. "SignOutput").
 *                            This name is used for both the Response wrapper message name
 *                            suffix AND the data message name. The Response wrapper message
 *                            is always named `<grpcMethod>Response`; the data message is
 *                            named `responseModelName` (e.g. "SignOutput").
 * @param responseFields    - Resolved field list for the response DTO (the data message fields).
 * @param nestedMessages    - Distinct nested models from request/response walks.
 * @param onError           - Callback for D2TSP001 (unmapped scalar) / streaming errors.
 * @returns EmittedFile on success, undefined when a scalar is unmapped.
 */
export function emitProto(
  _opName: string,
  grpcService: string,
  grpcMethod: string,
  streaming: string,
  protoPackage: string,
  protoCsharpNs: string,
  sourceSpec: string,
  requestModelName: string,
  requestFields: readonly FieldInfo[],
  responseModelName: string,
  responseFields: readonly FieldInfo[],
  nestedMessages: readonly NestedModel[],
  onError: (
    code: "unmapped-scalar" | "invalid-streaming-mode",
    message: string,
  ) => void,
): EmittedFile | undefined {
  // Resolve proto fields for request + response data message.
  const reqProtoFields = resolveProtoFields(requestFields, onError);
  if (reqProtoFields === undefined) return undefined;

  const respDataProtoFields = resolveProtoFields(responseFields, onError);
  if (respDataProtoFields === undefined) return undefined;

  // Resolve nested message proto fields.
  const resolvedNested: Array<{
    name: string;
    fields: readonly ProtoFieldInfo[];
  }> = [];
  for (const nm of nestedMessages) {
    const nestedFields = resolveProtoFields(nm.fields, onError);
    if (nestedFields === undefined) return undefined;
    resolvedNested.push({ name: nm.name, fields: nestedFields });
  }

  // The rpc declaration uses <grpcMethod>Response as the response message name.
  const protoResponseMsgName = `${grpcMethod}Response`;

  // Build the rpc declaration based on streaming mode.
  const rpcLine = buildRpcLine(
    grpcMethod,
    streaming,
    requestModelName,
    protoResponseMsgName,
    onError,
  );
  if (rpcLine === undefined) return undefined;

  // buildBanner returns a string ending with "\n". Use as-is to preserve the
  // blank separator line between the auto-generated block and the file body.
  const banner = buildBanner(sourceSpec);
  const lines: string[] = [];

  // Prepend banner then the first content line — banner already ends with "\n".
  lines.push(banner + 'syntax = "proto3";');
  lines.push("");
  // Import the common D2ResultProto envelope definition. The import path mirrors
  // the layout in contracts/protos/ (e.g. jobs.proto imports "common/v1/d2_result.proto").
  lines.push('import "common/v1/d2_result.proto";');
  lines.push("");
  lines.push(`package ${protoPackage};`);
  lines.push("");
  lines.push(`option csharp_namespace = "${protoCsharpNs}";`);
  lines.push("");

  // Service declaration.
  lines.push(`service ${grpcService} {`);
  lines.push(`  ${rpcLine}`);
  lines.push("}");

  // Emit request message (UNCHANGED — no envelope on requests).
  lines.push("");
  lines.push(emitMessage(requestModelName, reqProtoFields));

  // Emit the Response envelope wrapper. The response carries:
  //   field 1: d2.common.v1.D2ResultProto result  — the D2Result envelope
  //   field 2: <responseModelName> data            — the typed output payload
  // Success AND failure both ride the envelope; gRPC status stays OK for business results.
  lines.push("");
  lines.push(
    emitResponseEnvelopeMessage(protoResponseMsgName, responseModelName),
  );

  // Emit the response data message (the DTO fields).
  lines.push("");
  lines.push(emitMessage(responseModelName, respDataProtoFields));

  // Emit nested messages.
  for (const nm of resolvedNested) {
    lines.push("");
    lines.push(emitMessage(nm.name, nm.fields));
  }

  lines.push("");

  // Build file name: snake-case service + method.
  const fileName = `${toSnake(grpcService)}_${toSnake(grpcMethod)}.g.proto`;
  return { fileName, content: lines.join("\n") };
}

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

/**
 * Build the `rpc M(Req) returns (Resp);` line for the given streaming mode.
 *
 * Streaming mode → rpc form:
 *   unary        → rpc M(Req) returns (Resp);
 *   serverStream → rpc M(Req) returns (stream Resp);
 *   clientStream → rpc M(stream Req) returns (Resp);
 *   bidiStream   → rpc M(stream Req) returns (stream Resp);
 */
function buildRpcLine(
  method: string,
  streaming: string,
  reqName: string,
  respName: string,
  onError: (
    code: "unmapped-scalar" | "invalid-streaming-mode",
    message: string,
  ) => void,
): string | undefined {
  switch (streaming) {
    case "unary":
      return `rpc ${method}(${reqName}) returns (${respName});`;
    case "serverStream":
      return `rpc ${method}(${reqName}) returns (stream ${respName});`;
    case "clientStream":
      return `rpc ${method}(stream ${reqName}) returns (${respName});`;
    case "bidiStream":
      return `rpc ${method}(stream ${reqName}) returns (stream ${respName});`;
    default:
      onError(
        "invalid-streaming-mode",
        `D2TSP003: unknown streaming mode '${streaming}' on gRPC method '${method}' — expected unary | serverStream | clientStream | bidiStream`,
      );
      return undefined;
  }
}

/**
 * Resolve FieldInfo[] → ProtoFieldInfo[], assigning 1-based field numbers.
 * Returns undefined when any field's proto type cannot be resolved (onError already called).
 */
function resolveProtoFields(
  fields: readonly FieldInfo[],
  onError: (
    code: "unmapped-scalar" | "invalid-streaming-mode",
    message: string,
  ) => void,
): readonly ProtoFieldInfo[] | undefined {
  const result: ProtoFieldInfo[] = [];
  let fieldNumber = 1;

  for (const f of fields) {
    const resolved = resolveOneField(f, fieldNumber, onError);
    if (resolved === undefined) return undefined;
    result.push(resolved);
    fieldNumber++;
  }

  return result;
}

/**
 * Resolve one FieldInfo to a ProtoFieldInfo.
 * Collection fields (csType starts with IReadOnlyList<>) → repeated.
 * Nested model fields → use the model name as proto type (PascalCase).
 */
function resolveOneField(
  f: FieldInfo,
  fieldNumber: number,
  onError: (
    code: "unmapped-scalar" | "invalid-streaming-mode",
    message: string,
  ) => void,
): ProtoFieldInfo | undefined {
  const protoName = toSnake(f.name);

  // Enum / string-literal-union field → a proto `string` field (the member-name
  // wire string). Handled before the csType-based scalar resolution because the
  // field's csType is the enum NAME (e.g. "KeyKind"), not a registry scalar. A
  // repeated enum field is `repeated string`. f.protoType is "string" (set by
  // the walker for enum fields).
  if (f.enumRef !== undefined)
    return {
      protoName,
      protoType: f.protoType ?? "string",
      fieldNumber,
      repeated: f.repeated,
    };

  // Collection field: csType = "IReadOnlyList<T>" → repeated T
  if (f.csType.startsWith("IReadOnlyList<")) {
    const elemCsType = f.csType.slice("IReadOnlyList<".length, -1);
    // Model-typed collection: f.nested is set (walkModel sets it for model array elements).
    // Scalar collection: f.nested is undefined → resolve strictly via the registry.
    const protoType =
      f.nested !== undefined
        ? f.nested.name
        : resolveProtoFromScalarCsType(elemCsType, f.name, onError);
    if (protoType === undefined) return undefined;
    return { protoName, protoType, fieldNumber, repeated: true };
  }

  // Nested model field: nested is set, csType is the model name (no suffix for non-optional).
  if (f.nested !== undefined) {
    const modelName = f.nested.name;
    return { protoName, protoType: modelName, fieldNumber, repeated: false };
  }

  // Scalar field: resolve strictly from the scalar registry (no passthrough for unknown types).
  const protoType = resolveProtoFromScalarCsType(
    f.csType.replace("?", ""),
    f.name,
    onError,
  );
  if (protoType === undefined) return undefined;
  return { protoName, protoType, fieldNumber, repeated: false };
}

/**
 * Lookup the proto type for a C# scalar type via the registry reverse-lookup table.
 *
 * This is strictly for scalar resolution — it does NOT pass through unknown PascalCase types.
 * Unknown types trigger D2TSP001 (loud failure). Callers that need model-type passthrough
 * should check `isModelTypeName()` before calling this function.
 */
function resolveProtoFromScalarCsType(
  csType: string,
  fieldName: string,
  onError: (
    code: "unmapped-scalar" | "invalid-streaming-mode",
    message: string,
  ) => void,
): string | undefined {
  const proto = CS_TO_PROTO.get(csType);
  if (proto !== undefined) return proto;

  onError(
    "unmapped-scalar",
    `D2TSP001: cannot resolve proto type for C# type '${csType}' on field '${fieldName}' — no mapping in the scalar registry`,
  );
  return undefined;
}

/**
 * Emit the D2ResultProto envelope Response message block.
 *
 * The shape is always:
 *   message <protoResponseMsgName> {
 *     d2.common.v1.D2ResultProto result = 1;
 *     <dataMessageName> data = 2;
 *   }
 *
 * Field 1 carries the D2Result envelope (success OR failure); field 2 carries
 * the typed output payload (populated on success; absent/default on failure).
 * gRPC status stays OK for all business results — transport faults use RpcException.
 */
function emitResponseEnvelopeMessage(
  protoResponseMsgName: string,
  dataMessageName: string,
): string {
  const lines: string[] = [];
  lines.push(`message ${protoResponseMsgName} {`);
  lines.push("  d2.common.v1.D2ResultProto result = 1;");
  lines.push(`  ${dataMessageName} data = 2;`);
  lines.push("}");
  return lines.join("\n");
}

/**
 * Emit one proto3 message block.
 * Empty messages emit `message Name {}` (well-formed proto3).
 */
function emitMessage(name: string, fields: readonly ProtoFieldInfo[]): string {
  if (fields.length === 0) return `message ${name} {}`;

  const lines: string[] = [];
  lines.push(`message ${name} {`);
  for (const f of fields) {
    const repeated = f.repeated ? "repeated " : "";
    lines.push(
      `  ${repeated}${f.protoType} ${f.protoName} = ${f.fieldNumber};`,
    );
  }
  lines.push("}");
  return lines.join("\n");
}

// ---------------------------------------------------------------------------
// Scalar registry reverse-lookup table (C# type → proto3 type).
// Built from the same registry used by model-walk.ts / scalar-registry.ts.
// This table is the inverse: scalar-registry.ts maps TS scalar name → {cs, proto, ts};
// here we map cs → proto so the proto emitter can resolve from already-emitted C# types.
// ---------------------------------------------------------------------------

const CS_TO_PROTO = new Map<string, string>([
  ["string", "string"], // string + url (url C# type is string; see scalar-registry.ts)
  ["bool", "bool"],
  ["byte[]", "bytes"],
  ["long", "int64"], // integer + int64 + safeint all map to long/int64
  ["sbyte", "int32"], // int8
  ["short", "int32"], // int16
  ["int", "int32"], // int32
  ["byte", "uint32"], // uint8
  ["ushort", "uint32"], // uint16
  ["uint", "uint32"], // uint32
  ["ulong", "uint64"], // uint64
  ["double", "double"], // float64 + float + numeric
  ["float", "float"], // float32
  ["decimal", "string"], // decimal + decimal128 → string (lossless wire)
  ["DateTimeOffset", "string"], // utcDateTime + offsetDateTime → string (ISO-8601 "O"; the
  // ?-strip at resolveOneField covers DateTimeOffset?). The offset-free temporal scalars
  // (plainDate / plainTime / plainDateTime) and duration map cs:"string" → already covered above.
  // NOTE: the `url` TypeSpec scalar maps to cs:"string" in scalar-registry.ts, so the
  // lookup key here is "string" (already above). A separate ["url","string"] entry
  // would be unreachable — the resolver receives the C# type string, not the TS scalar name.
]);
