// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { navigateProgram, NoTarget } from "@typespec/compiler";
import type { EmitContext, Model, Operation } from "@typespec/compiler";
import { getHttpOperation, getOperationVerb } from "@typespec/http";
import {
  D2_SERVED_BY_KEY,
  D2_GRPC_METHOD_KEY,
  D2_IN_PROCESS_KEY,
  D2_COMMAND_KEY,
  D2_QUERY_KEY,
  D2_INTERNAL_KEY,
  D2_SERVER_PUSH_KEY,
  D2_REQUIRE_ANY_SCOPE_KEY,
  D2_REQUIRE_ALL_SCOPES_KEY,
  D2_HARMLESS_KEY,
  D2_RATE_LIMIT_TIER_KEY,
  D2_CSRF_KEY,
  D2_IDEMPOTENT_KEY,
} from "@d2/typespec-decorators";
import type { IdempotentPayload } from "@d2/typespec-decorators";
import { emitGeneratedFile, resolveOutputPath } from "./lib/emit-file.js";
import { walkModel } from "./lib/model-walk.js";
import { emitCsharpDtos } from "./lib/csharp-dto-emitter.js";
import { emitTsDtos } from "./lib/ts-dto-emitter.js";
import { emitProto } from "./lib/proto-emitter.js";
import { emitGrpcService } from "./lib/grpc-service-emitter.js";
import type { GrpcDelegationTarget } from "./lib/grpc-service-emitter.js";
import { emitHandlerInterface } from "./lib/handler-interface-emitter.js";
import { emitFacade } from "./lib/facade-emitter.js";
import type { ExposedOp } from "./lib/facade-emitter.js";
import { emitGrpcClient, emitClientKeys } from "./lib/grpc-client-emitter.js";
import type { GrpcClientOp } from "./lib/grpc-client-emitter.js";
import { emitRoutePolicy } from "./lib/route-policy-emitter.js";
import { emitIdempotencyStoreSeam } from "./lib/idempotency-gate-emitter.js";
import type {
  DelegationTarget,
  HttpVerb,
  ScopePolicy,
} from "./lib/route-policy-emitter.js";
import { $lib } from "./lib.js";

// The $onEmit entry point drives six artifact families per tsp compile:
//
//   1. operations-manifest.json — operations smoke manifest from the initial
//      scaffold; kept so the operations-manifest integration test stays green
//      alongside DTO emission.
//   2. <Op>Input.g.cs + <Op>Output.g.cs — C# sealed-record DTO pairs for
//      every operation with a concrete input or output model. Namespace is
//      determined by exposure routing (see "Namespace routing" below).
//   3. I<Op>Handler.g.cs — C# handler interface per op (EVERY op — exposed
//      and internal). Lands in the app CQRS namespace.
//   4. <op>-dto.g.ts — TypeScript interface pair for the same operations.
//   5. <Service>_<method>.g.proto — proto3 service + message declarations for
//      every operation decorated with @d2GrpcMethod.
//   6. <Service>Service.g.cs + <Op>TransportMappers.g.cs — C# gRPC service
//      class (extends the Grpc.Tools base) + transport mappers for those ops.
//
// Namespace routing for C# DTOs:
//   EXPOSED op (@d2InProcess / @d2GrpcMethod / @d2ServerPush / @route):
//     DTOs → `csharp-clients-namespace` (Clients project).
//   INTERNAL op (@d2Internal):
//     DTOs → `<csharp-app-namespace-base>.<Category>.<PascalOp>`.
//   FIXTURE ops (no `csharp-app-namespace-base`):
//     DTOs → `csharp-namespace` (legacy fixture placeholder; unchanged).
//
//   The app-layer I<Op>Handler interface always lands in:
//     `<csharp-app-namespace-base>.<Category>.<PascalOp>`   (with emitUsing=false)
//   OR `grpc-service-namespace` for fixture ops              (with emitUsing=true).
//
// tspconfig options:
//   csharp-namespace       — fixture DTO namespace (legacy; kept for backward compat).
//   csharp-clients-namespace — Clients project namespace for exposed-op DTOs + façade.
//   csharp-app-namespace-base — app handler-namespace base; per-op CQRS path derived
//                               as <base>.<Category>.<PascalOp>.
//   proto-package          — proto3 package declaration.
//   proto-csharp-namespace — C# namespace for Grpc.Tools-generated proto types.
//   grpc-service-namespace — C# namespace for the generated gRPC service-impl class.

/** Shape of one operation entry in the smoke manifest. */
export interface ManifestOperation {
  readonly name: string;
  readonly servedBy: string | undefined;
  readonly hasGrpc: boolean;
  readonly inProcess: boolean;
}

/** Shape of the emitted operations-manifest.json. */
export interface OperationsManifest {
  readonly emitter: string;
  readonly operationCount: number;
  readonly operations: readonly ManifestOperation[];
}

/**
 * TypeSpec emitter entry point. Called once per tsp compile when this package
 * appears in the consumer's tspconfig.yaml `emit:` list.
 */
