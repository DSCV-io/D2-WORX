// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// gRPC client emitter — pure string-template emission of the four generated
// files that form the per-module cross-process gRPC client layer:
//
//   1. I<Module>GrpcClient.g.cs   → Clients namespace
//      The per-module gRPC client interface. Lists the module's @d2GrpcMethod
//      operations with the transport-aware signature (pipelineOverride param).
//      Does NOT implement the in-process façade (I<Module>Api) — the op-sets
//      may differ (@d2GrpcMethod vs @d2InProcess), and the gRPC client is the
//      cross-process surface only.
//
//   2. <Module>GrpcClient.g.cs    → Clients namespace
//      The sealed implementing class. Injects the Grpc.Tools stub and the keyed
//      ResilientPipeline. Runs the THROWING stub call inside pipeline.ExecuteAsync
//      (the only path that retries transport faults); captures the D2ResultProto
//      envelope OUT of the closure so the business D2Result can be reconstructed
//      with full fidelity AFTER ExecuteAsync returns.
//      ReturnType: ValueTask<D2Result<<Op>Output?>> (matches the façade family).
//
//   3. <Op>ClientMappers.g.cs     → same Clients namespace
//      The INVERSE of the server <Op>TransportMappers.g.cs:
//        <Op>Input → <Op>Request  (DTO → proto request; byte[] → ByteString.CopyFrom)
//        <Op>Response data → <Op>Output?  (proto data → DTO; ByteString → byte[])
//      Same global:: alias pattern as the server mapper (ProtoSignOutput alias when
//      the proto data message name collides with the DTO name).
//
//   4. <Module>GrpcClientsGenerated.g.cs  → Clients namespace
//      The generated DI extension (C# 14 extension member form):
//        AddGrpcClient<Stub>(opts) with channel address from <Module>GrpcClientOptions,
//          AUTO-CHAINING .AddD2ForwardedJwt().AddD2WorkloadCertificate() on every channel.
//        AddResilientPipeline<string, <Op>Output?> with the gRPC-only IsTransient.
//        AddTransient<I<Module>GrpcClient, <Module>GrpcClient>.
//      Called from a hand-written composition root (regen-safe, like the façade DI ext).
//      AUTO-WIRES the per-channel outbound auth — .AddD2ForwardedJwt() forwards the
//      request-scoped transaction-token and .AddD2WorkloadCertificate() presents the
//      workload mTLS leaf — so a host can never forget to attach it to a generated
//      internal client (fail-safe). The host supplies only the one-time config the
//      generator cannot invent: AddD2ForwardedJwtOutbound() + AddD2WorkloadCertificateOutbound().
//
// Load-bearing body discipline (D-3 captured-envelope, §E):
//   The pipeline op CANNOT express a business D2Result failure as:
//     • a value (pipeline wraps it in Ok) — WRONG: would wrongly retry a business failure.
//     • a throw (pipeline would retry) — WRONG: business failures ride gRPC status OK.
//   Resolution: capture the D2ResultProto envelope outside the op closure.
//     1. Op throws for TRANSPORT faults (RpcException propagates → custom IsTransient retries).
//     2. On SUCCESS the op captures the response.Result envelope and returns the mapped data.
//     3. After ExecuteAsync: if it succeeded AND the envelope is non-null, reconstruct the
//        full business D2Result via envelope.ToD2Result(pipelineResult.Data).
//        If it failed (transport fault mapped to ServiceUnavailable/Canceled/etc.), return
//        the pipeline D2Result verbatim.
//
// Conventions:
//   - Auto-generated banner, #nullable enable, namespace BEFORE using.
//   - global:: rooted aliases (SC1 namespace-shadowing prevention).
//   - C# 14 extension(T target) { } block form for mappers and DI extension.
//   - sealed impl class; primary ctor injects stub + keyed pipeline.
//   - Primary-ctor parameters carry NO r_ prefix; kept private fields DO (r_pipeline).
//   - No phase/step/deliverable/audit-round identifiers in emitted code or source.
//   - American English: Canceled (single L).

import { buildBanner } from "./banner.js";
import { toPascal } from "./name-transforms.js";
import type { FieldInfo } from "./model-walk.js";
import type { EmittedFile } from "./csharp-dto-emitter.js";
import { collectFieldEnums, emitEnumMapperHelpers } from "./enum-mapper.js";

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/**
 * One @d2GrpcMethod operation collected for a module during the per-op walk.
 * The grpc-client emitter receives a list of these (grouped by @d2ServedBy module)
 * and emits four files per module.
 */
