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
import type { KeyCustodianClient } from "../key-custodian-client.js";

/**
 * Construct a gRPC client interceptor that attaches the BFF's internal
 * token to every outbound call as `Authorization: Bearer <jwt>`. On a
 * `UNAUTHENTICATED` response it clears the cache so the NEXT call hits
 * KeyCustodian fresh — the @grpc/grpc-js interceptor SPI does not
 * support truly re-issuing the same call, so the retry layer (e.g.
 * `@d2/resilience`'s `RetryHelper`) is responsible for the second
 * attempt. The cache-clear ensures that retry uses a fresh token.
 *
 * SECURITY-CRITICAL: token bytes never reach Pino. Diagnostic logs only
 * record metadata SHAPE — `{ method, hasToken, status }` — never values.
 */
export function createInternalTokenInterceptor(opts: {
  cache: InternalTokenCache;
  keyCustodian: KeyCustodianClient;
  logger?: ILogger;
}): Interceptor {
  const { cache, keyCustodian, logger } = opts;

  async function getToken(): Promise<string | null> {
    const cached = cache.tryGet();
    if (cached !== null) return cached.accessToken;
    const result = await keyCustodian.acquireToken();
    if (!result.success || result.data === undefined) {
      logger?.warn("internal-token interceptor: KeyCustodian acquire failed", {
        errorCode: result.errorCode,
      });
      return null;
    }
    cache.set(result.data);
    return result.data.accessToken;
  }

  return (options, nextCall) => {
    return new InterceptingCall(nextCall(options), {
      start(metadata, listener, next) {
        const sendWithToken = (token: string | null): void => {
          if (token !== null) {
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
        getToken().then(sendWithToken, () => sendWithToken(null));
      },
    });
  };
}
