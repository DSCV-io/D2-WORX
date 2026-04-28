import { createMiddleware } from "hono/factory";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { D2Result } from "@d2/result";
import { TK } from "@d2/i18n";
import type { OrgType } from "@d2/handler";
import { readRequestContext } from "./read-context.js";

/**
 * Requires the authenticated user's active org to be one of the listed
 * `OrgType` values. Mirrors `.RequireOrgType(...)` on .NET.
 *
 * - 401 if not authenticated.
 * - 403 if active org type is not in the allowed set.
 */
export function requireOrgType(...allowed: OrgType[]) {
  const allowedSet = new Set<OrgType>(allowed);
  return createMiddleware(async (c, next) => {
    const ctx = readRequestContext(c);
    if (!ctx?.isAuthenticated || !ctx.userId) {
      return c.json(D2Result.unauthorized(), 401 as ContentfulStatusCode);
    }
    if (!ctx.targetOrgType || !allowedSet.has(ctx.targetOrgType)) {
      return c.json(
        D2Result.forbidden({ messages: [TK.middleware.errors.ORG_TYPE_NOT_AUTHORIZED] }),
        403 as ContentfulStatusCode,
      );
    }
    await next();
  });
}
