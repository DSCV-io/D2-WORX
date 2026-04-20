import { createMiddleware } from "hono/factory";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { D2Result } from "@d2/result";
import { TK } from "@d2/i18n";
import { isValidRole } from "@d2/handler";
import { readRequestContext } from "./read-context.js";

/**
 * Requires the authenticated user to have an active organization. The target
 * org id and type must be present, and the target role must be a valid
 * `Role` value in the canonical hierarchy. Mirrors `.RequireOrg()` on .NET.
 *
 * - 401 if not authenticated.
 * - 403 if authenticated but missing any of the target-org fields, or if
 *   targetOrgRole is not a recognized role name.
 */
export function requireOrg() {
  return createMiddleware(async (c, next) => {
    const ctx = readRequestContext(c);
    if (!ctx?.isAuthenticated || !ctx.userId) {
      return c.json(D2Result.unauthorized(), 401 as ContentfulStatusCode);
    }
    if (!ctx.targetOrgId || !ctx.targetOrgType || !isValidRole(ctx.targetOrgRole)) {
      return c.json(
        D2Result.forbidden({ messages: [TK.middleware.errors.NO_ACTIVE_ORGANIZATION] }),
        403 as ContentfulStatusCode,
      );
    }
    await next();
  });
}