export async function $onEmit(context: EmitContext): Promise<void> {
  const program = context.program;
  const ops: ManifestOperation[] = [];

  // Read emitter options; fall back to safe placeholders when absent.
  const rawOptions = (context.options ?? {}) as Record<string, unknown>;
  const csNamespace =
    typeof rawOptions["csharp-namespace"] === "string" &&
    rawOptions["csharp-namespace"].length > 0
      ? rawOptions["csharp-namespace"]
      : "D2.Generated";
  // Clients namespace for exposed-op DTOs + façade interface (façade-layer routing).
  const csClientsNamespace =
    typeof rawOptions["csharp-clients-namespace"] === "string" &&
    rawOptions["csharp-clients-namespace"].length > 0
      ? rawOptions["csharp-clients-namespace"]
      : undefined;
  // App handler-namespace base; per-op CQRS path =
  // <base>.<Category>.<PascalOp>. When absent, falls back to fixture mode.
  const csAppNamespaceBase =
    typeof rawOptions["csharp-app-namespace-base"] === "string" &&
    rawOptions["csharp-app-namespace-base"].length > 0
      ? rawOptions["csharp-app-namespace-base"]
      : undefined;
  const protoPackage =
    typeof rawOptions["proto-package"] === "string" &&
    rawOptions["proto-package"].length > 0
      ? rawOptions["proto-package"]
      : "d2.generated.v1";
  const protoCsharpNs =
    typeof rawOptions["proto-csharp-namespace"] === "string" &&
    rawOptions["proto-csharp-namespace"].length > 0
      ? rawOptions["proto-csharp-namespace"]
      : "D2.Generated.Protos.V1";
  const grpcServiceNs =
    typeof rawOptions["grpc-service-namespace"] === "string" &&
    rawOptions["grpc-service-namespace"].length > 0
      ? rawOptions["grpc-service-namespace"]
      : "D2.Generated.Grpc";

  // Collect exposed ops per module (grouped by @d2ServedBy) for the façade emitter.
  // Façade is one-per-module so it is emitted AFTER the per-op walk gathers all ops.
  const exposedOpsByModule = new Map<string, ExposedOp[]>();

  // Collect gRPC-method ops per module (grouped by @d2ServedBy) for the gRPC client emitter.
  // The client emitter emits one interface + impl + mappers + DI-ext per module, after the walk.
  // Only populated when csClientsNamespace is configured (real-module mode).
  const grpcOpsByModule = new Map<string, GrpcClientOp[]>();

  // Track which registration namespaces contain at least one idempotent route.
  // The idempotency-store seam is emitted ONCE per namespace (mirrors the
  // policy-markers approach) after the per-op walk completes.
  const idempotentNamespaces = new Set<string>();
  // Track the last specHint per namespace for the seam banner.
  const idempotentNamespaceSpec = new Map<string, string>();

  navigateProgram(program, {
    operation(op: Operation) {
      ops.push({
        name: op.name,
        servedBy: program.stateMap(D2_SERVED_BY_KEY).get(op) as
          | string
          | undefined,
        hasGrpc: program.stateMap(D2_GRPC_METHOD_KEY).get(op) !== undefined,
        inProcess: program.stateMap(D2_IN_PROCESS_KEY).get(op) === true,
      });

      // Derive the source spec path hint for the banner.
      // TypeSpec exposes the file via op.file / op.node?.file — use a relative
      // path if available; fall back to the operation name as a hint.
      const specHint = tryGetSpecPath(op) ?? `<typespec op: ${op.name}>`;

      // Resolve input model: TypeSpec op parameters is a Model whose properties
      // are the named params. When the op has no params the properties map is empty.
      //
      // When the op has a single named param (e.g. `op sign(input: SignInput)`),
      // the parameters model has one property `input` whose type is `SignInput`.
      // We unwrap that to use the model directly so field-level @d2Redact state
      // (stored on the inner model's properties) is accessible.
      const rawParams = op.parameters as Model | undefined;
      const inputModel =
        rawParams !== undefined
          ? (resolveSingleNamedParam(rawParams) ?? rawParams)
          : undefined;
      const outputModel =
        op.returnType?.kind === "Model" ? (op.returnType as Model) : undefined;

      // Determine exposure and category for namespace routing.
      const isExposed = resolveIsExposed(program, op);
      const isInternal = program.stateMap(D2_INTERNAL_KEY).get(op) === true;
      const category = resolveCategory(program, op);

      // Resolve the DTO namespace for C# emission:
      //   - Real-module exposed op + Clients namespace configured → Clients ns.
      //   - Real-module internal op + app-namespace-base configured → app CQRS ns.
      //   - Fixture op (no app-namespace-base) → fixture csNamespace.
      const dtoCsNamespace = resolveDtoNamespace(
        isExposed,
        isInternal,
        category,
        op.name,
        csNamespace,
        csClientsNamespace,
        csAppNamespaceBase,
        program,
        op,
      );

      // Emit C# + TS DTOs only when we have at least one side with a concrete model.
      // Returns true when emission succeeded (no walk errors); the handler-interface
      // emitter is gated on the same success so error paths produce zero .g.cs output.
      let dtoEmitSucceeded = false;
      if (inputModel !== undefined || outputModel !== undefined) {
        dtoEmitSucceeded = emitDtoPair(
          context,
          program,
          op.name,
          dtoCsNamespace,
          specHint,
          inputModel,
          outputModel,
        );
      }

      // Emit I<Op>Handler.g.cs for EVERY op (exposed and internal).
      // Gated on dtoEmitSucceeded so unmapped-scalar / unsupported-property-type
      // errors do not produce a partial handler-interface file with broken type refs.
      // The handler interface always lands in the app CQRS namespace
      // (or the fixture grpcServiceNs when csAppNamespaceBase is absent).
      if (dtoEmitSucceeded) {
        const handlerNs = resolveHandlerNamespace(
          category,
          op.name,
          grpcServiceNs,
          csAppNamespaceBase,
        );
        // emitUsing=false when a real app project has GlobalUsings supplying the import;
        // emitUsing=true for fixture namespaces that have no such global using.
        const handlerEmitUsing = csAppNamespaceBase === undefined;
        const inputTypeName =
          (inputModel?.name?.length ?? 0) > 0
            ? inputModel!.name
            : `${toPascalFromCamel(op.name)}Input`;
        const outputTypeName =
          (outputModel?.name?.length ?? 0) > 0
            ? outputModel!.name
            : `${toPascalFromCamel(op.name)}Output`;
        // Pass the DTO namespace when it differs from the handler namespace so the
        // emitter can add a per-file using for the Clients types (exposed ops only).
        const dtoNsForHandler =
          dtoCsNamespace !== handlerNs ? dtoCsNamespace : undefined;
        const handlerFile = emitHandlerInterface(
          op.name,
          handlerNs,
          inputTypeName,
          outputTypeName,
          handlerEmitUsing,
          specHint,
          dtoNsForHandler,
        );
        const handlerPath = resolveOutputPath(context, handlerFile.fileName);
        void emitGeneratedFile(program, handlerPath, handlerFile.content);

        // Collect exposed ops for the façade emitter (one-per-module, emitted after the walk).
        // Only collect when we have a real-module context (csAppNamespaceBase + csClientsNamespace)
        // and the op is exposed (not @d2Internal). Fixture ops (no csAppNamespaceBase) are skipped.
        // Only collect ops that have a known CQRS category — ops missing @d2Command / @d2Query
        // already fired D2TSP003 and must not appear in the façade interface.
        if (
          isExposed &&
          category !== undefined &&
          csAppNamespaceBase !== undefined &&
          csClientsNamespace !== undefined
        ) {
          const servedBy = program.stateMap(D2_SERVED_BY_KEY).get(op) as
            | string
            | undefined;
          if (servedBy !== undefined && servedBy.length > 0) {
            const existing = exposedOpsByModule.get(servedBy) ?? [];
            existing.push({
              opName: op.name,
              inputTypeName,
              outputTypeName,
              sourceSpec: specHint,
              category: category as "Commands" | "Queries",
            });
            exposedOpsByModule.set(servedBy, existing);
          }
        }
      }

      // Emit proto + gRPC service impl only for ops carrying @d2GrpcMethod.
      const grpcPayload = program.stateMap(D2_GRPC_METHOD_KEY).get(op) as
        | { service: string; method: string; streaming: string }
        | undefined;
      if (
        grpcPayload !== undefined &&
        (inputModel !== undefined || outputModel !== undefined)
      ) {
        // Compute the delegation target for gRPC — same rule as the route emitter:
        // @d2InProcess → façade; else → I<Op>Handler.
        const grpcInProcess =
          program.stateMap(D2_IN_PROCESS_KEY).get(op) === true;
        const grpcServedBy = program.stateMap(D2_SERVED_BY_KEY).get(op) as
          | string
          | undefined;
        const grpcPascalOp = toPascalFromCamel(op.name);

        let grpcDelegationTarget: GrpcDelegationTarget;
        if (
          grpcInProcess &&
          grpcServedBy !== undefined &&
          grpcServedBy.length > 0
        ) {
          // Façade delegation — use the same naming logic as the route emitter.
          const facadeTypeName =
            csAppNamespaceBase !== undefined && csClientsNamespace !== undefined
              ? `I${grpcServedBy}Api`
              : `I${grpcServedBy}SignerFacade`;
          const facadeNs =
            csAppNamespaceBase !== undefined && csClientsNamespace !== undefined
              ? csClientsNamespace
              : `${grpcServiceNs}.Facade`;
          grpcDelegationTarget = {
            kind: "facade",
            typeName: facadeTypeName,
            methodName: `${grpcPascalOp}Async`,
            targetNamespace: facadeNs,
          };
        } else {
          // Handler delegation.
          grpcDelegationTarget = {
            kind: "handler",
            typeName: `I${grpcPascalOp}Handler`,
            methodName: "HandleAsync",
            targetNamespace: undefined,
          };
        }

        emitProtoAndGrpcService(
          context,
          program,
          op.name,
          grpcPayload,
          protoPackage,
          protoCsharpNs,
          grpcServiceNs,
          dtoCsNamespace,
          specHint,
          inputModel,
          outputModel,
          grpcDelegationTarget,
        );

        // Collect this op for the per-module gRPC client emitter (real-module only).
        // Only when csClientsNamespace is configured — fixture ops skip client emit.
        // Walk the models again (walkModel is pure; no side effects from the second call).
        if (
          csClientsNamespace !== undefined &&
          grpcServedBy !== undefined &&
          grpcServedBy.length > 0
        ) {
          // Re-walk the (already-validated) models to collect the client field lists.
          // emitProtoAndGrpcService ran first and returns early on any walk error, so by
          // here the models are known-valid; the error sink + the anonymous-model name
          // fallbacks are defensive and exercised only by malformed input that the proto
          // block already rejected (covered there). The DTO-collection block above mirrors
          // this exact shape.
          /* v8 ignore start — defensive: walk error sink + anonymous-model name fallbacks (proto block validated first) */
          const clientInputWalk =
            inputModel !== undefined
              ? walkModel(program, inputModel, () => undefined)
              : { fields: [], nestedModels: [], nestedEnums: [] };
          const clientOutputWalk =
            outputModel !== undefined
              ? walkModel(program, outputModel, () => undefined)
              : { fields: [], nestedModels: [], nestedEnums: [] };

          const requestModelName =
            (inputModel?.name?.length ?? 0) > 0
              ? inputModel!.name
              : `${grpcPascalOp}Input`;
          const responseModelName =
            (outputModel?.name?.length ?? 0) > 0
              ? outputModel!.name
              : `${grpcPascalOp}Output`;
          /* v8 ignore stop */

          const clientOp: GrpcClientOp = {
            opName: op.name,
            grpcService: grpcPayload.service,
            grpcMethod: grpcPayload.method,
            protoCsharpNs,
            dtoCsharpNs: dtoCsNamespace,
            sourceSpec: specHint,
            requestModelName,
            requestFields: clientInputWalk.fields,
            responseModelName,
            responseFields: clientOutputWalk.fields,
          };

          const existing = grpcOpsByModule.get(grpcServedBy) ?? [];
          existing.push(clientOp);
          grpcOpsByModule.set(grpcServedBy, existing);
        }
      }

      // Emit REST route registration for ops carrying @route and not @d2Internal.
      // getHttpOperation yields [httpOp, diags]; a verb+path means the op has @route.
      // Skip if the op is @d2Internal (the decorator layer forbids internal + route,
      // but guard defensively here for emitter robustness).
      if (!isInternal) {
        emitRouteIfPresent(
          context,
          program,
          op,
          isExposed,
          dtoCsNamespace,
          grpcServiceNs,
          csAppNamespaceBase,
          csClientsNamespace,
          specHint,
          inputModel,
          outputModel,
          idempotentNamespaces,
          idempotentNamespaceSpec,
        );
      }
    },
  });

  // ---- Façade emitter — one interface + impl + DI-ext per module (after the per-op walk) ----
  // The façade is one-per-module; it groups ALL exposed ops for a @d2ServedBy value and emits
  // three files: the interface (in Clients), the impl, and the DI extension (both in app/).
  // Only fires when both csClientsNamespace and csAppNamespaceBase are configured.
  if (csClientsNamespace !== undefined && csAppNamespaceBase !== undefined) {
    for (const [moduleName, moduleOps] of exposedOpsByModule) {
      // Derive the app namespace root from the namespace base (strip the per-op CQRS suffix).
      // The base is e.g. "D2.Edge.KeyCustodian.App.Application.Handlers" →
      // app namespace root = "D2.Edge.KeyCustodian.App.Application".
      const appNsRoot = csAppNamespaceBase.replace(/\.Handlers$/, "");
      const facadeFiles = emitFacade(
        moduleName,
        moduleOps,
        csClientsNamespace,
        appNsRoot,
      );
      for (const f of facadeFiles) {
        const facadePath = resolveOutputPath(context, f.fileName);
        void emitGeneratedFile(program, facadePath, f.content);
      }
    }
  }

  // ---- gRPC client emitter — one interface + impl + mappers + DI-ext per module ----
  // Fires after the per-op walk collects all @d2GrpcMethod ops per module.
  // Only in real-module mode (csClientsNamespace configured).
  if (csClientsNamespace !== undefined) {
    for (const [moduleName, moduleOps] of grpcOpsByModule) {
      const clientFiles = emitGrpcClient(
        moduleName,
        moduleOps,
        csClientsNamespace,
      );
      for (const f of clientFiles) {
        const clientPath = resolveOutputPath(context, f.fileName);
        void emitGeneratedFile(program, clientPath, f.content);
      }
      // Per-op client keys constants (<Op>ClientKeys.g.cs).
      for (const clientOp of moduleOps) {
        const keysFile = emitClientKeys(
          clientOp.opName,
          csClientsNamespace,
          clientOp.sourceSpec,
        );
        const keysPath = resolveOutputPath(context, keysFile.fileName);
        void emitGeneratedFile(program, keysPath, keysFile.content);
      }
    }
  }

  // ---- Idempotency-store seam — one per registration namespace (after the per-op walk) ------
  // Emitted for every namespace that contains at least one idempotent routed op.
  // idempotentNamespaces and idempotentNamespaceSpec are updated together in the per-op
  // walk (lines 835-836), so has(ns) is always true here; the guard is belt-and-suspenders.
  // idempotentNamespaceSpec is populated only when idempotentNamespaces.add(ns) also fires
  // (both updates are in the same if-block in the per-op walk). Iterate spec entries directly —
  // the has(ns) guard is redundant and would create an unreachable false branch.
  for (const [ns, specHint] of idempotentNamespaceSpec) {
    const seamFile = emitIdempotencyStoreSeam(ns, specHint);
    const seamPath = resolveOutputPath(context, seamFile.fileName);
    void emitGeneratedFile(program, seamPath, seamFile.content);
  }

  // ---- Smoke manifest (kept so the operations-manifest integration test stays green) --------
  const manifest: OperationsManifest = {
    emitter: "@d2/typespec-emitters",
    operationCount: ops.length,
    operations: ops,
  };

  const manifestPath = resolveOutputPath(context, "operations-manifest.json");
  await emitGeneratedFile(
    program,
    manifestPath,
    JSON.stringify(manifest, null, 2),
  );
}

