// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

export { closeChannel, getChannel, type GetChannelOptions } from "./channel.js";
export {
  InternalTokenCache,
  type TryGetResult,
} from "./internal-token-cache.js";
export {
  HttpInternalTokenClient,
  type HttpInternalTokenClientOptions,
  type InternalTokenClient,
} from "./internal-token-client.js";
export { createInternalTokenInterceptor } from "./interceptors/internal-token.js";
export { createContextPropagationInterceptor } from "./interceptors/context-propagation.js";
export type { InternalTokenSnapshot } from "./types.js";
export {
  D2GrpcTrailers,
  type D2GrpcTrailer,
  ALL_D2_GRPC_TRAILERS,
} from "./grpc-trailers.g.js";
// gRPC result codec — D2Result ↔ D2ResultProto wire round-trip
export { d2ResultToProto } from "./d2-result-to-proto.js";
export { d2ResultFromProto } from "./d2-result-from-proto.js";
export {
  handleGrpcCall,
  isTransientGrpcError,
  unaryCall,
  type UnaryCallOptions,
} from "./handle-grpc-call.js";
