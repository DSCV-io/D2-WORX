// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// gRPC service-impl emitter — pure string-template emission of:
//   1. <Service>Service.g.cs   — C# sealed class extending the Grpc.Tools-generated base,
//                                delegating to IHandler<TInput, TOutput>.
//   2. <Op>TransportMappers.g.cs — C# 14 extension-member mappers (proto ↔ DTO).
//
// Conventions:
//   - Base class is `global::<protoNs>.<Service>.<Service>Base` (global:: SC1 lesson).
//   - Proto message names follow <Method>Request / <Method>Response (matching the
//     hand-authored proto RPC-message convention in contracts/protos/).
//   - DTO types follow <Op>Input / <Op>Output — distinct from proto message names so
//     no using-alias disambiguation is needed.
//   - bytes ↔ byte[] conversion uses Google.Protobuf ByteString.CopyFrom / .ToByteArray().
//   - C# 14 block-form extension members: `extension(T target) { public ... Method() }`.
//   - Auto-generated banner; no phase/step/deliverable/audit-round identifiers.
//   - D2Result success path: maps output to proto response.
//     D2Result failure path: throws RpcException(Status.Internal, empty detail) —
//     info-leak-free per codebase posture (matches auth gRPC tests).
//   - No auth logic in the generated service (auth is a transport-layer concern).

import { buildBanner } from "./banner.js";
import { toPascal } from "./name-transforms.js";
import type { FieldInfo } from "./model-walk.js";
import type { EmittedFile } from "./csharp-dto-emitter.js";

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Emit the C# gRPC service-impl pair for one operation:
 *   1. `<Service>Service.g.cs`   — the gRPC service class.
 *   2. `<Op>TransportMappers.g.cs` — the proto ↔ DTO extension-member mappers.
 *
 * Pure function — no I/O; returns EmittedFile[] so tests can assert content directly.
 *
 * @param opName              - lowerCamelCase op name (e.g. "sign").
 * @param grpcService         - gRPC service name (e.g. "KeyCustodianSigner").
 * @param grpcMethod          - gRPC method name (e.g. "Sign").
 * @param protoCsharpNs       - C# namespace for the Grpc.Tools-generated proto types
 *                              (e.g. "D2.Services.Protos.KeyCustodian.V1").
 * @param serviceImplNs       - C# namespace for the generated service class + mapper
 *                              (e.g. "D2.Edge.Tests.TypeSpecGrpc.Generated").
 * @param dtoCsharpNs         - C# namespace where the handler DTO types live
 *                              (e.g. "D2.Edge.Tests.TypeSpecDto.Generated").
 * @param sourceSpec          - Relative spec path for the banner.
 * @param protoRequestName    - Proto message name for the request (e.g. "SignRequest").
 * @param protoResponseName   - Proto message name for the response (e.g. "SignResponse").
 * @param requestModelName    - TypeSpec DTO model name for the request (e.g. "SignInput").
 * @param requestFields       - Resolved field list for the request DTO.
 * @param responseModelName   - TypeSpec DTO model name for the response (e.g. "SignOutput").
 * @param responseFields      - Resolved field list for the response DTO.
 * @returns [serviceFile, mapperFile] pair.
 */