export interface GrpcClientOp {
  /** lowerCamelCase op name (e.g. "sign"). */
  readonly opName: string;
  /** gRPC service name from @d2GrpcMethod (e.g. "KeyCustodianSigner"). */
  readonly grpcService: string;
  /** gRPC method name from @d2GrpcMethod (e.g. "Sign"). */
  readonly grpcMethod: string;
  /** Proto C# namespace (e.g. "D2.Services.Protos.KeyCustodian.V1"). */
  readonly protoCsharpNs: string;
  /** DTO C# namespace (where <Op>Input / <Op>Output live). */
  readonly dtoCsharpNs: string;
  /** Source spec path for the banner. */
  readonly sourceSpec: string;
  /** Name of the request DTO type (e.g. "SignInput"). */
  readonly requestModelName: string;
  /** Fields of the request DTO (for the DTO→proto mapper). */
  readonly requestFields: readonly FieldInfo[];
  /** Name of the response DTO type (e.g. "SignOutput"). */
  readonly responseModelName: string;
  /** Fields of the response DTO (for the proto→DTO mapper). */
  readonly responseFields: readonly FieldInfo[];
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Emit the four gRPC-client files for one module.
 *
 * Pure function — no I/O. Returns the four EmittedFile instances in order:
 *   [0] the interface file  (I<Module>GrpcClient.g.cs, Clients ns)
 *   [1] the impl file       (<Module>GrpcClient.g.cs, Clients ns)
 *   [2] the mappers file    (<Op>ClientMappers.g.cs, Clients ns)
 *   [3] the DI-ext file     (<Module>GrpcClientsGenerated.g.cs, Clients ns)
 *
 * @param moduleName      - The @d2ServedBy module name in PascalCase
 *                          (e.g. "KeyCustodian"). Drives interface/impl/DI type names.
 * @param ops             - All @d2GrpcMethod operations for this module, in encounter
 *                          order (determines method order in the interface and
 *                          constructor-parameter order in the impl).
 * @param clientsNs       - C# namespace for the Clients project.
 * @returns Exactly four EmittedFile instances, or an empty array when ops is empty.
 */
export function emitGrpcClient(
  moduleName: string,
  ops: readonly GrpcClientOp[],
  clientsNs: string,
): EmittedFile[] {
  if (moduleName.length === 0)
    throw new Error("emitGrpcClient: moduleName must not be empty");
  if (clientsNs.length === 0)
    throw new Error("emitGrpcClient: clientsNs must not be empty");
  if (ops.length === 0) return [];

  // Use the first op's sourceSpec for the banner (all ops in a module share the spec).
  const sourceSpec = ops[0]!.sourceSpec;
  const banner = buildBanner(sourceSpec);

  const interfaceName = `I${moduleName}GrpcClient`;
  const implName = `${moduleName}GrpcClient`;

  return [
    emitInterface(interfaceName, moduleName, ops, clientsNs, banner),
    emitImpl(interfaceName, implName, moduleName, ops, clientsNs, banner),
    emitClientMappers(ops, clientsNs, banner),
    emitGrpcClientsDiExtension(
      moduleName,
      interfaceName,
      implName,
      ops,
      clientsNs,
      banner,
    ),
  ];
}

// ---------------------------------------------------------------------------
// Private helpers — one per generated file
// ---------------------------------------------------------------------------

function emitInterface(
  interfaceName: string,
  moduleName: string,
  ops: readonly GrpcClientOp[],
  clientsNs: string,
  banner: string,
): EmittedFile {
  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${clientsNs};`);
  lines.push("");
  // DTO type aliases (global:: rooted, SA1210-sorted) so the bare request/response
  // model names in the method signatures resolve unambiguously even when the DTO
  // namespace differs from the client namespace. Mirrors the server emitter's
  // alias-using convention. Omitted when the DTO type lives in this namespace.
  for (const alias of collectDtoAliasUsings(ops, clientsNs)) lines.push(alias);
  lines.push("using D2.Shared.Resilience.Pipeline;");
  lines.push("");

  lines.push(`/// <summary>`);
  lines.push(
    `/// Generated cross-process gRPC client interface for the ${moduleName} module.`,
  );
  lines.push(
    `/// Lists the module's gRPC-exposed operations with the transport-aware signature.`,
  );
  lines.push(`/// </summary>`);
  lines.push(`public interface ${interfaceName}`);
  lines.push("{");

  for (const op of ops) {
    const pascalOp = toPascal(op.opName);
    lines.push(
      `    /// <summary>Dispatches the <c>${pascalOp}</c> operation over gRPC.</summary>`,
    );
    lines.push(
      `    ValueTask<D2Result<${op.responseModelName}?>> ${pascalOp}Async(`,
    );
    lines.push(`        ${op.requestModelName} input,`);
    lines.push(
      `        ResilientPipeline<string, ${op.responseModelName}?>? pipelineOverride = null,`,
    );
    lines.push(`        CancellationToken ct = default);`);
  }

  lines.push("}");
  lines.push("");

  return { fileName: `${interfaceName}.g.cs`, content: lines.join("\n") };
}

function emitImpl(
  interfaceName: string,
  implName: string,
  moduleName: string,
  ops: readonly GrpcClientOp[],
  clientsNs: string,
  banner: string,
): EmittedFile {
  // The impl groups ops by gRPC service (one stub field per distinct service).
  // Collect distinct (grpcService, protoCsharpNs) pairs in encounter order.
  const services = new Map<
    string,
    { grpcService: string; protoCsharpNs: string }
  >();
  for (const op of ops) {
    if (!services.has(op.grpcService))
      services.set(op.grpcService, {
        grpcService: op.grpcService,
        protoCsharpNs: op.protoCsharpNs,
      });
  }

  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${clientsNs};`);
  lines.push("");

  // Plain namespace usings (SA1210-sorted). The proto stub + proto Request/Response
  // are referenced via global:: roots in the body, so the proto SERVICE namespace is
  // NOT imported bare here — importing it would collide with the DTO <Op>Output type
  // name (the proto data message shares the DTO name). D2ResultProto comes from the
  // common-proto namespace; the rest are shared-lib namespaces.
  const usingSet = new Set<string>();
  usingSet.add("D2.Services.Protos.Common.V1");
  usingSet.add("D2.Shared.Resilience.Pipeline");
  usingSet.add("D2.Shared.Resilience.Retry");
  usingSet.add("D2.Shared.Result.Grpc");
  usingSet.add("Grpc.Core");
  usingSet.add("Microsoft.Extensions.DependencyInjection");

  // DTO type aliases (global:: rooted) so the bare request/response model names in the
  // method signatures + pipeline generic args resolve unambiguously, and never collide
  // with the same-named proto data message. Omitted when the DTO lives in this namespace.
  for (const alias of collectDtoAliasUsings(ops, clientsNs)) lines.push(alias);
  const sortedUsings = [...usingSet].sort();
  for (const ns of sortedUsings) lines.push(`using ${ns};`);
  lines.push("");

  // Build the primary constructor parameter list:
  //   - one stub param per distinct gRPC service
  //   - one keyed pipeline param per op (one pipeline per op so each can be tuned independently)
  // Primary-ctor params: NO r_ prefix.
  const stubParams = [...services.values()].map(
    ({ grpcService, protoCsharpNs }) =>
      `global::${protoCsharpNs}.${grpcService}.${grpcService}Client ${lowerFirst(grpcService)}Stub`,
  );
  const pipelineParams = ops.map(
    (op) =>
      `[FromKeyedServices(${toPascal(op.opName)}ClientKeys.PIPELINE)] ` +
      `ResilientPipeline<string, ${op.responseModelName}?> ${op.opName}Pipeline`,
  );
  const allCtorParams = [...stubParams, ...pipelineParams];

  lines.push(`/// <summary>`);
  lines.push(
    `/// Generated sealed cross-process gRPC client for the ${moduleName} module.`,
  );
  lines.push(
    `/// Runs each throwing stub call inside the keyed ResilientPipeline (transport-only`,
  );
  lines.push(
    `/// retry); reconstructs the business D2Result from the captured envelope after the`,
  );
  lines.push(`/// pipeline returns.`);
  lines.push(`/// </summary>`);
  lines.push(`public sealed class ${implName}(`);
  for (let i = 0; i < allCtorParams.length; i++) {
    const comma = i < allCtorParams.length - 1 ? "," : "";
    lines.push(`    ${allCtorParams[i]}${comma}`);
  }
  lines.push(`) : ${interfaceName}`);
  lines.push("{");

  // r_ private fields: one per injected pipeline (kept after primary ctor).
  for (const op of ops) {
    const pascalOp = toPascal(op.opName);
    lines.push(
      `    private readonly ResilientPipeline<string, ${op.responseModelName}?> r_${op.opName}Pipeline = ${op.opName}Pipeline;`,
    );
    void pascalOp; // consumed below
  }
  lines.push("");

  // Per-op method pair: public async wrapper + private core.
  for (const op of ops) {
    const pascalOp = toPascal(op.opName);
    const svcEntry = services.get(op.grpcService)!;
    const stubFieldName = `${lowerFirst(op.grpcService)}Stub`;
    const protoRequestName = `${op.grpcMethod}Request`;
    const protoResponseName = `${op.grpcMethod}Response`;
    const protoDataAlias = `Proto${op.responseModelName}`;

    // Global-alias short names used in the method body.
    const fqRequest = `global::${svcEntry.protoCsharpNs}.${protoRequestName}`;
    const fqResponse = `global::${svcEntry.protoCsharpNs}.${protoResponseName}`;
    const fqProtoData = `global::${svcEntry.protoCsharpNs}.${op.responseModelName}`;
    const fqDto = `global::${op.dtoCsharpNs}.${op.responseModelName}`;

    void fqRequest; // type references in method bodies via using aliases
    void fqResponse;
    void fqProtoData;
    void fqDto;
    void protoDataAlias;

    // When the response carries ≥1 enum field, the client mapper To<Output>()
    // returns D2Result<<Output>> (it parses each proto enum string back to the C#
    // enum, fail-loud on unknown). The closure captures any parse failure out of
    // band; the post-pipeline path surfaces it as the business result.
    const responseHasEnums = op.responseFields.some(
      (f) => f.enumRef !== undefined,
    );

    // Public interface impl — thin dispatcher; uses override or injected pipeline.
    lines.push(`    /// <inheritdoc/>`);
    lines.push(
      `    public ValueTask<D2Result<${op.responseModelName}?>> ${pascalOp}Async(`,
    );
    lines.push(`        ${op.requestModelName} input,`);
    lines.push(
      `        ResilientPipeline<string, ${op.responseModelName}?>? pipelineOverride = null,`,
    );
    lines.push(`        CancellationToken ct = default)`);
    lines.push(
      `        => ${pascalOp}CoreAsync(input, pipelineOverride ?? r_${op.opName}Pipeline, ct);`,
    );
    lines.push("");

    // Private async core — captured-envelope body (§E).
    lines.push(
      `    private async ValueTask<D2Result<${op.responseModelName}?>> ${pascalOp}CoreAsync(`,
    );
    lines.push(`        ${op.requestModelName} input,`);
    lines.push(
      `        ResilientPipeline<string, ${op.responseModelName}?> pipeline,`,
    );
    lines.push(`        CancellationToken ct)`);
    lines.push("    {");
    lines.push(`        var request = input.To${protoRequestName}();`);
    lines.push(
      `        D2ResultProto? envelope = null;                  // captured out of the closure`,
    );
    lines.push(
      `        RpcException? transportFault = null;             // captured out of the closure`,
    );

    if (responseHasEnums) {
      lines.push(
        `        D2Result<${op.responseModelName}>? responseParseFailure = null;  // client-side enum parse failure`,
      );
    }

    lines.push(`        var pipelineResult = await pipeline.ExecuteAsync(`);
    lines.push(`            ${toPascal(op.opName)}ClientKeys.PIPELINE_KEY,`);
    lines.push(`            async innerCt =>`);
    lines.push("            {");
    lines.push("                try");
    lines.push("                {");
    lines.push(
      `                    var response = await ${stubFieldName}.${op.grpcMethod}Async(request, cancellationToken: innerCt);`,
    );
    lines.push(
      `                    envelope = response.Result;          // business result (gRPC status OK)`,
    );

    if (responseHasEnums) {
      // The response mapper returns D2Result<<Output>> (it parses each enum string,
      // fail-loud on unknown). A parse failure of a server SUCCESS payload is a
      // client-side ValidationFailed — capture it out of band and complete the op
      // with null data so the post-pipeline path can surface the parse failure.
      lines.push(`                    if (response.Data is null)`);
      lines.push(`                        return default;`);
      lines.push("");
      lines.push(
        `                    var dataResult = response.Data.To${op.responseModelName}();`,
      );
      lines.push(`                    if (!dataResult.Success)`);
      lines.push("                    {");
      lines.push(
        `                        responseParseFailure = dataResult;  // capture; surface after the pipeline`,
      );
      lines.push(`                        return default;`);
      lines.push("                    }");
      lines.push("");
      lines.push(`                    return dataResult.Data;`);
    } else {
      lines.push(
        `                    return response.Data is null ? default : response.Data.To${op.responseModelName}();`,
      );
    }

    lines.push("                }");
    lines.push("                catch (RpcException ex)");
    lines.push("                {");
    lines.push(
      `                    transportFault = ex;                 // capture; rethrow so the retry layer still sees the throw`,
    );
    lines.push("                    throw;");
    lines.push("                }");
    lines.push("            },");
    lines.push(`            ct);`);
    lines.push(
      `        // Transport fault: the pipeline classifies RpcException via the gRPC-agnostic generic`,
    );
    lines.push(
      `        // path (mis-mapping to UnhandledException); remap the captured RpcException to the`,
    );
    lines.push(
      `        // gRPC-aware code (Cancelled -> Canceled, else -> ServiceUnavailable).`,
    );
    lines.push(
      `        if (!pipelineResult.Success && transportFault is not null)`,
    );
    lines.push(
      `            return transportFault.ToTransportFaultResult<${op.responseModelName}?>();`,
    );

    if (responseHasEnums) {
      // A client-side enum parse failure overrides the (successful) transport result:
      // the server's payload carried a wire value this client cannot map.
      lines.push(
        `        // Client could not map a response enum wire value → ValidationFailed (strict, no fallback).`,
      );
      lines.push(`        if (responseParseFailure is not null)`);
      lines.push(
        `            return D2Result<${op.responseModelName}?>.BubbleFail(responseParseFailure);`,
      );
    }

    lines.push(
      `        // Business result: reconstruct the full D2Result from the captured envelope. Other`,
    );
    lines.push(
      `        // pipeline failures (CircuitOpen, RateLimit, caller-cancel) pass through verbatim.`,
    );
    lines.push(`        return pipelineResult.Success && envelope is not null`);
    lines.push(
      `            ? envelope.ToD2Result<${op.responseModelName}?>(pipelineResult.Data)`,
    );
    lines.push(`            : pipelineResult;`);
    lines.push("    }");
    lines.push("");
  }

  // Close the class.
  // Remove the last blank line to avoid double blank before "}". Every op-method block
  // ends with a pushed "" (above), so the trailing line is always empty for a non-empty
  // ops list (guarded by the ops.length === 0 early return); the else is defensive.
  /* v8 ignore start — defensive: the loop always leaves a trailing blank line to pop */
  if (lines[lines.length - 1] === "") lines.pop();
  /* v8 ignore stop */
  lines.push("}");
  lines.push("");

  return { fileName: `${implName}.g.cs`, content: lines.join("\n") };
}

function emitClientMappers(
  ops: readonly GrpcClientOp[],
  clientsNs: string,
  banner: string,
): EmittedFile {
  // Client mappers are the INVERSE of the server transport mappers:
  //   server: SignRequest → SignInput,  D2Result<SignOutput?> → SignResponse
  //   client: SignInput → SignRequest,  SignResponse.Data → SignOutput?
  //
  // One mapper file covers ALL ops in the module (mirrors the server per-op file,
  // but we emit one file for the module since the server emitter is per-op).
  // In practice, for the fixture there is one op; the multi-op case is handled
  // by emitting one static class per op inside the same file.

  // Collect all distinct (grpcService, protoCsharpNs) pairs.
  const services = new Map<
    string,
    { grpcService: string; protoCsharpNs: string }
  >();
  for (const op of ops) {
    if (!services.has(op.grpcService))
      services.set(op.grpcService, {
        grpcService: op.grpcService,
        protoCsharpNs: op.protoCsharpNs,
      });
  }

  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${clientsNs};`);
  lines.push("");

  // Usings (sorted; global:: aliases in the mapper bodies).
  // Skip any namespace equal to clientsNs (never using your own namespace).
  const usingSet = new Set<string>();
  for (const { protoCsharpNs } of services.values())
    usingSet.add(protoCsharpNs);
  for (const op of ops)
    if (op.dtoCsharpNs !== clientsNs) usingSet.add(op.dtoCsharpNs);
  usingSet.add("Google.Protobuf");

  // Collect the distinct enums referenced by any op's input fields (the client
  // maps DTO enum → proto string via .ToWire() on the outbound request). TK +
  // D2Result come into play only for the inbound Parse<Enum>Wire helper, which is
  // still emitted for symmetry with the server mapper.
  const allClientEnums = collectFieldEnums(
    ops.flatMap((op) => [...op.requestFields, ...op.responseFields]),
  );
  // Enum aliases are emitted ONLY when the DTO namespace differs from the clients
  // namespace; a self-referential `using X = …Ns.X;` for a type in THIS namespace
  // conflicts with the type declaration (CS0576).
  const enumAliasLines: string[] = [];
  for (const op of ops)
    if (op.dtoCsharpNs !== clientsNs)
      for (const f of [...op.requestFields, ...op.responseFields])
        if (
          f.enumRef !== undefined &&
          !enumAliasLines.some((l) => l.includes(` ${f.enumRef!.name} =`))
        )
          enumAliasLines.push(
            `using ${f.enumRef.name} = global::${op.dtoCsharpNs}.${f.enumRef.name};`,
          );

  const sortedUsings = [...usingSet].sort();
  for (const ns of sortedUsings) lines.push(`using ${ns};`);
  for (const alias of enumAliasLines.sort()) lines.push(alias);
  if (allClientEnums.length > 0) {
    lines.push("using D2.Shared.I18n;");
    lines.push("using D2.Shared.Result;");
  }
  lines.push("");

  // Per-op mapper class.
  for (let oi = 0; oi < ops.length; oi++) {
    const op = ops[oi]!;
    const pascalOp = toPascal(op.opName);
    const protoRequestName = `${op.grpcMethod}Request`;
    const protoResponseName = `${op.grpcMethod}Response`;
    // Proto data message name == DTO name (<Op>Output) — disambiguate via alias.
    const protoDataAlias = `Proto${op.responseModelName}`;

    // global:: aliases for this op.
    const fqRequest = `global::${op.protoCsharpNs}.${protoRequestName}`;
    const fqResponse = `global::${op.protoCsharpNs}.${protoResponseName}`;
    const fqProtoData = `global::${op.protoCsharpNs}.${op.responseModelName}`;
    const fqDtoInput = `global::${op.dtoCsharpNs}.${op.requestModelName}`;
    const fqDtoOutput = `global::${op.dtoCsharpNs}.${op.responseModelName}`;

    const mapperClassName = `${pascalOp}ClientMappers`;

    lines.push(
      `/// <summary>Generated client-side mappers: DTO ↔ proto for the <c>${pascalOp}</c> operation (inverse of server transport mappers).</summary>`,
    );
    lines.push(`internal static class ${mapperClassName}`);
    lines.push("{");

