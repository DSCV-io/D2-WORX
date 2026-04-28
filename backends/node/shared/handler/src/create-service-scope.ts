import type { ServiceProvider, ServiceScope } from "@d2/di";
import type { ILogger } from "@d2/logging";
import { ILoggerKey } from "@d2/logging";
import { HandlerContext } from "./handler-context.js";
import { IHandlerContextKey, IRequestContextKey } from "./service-keys.js";
import type { IRequestContext } from "./i-request-context.js";
import { requestContextStorage, requestLoggerStorage } from "./ambient-context.js";

/**
 * Creates a disposable DI scope with a fresh traceId and no auth context.
 * Used for per-RPC, per-message, callback, and startup operations.
 *
 * This extracts the common pattern from Auth's createCallbackScope and Comms' createServiceScope.
 *
 * @param provider - The root ServiceProvider to create a scope from.
 * @param logger - Optional logger override. If not provided, resolves ILoggerKey from the provider.
 */
export function createServiceScope(provider: ServiceProvider, logger?: ILogger): ServiceScope {
  const scope = provider.createScope();
  const requestContext: IRequestContext = {
    traceId: crypto.randomUUID(),
    isAuthenticated: false,
    isTrustedService: null,
    isOrgEmulating: null,
    isUserImpersonating: null,
  };
  scope.setInstance(IRequestContextKey, requestContext);
  const resolvedLogger = logger ?? provider.resolve(ILoggerKey);
  scope.setInstance(IHandlerContextKey, new HandlerContext(requestContext, resolvedLogger));

  // Set ambient storage so handlers see per-request context regardless of
  // how they were constructed (singleton vs scoped). Uses enterWith() to
  // bind to the current async execution context.
  requestContextStorage.enterWith(requestContext);
  requestLoggerStorage.enterWith(resolvedLogger);

  return scope;
}
