// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Package barrel — the API surface that consumers import.
// Re-exports the emitter entry point, the $lib descriptor, and the
// shared-lib public surface (scalar registry, name transforms, banner,
// emit-file utilities, model walker, DTO emitters) so all emitters in the
// fleet import from the barrel.

export {
  $onEmit,
  resolveStringMapOption,
  resolveProcessKindByModule,
  resolveRepoRootFromProjectRoot,
  resolveTsClientOutputDirs,
} from "./emitter.js";
export type {
  ManifestOperation,
  OperationsManifest,
  ProcessKind,
} from "./emitter.js";

export { $lib } from "./lib.js";

export {
  emitBridgeRegistration,
  emitMapAllBridges,
} from "./lib/bridge-emitter.js";
export type { BridgeEmitInput, BridgeModuleOp } from "./lib/bridge-emitter.js";

export { resolveScalar, hasScalar } from "./lib/scalar-registry.js";
export type { ScalarMapping } from "./lib/scalar-registry.js";

export { toSnake, toPascal } from "./lib/name-transforms.js";

export { buildBanner } from "./lib/banner.js";

export { emitGeneratedFile, resolveOutputPath } from "./lib/emit-file.js";

export { walkModel } from "./lib/model-walk.js";
export type { FieldInfo, NestedModel, WalkResult } from "./lib/model-walk.js";

export { emitCsharpDtos } from "./lib/csharp-dto-emitter.js";
export type { EmittedFile } from "./lib/csharp-dto-emitter.js";

export { emitTsDtos } from "./lib/ts-dto-emitter.js";
export type { EmittedTsFile } from "./lib/ts-dto-emitter.js";

export { emitProto } from "./lib/proto-emitter.js";
export type { ProtoFieldInfo, StreamingMode } from "./lib/proto-emitter.js";

export { emitGrpcService } from "./lib/grpc-service-emitter.js";

export {
  emitIdempotencyStoreSeam,
  buildIdempotencyGate,
} from "./lib/idempotency-gate-emitter.js";
export type {
  IdempotencyGateInput,
  IdempotencyGateWeave,
  IdempotencyKeySource,
} from "./lib/idempotency-gate-emitter.js";

export {
  emitOpenApiDocuments,
  injectD2Extensions,
  OPENAPI_GENERATED_BY_EMITTER,
} from "./lib/openapi-emitter.js";
export type {
  D2ScopeExtension,
  D2GeneratedByExtension,
  OpenApiHttpMethod,
} from "./lib/openapi-emitter.js";

export {
  WIRE_CHANNEL_GRAMMAR,
  parseChannel,
  expectedCsharpChannelSegment,
  validateChannelAgreement,
} from "./lib/wire-channel.js";
export type { WireChannel } from "./lib/wire-channel.js";

export { emitWireVersionConstant } from "./lib/wire-version-emitter.js";

export { emitWireIdentityManifest } from "./lib/wire-manifest-emitter.js";
export type { WireIdentityManifest } from "./lib/wire-manifest-emitter.js";