    // Extension block 1: DTO input → proto request (<Op>Input → <Op>Request).
    lines.push(`    extension(${fqDtoInput} input)`);
    lines.push("    {");
    lines.push(`        internal ${fqRequest} To${protoRequestName}()`);
    lines.push("        {");
    if (op.requestFields.length === 0) {
      lines.push(`            return new ${fqRequest}();`);
    } else {
      lines.push(`            return new ${fqRequest}`);
      lines.push("            {");
      for (const f of op.requestFields) {
        const propName = toPascal(f.name);
        const rhs = buildClientDtoToProto(f, "input");
        lines.push(`                ${propName} = ${rhs},`);
      }
      lines.push("            };");
    }
    lines.push("        }");
    lines.push("    }");
    lines.push("");

    // Extension block 2: proto data message → DTO output (<Op>Output proto → DTO).
    // The proto data message name is the same as the DTO name (<Op>Output); use the
    // global:: alias (Proto<Op>Output) to disambiguate.
    //
    // When the response carries ≥1 enum field, the proto string is parsed back to
    // the C# enum via Parse<Enum>Wire (fail-loud ValidationFailed on an unknown
    // value), so the mapper returns D2Result<<Output>> — symmetric with the server
    // request mapper's inbound parse. The client impl checks .Success and surfaces a
    // parse failure as the business result (the server sent a value the client cannot
    // map → ValidationFailed).
    const responseHasEnums = op.responseFields.some(
      (f) => f.enumRef !== undefined,
    );
    lines.push(`    extension(${fqProtoData} data)`);
    lines.push("    {");