// ---------------------------------------------------------------------------
// Namespace routing helpers
// ---------------------------------------------------------------------------

/**
 * Resolve whether an operation is "exposed" — carries any of the four
 * transport decorators that make it reachable across a boundary.
 * "Internal" ops (@d2Internal) are the complement; the decorator layer
 * enforces mutual exclusion.
 */
function resolveIsExposed(
  program: Parameters<typeof walkModel>[0],
  op: Operation,
): boolean {
  const hasInProcess = program.stateMap(D2_IN_PROCESS_KEY).get(op) === true;
  const hasGrpc = program.stateMap(D2_GRPC_METHOD_KEY).get(op) !== undefined;
  const hasServerPush =
    program.stateMap(D2_SERVER_PUSH_KEY).get(op) !== undefined;
  // @route is not in the state-map directly — check via @typespec/http's getHttpOperation.
  // The exposure decorators are @d2InProcess / @d2GrpcMethod / @d2ServerPush.
  // @route is handled by the route emitter, not this pass. isExposed is therefore a union of the three keys.
  return hasInProcess || hasGrpc || hasServerPush;
}

/**
 * Resolve the CQRS category for an operation.
 * Returns "Commands" for @d2Command ops, "Queries" for @d2Query ops,
 * or throws D2TSP003 via the returned error string (caller must propagate).
 */
