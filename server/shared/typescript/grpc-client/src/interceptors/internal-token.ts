// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  InterceptingCall,
  type InterceptingListener,
  type Interceptor,
  type Metadata,
  status as GrpcStatus,
} from "@grpc/grpc-js";
import { HttpHeaders } from "@d2/headers-http";
import type { ILogger } from "@d2/logging";
import type { InternalTokenCache } from "../internal-token-cache.js";
import type { InternalTokenClient } from "../internal-token-client.js";

/**
 * Construct a gRPC client interceptor that attaches the BFF's internal
 * token to every outbound call as `Authorization: Bearer <jwt>`. On a
 * `UNAUTHENTICATED` response it clears the cache so the NEXT call re-acquires
 * fresh — the @grpc/grpc-js interceptor SPI does not support truly re-issuing
 * the same call, so the retry layer (e.g. `@d2/resilience`'s `RetryHelper`) is
 * responsible for the second attempt. The cache-clear ensures that retry uses a
 * fresh token. When the cached token is in the refresh-ahead window,
 * {@link TryGetResult.shouldRefreshAhead} returns `true` and a
 * fire-and-forget background mint is triggered to avoid expiry latency on the
 * next call.
 *
 * SECURITY-CRITICAL: token bytes never reach Pino. Diagnostic logs only
 * record metadata SHAPE — `{ method, hasToken, status }` — never values.
 */
export function createInternalTokenInterceptor(opts: {
  cache: InternalTokenCache;
  tokenClient: InternalTokenClient;
  logger?: ILogger;
}): Interceptor {
  const { cache, tokenClient, logger } = opts;

  async function mintAndCache(): Promise<string | undefined> {
    const result = await tokenClient.acquireToken();
    if (!result.success || result.data === undefined) {
      logger?.warn("internal-token interceptor: token acquire failed", {
        errorCode: result.errorCode,
      });
      return undefined;
    }
    cache.set(result.data);
    return result.data.accessToken;
  }

  async function getToken(): Promise<string | undefined> {
    const { snapshot, shouldRefreshAhead } = cache.tryGet();
    if (snapshot !== undefined) {
      if (shouldRefreshAhead) {
        // Token is still valid but entering the refresh-ahead window — fire a
        // background mint so the next call finds a fresh token.  Do NOT await.
        // Errors are swallowed: a failed ahead-refresh just means the next call
        // mints synchronously; there must be NO unhandled promise rejection.
        mintAndCache().catch((err: unknown) => {
          logger?.warn("internal-token interceptor: refresh-ahead failed", {
            errorName: err instanceof Error ? err.name : "unknown",
          });
        });
      }
      return snapshot.accessToken;
    }
    return mintAndCache();
  }

  return (options, nextCall) => {
    return new InterceptingCall(nextCall(options), {
      start(metadata, listener, next) {
        const sendWithToken = (token: string | undefined): void => {
          if (token !== undefined) {
            metadata.set(HttpHeaders.AUTHORIZATION, `Bearer ${token}`);
          }
          // Wrap the listener so we can detect UNAUTHENTICATED + clear
          // the cache so the next attempt re-acquires fresh.
          const wrappedListener: InterceptingListener = {
            onReceiveMetadata(md: Metadata) {
              listener.onReceiveMetadata(md);
            },
            onReceiveMessage(msg: unknown) {
              listener.onReceiveMessage(msg);
            },
            onReceiveStatus(status) {
              if (status.code === GrpcStatus.UNAUTHENTICATED) {
                logger?.info("internal-token interceptor: clearing on 401", {
                  method: options.method_definition.path,
                });
                cache.clear();
              }
              listener.onReceiveStatus(status);
            },
          };
          next(metadata, wrappedListener);
        };
        getToken().then(sendWithToken, () => sendWithToken(undefined));
      },
    });
  };
}
