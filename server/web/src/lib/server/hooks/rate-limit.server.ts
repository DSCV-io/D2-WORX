/**
 * SvelteKit Handle wrapper for distributed rate limiting.
 * Mirrors auth/api/src/middleware/distributed-rate-limit.ts (~20 lines).
 *
 * Checks multi-dimensional sliding-window rate limits using enriched
 * request context from the request-enrichment handle. Returns 429 with
 * Retry-After header if any dimension exceeds its threshold.
 */
import type { Handle } from "@sveltejs/kit";
import { TK } from "@d2/i18n/keys";
import { getMiddlewareContext } from "../middleware.server";

export function createRateLimitHandle(): Handle {
  return async ({ event, resolve }) => {
    const ctx = getMiddlewareContext();
    if (!ctx) return resolve(event);

    if (!event.locals.requestContext) return resolve(event);

    const result = await ctx.rateLimitCheck.handleAsync({
      requestContext: event.locals.requestContext,
    });

    if (result.success && result.data?.isBlocked) {
      const retryAfterSec = result.data.retryAfterMs
        ? Math.ceil(result.data.retryAfterMs / 1000)
        : 300;

      return new Response(
        JSON.stringify({
          success: false,
          messages: [TK.common.errors.TOO_MANY_REQUESTS],
          statusCode: 429,
          errorCode: "RATE_LIMITED",
        }),
        {
          status: 429,
          headers: {
            "Content-Type": "application/json",
            "Retry-After": String(retryAfterSec),
          },
        },
      );
    }

    return resolve(event);
  };
}
