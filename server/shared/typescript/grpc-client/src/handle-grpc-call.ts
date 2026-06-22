// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  type D2Result,
  canceled,
  unauthorized,
  serviceUnavailable,
  unhandledException,
} from "@d2/result";
import type { D2ResultProto } from "@d2/protos";
import { Metadata } from "@grpc/grpc-js";
import type { ServiceError, ClientUnaryCall, CallOptions } from "@grpc/grpc-js";
import { TK } from "@d2/i18n-keys";
import { d2ResultFromProto } from "./d2-result-from-proto.js";

// gRPC status codes — subset used for transient classification and
// semantic mapping. Mirrors .NET `StatusCode` enum from `Grpc.Core`.
const _STATUS_CANCELLED = 1;
const _STATUS_DEADLINE_EXCEEDED = 4;
const _STATUS_RESOURCE_EXHAUSTED = 8;
const _STATUS_ABORTED = 10;
const _STATUS_INTERNAL = 13;
const _STATUS_UNAVAILABLE = 14;
const _STATUS_UNAUTHENTICATED = 16;

/**
 * Type guard for gRPC `ServiceError`.
 * `ServiceError` extends `Error` with a numeric `code` (gRPC status code).
 */
function isServiceError(err: unknown): err is ServiceError {
  return (
    err instanceof Error &&
    "code" in err &&
    typeof (err as ServiceError).code === "number"
  );
}

/**
 * Execute a gRPC unary call and convert the response to `D2Result<TData>`.
 * Mirrors .NET `AsyncUnaryCall<TProto>.HandleAsync<TData>(resultSelector, dataSelector)`.
 *
 * On a successful response: calls `resultSelector(response)` → `D2ResultProto`;
 * calls `dataSelector(response)` → typed payload; stitches them via
 * `d2ResultFromProto(proto, data)` and returns the `D2Result`.
 *
 * On a gRPC transport fault (`ServiceError`): returns a TK-constant-messaged
 * failure (`canceled`, `unauthorized`, or `serviceUnavailable`) — NEVER
 * exposes `err.details` or `err.message`. User-facing messages are TK constants;
 * raw transport strings (broker URIs, host detail) never reach the client.
 *
 * On any other exception: returns `unhandledException` with a TK constant.
 *
 * @param callFn         - Factory that returns the Promise of the gRPC response.
 * @param resultSelector - Extracts the `D2ResultProto` envelope from the response.
 * @param dataSelector   - Extracts the typed payload from the response.
 * @param traceId        - Optional trace identifier threaded into transport-fault
 *                         results so callers can correlate the failure. Mirrors
 *                         .NET `HandleAsync<TData>(…, string? traceId = null)`.
 */
export async function handleGrpcCall<TResponse, TData>(
  callFn: () => Promise<TResponse>,
  resultSelector: (response: TResponse) => D2ResultProto,
  dataSelector: (response: TResponse) => TData | undefined,
  traceId?: string,
): Promise<D2Result<TData>> {
  try {
    const response = await callFn();
    const proto = resultSelector(response);
    const data = dataSelector(response);
    return d2ResultFromProto<TData>(proto, data);
  } catch (err: unknown) {
    if (isServiceError(err)) {
      // User-facing messages are TK constants — raw transport strings (broker
      // URIs, host detail) never reach the client. The gRPC numeric `err.code`
      // (closed enum) is safe to log separately if needed.
      if (err.code === _STATUS_CANCELLED)
        return canceled<TData>({
          messages: [TK.common.errors.CANCELED],
          traceId,
        });
      if (err.code === _STATUS_UNAUTHENTICATED)
        return unauthorized<TData>({
          messages: [TK.common.errors.UNAUTHORIZED],
          traceId,
        });
      return serviceUnavailable<TData>({
        messages: [TK.common.errors.SERVICE_UNAVAILABLE],
        traceId,
      });
    }
    return unhandledException<TData>({
      messages: [TK.common.errors.UNKNOWN],
      traceId,
    });
  }
}

/**
 * Returns `true` when the gRPC status code indicates a transient fault that
 * a caller may safely retry.
 *
 * Transient set (mirrors .NET `ProtoExtensions.IsTransientGrpcException`):
 * - `4  DEADLINE_EXCEEDED` — timeout; retry with backoff
 * - `8  RESOURCE_EXHAUSTED` — throttled; retry with backoff
 * - `10 ABORTED` — concurrency / optimistic-lock failure; retry
 * - `13 INTERNAL` — transient server-side error
 * - `14 UNAVAILABLE` — service down / restart; retry
 *
 * Non-transient (not safe to retry without intervention):
 * - `1  CANCELLED` — caller canceled
 * - `3  INVALID_ARGUMENT` — bad request; fix the input
 * - `5  NOT_FOUND` — resource missing
 * - `7  PERMISSION_DENIED` — auth policy failure
 * - `16 UNAUTHENTICATED` — token expired/invalid; refresh before retry
 */
export function isTransientGrpcError(err: unknown): boolean {
  if (!isServiceError(err)) return false;
  const code = err.code;
  return (
    code === _STATUS_DEADLINE_EXCEEDED ||
    code === _STATUS_RESOURCE_EXHAUSTED ||
    code === _STATUS_ABORTED ||
    code === _STATUS_INTERNAL ||
    code === _STATUS_UNAVAILABLE
  );
}

/**
 * Callback-style gRPC unary method signature (without call options).
 */
type GrpcUnaryMethod<TReq, TRes> = (
  request: TReq,
  callback: (error: ServiceError | null, response: TRes) => void,
) => ClientUnaryCall;

/**
 * Callback-style gRPC unary method signature (with metadata + call options).
 */
type GrpcUnaryMethodFull<TReq, TRes> = (
  request: TReq,
  metadata: Metadata,
  options: CallOptions,
  callback: (error: ServiceError | null, response: TRes) => void,
) => ClientUnaryCall;

/** Options accepted by `unaryCall`. */
export interface UnaryCallOptions {
  /** Request deadline in milliseconds from now. Passed as a gRPC `CallOptions` deadline. */
  readonly deadlineMs?: number;
}

/**
 * Wrap a `@grpc/grpc-js` callback-style unary method in a Promise.
 * Bridges the gRPC callback API to the Promise-based `handleGrpcCall` ergonomics.
 *
 * Usage:
 * ```ts
 * const result = await handleGrpcCall(
 *   () => unaryCall(client.doThing.bind(client), req, { deadlineMs: 5_000 }),
 *   r => r.result,
 *   r => r.data,
 * );
 * ```
 *
 * @param method  - Bound gRPC client method (callback-style).
 * @param request - The request message.
 * @param opts    - Optional `deadlineMs` for a call-level deadline.
 */
export function unaryCall<TReq, TRes>(
  method: GrpcUnaryMethod<TReq, TRes> | GrpcUnaryMethodFull<TReq, TRes>,
  request: TReq,
  opts: UnaryCallOptions = {},
): Promise<TRes> {
  return new Promise<TRes>((resolve, reject) => {
    const callback = (error: ServiceError | null, response: TRes) => {
      if (error !== null) {
        reject(error);
        return;
      }
      resolve(response);
    };

    if (opts.deadlineMs !== undefined) {
      const callOpts: CallOptions = {
        deadline: new Date(Date.now() + opts.deadlineMs),
      };
      (method as GrpcUnaryMethodFull<TReq, TRes>)(
        request,
        new Metadata(),
        callOpts,
        callback,
      );
    } else {
      (method as GrpcUnaryMethod<TReq, TRes>)(request, callback);
    }
  });
}
