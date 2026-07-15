// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { existsSync } from "node:fs";
import { basename, dirname, isAbsolute, join, relative } from "node:path";
import { navigateProgram, NoTarget } from "@typespec/compiler";
import type {
  EmitContext,
  Model,
  Namespace,
  Operation,
} from "@typespec/compiler";
import { getVersion } from "@typespec/versioning";
import { getHttpOperation, getOperationVerb } from "@typespec/http";
import {
  D2_SERVED_BY_KEY,
  D2_CONCERN_KEY,
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
  D2_RESILIENCE_RETRY_WHEN_KEY,
  D2_RESILIENCE_FAIL_WHEN_KEY,
  D2_RESILIENCE_KEY,
  D2_RESERVED_KEY,
  parseResultPredicate,
  parse as parseResiliencePipeline,
} from "@dcsv-io/d2-typespec-decorators";
import type {
  IdempotentPayload,
  PredicateNode,
  ReservedPayload,
  ResiliencePolicyNode,
} from "@dcsv-io/d2-typespec-decorators";
import { emitGeneratedFile, resolveOutputPath } from "./lib/emit-file.js";
import { walkModel } from "./lib/model-walk.js";
import { emitCsharpDtos } from "./lib/csharp-dto-emitter.js";
import { emitTsDtos } from "./lib/ts-dto-emitter.js";
import type { EmittedTsFile } from "./lib/ts-dto-emitter.js";
import { emitProto } from "./lib/proto-emitter.js";
import type { NestedMessageDescriptor } from "./lib/proto-emitter.js";
import { emitGrpcService } from "./lib/grpc-service-emitter.js";
import type { GrpcDelegationTarget } from "./lib/grpc-service-emitter.js";
import { emitHandlerInterface } from "./lib/handler-interface-emitter.js";
import { emitFacade } from "./lib/facade-emitter.js";
import type { ExposedOp } from "./lib/facade-emitter.js";
import { emitGrpcClient, emitClientKeys } from "./lib/grpc-client-emitter.js";
import type { GrpcClientOp } from "./lib/grpc-client-emitter.js";
import { emitTsGrpcClient } from "./lib/ts-grpc-client-emitter.js";
import type { TsGrpcClientOp } from "./lib/ts-grpc-client-emitter.js";
import { emitTsRestClient } from "./lib/ts-rest-client-emitter.js";
import type {
  RestAuthIntent,
  RestIdempotencyKeySource,
  RestVerb,
  TsRestClientOp,
} from "./lib/ts-rest-client-emitter.js";
import {
  emitResultPredicates,
  emitBusinessRetrySignal,
} from "./lib/result-predicate-emitter.js";
import { emitRoutePolicy } from "./lib/route-policy-emitter.js";
import {
  emitBridgeRegistration,
  emitMapAllBridges,
} from "./lib/bridge-emitter.js";
import type { BridgeModuleOp } from "./lib/bridge-emitter.js";
import { emitOpenApiDocuments } from "./lib/openapi-emitter.js";
import { emitIdempotencyStoreSeam } from "./lib/idempotency-gate-emitter.js";
import {
  emitSseDispatcher,
  emitSseDispatchersDiExtension,
  emitSseEmitSinkSeam,
} from "./lib/sse-dispatch-emitter.js";
import type {
  SseChannelClass,
  SseDispatchOp,
} from "./lib/sse-dispatch-emitter.js";
import type {
  DelegationTarget,
  HttpVerb,
  ScopePolicy,
} from "./lib/route-policy-emitter.js";
import { toKebab } from "./lib/name-transforms.js";
import { $lib } from "./lib.js";
import { validateChannelAgreement } from "./lib/wire-channel.js";
import type { WireChannel } from "./lib/wire-channel.js";
import { emitWireVersionConstant } from "./lib/wire-version-emitter.js";
import { emitWireIdentityManifest } from "./lib/wire-manifest-emitter.js";

// The $onEmit entry point drives these artifact families per tsp compile:
//
//   1. operations-manifest.json — operations smoke manifest from the initial
//      scaffold; kept so the operations-manifest integration test stays green
//      alongside DTO emission.
//   2. <Op>Input.g.cs + <Op>Output.g.cs — C# sealed-record DTO pairs for
//      every operation with a concrete input or output model. Namespace is
//      determined by exposure routing (see "Namespace routing" below).
//   3. I<Op>Handler.g.cs — C# handler interface per op that has a request side
//      (exposed and internal). A PURE server-push op (only @d2ServerPush) is a
//      caller, not a request server, and emits NO handler. Lands in the app
//      CQRS namespace.
//   4. <op>-dto.g.ts — TypeScript interface pair for the same operations.
//   5. <Service>_<method>.g.proto — proto3 service + message declarations for
//      every operation decorated with @d2GrpcMethod.
//   6. <Service>Service.g.cs + <Op>TransportMappers.g.cs — C# gRPC service
//      class (extends the Grpc.Tools base) + transport mappers for those ops.
//   7. The C# cross-process gRPC client layer (per @d2ServedBy module) +
//      @d2Resilience predicate twins (C# + TS) + the retry sentinel.
//   8. <module>-grpc-client.g.ts — the TS SSR gRPC client (per @d2ServedBy
//      module): the TS twin of the C# gRPC client, delegating to the real
//      @dcsv-io/d2-grpc-client seam over the ts-proto grpc-js stub, folding in the
//      emitted TS predicate twin's retry-arm for a @d2Resilience op.
//   9. <module>-rest-client.g.ts — the TS browser REST client (per @d2ServedBy
//      module): per-@route typed fns delegating to the $lib apiCall/apiCallAnon
//      substrate (ProblemDetails / envelope → D2Result).
//  10. <service>[.<version>].openapi.g.json — the OpenAPI 3.0 document per
//      @service namespace (one per version when @versioned), produced by the
//      stock @typespec/openapi3 getOpenAPI3 seam with the four x-d2-* policy
//      extensions (x-d2-scope / x-d2-tier / x-d2-audience / x-d2-csrf) layered
//      on top from the @d2* stateMaps. Only emitted when a @service exists.
//  11. The server-push DISPATCH layer for every @d2ServerPush op:
//      D2GeneratedSseEmitSink.g.cs (the emitter-owned seam family — channel-class
//      enum + channel-target record struct + generic-payload sink interface, one
//      per registration namespace), I<Op>Dispatcher.g.cs + <Op>Dispatcher.g.cs
//      (the per-op dispatcher with the channel class baked from pushTarget + the
//      op-name event-type literal + the <Op>Output payload), and
//      <Module>SseDispatchersGenerated.g.cs (the per-module Transient DI-ext).
//      The text/event-stream wire framing stays hand-written fringe (the sink's
//      job). A push op whose output has no emittable payload fires D2TSP008
//      (server-push-requires-payload) and emits no partial dispatcher.
//  12. WireVersion.g.cs — C# static class with public const CHANNEL/GENERATION/
//      STABILITY in the proto-csharp-namespace. Emitted once when ≥1 @d2GrpcMethod
//      op produced a proto and the channel validated. Co-located with the
//      Grpc.Tools proto types so runtimes reference e.g.
//      D2.Services.Protos.KeyCustodian.V2Alpha.WireVersion.CHANNEL.
//  13. wire-identity.manifest.g.json — JSON record of the agree-by-construction
//      wire-identity facts (protoPackage, protoCsharpNamespace, generation,
//      stability, channel). Emitted alongside WireVersion.g.cs (same gate).
//      Deliberately omits published package names (parked for a later step).
//
// Channel cross-validation (before any proto emit):
//   validateChannelAgreement is called once at $onEmit start after reading
//   proto-package + proto-csharp-namespace. A proto-package ↔ proto-csharp-namespace
//   channel mismatch (or ↔ @versioned active-version mismatch) fires D2TSP010
//   (channel-segment-mismatch, error) and prevents emit of the two new artifacts.
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
//   process-kind-by-module — ServedBy → "edge-module" | "standalone" (host routing).
//   csharp-routes-namespace — ServedBy → Edge.Api routes C# namespace (edge-module Map*).
//   csharp-bridge-namespace — ServedBy → Edge.Api bridges C# namespace (standalone Map*).