function resolveCategory(
  program: Parameters<typeof walkModel>[0],
  op: Operation,
): "Commands" | "Queries" | undefined {
  const isCommand = program.stateMap(D2_COMMAND_KEY).get(op) === true;
  const isQuery = program.stateMap(D2_QUERY_KEY).get(op) === true;
  if (isCommand && !isQuery) return "Commands";
  if (isQuery && !isCommand) return "Queries";
  // Neither or both — D2TSP003 defensive guard.
  return undefined;
}

/**
 * Resolve the C# namespace for emitting DTOs.
 *
 * Routing table:
 *   - csAppNamespaceBase present + isExposed + csClientsNamespace present
 *     → Clients namespace (exposed ops' DTOs live in Clients per D-c).
 *   - csAppNamespaceBase present + isInternal + category resolved
 *     → `<base>.<Category>.<PascalOp>` (internal-op DTOs in app CQRS ns).
 *   - No csAppNamespaceBase (fixture mode)
 *     → csNamespace (legacy fixture placeholder).
 *   - csAppNamespaceBase present + category missing
 *     → falls back to csNamespace (emitter reports D2TSP003 separately via
 *       resolveCategory returning undefined; here we just avoid a crash).
 */
function resolveDtoNamespace(
  isExposed: boolean,
  isInternal: boolean,
  category: "Commands" | "Queries" | undefined,
  opName: string,
  csNamespace: string,
  csClientsNamespace: string | undefined,
  csAppNamespaceBase: string | undefined,
  program: Parameters<typeof walkModel>[0],
  op: Operation,
): string {
  if (csAppNamespaceBase === undefined)
    // Fixture mode — use the legacy csharp-namespace.
    return csNamespace;

  if (isExposed && csClientsNamespace !== undefined) return csClientsNamespace;

  if (isInternal || !isExposed) {
    if (category !== undefined) {
      const pascalOp = toPascalFromCamel(opName);
      return `${csAppNamespaceBase}.${category}.${pascalOp}`;
    }
    // Missing category — D2TSP003 (loud; report and fall back to avoid crash).
    $lib.reportDiagnostic(program, {
      code: "missing-cqrs-category",
      format: { op: opName },
      target: op,
    });
    return csNamespace;
  }

  // Exposed but no Clients namespace configured — fall back to fixture ns.
  return csNamespace;
}

