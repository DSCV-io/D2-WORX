import { createMiddleware } from "hono/factory";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { D2Result } from "@d2/result";
import { readRequestContext } from "./read-context.js";

/**
 * Requires the request to be from a trusted service (S2S, validated by
 * service-key middleware). Mirrors `.RequireTrustedService()` on .NET.
 * Rejects with 401 when `isTrustedService !== true`.
 *
 * Apply at the route or route-group level — handlers downstream can assume
 * the call originated from another internal service, not from a browser.
 */
export function requireTrustedService() {
  return createMiddleware(async (c, next) => {
    const ctx = readRequestContext(c);
    if (ctx?.isTrustedService !== true) {
      return c.json(D2Result.unauthorized(), 401 as ContentfulStatusCode);
    }
    await next();
  });
}
