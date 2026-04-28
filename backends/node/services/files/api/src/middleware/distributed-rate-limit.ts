import { createMiddleware } from "hono/factory";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { D2Result } from "@d2/result";
import { TK } from "@d2/i18n";
import type { RateLimit } from "@d2/interfaces";
import { REQUEST_CONTEXT_KEY } from "./request-enrichment.js";

/**
 * Creates Hono middleware that checks distributed rate limits.
 * Must run AFTER request enrichment middleware (needs requestContext in context).
 */
export function createDistributedRateLimitMiddleware(checkHandler: RateLimit.ICheckHandler) {
  return createMiddleware(async (c, next) => {
    const requestContext = c.get(REQUEST_CONTEXT_KEY);

    if (!requestContext) {
      await next();
      return;
    }

    const result = await checkHandler.handleAsync({ requestContext });

    if (result.success && result.data?.isBlocked) {
      const retryAfterSec = result.data.retryAfterMs
        ? Math.ceil(result.data.retryAfterMs / 1000)
        : 300;
      c.header("Retry-After", String(retryAfterSec));
      return c.json(
        D2Result.tooManyRequests({ messages: [TK.common.errors.TOO_MANY_REQUESTS] }),
        429 as ContentfulStatusCode,
      );
    }

    await next();
  });
}