/**
 * Resolve the C# namespace for emitting the I<Op>Handler interface.
 *
 * For real-module ops (csAppNamespaceBase present + category resolved):
 *   `<base>.<Category>.<PascalOp>`
 * For fixture ops (no csAppNamespaceBase):
 *   `grpcServiceNs` (the fixture gRPC namespace, matches the existing fixture
 *   pattern for ISignHandler in D2.Edge.Tests.TypeSpecGrpc.Generated).
 */
function resolveHandlerNamespace(
  category: "Commands" | "Queries" | undefined,
  opName: string,
  grpcServiceNs: string,
  csAppNamespaceBase: string | undefined,
): string {
  if (csAppNamespaceBase !== undefined && category !== undefined) {
    const pascalOp = toPascalFromCamel(opName);
    return `${csAppNamespaceBase}.${category}.${pascalOp}`;
  }
  return grpcServiceNs;
}

/** Convert lowerCamelCase op name to PascalCase (local copy to avoid circular import). */
function toPascalFromCamel(s: string): string {
  if (s.length === 0) return s;
  return s[0]!.toUpperCase() + s.slice(1);
}

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

function emitDtoPair(
  context: EmitContext,
  program: Parameters<typeof walkModel>[0],
  opName: string,
  csNamespace: string,
  specHint: string,
  inputModel: Model | undefined,
  outputModel: Model | undefined,
): boolean {
  const errors: string[] = [];
  const onError = (
    code:
      | "unmapped-scalar"
      | "unsupported-property-type"
      | "unsupported-union-shape",
    message: string,
  ): void => {
    errors.push(message);
    // Report a TypeSpec diagnostic so tsp compile exits non-zero (error severity).
    // The `message` string already contains the full human-readable description;
    // we pass it through the format parameter that matches the paramMessage template.
    if (code === "unmapped-scalar")
      $lib.reportDiagnostic(program, {
        code: "unmapped-scalar",
        format: { scalar: message },
        target: NoTarget,
      });
    else if (code === "unsupported-union-shape")
      $lib.reportDiagnostic(program, {
        code: "unsupported-union-shape",
        format: { property: message },
        target: NoTarget,
      });
    else
      // unsupported-property-type: split the message to extract kind + property context.
      $lib.reportDiagnostic(program, {
        code: "unsupported-property-type",
        format: { kind: "unsupported", property: message },
        target: NoTarget,
      });
  };

  // Walk input model (empty walk when op has no params).
  const inputWalk =
    inputModel !== undefined
      ? walkModel(program, inputModel, onError)
      : { fields: [], nestedModels: [], nestedEnums: [] };

  // Walk output model (empty when op returns void).
  const outputWalk =
    outputModel !== undefined
      ? walkModel(program, outputModel, onError)
      : { fields: [], nestedModels: [], nestedEnums: [] };

  if (errors.length > 0) return false; // Diagnostics already reported; don't emit partial files.

  // ---- C# DTO emission ----
  const csFiles = emitCsharpDtos(
    opName,
    csNamespace,
    specHint,
    inputWalk.fields,
    outputWalk.fields,
    outputWalk.nestedModels,
    inputWalk.nestedEnums,
    outputWalk.nestedEnums,
  );

  for (const f of csFiles) {
    const path = resolveOutputPath(context, f.fileName);
    void emitGeneratedFile(program, path, f.content);
  }

  // ---- TS DTO emission ----
  const tsFile = emitTsDtos(
    opName,
    specHint,
    inputWalk.fields,
    outputWalk.fields,
    outputWalk.nestedModels,
    inputWalk.nestedEnums,
    outputWalk.nestedEnums,
  );

  const tsPath = resolveOutputPath(context, tsFile.fileName);
  void emitGeneratedFile(program, tsPath, tsFile.content);
  return true;
}