    if (responseHasEnums) {
      emitClientResponseMapperWithEnums(
        lines,
        op.responseModelName,
        fqDtoOutput,
        op.responseFields,
      );
    } else {
      lines.push(`        internal ${fqDtoOutput} To${op.responseModelName}()`);
      lines.push("        {");
      if (op.responseFields.length === 0) {
        lines.push(`            return new ${fqDtoOutput}();`);
      } else {
        const args = op.responseFields.map((f) =>
          buildClientProtoToDto(f, "data"),
        );
        lines.push(`            return new ${fqDtoOutput}(${args.join(", ")});`);
      }
      lines.push("        }");
    }
    lines.push("    }");

    // Per-enum ToWire / Parse<Enum>Wire helper blocks (inverse-symmetric with the
    // server mapper). The client maps DTO enum → proto string via .ToWire() on the
    // outbound request; Parse<Enum>Wire is emitted for symmetry + the inbound path.
    const opEnums = collectFieldEnums([
      ...op.requestFields,
      ...op.responseFields,
    ]);
    emitEnumMapperHelpers((l) => lines.push(l), opEnums);

    lines.push("}");
    if (oi < ops.length - 1) lines.push("");
    void protoResponseName; // used in the DI ext / impl; suppress lint
    void protoDataAlias; // named above for clarity; resolve the intent via fqProtoData
    void fqResponse;
  }

  lines.push("");

  // Mapper file name: for a single op (fixture case) use <PascalOp>ClientMappers.g.cs.
  // For multi-op, use <Module>ClientMappers.g.cs. The fixture has one op.
  const firstOp = ops[0]!;
  const fileName =
    ops.length === 1
      ? `${toPascal(firstOp.opName)}ClientMappers.g.cs`
      : `${ops.map((o) => toPascal(o.opName)).join("")}ClientMappers.g.cs`;

  return { fileName, content: lines.join("\n") };
}