/** Shape of one operation entry in the smoke manifest. */
export interface ManifestOperation {
  readonly name: string;
  readonly servedBy?: string;
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
 * A gRPC client op collected during the per-op walk, paired with its output
 * Model so the per-module @d2Resilience predicate emitter can crawl the data
 * path at gen time. The `clientOp` already carries any parsed predicate ASTs.
 */
interface CollectedGrpcOp {
  readonly clientOp: GrpcClientOp;
  readonly outputModel?: Model;
  /**
   * Max-attempts budget parsed from the op's @d2Resilience("retry(N)") DSL, when
   * present. Threaded into the TS SSR gRPC client's predicate retry pipeline
   * (`maxAttempts`). Undefined when the op carries no @d2Resilience pipeline DSL.
   */
  readonly retryBudget?: number;
  /**
   * The op's @d2Concern segment, threaded into the TS SSR gRPC client's DTO
   * import-path builder ONLY for a `ts-client-output-dirs` mirror target (where
   * the DTOs are written to `<concern-kebab>/` and the client to `facade/`).
   * Undefined when the op carries no concern.
   */
  readonly concern: string | undefined;
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

  // Process-kind map (ServedBy → "edge-module" | "standalone"). Required for
  // real-module host routing of any @route op; fixture mode tolerates absence.
  const processKindByModule = resolveProcessKindByModule(
    rawOptions["process-kind-by-module"],
  );

  // Routes namespace map (ServedBy → full C# ns). Production edge-module Map*
  // land under Edge.Api.Routes.*; missing key is fail-loud in real-module mode.
  const csharpRoutesNamespace = resolveStringMapOption(
    rawOptions["csharp-routes-namespace"],
  );

  // Bridge namespace map (ServedBy → full C# ns). Standalone public HTTP bridges
  // land under Edge.Api.Bridges.*; missing key is fail-loud for bridge ops.
  const csharpBridgeNamespace = resolveStringMapOption(
    rawOptions["csharp-bridge-namespace"],
  );

  // TS-client production-emission target (per @d2ServedBy module). A config-only,
  // concern-driven mirror: for every module named in this map, the emitted TS SSR
  // gRPC client (<module>-grpc-client.g.ts) AND the TS DTOs of that module's
  // @d2GrpcMethod ops are ALSO written to the mapped directory (a real consumer
  // package's generated/ folder), in addition to the standard emitter-output-dir.
  // Zero per-op special cases — routing is driven purely by (module → dir) config
  // × the op's own @d2ServedBy membership, exactly like the C# clients-namespace
  // routing. The mapped dir is repo-root-relative (resolved via
  // resolveRepoRootFromProjectRoot — same derivation tryGetSpecPath uses; works
  // for nested packages under contracts/typespec/<module>). Absent /
  // malformed option ⇒ no production mirror (standard dist/generated only).
  const tsClientOutputDirs = resolveTsClientOutputDirs(
    rawOptions["ts-client-output-dirs"],
    (program as { projectRoot?: string }).projectRoot,
  );

  // ---- Channel cross-validation (D2TSP010) ----
  // Resolve the @versioned active-version channel from any @versioned namespace
  // in the program. A @versioned namespace carries a Versions enum whose member
  // VALUES are the channel strings (e.g. "v2alpha"). We use the last member's
  // value as the active channel — the highest version in the enum.
  let versionedChannel: string | undefined;

  navigateProgram(program, {
    namespace(ns: Namespace) {
      /* v8 ignore start — @versioned VersionMap extraction depends on the @typespec/versioning subsystem; exercised end-to-end by the integration compile suite (dist/-loaded, not src-instrumented). Unit-mocking getVersion would test the mock, not the resolution. */
      const versionMap = getVersion(program, ns);

      if (versionMap === undefined) return;

      const versions = versionMap.getVersions();
      if (versions.length === 0) return;

      // Use the last member value as the active channel (highest version in the enum).
      // For single-member enums this is the only member.
      const latestVersion = versions[versions.length - 1];
      // First @versioned namespace by navigateProgram walk order wins; subsequent ones are ignored.
      if (latestVersion !== undefined && versionedChannel === undefined)
        versionedChannel = latestVersion.value;
      /* v8 ignore stop */
    },
  });

  // Cross-validate proto-package ↔ proto-csharp-namespace ↔ @versioned channel.
  // Returns the parsed WireChannel on agreement; fires D2TSP010 + returns undefined on mismatch.
  const validatedChannel: WireChannel | undefined = validateChannelAgreement(
    protoPackage,
    protoCsharpNs,
    versionedChannel,
    (code, message) => {
      /* v8 ignore start — defensive: onError is typed (code: string) but validateChannelAgreement only ever emits "channel-segment-mismatch", so the guard's false arm is unreachable */
      if (code === "channel-segment-mismatch")
        $lib.reportDiagnostic(program, {
          code: "channel-segment-mismatch",
          format: { detail: message },
          target: NoTarget,
        });
      /* v8 ignore stop */
    },
  );

  // Collect exposed ops per module (grouped by @d2ServedBy) for the façade emitter.
  // Façade is one-per-module so it is emitted AFTER the per-op walk gathers all ops.
  const exposedOpsByModule = new Map<string, ExposedOp[]>();

  // Collect gRPC-method ops per module (grouped by @d2ServedBy) for the gRPC client emitter.
  // The client emitter emits one interface + impl + mappers + DI-ext per module, after the walk.
  // Only populated when csClientsNamespace is configured (real-module mode). Each entry pairs
  // the GrpcClientOp (with any parsed @d2Resilience predicate ASTs attached) with the op's output
  // Model so the per-module predicate emitter can do its gen-time data-path crawl.
  const grpcOpsByModule = new Map<string, CollectedGrpcOp[]>();

  // Collect standalone bridge ops per module for MapAll{Module}Bridges emission after the walk.
  // Keyed by @d2ServedBy; value carries the op names + the resolved bridge registration ns
  // (same ns for all ops of a module — map is keyed by ServedBy).
  const bridgeOpsByModule = new Map<
    string,
    { registrationNs: string; sourceSpec: string; ops: BridgeModuleOp[] }
  >();

  // Per-module collection of emitted TS DTO files for @d2GrpcMethod ops whose
  // module has a configured ts-client-output-dir. Captured during the per-op walk
  // (the TS DTO content is produced there) and mirrored to the production target
  // dir alongside the module's TS gRPC client after the walk. Deduped by file name
  // (a DTO model shared across ops emits one file). Each entry carries the op's
  // @d2Concern so the mirror writes the DTO into its `<concern-kebab>/` subfolder
  // (co-located with the concern's runtime, mirroring the .NET client). Empty
  // unless tsClientOutputDirs names the op's module.
  const tsDtoFilesByModule = new Map<
    string,
    { file: EmittedTsFile; concern: string | undefined }[]
  >();

  // Gate for WireVersion.g.cs + wire-identity manifest: tracks whether any @d2GrpcMethod op
  // successfully produced a proto. Populated in fixture mode (no csClientsNamespace) AND real-
  // module mode — unlike grpcOpsByModule which is real-module only. The specHint from the first
  // such op is captured for the WireVersion banner.
  let anyGrpcProtoEmitted = false;
  let wireSpecHintCapture: string | undefined;

  // Collect @route ops per module (grouped by @d2ServedBy) for the TS browser
  // REST client emitter. The route dispatch is otherwise fire-and-emit per op;
  // this map groups the resolved route info (verb, path, auth intent, idempotency
  // keySource, model names) so the per-module REST client is emitted after the walk.
  const restOpsByModule = new Map<string, TsRestClientOp[]>();

  // Track which registration namespaces contain at least one idempotent route.
  // The idempotency-store seam is emitted ONCE per namespace (mirrors the
  // policy-markers approach) after the per-op walk completes.
  const idempotentNamespaces = new Set<string>();
  // Track the last specHint per namespace for the seam banner.
  const idempotentNamespaceSpec = new Map<string, string>();

  // Collect @d2ServerPush ops per module (grouped by @d2ServedBy) for the SSE
  // dispatch-DI extension (one-per-module, emitted after the per-op walk).
  const pushOpsByModule = new Map<string, SseDispatchOp[]>();
  // Track the last specHint per namespace that contains ≥1 push op. The SSE
  // emit-sink seam is emitted ONCE per namespace (mirrors the idempotency seam).
  const sseNamespaceSpec = new Map<string, string>();

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
      // tryGetSpecPath converts the absolute file.path from the TypeSpec AST to
      // a repo-relative path (relative to the monorepo root resolved from
      // program.projectRoot via resolveRepoRootFromProjectRoot). Falls back to
      // the operation name when the AST does not expose a file path (e.g.
      // in-process test invocations that supply their own sourceSpec strings
      // rather than going through $onEmit).
      // `program.projectRoot` may be absent on synthetic test programs.
      const specHint =
        tryGetSpecPath(op, (program as { projectRoot?: string }).projectRoot) ??
        `<typespec op: ${op.name}>`;

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
      //
      // A PURE-push op (only @d2ServerPush, no request exposure, not @d2Internal) emits
      // ONLY the output payload — never an input DTO. A param-less pure-push op would
      // otherwise emit an orphan parameterless <Op>Input record (TypeSpec's parameters
      // container is a non-undefined empty Model for a param-less op, so inputModel is
      // always defined), which nothing consumes. Suppress it via dtoInputModel.
      const dtoInputModel = isPurePush(program, op) ? undefined : inputModel;
      let dtoEmitSucceeded = false;
      // The emitted TS DTO file for this op (captured so it can be mirrored to a
      // configured ts-client-output-dir when the op is a @d2GrpcMethod op of a
      // targeted module). Undefined when no DTO was emitted.
      let emittedTsDto: EmittedTsFile | undefined;