function emitProtoAndGrpcService(
  context: EmitContext,
  program: Parameters<typeof walkModel>[0],
  opName: string,
  grpc: { service: string; method: string; streaming: string },
  protoPackage: string,
  protoCsharpNs: string,
  grpcServiceNs: string,
  dtoCsharpNs: string,
  specHint: string,
  inputModel: Model | undefined,
  outputModel: Model | undefined,
  delegationTarget?: GrpcDelegationTarget,
): void {
  const errors: string[] = [];
  const onError = (
    code:
      | "unmapped-scalar"
      | "unsupported-property-type"
      | "unsupported-union-shape"
      | "invalid-streaming-mode",
    message: string,
  ): void => {
    errors.push(message);
    if (code === "unmapped-scalar")
      $lib.reportDiagnostic(program, {
        code: "unmapped-scalar",
        format: { scalar: message },
        target: NoTarget,
      });
    else if (code === "invalid-streaming-mode")
      $lib.reportDiagnostic(program, {
        code: "unmapped-scalar",
        format: { scalar: message },
        target: NoTarget,
      });
    else if (code === "unsupported-union-shape")
      $lib.reportDiagnostic(program, {
        code: "unsupported-union-shape",
        format: { property: message },
        target: NoTarget,
      });
    else
      $lib.reportDiagnostic(program, {
        code: "unsupported-property-type",
        format: { kind: "unsupported", property: message },
        target: NoTarget,
      });
  };

  const inputWalk =
    inputModel !== undefined
      ? walkModel(program, inputModel, onError)
      : { fields: [], nestedModels: [], nestedEnums: [] };

  const outputWalk =
    outputModel !== undefined
      ? walkModel(program, outputModel, onError)
      : { fields: [], nestedModels: [], nestedEnums: [] };

  if (errors.length > 0) return;

  // ---- proto emission ----
  // Proto message names follow the <Method>Request / <Method>Response convention
  // (matches existing hand-authored protos in contracts/protos/) and are distinct
  // from the DTO model names (<Op>Input / <Op>Output), eliminating any name collision.
  const protoRequestName = `${grpc.method}Request`;
  const protoResponseName = `${grpc.method}Response`;

  // DTO model names come from the TypeSpec model name (e.g. SignInput / SignOutput).
  const dtoRequestName = inputModel?.name ?? `${grpc.method}Input`;
  const dtoResponseName = outputModel?.name ?? `${grpc.method}Output`;

  const protoFile = emitProto(
    opName,
    grpc.service,
    grpc.method,
    grpc.streaming,
    protoPackage,
    protoCsharpNs,
    specHint,
    protoRequestName,
    inputWalk.fields,
    // The proto DATA message is named after the DTO output model (<Op>Output) —
    // distinct from the <Method>Response envelope wrapper (proto-emitter derives
    // that name itself). Passing protoResponseName here would name the data message
    // <Method>Response too, colliding with the wrapper and failing protoc.
    dtoResponseName,
    outputWalk.fields,
    outputWalk.nestedModels,
    onError,
  );
  if (errors.length > 0 || protoFile === undefined) return;

  const protoPath = resolveOutputPath(context, protoFile.fileName);
  void emitGeneratedFile(program, protoPath, protoFile.content);

  // ---- gRPC service + mapper emission ----
  const [serviceFile, mapperFile] = emitGrpcService(
    opName,
    grpc.service,
    grpc.method,
    protoCsharpNs,
    grpcServiceNs,
    dtoCsharpNs,
    specHint,
    protoRequestName,
    protoResponseName,
    dtoRequestName,
    inputWalk.fields,
    dtoResponseName,
    outputWalk.fields,
    delegationTarget,
  );

  const servicePath = resolveOutputPath(context, serviceFile.fileName);
  void emitGeneratedFile(program, servicePath, serviceFile.content);

  const mapperPath = resolveOutputPath(context, mapperFile.fileName);
  void emitGeneratedFile(program, mapperPath, mapperFile.content);
}