function emitGrpcClientsDiExtension(
  moduleName: string,
  interfaceName: string,
  implName: string,
  ops: readonly GrpcClientOp[],
  clientsNs: string,
  banner: string,
): EmittedFile {
  // Collect distinct gRPC services (one AddGrpcClient per service).
  const services = new Map<
    string,
    { grpcService: string; protoCsharpNs: string }
  >();
  for (const op of ops) {
    if (!services.has(op.grpcService))
      services.set(op.grpcService, {
        grpcService: op.grpcService,
        protoCsharpNs: op.protoCsharpNs,
      });
  }

  const extensionMethodName = `AddD2${moduleName}GrpcClients`;
  const optionsClassName = `${moduleName}GrpcClientOptions`;
  const fileName = `${moduleName}GrpcClientsGenerated.g.cs`;

  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${clientsNs};`);
  lines.push("");
  // DTO type aliases (global:: rooted) so the bare <Op>Output in the AddResilientPipeline
  // / RetryOptions generic args resolves when the DTO namespace differs from the client
  // namespace. Omitted when the DTO type lives in this namespace.
  for (const alias of collectDtoAliasUsings(ops, clientsNs)) lines.push(alias);
  // D2.Shared.Auth.Outbound.Grpc supplies the per-channel .AddD2ForwardedJwt() /
  // .AddD2WorkloadCertificate() extensions auto-chained onto each registered client.
  lines.push("using D2.Shared.Auth.Outbound.Grpc;");
  lines.push("using D2.Shared.Resilience.Pipeline;");
  lines.push("using D2.Shared.Resilience.Retry;");
  lines.push("using D2.Shared.Result.Grpc;");
  lines.push("using Grpc.Core;");
  lines.push("using Microsoft.Extensions.DependencyInjection;");
  lines.push("");

  // Options record (host-supplied channel address — never a literal in generated code).
  lines.push(
    `/// <summary>Options for the ${moduleName} gRPC client channel. Supplied by the host composition root.</summary>`,
  );
  lines.push(`public sealed record ${optionsClassName}`);
  lines.push("{");
  lines.push(
    `    /// <summary>gRPC channel endpoint address for the ${moduleName} service.</summary>`,
  );
  lines.push(`    public required Uri Address { get; init; }`);
  lines.push("}");
  lines.push("");

  // DI extension class.
  lines.push(`/// <summary>`);
  lines.push(
    `/// Generated DI extension for the ${moduleName} gRPC client layer.`,
  );
  lines.push(`/// Called from the hand-written host composition root.`);
  lines.push(`/// </summary>`);
  lines.push(
    `public static class ${moduleName}GrpcClientsGeneratedServiceCollectionExtensions`,
  );
  lines.push("{");
  lines.push(`    extension(IServiceCollection services)`);
  lines.push("    {");
  lines.push(`        /// <summary>`);
  lines.push(
    `        /// Registers the ${moduleName} gRPC client: channel, per-op resilience pipelines,`,
  );
  lines.push(
    `        /// and the <see cref="${interfaceName}"/> → <see cref="${implName}"/> binding.`,
  );
  lines.push(
    `        /// Auto-chains <c>.AddD2ForwardedJwt().AddD2WorkloadCertificate()</c> on the gRPC`,
  );
  lines.push(
    `        /// client builder so every internal call forwards the request-scoped transaction-token`,
  );
  lines.push(
    `        /// and presents the workload mTLS leaf — the host never chains the per-channel outbound`,
  );
  lines.push(
    `        /// auth and so can never forget it. The TLS channel (SecureSsl default) carries the`,
  );
  lines.push(
    `        /// forwarded-JWT credential, which the inbound transport's ambient holder resolves per`,
  );
  lines.push(
    `        /// call. The host supplies only the one-time config the generator cannot invent:`,
  );
  lines.push(
    `        /// <c>AddD2ForwardedJwtOutbound()</c> + <c>AddD2WorkloadCertificateOutbound()</c>.`,
  );
  lines.push(`        /// </summary>`);
  lines.push(
    `        public IServiceCollection ${extensionMethodName}(${optionsClassName} options)`,
  );
  lines.push("        {");

  // Register the gRPC channel per distinct service, auto-chaining the per-channel
  // outbound auth onto each so the host never wires it (fail-safe).
  for (const { grpcService, protoCsharpNs } of services.values()) {
    lines.push(
      `            // ${grpcService} channel — address from host-supplied options.`,
    );
    lines.push(
      `            services.AddGrpcClient<global::${protoCsharpNs}.${grpcService}.${grpcService}Client>(o =>`,
    );
    lines.push(`                o.Address = options.Address)`);
    lines.push(
      `                // Auto-wired outbound auth — host never chains this (fail-safe).`,
    );
    lines.push(`                .AddD2ForwardedJwt()`);
    lines.push(`                .AddD2WorkloadCertificate();`);
    lines.push("");
  }

  // Register per-op resilience pipelines.
  for (const op of ops) {
    const pascalOp = toPascal(op.opName);
    lines.push(
      `            // ${pascalOp} resilience pipeline — retry on gRPC transport transients only.`,
    );
    lines.push(
      `            // Replace with ResilientPipeline<…>.PassThrough in tests that do not need retry.`,
    );
    lines.push(
      `            services.AddResilientPipeline<string, ${op.responseModelName}?>(`,
    );
    lines.push(`                ${pascalOp}ClientKeys.PIPELINE,`);
    lines.push(
      `                b => b.UseRetries(new RetryOptions<${op.responseModelName}?>`,
    );
    lines.push("                {");
    lines.push(
      `                    IsTransient = ex => ex is RpcException r && ProtoExtensions.IsTransientGrpcException(r),`,
    );
    lines.push("                }));");
    lines.push("");
  }

  // Register the client interface → impl.
  lines.push(
    `            services.AddTransient<${interfaceName}, ${implName}>();`,
  );
  lines.push(`            return services;`);
  lines.push("        }");
  lines.push("    }");
  lines.push("}");
  lines.push("");

  return { fileName, content: lines.join("\n") };
}

