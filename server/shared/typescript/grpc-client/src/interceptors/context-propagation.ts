// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { InterceptingCall, type Interceptor } from "@grpc/grpc-js";
import { CommonHeaders } from "@d2/headers-common";
import type { ILogger } from "@d2/logging";
import {
  PropagatedContextSerializer,
  type IPropagatedContext,
} from "@d2/request-context-abstractions";

/**
 * Construct a gRPC client interceptor that injects the current request's
 * `IPropagatedContext` envelope as the `x-d2-context` metadata key, and
 * forwards `traceparent` / `tracestate` from the active W3C propagator.
 *
 * The envelope is base64url-of-JSON via
 * `PropagatedContextSerializer.serialize()` — same wire format as the
 * .NET emitter.
 *
 * SECURITY: only the SHAPE of the context is logged in diagnostic logs;
 * the actual envelope value never reaches Pino.
 */
export function createContextPropagationInterceptor(opts: {
  /**
   * Returns the current request's propagated context, or undefined when
   * off-request (e.g. service-to-service from a non-request worker).
   */
  readonly getCurrentContext: () => IPropagatedContext | undefined;
  /** Returns the current `traceparent` (W3C trace context), or undefined. */
  readonly getCurrentTraceparent?: () => string | undefined;
  /** Returns the current `tracestate`, or undefined. */
  readonly getCurrentTracestate?: () => string | undefined;
  readonly logger?: ILogger;
}): Interceptor {
  const {
    getCurrentContext,
    getCurrentTraceparent,
    getCurrentTracestate,
    logger,
  } = opts;

  return (options, nextCall) => {
    return new InterceptingCall(nextCall(options), {
      start(metadata, listener, next) {
        const ctx = getCurrentContext();
        if (ctx !== undefined) {
          const json = PropagatedContextSerializer.serialize(ctx);
          const encoded = Buffer.from(json, "utf8").toString("base64url");
          metadata.set(CommonHeaders.PROPAGATED_CONTEXT, encoded);
        }
        const tp = getCurrentTraceparent?.();
        if (tp !== undefined && tp.length > 0) {
          metadata.set(CommonHeaders.TRACEPARENT, tp);
        }
        const ts = getCurrentTracestate?.();
        if (ts !== undefined && ts.length > 0) {
          metadata.set(CommonHeaders.TRACESTATE, ts);
        }
        logger?.debug("context-propagation interceptor attached metadata", {
          method: options.method_definition.path,
          hasContext: ctx !== undefined,
          hasTraceparent: tp !== undefined && tp.length > 0,
          hasTracestate: ts !== undefined && ts.length > 0,
        });
        next(metadata, listener);
      },
    });
  };
}
