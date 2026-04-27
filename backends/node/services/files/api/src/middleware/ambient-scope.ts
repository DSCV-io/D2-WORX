import { createMiddleware } from "hono/factory";
import { requestContextStorage, requestLoggerStorage } from "@d2/handler";
import type { FilesVariables } from "../context-keys.js";

/**
 * Creates Hono middleware that establishes ambient per-request context
 * via AsyncLocalStorage.
 *
 * This makes HandlerContext.request and HandlerContext.logger automatically
 * return the per-request values for ALL handlers — including pre-auth
 * singletons that were constructed with static service-level defaults.
 *
 * Must run AFTER request enrichment middleware.
 */
export function createAmbientScopeMiddleware() {
  return createMiddleware<{ Variables: FilesVariables }>(async (c, next) => {
    const requestContext = c.var.requestContext;
    const logger = c.var.requestLogger;

    if (requestContext && logger) {
      return requestContextStorage.run(requestContext, () =>
        requestLoggerStorage.run(logger, () => next()),
      );
    }
    if (requestContext) {
      return requestContextStorage.run(requestContext, () => next());
    }
    await next();
  });
}