      if (dtoInputModel !== undefined || outputModel !== undefined) {
        const dtoResult = emitDtoPair(
          context,
          program,
          op.name,
          dtoCsNamespace,
          specHint,
          dtoInputModel,
          outputModel,
        );
        dtoEmitSucceeded = dtoResult.ok;
        emittedTsDto = dtoResult.tsFile;
      }

      // Emit I<Op>Handler.g.cs for every op that has a request side — and
      // collect it for the façade. A PURE server-push op (only @d2ServerPush, no
      // @route/@d2GrpcMethod/@d2InProcess, not @d2Internal) is the CALLER of an
      // event channel, not a request server: it emits ONLY the dispatcher (a
      // client stub) — no handler (a caller never registers one) and no façade
      // entry (a façade method would delegate to the absent handler). A COMBINED
      // op (push + a request exposure) is NOT pure-push, so it still gets both
      // for the request side.
      //
      // Gated on dtoEmitSucceeded so unmapped-scalar / unsupported-property-type
      // errors do not produce a partial handler-interface file with broken type refs.
      // The handler interface always lands in the app CQRS namespace
      // (or the fixture grpcServiceNs when csAppNamespaceBase is absent).
      if (dtoEmitSucceeded && !isPurePush(program, op)) {
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
              // The concern-qualified namespace the op's DTOs were emitted to
              // (<clients-ns>.<Concern>) — the façade interface + impl import it.
              dtoNamespace: dtoCsNamespace,
            });
            exposedOpsByModule.set(servedBy, existing);
          }
        }
      }

      // Emit the server-push DISPATCH layer for ops carrying @d2ServerPush.
      // The dispatcher delivers the op's <Op>Output payload to the recipient
      // channel; the channel class is baked from the pushTarget. A push op whose
      // output has no emittable payload (void return / zero-field, zero-nested
      // output walk) fires D2TSP008 and emits no partial dispatcher.
      emitSsePushIfPresent(
        context,
        program,
        op,
        dtoCsNamespace,
        specHint,
        outputModel,
        pushOpsByModule,
        sseNamespaceSpec,
      );

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

        // Mirror this gRPC op's TS DTO into the module's configured production
        // TS-client dir (if the module is named in ts-client-output-dirs). This is
        // exactly the DTO surface the module's emitted TS gRPC client imports
        // (co-located, flat). Deduped by file name (a DTO shared across ops emits
        // one file). Standard dist/generated emission already happened above.
        /* v8 ignore start — ts-client-output-dirs DTO-mirror capture: exercised end-to-end by ts-client-output-dirs.integration.test.ts (dist/-loaded, not src-instrumented); the cross-op DTO-dedup arm needs two gRPC ops in one module sharing a DTO, covered there */
        if (
          grpcServedBy !== undefined &&
          emittedTsDto !== undefined &&
          tsClientOutputDirs.has(grpcServedBy)
        ) {
          const dtoFile = emittedTsDto;
          const dtoConcern = resolveConcern(program, op);
          const files = tsDtoFilesByModule.get(grpcServedBy) ?? [];

          if (!files.some((f) => f.file.fileName === dtoFile.fileName))
            files.push({ file: dtoFile, concern: dtoConcern });

          tsDtoFilesByModule.set(grpcServedBy, files);
        }
        /* v8 ignore stop */

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
              ? `${csClientsNamespace}.Facade`
              : `${grpcServiceNs}.Facade`;
          grpcDelegationTarget = {
            kind: "facade",
            typeName: facadeTypeName,
            methodName: `${grpcPascalOp}Async`,
            targetNamespace: facadeNs,
          };
        } else {
          // Handler delegation — targetNamespace is the App CQRS per-op folder
          // when csharp-app-namespace-base is set (production standalone / edge
          // non-@d2InProcess). Fixture mode (no base) co-locates with service ns
          // so targetNamespace may equal serviceImplNs (no extra using needed).
          const handlerNs = resolveHandlerNamespace(
            category,
            op.name,
            grpcServiceNs,
            csAppNamespaceBase,
          );
          grpcDelegationTarget = {
            kind: "handler",
            typeName: `I${grpcPascalOp}Handler`,
            methodName: "HandleAsync",
            targetNamespace: handlerNs,
          };
        }

        const protoEmitted = emitProtoAndGrpcService(
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

        // Mark that at least one proto was emitted. Captures the specHint from the first gRPC op
        // for use in the WireVersion banner. Works in both fixture and real-module modes.
        // Gate on protoEmitted: if the emit returned false (walk error / proto undefined),
        // the .proto was NOT written; setting the flag here would produce orphaned
        // WireVersion.g.cs + wire-identity.manifest.g.json with no proto on disk.
        if (protoEmitted && !anyGrpcProtoEmitted) {
          anyGrpcProtoEmitted = true;
          wireSpecHintCapture = specHint;
        }

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

          // Read back the @d2Resilience retryWhen / failWhen raw strings (stored
          // by the decorator on the two predicate state keys) and parse each into
          // its AST. The decorator validator already validated them at compile time,
          // so a parse error here would be a contract-level invariant break — surface
          // it via the emitter's diagnostic surface and skip the predicate (no broken emit).
          const retryWhenAst = parseOpPredicate(
            program,
            op,
            D2_RESILIENCE_RETRY_WHEN_KEY,
            "retryWhen",
          );
          const failWhenAst = parseOpPredicate(
            program,
            op,
            D2_RESILIENCE_FAIL_WHEN_KEY,
            "failWhen",
          );

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
            retryWhenAst,
            failWhenAst,
          };

          const retryBudget = parseRetryBudget(program, op);

          const existing = grpcOpsByModule.get(grpcServedBy) ?? [];
          existing.push({
            clientOp,
            outputModel,
            retryBudget,
            concern: resolveConcern(program, op),
          });
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
          processKindByModule,
          csharpRoutesNamespace,
          csharpBridgeNamespace,
          specHint,
          inputModel,
          outputModel,
          idempotentNamespaces,
          idempotentNamespaceSpec,
          restOpsByModule,
          bridgeOpsByModule,
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
      // The base is e.g. "DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers" →
      // app namespace root = "DcsvIo.D2.Private.Edge.KeyCustodian.App.Application".
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
      const clientOps = moduleOps.map((m) => m.clientOp);
      const clientFiles = emitGrpcClient(
        moduleName,
        clientOps,
        csClientsNamespace,
      );
      for (const f of clientFiles) {
        const clientPath = resolveOutputPath(context, f.fileName);
        void emitGeneratedFile(program, clientPath, f.content);
      }
      // Per-op client keys constants (<Op>ClientKeys.g.cs).
      for (const { clientOp } of moduleOps) {
        const keysFile = emitClientKeys(
          clientOp.opName,
          csClientsNamespace,
          clientOp.sourceSpec,
        );
        const keysPath = resolveOutputPath(context, keysFile.fileName);
        void emitGeneratedFile(program, keysPath, keysFile.content);
      }

      // ---- @d2Resilience predicate emission (per predicate-bearing op) ----
      // Emit the C# + TS predicate file pair for every op carrying retryWhen /
      // failWhen, plus the emitter-owned retry sentinel ONCE per module (shared
      // by every predicate-bearing op's client closure + DI-ext IsTransient arm).
      let moduleHasPredicate = false;
      for (const { clientOp, outputModel } of moduleOps) {
        if (
          clientOp.retryWhenAst === undefined &&
          clientOp.failWhenAst === undefined
        )
          continue;

        moduleHasPredicate = true;
        const predicateFiles = emitResultPredicates({
          opName: clientOp.opName,
          responseModelName: clientOp.responseModelName,
          outputModel,
          clientsNs: csClientsNamespace,
          dtoCsharpNs: clientOp.dtoCsharpNs,
          sourceSpec: clientOp.sourceSpec,
          retryWhen: clientOp.retryWhenAst,
          failWhen: clientOp.failWhenAst,
        });
        for (const f of predicateFiles) {
          const predicatePath = resolveOutputPath(context, f.fileName);
          void emitGeneratedFile(program, predicatePath, f.content);
        }
      }

      if (moduleHasPredicate) {
        // One shared sentinel per module — only the retryWhen arm throws it, but
        // every predicate-bearing op's DI-ext IsTransient arm references it.
        const sentinelSpec = moduleOps[0]!.clientOp.sourceSpec;
        const sentinelFile = emitBusinessRetrySignal(
          csClientsNamespace,
          sentinelSpec,
        );
        const sentinelPath = resolveOutputPath(context, sentinelFile.fileName);
        void emitGeneratedFile(program, sentinelPath, sentinelFile.content);
      }
    }
  }

  // ---- TS SSR gRPC client — one <module>-grpc-client.g.ts per @d2ServedBy module ----
  // Reuses the already-collected grpcOpsByModule (one TS client per module with
  // ≥1 @d2GrpcMethod op). The TS twin of the C# gRPC client: delegates to the real
  // @dcsv-io/d2-grpc-client seam over the ts-proto grpc-js stub; folds the emitted TS
  // predicate twin into the retry-arm for a @d2Resilience op.
  for (const [moduleName, moduleOps] of grpcOpsByModule) {
    // Thread each op's @d2Concern into the TS client ONLY when this module is a
    // ts-client-output-dirs mirror target — the mirror writes DTOs into
    // `<concern-kebab>/` and the client into `facade/`, so the client's DTO
    // imports are concern-relative (`../<concern-kebab>/<file>.js`). A non-mirrored
    // module (fixtures + the standard emitter-output copy) keeps the flat
    // co-located layout, so concern stays unset there and imports remain `./`.
    const isMirrorTarget = tsClientOutputDirs.has(moduleName);
    const tsOps: TsGrpcClientOp[] = moduleOps.map((m) => {
      // Concern is threaded ONLY for a ts-client-output-dirs mirror target; the
      // mapped path (isMirrorTarget true → the concern-relative import layout) is
      // exercised end-to-end by ts-client-output-dirs.integration.test.ts
      // (dist-loaded, not src-instrumented — same reason the mirror block below is
      // v8-ignored), so the true arm is not visible to src coverage.
      let concern: string | undefined;
      /* v8 ignore start — mirror-target concern threading: exercised end-to-end by ts-client-output-dirs.integration.test.ts (dist-loaded, not src-instrumented), same as the mirror block below */
      if (isMirrorTarget) concern = m.concern;
      /* v8 ignore stop */

      return {
        opName: m.clientOp.opName,
        grpcService: m.clientOp.grpcService,
        grpcMethod: m.clientOp.grpcMethod,
        sourceSpec: m.clientOp.sourceSpec,
        requestModelName: m.clientOp.requestModelName,
        requestFields: m.clientOp.requestFields,
        responseModelName: m.clientOp.responseModelName,
        responseFields: m.clientOp.responseFields,
        concern,
        retryWhenAst: m.clientOp.retryWhenAst,
        failWhenAst: m.clientOp.failWhenAst,
        retryBudget: m.retryBudget,
      };
    });
    const tsClientFiles = emitTsGrpcClient(moduleName, tsOps);
    for (const f of tsClientFiles) {
      const tsClientPath = resolveOutputPath(context, f.fileName);
      void emitGeneratedFile(program, tsClientPath, f.content);
    }

    // Production-emission mirror: when this module is named in
    // ts-client-output-dirs, ALSO write its TS gRPC client + the TS DTOs of its
    // @d2GrpcMethod ops (captured during the walk) to the mapped directory so a
    // real consumer package can import the generated wire surface directly,
    // co-located by concern (mirroring the .NET client): the gRPC client lands in
    // `facade/`, each DTO in its `<concern-kebab>/` folder. The set is complete +
    // self-consistent by construction — the client's concern-relative imports
    // resolve exactly those DTO files.
    const tsClientTargetDir = tsClientOutputDirs.get(moduleName);
    /* v8 ignore start — ts-client-output-dirs production mirror: exercised end-to-end by ts-client-output-dirs.integration.test.ts (dist/-loaded, not src-instrumented) */
    if (tsClientTargetDir !== undefined) {
      for (const f of tsClientFiles) {
        void emitGeneratedFile(
          program,
          join(tsClientTargetDir, "facade", f.fileName),
          f.content,
        );
      }
      for (const dto of tsDtoFilesByModule.get(moduleName) ?? []) {
        const dtoDir =
          dto.concern !== undefined
            ? join(tsClientTargetDir, toKebab(dto.concern))
            : tsClientTargetDir;
        void emitGeneratedFile(
          program,
          join(dtoDir, dto.file.fileName),
          dto.file.content,
        );
      }
    }
    /* v8 ignore stop */
  }

  // ---- TS browser REST client — one <module>-rest-client.g.ts per @d2ServedBy module ----
  // Uses the restOpsByModule collected during the per-op walk (one TS REST client
  // per module with ≥1 @route op). Per-@route typed fns delegating to the $lib
  // apiCall/apiCallAnon substrate (ProblemDetails / envelope → D2Result).
  for (const [moduleName, restOps] of restOpsByModule) {
    const restClientFiles = emitTsRestClient(moduleName, restOps);
    for (const f of restClientFiles) {
      const restClientPath = resolveOutputPath(context, f.fileName);
      void emitGeneratedFile(program, restClientPath, f.content);
    }
  }

  // ---- Edge bridge MapAll{Module}Bridges — one per standalone module with ≥1 bridge ----
  for (const [moduleName, collected] of bridgeOpsByModule) {
    const mapAll = emitMapAllBridges(
      moduleName,
      collected.ops,
      collected.registrationNs,
      collected.sourceSpec,
    );
    if (mapAll !== undefined) {
      const mapAllPath = resolveOutputPath(context, mapAll.fileName);
      void emitGeneratedFile(program, mapAllPath, mapAll.content);
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

  // ---- Server-push DI extension — one per module (after the per-op walk) ---------------------
  // Emitted for every module that contains ≥1 @d2ServerPush op; registers each
  // op's dispatcher Transient. Fires in both fixture + real-module modes (the
  // dispatch layer is host-independent; @d2ServedBy is the per-module key).
  for (const [moduleName, moduleOps] of pushOpsByModule) {
    const diFile = emitSseDispatchersDiExtension(
      moduleName,
      moduleOps,
      moduleOps[0]!.dtoNamespace,
      moduleOps[0]!.sourceSpec,
    );
    const diPath = resolveOutputPath(context, diFile.fileName);
    void emitGeneratedFile(program, diPath, diFile.content);
  }

  // ---- Server-push emit-sink seam — one per registration namespace ---------------------------
  // Emitted for every namespace that contains ≥1 push op. sseNamespaceSpec is
  // populated together with the per-op dispatcher emission in the walk, so a
  // namespace appears here iff it has a dispatcher referencing the seam.
  for (const [ns, specHint] of sseNamespaceSpec) {
    const seamFile = emitSseEmitSinkSeam(ns, specHint);
    const seamPath = resolveOutputPath(context, seamFile.fileName);
    void emitGeneratedFile(program, seamPath, seamFile.content);
  }

  // ---- WireVersion constant + wire-identity manifest (once, when ≥1 proto was emitted) ------
  // Emitted when the channel validated AND at least one @d2GrpcMethod op emitted a proto.
  // anyGrpcProtoEmitted is set in the per-op walk for every gRPC op in both fixture and
  // real-module modes (unlike grpcOpsByModule which is real-module only). Both artifacts are in
  // the same proto C# namespace (protoCsharpNs) so they co-locate with the Grpc.Tools proto types.
  if (validatedChannel !== undefined && anyGrpcProtoEmitted) {
    // wireSpecHintCapture is set on the first gRPC op; fall back to protoPackage for safety
    // (unreachable when anyGrpcProtoEmitted is true, since the first op sets the capture).
    /* v8 ignore start — unreachable: anyGrpcProtoEmitted true ⇒ the first gRPC op already set wireSpecHintCapture, so the ?? protoPackage fallback never fires */
    const wireSpecHint = wireSpecHintCapture ?? protoPackage;
    /* v8 ignore stop */

    const wireVersionFile = emitWireVersionConstant(
      protoCsharpNs,
      validatedChannel,
      wireSpecHint,
    );
    const wireVersionPath = resolveOutputPath(
      context,
      wireVersionFile.fileName,
    );
    void emitGeneratedFile(program, wireVersionPath, wireVersionFile.content);

    const wireManifestFile = emitWireIdentityManifest(
      protoPackage,
      protoCsharpNs,
      validatedChannel,
    );
    const wireManifestPath = resolveOutputPath(
      context,
      wireManifestFile.fileName,
    );
    void emitGeneratedFile(program, wireManifestPath, wireManifestFile.content);
  }

  // ---- OpenAPI x-d2-* document(s) — one per @service namespace × version --------------------
  // Runs the genuine stock @typespec/openapi3 emitter (getOpenAPI3) for the HTTP
  // shape, then layers the four x-d2-* policy extensions read from the @d2*
  // stateMaps. Emits one file per (service × version); returns no files when the
  // program declares no @service namespace.
  const openApiFiles = await emitOpenApiDocuments(program);
  for (const f of openApiFiles) {
    const openApiPath = resolveOutputPath(context, f.fileName);
    await emitGeneratedFile(program, openApiPath, f.content);
  }

  // ---- Smoke manifest (kept so the operations-manifest integration test stays green) --------
  const manifest: OperationsManifest = {
    emitter: "@dcsv-io/d2-typespec-emitters",
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
 * Resolve whether an operation is a PURE server-push op — it carries
 * `@d2ServerPush` and NONE of the request-exposure decorators (`@route` /
 * `@d2GrpcMethod` / `@d2InProcess`) and is not `@d2Internal`.
 *
 * A pure-push op is the CALLER of an event channel, not a server of requests:
 * it generates ONLY the dispatcher (a client stub) — no `I<Op>Handler` (a
 * caller never registers a handler to reach a service) and no façade entry (a
 * façade method would delegate to the now-absent handler). The SSE gateway is
 * the server; it implements the one generic `D2GeneratedSseEmitSink` seam once
 * for every event, so there is zero per-event server code.
 *
 * This is SELECTIVE: a COMBINED op (e.g. `@d2ServerPush` + `@d2GrpcMethod`)
 * still has a request side, so it is NOT pure-push and STILL gets a handler +
 * façade entry for that request side.
 *
 * `@route` is not in the state map; it is detected via the same `getOperationVerb`
 * mechanism the route emitter uses (an explicit HTTP verb decorator means `@route`).
 */
function isPurePush(
  program: Parameters<typeof walkModel>[0],
  op: Operation,
): boolean {
  const hasServerPush =
    program.stateMap(D2_SERVER_PUSH_KEY).get(op) !== undefined;
  if (!hasServerPush) return false;

  // A request exposure (gRPC / in-process / @route) OR @d2Internal means the op
  // has a server side that owns a handler — not pure-push. @route is detected via
  // getOperationVerb (an explicit HTTP verb decorator), the same signal the route
  // emitter uses; the others are state-map markers.
  const hasGrpc = program.stateMap(D2_GRPC_METHOD_KEY).get(op) !== undefined;
  const hasInProcess = program.stateMap(D2_IN_PROCESS_KEY).get(op) === true;
  const isInternal = program.stateMap(D2_INTERNAL_KEY).get(op) === true;
  const hasRoute =
    getOperationVerb(program as Parameters<typeof getOperationVerb>[0], op) !==
    undefined;

  // Pure-push iff NONE of the request-side / internal markers hold. Combining
  // them through `.some` over a flag array keeps a single decision point — the
  // pure-push path (all flags false) and any combined op (≥1 flag true) cover it.
  const hasRequestSideOrInternal = [
    hasGrpc,
    hasInProcess,
    hasRoute,
    isInternal,
  ].some((flag) => flag);

  return !hasRequestSideOrInternal;
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
 * Resolve the co-location concern segment for an operation (the @d2Concern
 * value). Returns the segment string, or undefined when the op carries no
 * @d2Concern. Drives the concern-qualified clients namespace + folder for a
 * client-exposed op.
 */
function resolveConcern(
  program: Parameters<typeof walkModel>[0],
  op: Operation,
): string | undefined {
  const concern = program.stateMap(D2_CONCERN_KEY).get(op) as
    | string
    | undefined;
  return concern !== undefined && concern.length > 0 ? concern : undefined;
}

/**
 * Resolve the C# namespace for emitting DTOs.
 *
 * Routing table:
 *   - csAppNamespaceBase present + isExposed + csClientsNamespace present
 *     → Clients namespace (exposed ops' DTOs live in the Clients project).
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

  if (isExposed && csClientsNamespace !== undefined) {
    // Real-module client-exposed op: its transport DTOs live in a concern-named
    // namespace + folder (<clients-ns>.<Concern>) co-located with the runtime
    // that serves them. The concern is declared via @d2Concern; a missing concern
    // is a loud build failure (D2TSP013) — the emitter cannot place them by concern.
    const concern = resolveConcern(program, op);
    if (concern === undefined) {
      $lib.reportDiagnostic(program, {
        code: "missing-concern",
        format: { op: opName },
        target: op,
      });
      // Fall back to the un-suffixed clients namespace to avoid a crash; the
      // error-severity diagnostic already fails the compile.
      return csClientsNamespace;
    }

    return `${csClientsNamespace}.${concern}`;
  }

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
 *   pattern for ISignHandler in DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpc.Generated).
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
): { ok: boolean; tsFile: EmittedTsFile | undefined } {
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

  if (errors.length > 0) return { ok: false, tsFile: undefined }; // Diagnostics already reported; don't emit partial files.

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

  // emitCsharpDtos always returns [inputFile, outputFile]. When inputModel is
  // undefined, the caller has decided not to emit the input side (pure-push
  // ops emit only the output payload DTO — no input DTO). Skip csFiles[0] in
  // that case so no orphan parameterless <Op>Input record lands on disk.
  const csFilesToEmit = inputModel === undefined ? csFiles.slice(1) : csFiles;

  for (const f of csFilesToEmit) {
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
  return { ok: true, tsFile };
}

/**
 * Resolve the D2 monorepo root from a TypeSpec `program.projectRoot` (the
 * directory that contains the active tspconfig.yaml).
 *
 * Nested packages (`contracts/typespec/key-custodian`, `…/audit`) and the
 * historical root package (`contracts/typespec`) must resolve to the **same**
 * repo root so repo-root-relative options (`ts-client-output-dirs`, banner
 * source paths) land under `server/…`, never under `contracts/server/…`.
 *
 * 1. Walk ancestors until a directory contains both `D2.slnx` and
 *    `contracts/typespec` (definitive on-disk markers).
 * 2. Path-shape fallback: walk until basename is `typespec` with parent
 *    `contracts`, then take that directory's grandparent (synthetic unit-test
 *    paths that never touch the real disk).
 * 3. Ultimate fallback: two levels up — historical layout where tspconfig
 *    always lived at `contracts/typespec`.
 */
export function resolveRepoRootFromProjectRoot(projectRoot: string): string {
  // On-disk marker walk (real `tsp compile` against the monorepo).
  let dir = projectRoot;

  for (let i = 0; i < 20; i++) {
    if (
      existsSync(join(dir, "server", "D2.slnx")) &&
      existsSync(join(dir, "contracts", "typespec"))
    ) {
      return dir;
    }

    const parent = dirname(dir);

    if (parent === dir) break;

    dir = parent;
  }

  // Path-shape fallback (synthetic unit-test roots; missing on-disk markers).
  dir = projectRoot;

  for (let i = 0; i < 20; i++) {
    if (
      basename(dir) === "typespec" &&
      basename(dirname(dir)) === "contracts"
    ) {
      return dirname(dirname(dir));
    }

    const parent = dirname(dir);

    if (parent === dir) break;

    dir = parent;
  }

  // Historical layout: tspconfig at contracts/typespec → grandparent is repo.
  return join(projectRoot, "../..");
}

/**
 * Resolve the `ts-client-output-dirs` emitter option into a
 * `Map<moduleName, absoluteDir>`. The option is a JSON object mapping a
 * @d2ServedBy module name to a repo-root-relative output directory; each value
 * is resolved against the monorepo root via `resolveRepoRootFromProjectRoot`
 * (same derivation `tryGetSpecPath` uses — works for nested
 * `contracts/typespec/<module>` packages). A non-object option, a non-string
 * value, or an unresolvable projectRoot yields an empty map (no production
 * mirror — dist/generated emission is unaffected).
 */
export function resolveTsClientOutputDirs(
  raw: unknown,
  projectRoot: string | undefined,
): Map<string, string> {
  const result = new Map<string, string>();

  if (
    raw === null ||
    typeof raw !== "object" ||
    Array.isArray(raw) ||
    projectRoot === undefined
  )
    return result;

  const repoRoot = resolveRepoRootFromProjectRoot(projectRoot);

  for (const [moduleName, dir] of Object.entries(
    raw as Record<string, unknown>,
  )) {
    if (typeof dir !== "string" || dir.length === 0) continue;

    result.set(moduleName, isAbsolute(dir) ? dir : join(repoRoot, dir));
  }

  return result;
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
): boolean {
  const errors: string[] = [];
  const onError = (
    code:
      | "unmapped-scalar"
      | "unsupported-property-type"
      | "unsupported-union-shape"
      | "invalid-streaming-mode"
      | "unpinned-proto-field"
      | "duplicate-field-number",
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
    else if (code === "unpinned-proto-field")
      $lib.reportDiagnostic(program, {
        code: "unpinned-proto-field",
        format: { detail: message },
        target: NoTarget,
      });
    else if (code === "duplicate-field-number")
      $lib.reportDiagnostic(program, {
        code: "duplicate-field-number",
        format: { detail: message },
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

  if (errors.length > 0) return false;

  // ---- proto emission ----
  // Proto message names follow the <Method>Request / <Method>Response convention
  // (matches existing hand-authored protos in contracts/protos/) and are distinct
  // from the DTO model names (<Op>Input / <Op>Output), eliminating any name collision.
  const protoRequestName = `${grpc.method}Request`;
  const protoResponseName = `${grpc.method}Response`;

  // DTO model names come from the TypeSpec model name (e.g. SignInput / SignOutput).
  // A param-less op (e.g. getOrLazyProvisionOwnSealPrivateKey) has a non-undefined but EMPTY
  // parameters Model whose `.name` is the empty string — so `?? fallback` (nullish)
  // does NOT trigger and would yield an empty DTO name, emitting a malformed
  // `using  = ...;` alias + `internal  To()` in the transport mapper/service. Guard
  // on `.length > 0` (matching the grpc-client + DTO-collection sites) and fall back
  // to the synthesized `${toPascalFromCamel(opName)}Input` — byte-identical to the
  // name the C# DTO emitter (csharp-dto-emitter: `${pascalOp}Input`) writes for the
  // synthesized empty input record, so the alias resolves to the real DTO type.
  const dtoRequestName =
    (inputModel?.name?.length ?? 0) > 0
      ? inputModel!.name
      : `${toPascalFromCamel(opName)}Input`;
  const dtoResponseName =
    (outputModel?.name?.length ?? 0) > 0
      ? outputModel!.name
      : `${toPascalFromCamel(opName)}Output`;

  // Read @d2Reserved payloads for request and response models.
  const reservedMap = program.stateMap(D2_RESERVED_KEY);
  const inputReserved =
    inputModel !== undefined
      ? (reservedMap.get(inputModel) as ReservedPayload | undefined)
      : undefined;
  const outputReserved =
    outputModel !== undefined
      ? (reservedMap.get(outputModel) as ReservedPayload | undefined)
      : undefined;

  // Build nested message descriptors with their optional @d2Reserved payloads.
  // Merge nested models from both input and output walks (deduped by name in walkModel already).
  const allNestedModels = [
    ...inputWalk.nestedModels,
    ...outputWalk.nestedModels,
  ];
  const seenNestedNames = new Set<string>();
  const nestedDescriptors: NestedMessageDescriptor[] = [];

  // Assemble nested-message descriptors, deduping by name across the merged input +
  // output walks and attaching each nested model's optional @d2Reserved payload.
  // collectNested sets typeModel for every compiled model, so the reservedMap lookup
  // always runs; the non-null assertion is safe (a missing key yields undefined, and
  // typeModel is undefined only for hand-built NestedModel test fixtures, which never
  // reach this $onEmit assembly path).
  /* v8 ignore start — this $onEmit assembly loop is proven end-to-end by the nested-model gRPC op in proto-grpc-emit.integration.test.ts and at the emitProto layer by nested-model-grpc-byte-parity.test.ts; both load the dist emitter, so this src is not instrumented */
  for (const nm of allNestedModels) {
    if (seenNestedNames.has(nm.name)) continue;
    seenNestedNames.add(nm.name);
    nestedDescriptors.push({
      model: nm,
      reserved: reservedMap.get(nm.typeModel!) as ReservedPayload | undefined,
    });
  }
  /* v8 ignore stop */

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
    inputReserved,
    // The proto DATA message is named after the DTO output model (<Op>Output) —
    // distinct from the <Method>Response envelope wrapper (proto-emitter derives
    // that name itself). Passing protoResponseName here would name the data message
    // <Method>Response too, colliding with the wrapper and failing protoc.
    dtoResponseName,
    outputWalk.fields,
    outputReserved,
    nestedDescriptors,
    onError,
  );
  if (errors.length > 0 || protoFile === undefined) return false;

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

  return true;
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
  processKindByModule: ReadonlyMap<string, string>,
  csharpRoutesNamespace: ReadonlyMap<string, string>,
  csharpBridgeNamespace: ReadonlyMap<string, string>,
  specHint: string,
  inputModel: Model | undefined,
  outputModel: Model | undefined,
  idempotentNamespaces: Set<string>,
  idempotentNamespaceSpec: Map<string, string>,
  restOpsByModule: Map<string, TsRestClientOp[]>,
  bridgeOpsByModule: Map<
    string,
    { registrationNs: string; sourceSpec: string; ops: BridgeModuleOp[] }
  >,
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
  const hasGrpc = program.stateMap(D2_GRPC_METHOD_KEY).get(op) !== undefined;
  const pascalOp = toPascalFromCamel(op.name);
  const isRealModule =
    csAppNamespaceBase !== undefined && csClientsNamespace !== undefined;

  // ---- Process-kind + host-routing fail-louds (real-module mode) ----
  // needsHostRouting is true here (we only enter this function for @route ops).
  // Fixture mode (no clients-ns + app-base) keeps today's optional ServedBy path.
  let processKind: ProcessKind | undefined;
  if (servedBy !== undefined && servedBy.length > 0) {
    const rawKind = processKindByModule.get(servedBy);
    if (rawKind !== undefined) {
      if (rawKind !== "edge-module" && rawKind !== "standalone") {
        $lib.reportDiagnostic(program, {
          code: "unknown-process-kind",
          format: { servedBy, kind: rawKind },
          target: op,
        });
        return;
      }
      processKind = rawKind;
    }
  }

  if (isRealModule) {
    if (servedBy === undefined || servedBy.length === 0) {
      $lib.reportDiagnostic(program, {
        code: "missing-served-by-for-host-routing",
        format: { op: op.name },
        target: op,
      });
      return;
    }
    if (processKind === undefined) {
      $lib.reportDiagnostic(program, {
        code: "missing-process-kind",
        format: { op: op.name, servedBy },
        target: op,
      });
      return;
    }
  }

  const inputTypeName =
    (inputModel?.name?.length ?? 0) > 0 ? inputModel!.name : `${pascalOp}Input`;
  const outputTypeName =
    (outputModel?.name?.length ?? 0) > 0
      ? outputModel!.name
      : `${pascalOp}Output`;

  void isExposed; // exposure is already validated by the caller

  // Map the idempotency payload's lowerCamel field names to PascalCase C# property names.
  let idempotencyConfig:
    | {
        keySource: "header" | "derived";
        ttlSeconds: number;
        fields: readonly string[];
      }
    | undefined;
  if (idempotentPayload !== undefined) {
    const pascalFields = idempotentPayload.fields.map((f) => {
      /* v8 ignore start — defensive: @d2Idempotent guarantees non-empty field names, so this guard never fires */
      if (f.length === 0) return f;
      /* v8 ignore stop */
      return f[0]!.toUpperCase() + f.slice(1);
    });
    idempotencyConfig = {
      keySource: idempotentPayload.keySource as "header" | "derived",
      ttlSeconds: idempotentPayload.ttlSeconds,
      fields: pascalFields,
    };
  }

  // ---- Standalone → Edge HTTP→gRPC bridge (skip in-process Map*) ----
  if (processKind === "standalone") {
    if (!hasGrpc) {
      $lib.reportDiagnostic(program, {
        code: "standalone-route-requires-grpc",
        format: { op: op.name },
        target: op,
      });
      return;
    }

    const bridgeNs =
      servedBy !== undefined ? csharpBridgeNamespace.get(servedBy) : undefined;
    if (
      bridgeNs === undefined ||
      bridgeNs.length === 0 ||
      servedBy === undefined
    ) {
      $lib.reportDiagnostic(program, {
        code: "missing-bridge-namespace",
        format: {
          op: op.name,
          servedBy: servedBy ?? "",
        },
        target: op,
      });
      return;
    }

    // gRPC client interface lives in the clients package (real-module) or the
    // fixture grpc service ns. Host wires AddD2{Module}GrpcClients separately.
    const grpcClientNamespace =
      csClientsNamespace !== undefined ? csClientsNamespace : grpcServiceNs;

    if (idempotencyConfig !== undefined) {
      // Track bridge registration ns so D2GeneratedIdempotencyStore seam emits
      // (same as in-process Map* path).
      idempotentNamespaces.add(bridgeNs);
      idempotentNamespaceSpec.set(bridgeNs, specHint);
    }

    const bridgeFile = emitBridgeRegistration({
      opName: op.name,
      verb,
      routePath,
      moduleName: servedBy,
      grpcClientNamespace,
      inputTypeName,
      outputTypeName,
      dtoNamespace: dtoCsNamespace,
      scopePolicy,
      rateTier,
      csrf,
      idempotency: idempotencyConfig,
      registrationNamespace: bridgeNs,
      sourceSpec: specHint,
    });
    const bridgePath = resolveOutputPath(context, bridgeFile.fileName);
    void emitGeneratedFile(program, bridgePath, bridgeFile.content);

    const existingBridge = bridgeOpsByModule.get(servedBy);
    if (existingBridge !== undefined) {
      existingBridge.ops.push({ opName: op.name });
    } else {
      bridgeOpsByModule.set(servedBy, {
        registrationNs: bridgeNs,
        sourceSpec: specHint,
        ops: [{ opName: op.name }],
      });
    }

    // Public HTTP still exists on Edge — collect for the browser REST client.
    collectRestClientOp(
      program,
      op,
      servedBy,
      verb,
      routePath,
      scopePolicy,
      inputModel,
      inputTypeName,
      outputTypeName,
      specHint,
      idempotencyConfig,
      restOpsByModule,
    );
    return;
  }

  // ---- edge-module (or fixture mode without process-kind) → in-process Map* ----
  let delegationTarget: DelegationTarget;
  let delegationTargetNamespace: string;

  if (inProcess && servedBy !== undefined && servedBy.length > 0) {
    // Facade delegation: the fixture façade interface name is I<ServedBy>SignerFacade
    // (for the sign fixture, this is ISignFixtureSignerFacade — the fixture-specific
    // naming that avoids collision with the real IKeyCustodianApi).
    // In fixture mode (no csAppNamespaceBase), use the fixture gRPC namespace.
    // In real-module mode, use the clients namespace.
    const facadeTypeName =
      csAppNamespaceBase !== undefined && csClientsNamespace !== undefined
        ? `I${servedBy}Api`
        : `I${servedBy}SignerFacade`;
    const facadeNs =
      csAppNamespaceBase !== undefined && csClientsNamespace !== undefined
        ? `${csClientsNamespace}.Facade`
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

  // Resolve the registration namespace.
  // Production: csharp-routes-namespace[ServedBy] when set — never hard-derive
  // App….Routes once process-kind is edge-module in real-module mode.
  // Fixture / legacy: hard-derive from app-base or fall back to grpcServiceNs.
  let registrationNs: string | undefined;
  if (servedBy !== undefined && servedBy.length > 0) {
    const mapped = csharpRoutesNamespace.get(servedBy);
    if (mapped !== undefined && mapped.length > 0) registrationNs = mapped;
  }
  if (registrationNs === undefined) {
    if (isRealModule && processKind === "edge-module") {
      $lib.reportDiagnostic(program, {
        code: "missing-routes-namespace",
        format: {
          op: op.name,
          servedBy: servedBy ?? "",
        },
        target: op,
      });
      return;
    }
    // Fixture mode or real-module without process-kind (unreachable for real-module
    // host routing after the fail-louds above): preserve hard-derive fallback.
    registrationNs =
      csAppNamespaceBase !== undefined
        ? `${csAppNamespaceBase.replace(/\.Handlers$/, "")}.Routes`
        : grpcServiceNs;
  }

  if (idempotencyConfig !== undefined) {
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

  // ---- Collect this routed op for the per-module TS browser REST client ----
  if (servedBy !== undefined && servedBy.length > 0) {
    collectRestClientOp(
      program,
      op,
      servedBy,
      verb,
      routePath,
      scopePolicy,
      inputModel,
      inputTypeName,
      outputTypeName,
      specHint,
      idempotencyConfig,
      restOpsByModule,
    );
  }
}

/**
 * Collect a routed op into the per-module TS browser REST client map.
 * Shared by in-process Map* and standalone bridge paths (both are public HTTP).
 */
function collectRestClientOp(
  program: Parameters<typeof walkModel>[0],
  _op: Operation,
  servedBy: string,
  verb: HttpVerb,
  routePath: string,
  scopePolicy: ScopePolicy,
  inputModel: Model | undefined,
  inputTypeName: string,
  outputTypeName: string,
  specHint: string,
  idempotencyConfig:
    | {
        keySource: "header" | "derived";
        ttlSeconds: number;
        fields: readonly string[];
      }
    | undefined,
  restOpsByModule: Map<string, TsRestClientOp[]>,
): void {
  // Auth intent: harmless-only → apiCallAnon; any/all scope → apiCall.
  const authIntent: RestAuthIntent =
    scopePolicy.kind === "harmless" ? "harmless" : "scoped";
  // Idempotency keySource: header → client threads a key; derived → server-computed
  // (no client key); absent → none.
  const idempotencyKeySource: RestIdempotencyKeySource =
    idempotencyConfig === undefined ? "none" : idempotencyConfig.keySource;
  // Request fields for GET/DELETE query binding (the walk is pure/idempotent).
  /* v8 ignore start — defensive: walk error sink never fires (model pre-validated) + the no-input-model empty-walk arm */
  const restInputWalk =
    inputModel !== undefined
      ? walkModel(program, inputModel, () => undefined)
      : { fields: [], nestedModels: [], nestedEnums: [] };
  /* v8 ignore stop */

  const restOp: TsRestClientOp = {
    opName: _op.name,
    routePath,
    verb: verb.toUpperCase() as RestVerb,
    authIntent,
    sourceSpec: specHint,
    requestModelName: inputTypeName,
    requestFields: restInputWalk.fields,
    responseModelName: outputTypeName,
    idempotencyKeySource,
  };

  const existing = restOpsByModule.get(servedBy) ?? [];
  existing.push(restOp);
  restOpsByModule.set(servedBy, existing);
}

/** Closed set of process-kind values (tspconfig process-kind-by-module). */
export type ProcessKind = "edge-module" | "standalone";

/**
 * Resolve a ServedBy → string map option (e.g. csharp-routes-namespace,
 * csharp-bridge-namespace, process-kind-by-module). Non-object / empty /
 * non-string values yield an empty map.
 */
export function resolveStringMapOption(raw: unknown): Map<string, string> {
  const result = new Map<string, string>();
  if (raw === null || typeof raw !== "object" || Array.isArray(raw))
    return result;

  for (const [key, value] of Object.entries(raw as Record<string, unknown>)) {
    if (typeof value !== "string" || value.length === 0) continue;
    result.set(key, value);
  }
  return result;
}

/**
 * Resolve `process-kind-by-module` into a ServedBy → raw kind string map.
 * Closed-set validation happens at the op site (unknown-process-kind diagnostic)
 * so authors see the op that triggered the bad value.
 */
export function resolveProcessKindByModule(raw: unknown): Map<string, string> {
  return resolveStringMapOption(raw);
}

/**
 * Emit the server-push dispatcher pair for one operation if it carries
 * @d2ServerPush.
 *
 * The op's `<Op>Output` model is the event payload. When the output has no
 * emittable payload (a `void` return, or an output walk yielding zero fields and
 * zero nested models), D2TSP008 (server-push-requires-payload) fires and NO
 * partial dispatcher is emitted — a payload-less push is almost certainly an
 * author mistake (strict + fail-loud, matching the fleet's posture).
 *
 * The channel CLASS is baked from the decorator's stored pushTarget
 * ("user" | "session"). The event-type is the op-name literal. The op is also
 * collected into `pushOpsByModule` (keyed by @d2ServedBy) for the after-walk
 * per-module DI extension, and its namespace is tracked in `sseNamespaceSpec`
 * for the once-per-namespace emit-sink seam.
 *
 * The output model is re-walked here with a no-op error sink: emitDtoPair ran
 * first in the per-op walk and already reported any walk error, so the sink is
 * inert (never fires) for any program that reaches this point with a present
 * output model.
 */
function emitSsePushIfPresent(
  context: EmitContext,
  program: Parameters<typeof walkModel>[0],
  op: Operation,
  dtoCsNamespace: string,
  specHint: string,
  outputModel: Model | undefined,
  pushOpsByModule: Map<string, SseDispatchOp[]>,
  sseNamespaceSpec: Map<string, string>,
): void {
  const pushTarget = program.stateMap(D2_SERVER_PUSH_KEY).get(op) as
    | string
    | undefined;
  if (pushTarget === undefined) return;

  // Emit-gate: a push op must carry an emittable payload. A void return has no
  // output model; an empty output record has zero fields + zero nested models.
  // The walk error sink is inert (the model was validated by emitDtoPair first),
  // so it never fires and is excluded from coverage.
  /* v8 ignore start — inert: emitDtoPair already validated the output model, so this sink never fires */
  const inertOutputSink = () => undefined;
  /* v8 ignore stop */
  const outputWalk =
    outputModel !== undefined
      ? walkModel(program, outputModel, inertOutputSink)
      : { fields: [], nestedModels: [], nestedEnums: [] };
  if (outputWalk.fields.length === 0 && outputWalk.nestedModels.length === 0) {
    $lib.reportDiagnostic(program, {
      code: "server-push-requires-payload",
      format: { op: op.name },
      target: op,
    });

    return; // No partial dispatcher — the payload DTO does not exist.
  }

  // Map the decorator's pushTarget to the PascalCase C# channel-class member.
  // The decorator validator (validatePushTarget) guarantees "user" | "session";
  // any other value would already have failed the compile, so the else arm is a
  // defensive fallback for an unreachable state.
  const channelClass: SseChannelClass =
    pushTarget === "session" ? "Session" : "User";

  // Past the emit-gate the output model is always present (a void/empty output
  // already returned above), so the name is read directly. An anonymous output
  // model (empty name) falls back to the <PascalOp>Output convention.
  const pascalOp = toPascalFromCamel(op.name);
  const outputTypeName =
    outputModel!.name.length > 0 ? outputModel!.name : `${pascalOp}Output`;

  const dispatchOp: SseDispatchOp = {
    opName: op.name,
    channelClass,
    outputTypeName,
    dtoNamespace: dtoCsNamespace,
    sourceSpec: specHint,
  };

  const dispatcherFiles = emitSseDispatcher(dispatchOp);
  for (const f of dispatcherFiles) {
    const path = resolveOutputPath(context, f.fileName);
    void emitGeneratedFile(program, path, f.content);
  }

  // Collect for the after-walk per-module DI extension + the once-per-namespace
  // seam. The seam lives in the dispatcher's namespace so the impl resolves it
  // without a using. An op with @d2ServerPush but no @d2ServedBy has no module
  // to name the DI extension — the dispatcher still emits; only the DI grouping
  // is skipped (a defensive guard; the decorator layer pairs the two in practice).
  const servedBy = program.stateMap(D2_SERVED_BY_KEY).get(op) as
    | string
    | undefined;
  if (servedBy !== undefined && servedBy.length > 0) {
    const existing = pushOpsByModule.get(servedBy) ?? [];
    existing.push(dispatchOp);
    pushOpsByModule.set(servedBy, existing);
  }

  sseNamespaceSpec.set(dtoCsNamespace, specHint);
}

/**
 * Attempt to extract a human-readable source-spec path from the operation node.
 *
 * During `tsp compile`, each AST node's source file is reachable by walking
 * `node.parent` upward to the `TypeSpecScriptNode` (kind === 0), which carries
 * a `SourceFile.path` absolute path. This function converts that absolute path
 * to a repo-relative forward-slash path so the generated banner is
 * machine-independent and matches the strings used by the in-process byte-gate
 * tests (which pass relative `sourceSpec` strings directly).
 *
 * `projectRoot` is `program.projectRoot` — the directory that contains the
 * active tspconfig.yaml (historical `contracts/typespec/` or a nested package
 * such as `contracts/typespec/key-custodian/`). The monorepo root is derived
 * via `resolveRepoRootFromProjectRoot`, so `relative(repoRoot, filePath)`
 * produces e.g. `contracts/typespec/fixtures/sign-shaped.tsp` or
 * `contracts/typespec/key-custodian/key-custodian.tsp` regardless of package depth.
 *
 * Returns undefined when the TypeSpec version does not expose the file path via
 * the parent chain (e.g. purely synthetic operations created in-process by
 * tests).
 */
function tryGetSpecPath(
  op: Operation,
  projectRoot: string | undefined,
): string | undefined {
  // In real `tsp compile` runs, the SourceFile.path lives on the
  // TypeSpecScriptNode (AST kind === 0), reached by walking op.node.parent up.
  // In synthetic test programs (e.g. smoke-emit.test.ts), the path is placed
  // directly on op.node.file.path without AST scaffolding.
  //
  // Strategy: walk up from op.node looking for the first node that carries a
  // `file.path` string, respecting `kind === 0` for the real AST and falling
  // back to op.node itself for synthetic setups.

  type AnyNode = {
    kind?: number;
    file?: { path?: string };
    parent?: AnyNode;
  };

  let node: AnyNode | undefined = op.node as AnyNode | undefined;

  // Prefer the TypeSpecScriptNode (kind === 0) which is guaranteed by the real
  // AST; also accept the raw op.node if it directly carries a file.path (the
  // synthetic smoke-test pattern).
  let rawPath: string | undefined;

  while (node !== undefined) {
    if (
      node.kind === 0 ||
      (rawPath === undefined && node.file?.path !== undefined)
    ) {
      rawPath = node.file?.path;

      if (node.kind === 0) break; // found the canonical source; stop here
    }

    node = node.parent;
  }

  if (rawPath === undefined) return undefined;

  // When the path is already relative (e.g. synthetic test programs supply a
  // repo-relative string directly), return it unchanged.
  if (!isAbsolute(rawPath)) return rawPath;

  // During tsp compile, rawPath is the absolute disk path. Convert to a
  // repo-relative forward-slash path. projectRoot is the tspconfig.yaml
  // directory (any depth under contracts/typespec/); resolve monorepo root
  // via markers / path-shape, not a hard-coded `../..` depth.
  if (projectRoot === undefined) return rawPath;

  const repoRoot = resolveRepoRootFromProjectRoot(projectRoot);
  return relative(repoRoot, rawPath).replaceAll("\\", "/");
}

/**
 * Read the raw @d2Resilience predicate string stored on `stateKey` for `op` and
 * parse it into an AST. Returns undefined when the op carries no such predicate.
 *
 * The decorator's `validateResultPredicate` already ran THIS SAME parser at
 * compile time (and failed the compile on any malformed predicate), so a stored
 * predicate is always re-parseable here — the parse-failure branch is a
 * contract-level invariant guard that is unreachable for any program that
 * compiled. `_kind` names the predicate ("retryWhen" / "failWhen") for clarity
 * at the call site.
 */
function parseOpPredicate(
  program: Parameters<typeof walkModel>[0],
  op: Operation,
  stateKey: symbol,
  _kind: string,
): PredicateNode | undefined {
  const raw = program.stateMap(stateKey).get(op) as string | undefined;
  if (raw === undefined) return undefined;

  const parsed = parseResultPredicate(raw);
  /* v8 ignore start — unreachable: the decorator validator already gated this exact parse at compile time */
  if (!parsed.ok) return undefined;
  /* v8 ignore stop */
  return parsed.root;
}

/**
 * Read the @d2Resilience pipeline DSL stored on `op` and extract the retry
 * `maxAttempts` budget for the TS SSR gRPC client's predicate retry pipeline.
 * Returns undefined when the op carries no pipeline DSL or no `retry(...)` node
 * with an explicit count. The value is op-data (the op's own @d2Resilience
 * declaration), NOT a hand-copied constant.
 *
 * The decorator's $onValidate already ran THIS parser at compile time and failed
 * the compile on any malformed DSL, so a stored DSL is always re-parseable — the
 * parse-failure branch is a contract-level invariant guard, unreachable for any
 * program that compiled.
 */
function parseRetryBudget(
  program: Parameters<typeof walkModel>[0],
  op: Operation,
): number | undefined {
  const raw = program.stateMap(D2_RESILIENCE_KEY).get(op) as string | undefined;
  if (raw === undefined) return undefined;

  const parsed = parseResiliencePipeline(raw);
  /* v8 ignore start — unreachable: $onValidate already gated this exact parse at compile time */
  if (!parsed.ok) return undefined;
  /* v8 ignore stop */

  // Find the retry node's maxAttempts tunable. A predicate-bearing op's DSL carries
  // `retry(...)` at the root in every realistic case (the predicate IS the retry
  // condition), so the inner-walk fallback is defensive for an exotic nesting that
  // no in-scope predicate uses.
  const retryNode = findRetryNode(parsed.root);
  if (retryNode === undefined) return undefined;

  const max = retryNode.tunables["maxAttempts"];
  return typeof max === "number" ? max : undefined;
}

/**
 * Find the `retry` policy node in a linear @d2Resilience policy chain. Returns the
 * root when it is a retry (the realistic case for a predicate op), else walks the
 * single-inner chain. Returns undefined when no retry node exists.
 */
function findRetryNode(
  root: ResiliencePolicyNode,
): ResiliencePolicyNode | undefined {
  // Walk the linear policy chain (retry is at the root for a predicate op in the
  // common case, but may be nested under circuitBreaker/singleflight, or absent).
  let node: ResiliencePolicyNode | undefined = root;
  while (node !== undefined) {
    if (node.policy === "retry") return node;

    node = node.inner;
  }

  return undefined;
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