export function emitGrpcService(
  opName: string,
  grpcService: string,
  grpcMethod: string,
  protoCsharpNs: string,
  serviceImplNs: string,
  dtoCsharpNs: string,
  sourceSpec: string,
  protoRequestName: string,
  protoResponseName: string,
  requestModelName: string,
  requestFields: readonly FieldInfo[],
  responseModelName: string,
  responseFields: readonly FieldInfo[],
): [EmittedFile, EmittedFile] {
  // buildBanner returns a string that ends with "\n" (the trailing "" in the join).
  // We pass it as-is to the sub-emitters so the blank separator line is preserved.
  const banner = buildBanner(sourceSpec);
  const pascalOp = toPascal(opName);

  // Proto message names (<Method>Request / <Method>Response) are distinct from
  // DTO names (<Op>Input / <Op>Output), so no using-alias disambiguation is needed.

  const serviceFile = emitServiceClass(
    opName,
    grpcService,
    grpcMethod,
    protoCsharpNs,
    serviceImplNs,
    dtoCsharpNs,
    banner,
    pascalOp,
    protoRequestName,
    protoResponseName,
    requestModelName,
    responseModelName,
  );

  const mapperFile = emitTransportMappers(
    opName,
    protoCsharpNs,
    serviceImplNs,
    dtoCsharpNs,
    banner,
    protoRequestName,
    protoResponseName,
    requestModelName,
    responseModelName,
    requestFields,
    responseFields,
  );

  return [serviceFile, mapperFile];
}

// ---------------------------------------------------------------------------
// Internal: service class emitter
// ---------------------------------------------------------------------------

function emitServiceClass(
  opName: string,
  grpcService: string,
  grpcMethod: string,
  protoCsharpNs: string,
  serviceImplNs: string,
  dtoCsharpNs: string,
  banner: string,
  pascalOp: string,
  protoRequestName: string,
  protoResponseName: string,
  requestModelName: string,
  responseModelName: string,
): EmittedFile {
  const handlerInterface = `I${pascalOp}Handler`;
  const serviceClassName = `${grpcService}Service`;
  const baseClassFq = `global::${protoCsharpNs}.${grpcService}.${grpcService}Base`;

  const lines: string[] = [];

  // banner already ends with "\n" (the trailing "" in buildBanner's join).
  // Appending it as the first element then pushing the rest preserves the blank
  // separator line between the auto-generated block and the file content.
  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${serviceImplNs};`);
  lines.push("");

  // Proto message names (<Method>Request / <Method>Response) are distinct from
  // DTO names (<Op>Input / <Op>Output) — no collision, so no Proto*/Dto* alias
  // prefixes are needed. Short using-aliases carry the global:: root so SC1
  // namespace-shadowing cannot reach the generated code regardless of ambient usings.
  lines.push(`using ${protoRequestName} = global::${protoCsharpNs}.${protoRequestName};`);
  lines.push(`using ${protoResponseName} = global::${protoCsharpNs}.${protoResponseName};`);
  lines.push(`using ${requestModelName} = global::${dtoCsharpNs}.${requestModelName};`);
  lines.push(`using ${responseModelName} = global::${dtoCsharpNs}.${responseModelName};`);
  lines.push("using Grpc.Core;");
  lines.push("");

  // XML doc.
  lines.push(`/// <summary>Generated gRPC service for the <c>${grpcMethod}</c> operation, delegating to <see cref="${handlerInterface}"/>.</summary>`);

  // Class declaration — primary constructor takes the handler.
  lines.push(`public sealed class ${serviceClassName}(${handlerInterface} handler)`);
  lines.push(`    : ${baseClassFq}`);
  lines.push("{");

  // Override the rpc method.
  lines.push(`    /// <inheritdoc/>`);
  lines.push(`    public override async Task<${protoResponseName}> ${grpcMethod}(${protoRequestName} request, ServerCallContext context)`);
  lines.push("    {");
  lines.push(`        ${requestModelName} input = request.To${requestModelName}();`);
  lines.push(`        var result = await handler.HandleAsync(input, context.CancellationToken).ConfigureAwait(false);`);
  lines.push("        if (!result.IsOk)");
  lines.push("            throw new RpcException(new Status(StatusCode.Internal, string.Empty));");
  lines.push(`        return result.Data!.ToProto${responseModelName}();`);
  lines.push("    }");
  lines.push("}");
  lines.push("");

  void opName; // opName consumed indirectly via pascalOp

  const fileName = `${serviceClassName}.g.cs`;
  return { fileName, content: lines.join("\n") };
}

// ---------------------------------------------------------------------------
// Internal: transport mapper emitter
// ---------------------------------------------------------------------------