// ---------------------------------------------------------------------------
// Helper: per-op client keys constants class
// ---------------------------------------------------------------------------

/**
 * Emit the <Op>ClientKeys constants class for one op.
 * Carries the PIPELINE service key (for [FromKeyedServices]) and the
 * PIPELINE_KEY per-call key (for ExecuteAsync; singleflight uses this,
 * but the first-cut pipeline has no singleflight — the key is inert).
 */
export function emitClientKeys(
  opName: string,
  clientsNs: string,
  sourceSpec: string,
): EmittedFile {
  const banner = buildBanner(sourceSpec);
  const pascalOp = toPascal(opName);
  const className = `${pascalOp}ClientKeys`;

  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${clientsNs};`);
  lines.push("");

  lines.push(`/// <summary>`);
  lines.push(
    `/// Service-key constants for the <c>${pascalOp}</c> gRPC client resilience pipeline.`,
  );
  lines.push(`/// </summary>`);
  lines.push(`public static class ${className}`);
  lines.push("{");
  lines.push(
    `    /// <summary>DI service key for the keyed <c>ResilientPipeline&lt;TKey, TValue&gt;</c> registration.</summary>`,
  );
  lines.push(
    `    public const string PIPELINE = "${pascalOp}GrpcClientPipeline";`,
  );
  lines.push("");
  lines.push(`    /// <summary>`);
  lines.push(
    `    /// Per-call pipeline key passed to <c>ExecuteAsync</c>. Inert for retry-only`,
  );
  lines.push(
    `    /// pipelines; a singleflight-enabled pipeline must supply a per-request key`,
  );
  lines.push(`    /// instead to avoid wrongly deduplicating distinct calls.`);
  lines.push(`    /// </summary>`);
  lines.push(
    `    internal const string PIPELINE_KEY = "${pascalOp}GrpcClientCall";`,
  );
  lines.push("}");
  lines.push("");

  return { fileName: `${className}.g.cs`, content: lines.join("\n") };
}

