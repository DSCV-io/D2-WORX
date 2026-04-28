import { createMiddleware } from "hono/factory";
import type { ILogger } from "@d2/logging";
import { REQUEST_LOGGER_KEY, type FilesVariables } from "../context-keys.js";

export { REQUEST_LOGGER_KEY };

/**
 * Creates Hono middleware that creates a child logger with request context fields.
 * Must run AFTER request enrichment (needs requestContext).
 */
export function createRequestContextLoggingMiddleware(logger: ILogger) {
  return createMiddleware<{ Variables: FilesVariables }>(async (c, next) => {
    const requestContext = c.var.requestContext;

    if (requestContext) {
      const childLogger = logger.child({
        traceId: requestContext.traceId,
        userId: requestContext.userId,
        clientIp: requestContext.clientIp,
        path: c.req.path,
        method: c.req.method,
      });
      c.set(REQUEST_LOGGER_KEY, childLogger);
    }

    await next();
  });
}
