import { createMiddleware } from "hono/factory";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { D2Result } from "@d2/result";
import { readRequestContext } from "./read-context.js";

/**
 * Requires an authenticated user (any user — no org/role checks). Mirrors
 * `.RequireAuth()` on .NET. Rejects with 401 when no `IRequestContext` is
 * present or `isAuthenticated !== true` or `userId` is missing.
 *
 * Apply at the route or route-group level — handlers downstream can read
 * `c.get("requestContext").userId` and trust it is populated.
 */
export function requireAuth() {
  return createMiddleware(async (c, next) => {
    const ctx = readRequestContext(c);
    if (!ctx?.isAuthenticated || !ctx.userId) {
      return c.json(D2Result.unauthorized(), 401 as ContentfulStatusCode);
    }
    await next();
  });
}
