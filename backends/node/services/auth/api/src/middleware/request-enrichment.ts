import { createMiddleware } from "hono/factory";
import {
  enrichRequest,
  isInfrastructurePath,
  type RequestEnrichmentOptions,
} from "@d2/request-enrichment";
import type { FindWhoIs } from "@d2/geo-client";
import type { ILogger } from "@d2/logging";
import { REQUEST_CONTEXT_KEY } from "../context-keys.js";

export { REQUEST_CONTEXT_KEY };

/**
 * Creates Hono middleware that enriches every request with client info.
 * Sets `c.set("requestContext", ctx)` for downstream middleware/routes.
 *
 * Infrastructure paths (`/health`, `/ready`, `/metrics`, `/alive`, `/api/health`)
 * skip the full enrichment — including the WhoIs lookup, which is gRPC.
 * Probing `/health` thousands of times per minute should not trigger Geo
 * calls. Mirrors the .NET `InfrastructurePaths.IsInfrastructure` bypass.
 */
export function createRequestEnrichmentMiddleware(
  findWhoIs: FindWhoIs,
  options?: Partial<RequestEnrichmentOptions>,
  logger?: ILogger,
) {
  return createMiddleware(async (c, next) => {
    if (isInfrastructurePath(c.req.path)) {
      return next();
    }
    const headers: Record<string, string | undefined> = {};
    c.req.raw.headers.forEach((value, key) => {
      headers[key] = value;
    });

    const requestContext = await enrichRequest(headers, findWhoIs, options, logger);
    c.set(REQUEST_CONTEXT_KEY, requestContext);
    await next();
  });
}