// ---------------------------------------------------------------------------
// Private field-mapping helpers (inverse of grpc-service-emitter helpers)
// ---------------------------------------------------------------------------

/**
 * Build the expression for mapping one DTO input field to the proto request property.
 * INVERSE of buildProtoToDto in grpc-service-emitter.ts:
 *   byte[] → ByteString.CopyFrom(input.PascalName)
 *   enum   → input.PascalName.ToWire()  (DTO enum → proto member-name wire string)
 *   others → input.PascalName
 */
function buildClientDtoToProto(f: FieldInfo, source: string): string {
  const propName = toPascal(f.name);
  if (f.csType === "byte[]" || f.csType === "byte[]?")
    return `global::Google.Protobuf.ByteString.CopyFrom(${source}.${propName})`;
  if (f.enumRef !== undefined) return `${source}.${propName}.ToWire()`;
  return `${source}.${propName}`;
}

/**
 * Build the argument for mapping one proto data message field to the DTO constructor
 * in the NON-enum response path (the mapper returns a bare <Output>).
 * INVERSE of buildDtoToProto in grpc-service-emitter.ts:
 *   bytes → data.PascalName.ToByteArray()
 *   others → data.PascalName
 *
 * A RESPONSE enum field is handled by emitClientResponseMapperWithEnums instead
 * (the mapper returns D2Result<<Output>> and parses each enum via Parse<Enum>Wire);
 * this builder is only reached for the enum-free fields of an enum-free response.
 */
