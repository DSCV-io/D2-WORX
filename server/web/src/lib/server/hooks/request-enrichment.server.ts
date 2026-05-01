/**
 * SvelteKit Handle wrapper for request enrichment.
 * Mirrors auth/api/src/middleware/request-enrichment.ts (~15 lines).
 *
 * Resolves client IP, computes fingerprints, performs WhoIs lookup (fail-open),
 * and stores enriched request context on event.locals for downstream middleware.
 */
import type { Handle } from "@sveltejs/kit";
import { getMiddlewareContext, enrichRequest } from "../middleware.server";

export function createRequestEnrichmentHandle(): Handle {
  return async ({ event, resolve }) => {
    const ctx = getMiddlewareContext();
    if (!ctx) return resolve(event);

    const headers: Record<string, string | undefined> = {};
    event.request.headers.forEach((value, key) => {
      headers[key] = value;
    });

    const requestContext = await enrichRequest(headers, ctx.findWhoIs, undefined, ctx.logger);
    event.locals.requestContext = requestContext;

    return resolve(event);
  };
}