function emitTransportMappers(
  opName: string,
  protoCsharpNs: string,
  serviceImplNs: string,
  dtoCsharpNs: string,
  banner: string,
  protoRequestName: string,
  protoResponseName: string,
  requestModelName: string,
  responseModelName: string,
  requestFields: readonly FieldInfo[],
  responseFields: readonly FieldInfo[],
): EmittedFile {
  const pascalOp = toPascal(opName);
  const mapperClassName = `${pascalOp}TransportMappers`;

  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${serviceImplNs};`);
  lines.push("");

  // Short using-aliases for proto and DTO types carry global:: root so SC1 shadowing
  // cannot reach the generated code. No Proto*/Dto* prefixes needed: message names
  // (<Method>Request / <Method>Response) are distinct from DTO names (<Op>Input / <Op>Output).
  lines.push(`using ${protoRequestName} = global::${protoCsharpNs}.${protoRequestName};`);
  lines.push(`using ${protoResponseName} = global::${protoCsharpNs}.${protoResponseName};`);
  lines.push(`using ${requestModelName} = global::${dtoCsharpNs}.${requestModelName};`);
  lines.push(`using ${responseModelName} = global::${dtoCsharpNs}.${responseModelName};`);
  lines.push("using Google.Protobuf;");
  lines.push("");

  lines.push(`/// <summary>Generated transport mappers: proto ↔ DTO for the <c>${pascalOp}</c> operation.</summary>`);
  lines.push(`internal static class ${mapperClassName}`);
  lines.push("{");

  // Extension block: proto request → DTO input.
  lines.push(`    extension(${protoRequestName} request)`);
  lines.push("    {");
  lines.push(`        internal ${requestModelName} To${requestModelName}()`);
  lines.push("        {");

  if (requestFields.length === 0) {
    lines.push(`            return new ${requestModelName}();`);
  } else {
    const args = requestFields.map((f) => buildProtoToDto(f));
    lines.push(`            return new ${requestModelName}(${args.join(", ")});`);
  }
  lines.push("        }");
  lines.push("    }");
  lines.push("");

  // Extension block: DTO output → proto response.
  lines.push(`    extension(${responseModelName} output)`);
  lines.push("    {");
  lines.push(`        internal ${protoResponseName} ToProto${responseModelName}()`);
  lines.push("        {");

  if (responseFields.length === 0) {
    lines.push(`            return new ${protoResponseName}();`);
  } else {
    lines.push(`            return new ${protoResponseName}`);
    lines.push("            {");
    const assignments = responseFields.map((f) => `                ${toPascal(f.name)} = ${buildDtoToProto(f)}`);
    for (const assignment of assignments)
      lines.push(`${assignment},`);
    lines.push("            };");
  }
  lines.push("        }");
  lines.push("    }");

  lines.push("}");
  lines.push("");

  void dtoCsharpNs; // suppress unused-var

  const fileName = `${pascalOp}TransportMappers.g.cs`;
  return { fileName, content: lines.join("\n") };
}

/**
 * Build the expression for mapping one proto request field to the DTO constructor arg.
 * bytes → ByteString.ToByteArray(); all others → request.PascalName.
 */
function buildProtoToDto(f: FieldInfo): string {
  const propName = toPascal(f.name);
  // bytes type in C# DTO = byte[], in proto = Google.Protobuf.ByteString.
  if (f.csType === "byte[]" || f.csType === "byte[]?")
    return `request.${propName}.ToByteArray()`;
  return `request.${propName}`;
}

/**
 * Build the assignment RHS for mapping one DTO output field to proto response.
 * byte[] → ByteString.CopyFrom(output.PascalName); all others → output.PascalName.
 */
function buildDtoToProto(f: FieldInfo): string {
  const propName = toPascal(f.name);
  if (f.csType === "byte[]" || f.csType === "byte[]?")
    return `ByteString.CopyFrom(output.${propName})`;
  return `output.${propName}`;
}