function buildClientProtoToDto(f: FieldInfo, source: string): string {
  const propName = toPascal(f.name);
  if (f.csType === "byte[]" || f.csType === "byte[]?")
    return `${source}.${propName}.ToByteArray()`;
  return `${source}.${propName}`;
}

/**
 * Emit the proto→DTO response mapper for a response carrying ≥1 enum field. Each
 * enum field's proto string is parsed via Parse<Enum>Wire (fail-loud on unknown);
 * the mapper short-circuits to ValidationFailed and otherwise constructs the DTO.
 * The mapper returns D2Result<<Output>> instead of a bare <Output>.
 *
 * This is the inbound CLIENT analogue of the server's emitRequestMapperWithEnums
 * (grpc-service-emitter.ts): a proto string the client cannot map back to the C#
 * enum is a 400 ValidationFailed — strict, NO fallback sentinel, symmetric with the
 * server-side request parse and the JSON JsonStringEnumConverter policy.
 */
function emitClientResponseMapperWithEnums(
  lines: string[],
  responseModelName: string,
  fqDtoOutput: string,
  responseFields: readonly FieldInfo[],
): void {
  lines.push(
    `        internal D2Result<${fqDtoOutput}> To${responseModelName}()`,
  );
  lines.push("        {");

  // Parse each enum field; bind its local on success, short-circuit on failure.
  const ctorArgs: string[] = [];
  for (const f of responseFields) {
    if (f.enumRef !== undefined) {
      const local = `${f.name}Result`;
      lines.push(
        `            var ${local} = string.Parse${f.enumRef.name}Wire(data.${toPascal(f.name)});`,
      );
      lines.push(`            if (!${local}.Success)`);
      lines.push(
        `                return D2Result<${fqDtoOutput}>.ValidationFailed(`,
      );
      lines.push(
        `                    ${local}.Messages, ${local}.InputErrors, ${local}.ErrorCode, ${local}.Category, ${local}.TraceId);`,
      );
      lines.push("");
      ctorArgs.push(`${local}.Data`);
    } else {
      ctorArgs.push(buildClientProtoToDto(f, "data"));
    }
  }

  lines.push(
    `            return D2Result<${fqDtoOutput}>.Ok(new ${fqDtoOutput}(${ctorArgs.join(", ")}));`,
  );
  lines.push("        }");
}

/**
 * Build the global::-rooted DTO type alias usings for the request + response model
 * names across all ops in the module. Mirrors the server emitter's alias convention:
 * a bare `<Op>Output` in a signature would otherwise be ambiguous between the DTO type
 * and the same-named proto data message. Returns sorted `using <Name> = global::<dtoNs>.<Name>;`
 * lines. Returns an empty array when the DTO namespace equals the client namespace
 * (the DTO types are then namespace-local and resolve without an alias).
 */
function collectDtoAliasUsings(
  ops: readonly GrpcClientOp[],
  clientsNs: string,
): string[] {
  const aliases = new Map<string, string>(); // typeName -> dtoNs
  for (const op of ops) {
    if (op.dtoCsharpNs === clientsNs) continue;
    aliases.set(op.requestModelName, op.dtoCsharpNs);
    aliases.set(op.responseModelName, op.dtoCsharpNs);
  }
  return [...aliases.entries()]
    .map(([name, ns]) => `using ${name} = global::${ns}.${name};`)
    .sort();
}

// ---------------------------------------------------------------------------
// Private utility
// ---------------------------------------------------------------------------

function lowerFirst(s: string): string {
  // The empty-string branch is defensive — every caller passes a non-empty gRPC service name.
  /* v8 ignore start — defensive: gRPC service names are never empty */
  return s.length === 0 ? s : s[0]!.toLowerCase() + s.slice(1);
  /* v8 ignore stop */
}