/**
 * Emit the REST route registration for one operation if it carries @route.
 *
 * Calls getHttpOperation to detect the verb+path. Surfaces D2TSP004 (missing
 * auth intent), D2TSP005 (unsupported verb), and D2TSP006 (@d2Idempotent
 * without a @route) as error diagnostics. Skips the op entirely when
 * getHttpOperation yields no HTTP binding.
 */
function emitRouteIfPresent(
  context: EmitContext,
  program: Parameters<typeof walkModel>[0],
  op: Operation,
  isExposed: boolean,
  dtoCsNamespace: string,
  grpcServiceNs: string,
  csAppNamespaceBase: string | undefined,
  csClientsNamespace: string | undefined,
  specHint: string,
  inputModel: Model | undefined,
  outputModel: Model | undefined,
  idempotentNamespaces: Set<string>,
  idempotentNamespaceSpec: Map<string, string>,
): void {
  // Read the @d2Idempotent payload (if any) before the route check.
  // D2TSP006: if @d2Idempotent is present but no @route exists, fail loud.
  const idempotentPayload = program.stateMap(D2_IDEMPOTENT_KEY).get(op) as
    | IdempotentPayload
    | undefined;

  // Use getOperationVerb to detect if the op has an explicit HTTP verb decorator.
  // getOperationVerb returns undefined when no @get/@post/@put/@delete/@patch/@head
  // is present — that is the correct signal that this op has no @route.
  const explicitVerb = getOperationVerb(
    program as Parameters<typeof getOperationVerb>[0],
    op,
  );
  if (explicitVerb === undefined) {
    // D2TSP006 — @d2Idempotent without @route: fail loud.
    if (idempotentPayload !== undefined) {
      $lib.reportDiagnostic(program, {
        code: "idempotent-requires-route",
        format: { op: op.name },
        target: op,
      });
    }
    // No explicit verb decorator → no @route → skip (e.g. getJwks).
    return;
  }

  // Supported verbs for Minimal API Map* calls.
  const supportedVerbs: readonly string[] = [
    "get",
    "post",
    "put",
    "delete",
    "patch",
  ];
  if (!supportedVerbs.includes(explicitVerb)) {
    $lib.reportDiagnostic(program, {
      code: "unsupported-http-verb",
      format: { op: op.name, verb: explicitVerb },
      target: op,
    });
    return;
  }

  // Get full HTTP operation to extract the resolved path.
  const [httpOp, diags] = getHttpOperation(
    program as Parameters<typeof getHttpOperation>[0],
    op,
  );

  // Surface any error diagnostics from getHttpOperation.
  for (const d of diags) {
    if (d.severity === "error") {
      $lib.reportDiagnostic(program, {
        code: "unmapped-scalar",
        format: {
          scalar: `getHttpOperation error on '${op.name}': ${String(d.message)}`,
        },
        target: NoTarget,
      });
    }
  }

  const verb = explicitVerb as HttpVerb;
  const routePath = httpOp.path;

  // Resolve scope/harmless policy.
  const anyScopes = program.stateMap(D2_REQUIRE_ANY_SCOPE_KEY).get(op) as
    | string[]
    | undefined;
  const allScopes = program.stateMap(D2_REQUIRE_ALL_SCOPES_KEY).get(op) as
    | string[]
    | undefined;
  const harmless = program.stateMap(D2_HARMLESS_KEY).get(op) === true;

  let scopePolicy: ScopePolicy;
  if (anyScopes !== undefined && anyScopes.length > 0)
    scopePolicy = { kind: "any", scopes: anyScopes };
  else if (allScopes !== undefined && allScopes.length > 0)
    scopePolicy = { kind: "all", scopes: allScopes };
  else if (harmless) scopePolicy = { kind: "harmless" };
  else {
    // D2TSP004 — deny-by-default: routed op with no auth intent.
    $lib.reportDiagnostic(program, {
      code: "route-missing-auth-intent",
      format: { op: op.name },
      target: op,
    });
    return;
  }

  // Resolve rate-tier and CSRF markers.
  const rateTierRaw = program.stateMap(D2_RATE_LIMIT_TIER_KEY).get(op) as
    | { tier: string }
    | string
    | undefined;
  const rateTier =
    rateTierRaw !== undefined
      ? typeof rateTierRaw === "string"
        ? rateTierRaw
        : rateTierRaw.tier
      : undefined;

  const csrfRaw = program.stateMap(D2_CSRF_KEY).get(op) as
    | { posture: string }
    | string
    | undefined;
  const csrf =
    csrfRaw !== undefined
      ? typeof csrfRaw === "string"
        ? csrfRaw
        : csrfRaw.posture
      : undefined;

  // Resolve the delegation target.
  const inProcess = program.stateMap(D2_IN_PROCESS_KEY).get(op) === true;
  const servedBy = program.stateMap(D2_SERVED_BY_KEY).get(op) as
    | string
    | undefined;
  const pascalOp = toPascalFromCamel(op.name);

  let delegationTarget: DelegationTarget;
  let delegationTargetNamespace: string;

  if (inProcess && servedBy !== undefined && servedBy.length > 0) {
    // Facade delegation: the fixture façade interface name is I<ServedBy>SignerFacade
    // (for the sign fixture, this is IKeyCustodianSignerFacade — the fixture-specific
    // naming that avoids collision with the real IKeyCustodianApi).
    // In fixture mode (no csAppNamespaceBase), use the fixture gRPC namespace.
    // In real-module mode, use the clients namespace.
    const facadeTypeName =
      csAppNamespaceBase !== undefined && csClientsNamespace !== undefined
        ? `I${servedBy}Api`
        : `I${servedBy}SignerFacade`;
    const facadeNs =
      csAppNamespaceBase !== undefined && csClientsNamespace !== undefined
        ? csClientsNamespace
        : `${grpcServiceNs}.Facade`;
    delegationTarget = {
      kind: "facade",
      typeName: facadeTypeName,
      methodName: `${pascalOp}Async`,
    };
    delegationTargetNamespace = facadeNs;
  } else {
    // Handler delegation.
    delegationTarget = {
      kind: "handler",
      typeName: `I${pascalOp}Handler`,
      methodName: "HandleAsync",
    };
    // Handler namespace follows the same logic as resolveHandlerNamespace.
    const category = resolveCategory(program, op);
    delegationTargetNamespace =
      csAppNamespaceBase !== undefined && category !== undefined
        ? `${csAppNamespaceBase}.${category}.${pascalOp}`
        : grpcServiceNs;
  }

  // Resolve the registration namespace — in fixture mode use the gRPC service ns
  // (all transport output is fixture-validated per FLAG f / R7.10).
  const registrationNs =
    csAppNamespaceBase !== undefined
      ? `${csAppNamespaceBase.replace(/\.Handlers$/, "")}.Routes`
      : grpcServiceNs;

  const inputTypeName =
    (inputModel?.name?.length ?? 0) > 0 ? inputModel!.name : `${pascalOp}Input`;
  const outputTypeName =
    (outputModel?.name?.length ?? 0) > 0
      ? outputModel!.name
      : `${pascalOp}Output`;

  void isExposed; // exposure is already validated by the caller

  // Map the idempotency payload's lowerCamel field names to PascalCase C# property names.
  // walkModel populates FieldInfo.csName as PascalCase; here we do a simple camel→Pascal
  // conversion (matching the convention used everywhere in the emitter fleet) because
  // the inputWalk isn't passed into emitRouteIfPresent. The decorator guarantees valid
  // field names; we map defensively (empty string → would throw in buildIdempotencyGate).
  let idempotencyConfig:
    | {
        keySource: "header" | "derived";
        ttlSeconds: number;
        fields: readonly string[];
      }
    | undefined;
  if (idempotentPayload !== undefined) {
    const pascalFields = idempotentPayload.fields.map(
      /* v8 ignore next 1 — decorator guarantees non-empty; defensive guard for belt-and-suspenders */
      (f) => (f.length === 0 ? f : f[0]!.toUpperCase() + f.slice(1)),
    );
    idempotencyConfig = {
      keySource: idempotentPayload.keySource as "header" | "derived",
      ttlSeconds: idempotentPayload.ttlSeconds,
      fields: pascalFields,
    };
    // Track this namespace for seam emission.
    idempotentNamespaces.add(registrationNs);
    idempotentNamespaceSpec.set(registrationNs, specHint);
  }

  const routeFile = emitRoutePolicy({
    opName: op.name,
    verb,
    routePath,
    delegationTarget,
    delegationTargetNamespace,
    inputTypeName,
    outputTypeName,
    dtoNamespace: dtoCsNamespace,
    scopePolicy,
    rateTier,
    csrf,
    idempotency: idempotencyConfig,
    registrationNamespace: registrationNs,
    sourceSpec: specHint,
  });

  const routePath2 = resolveOutputPath(context, routeFile.fileName);
  void emitGeneratedFile(program, routePath2, routeFile.content);
}

/**
 * Attempt to extract a human-readable source-spec path from the operation node.
 * Returns undefined when the TypeSpec version does not expose the file path.
 */
function tryGetSpecPath(op: Operation): string | undefined {
  const node = op.node as { file?: { path?: string } } | undefined;
  return node?.file?.path;
}

/**
 * When an op has exactly one named parameter whose type is a Model (e.g.
 * `op sign(input: SignInput): SignOutput`), the TypeSpec parameters model
 * wraps that single model in an anonymous container. Unwrap it so the emitter
 * walks the inner model's properties (where @d2Redact state is stored).
 *
 * Returns undefined for flat-params ops (getJwks-style: no params or directly
 * flat scalar params on the op), in which case the caller uses the raw
 * parameters model as-is.
 */
function resolveSingleNamedParam(params: Model): Model | undefined {
  if (params.properties === undefined || params.properties.size !== 1)
    return undefined;
  const [, prop] = [...params.properties.entries()][0]!;
  if (prop.type.kind === "Model" && prop.type.name !== "Array")
    return prop.type;
  return undefined;
}
