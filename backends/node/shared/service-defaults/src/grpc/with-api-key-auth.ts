import { timingSafeEqual } from "node:crypto";
import type * as grpc from "@grpc/grpc-js";
import { status } from "@grpc/grpc-js";
import type { ILogger } from "@d2/logging";

/**
 * Options for the API key authentication wrapper.
 */
export interface ApiKeyAuthOptions {
  /** Set of valid API keys accepted by this service. */
  validKeys: ReadonlySet<string>;
  /** Logger instance for warning on missing/invalid keys. */
  logger: ILogger;
  /** Method names to pass through without API key validation (e.g. "checkHealth"). */
  exempt?: ReadonlySet<string>;
}

/**
 * Wraps a gRPC service implementation with API key validation.
 * Checks the `x-api-key` metadata header against the set of valid keys
 * and rejects with UNAUTHENTICATED if missing or invalid.
 *
 * Mirrors the .NET `ServiceKeyMiddleware` pattern — flat list of valid keys,
 * one per calling service (no context-key mapping needed).
 *
 * `@grpc/grpc-js` does not have a first-class ServerInterceptor type,
 * so we wrap each handler at the service object level.
 *
 * Note: This wrapper assumes all RPCs are unary (ServerUnaryCall + sendUnaryData).
 * If streaming RPCs are added, this must be extended.
 */
export function withApiKeyAuth<T extends Record<string, grpc.UntypedHandleCall>>(
  service: T,
  options: ApiKeyAuthOptions,
): T {
  const { validKeys, logger, exempt } = options;

  // Fail-closed at construction. If validKeys is empty, every authenticated
  // RPC would silently reject (correct) but the service signals "key auth is
  // configured" which is misleading. Throw so misconfiguration surfaces at
  // startup rather than appearing healthy and rejecting all real traffic.
  // Mirrors the pattern landed in commit 95ca94c7 for the auth/files API
  // key middleware (Hono).
  if (validKeys.size === 0) {
    throw new Error(
      "withApiKeyAuth: validKeys is empty. Refusing to wrap service " +
        "with auth that has no accepted keys (would reject all calls). " +
        "Provide at least one key or omit the wrapper if auth is intentionally disabled.",
    );
  }

  const wrapped = {} as Record<string, grpc.UntypedHandleCall>;

  for (const [method, handler] of Object.entries(service)) {
    // Exempt methods pass through without API key checks.
    if (exempt?.has(method)) {
      wrapped[method] = handler;
      continue;
    }

    wrapped[method] = (
      call: grpc.ServerUnaryCall<unknown, unknown>,
      callback: grpc.sendUnaryData<unknown>,
    ) => {
      const apiKey = (call as grpc.ServerUnaryCall<unknown, unknown>).metadata?.get(
        "x-api-key",
      )?.[0];

      if (!apiKey) {
        logger.warn(`Missing x-api-key header on RPC ${method}`);
        callback({
          code: status.UNAUTHENTICATED,
          message: "Missing x-api-key header.",
        });
        return;
      }

      if (!constantTimeHasKey(validKeys, apiKey as string)) {
        logger.warn(`Invalid API key on RPC ${method}`);
        callback({
          code: status.UNAUTHENTICATED,
          message: "Invalid API key.",
        });
        return;
      }

      // Key is valid — delegate to original handler.
      (handler as grpc.handleUnaryCall<unknown, unknown>)(call, callback);
    };
  }

  return wrapped as T;
}

/**
 * Constant-time check for whether a key exists in a set.
 * Iterates all keys and compares with `timingSafeEqual` to prevent timing attacks.
 * Returns true if any key matches.
 */
function constantTimeHasKey(validKeys: ReadonlySet<string>, candidate: string): boolean {
  const candidateBuffer = Buffer.from(candidate, "utf-8");
  let found = false;
  for (const key of validKeys) {
    const keyBuffer = Buffer.from(key, "utf-8");
    if (
      keyBuffer.length === candidateBuffer.length &&
      timingSafeEqual(keyBuffer, candidateBuffer)
    ) {
      found = true;
    }
  }
  return found;
}
