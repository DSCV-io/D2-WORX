import { createMiddleware } from "hono/factory";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { D2Result } from "@d2/result";
import { TK } from "@d2/i18n";
import { ROLE_HIERARCHY, isValidRole, type Role } from "@d2/handler";
import { readRequestContext } from "./read-context.js";

/**
 * Requires the authenticated user's active role to be at-or-above `minRole`
 * in the hierarchy (auditor < agent < officer < owner). Mirrors
 * `.RequireRole(min)` on .NET.
 *
 * - 401 if not authenticated.
 * - 403 if active role is below `minRole` or missing/invalid.
 */
export function requireRole(minRole: Role) {
  const minLevel = ROLE_HIERARCHY[minRole];
  return createMiddleware(async (c, next) => {
    const ctx = readRequestContext(c);
    if (!ctx?.isAuthenticated || !ctx.userId) {
      return c.json(D2Result.unauthorized(), 401 as ContentfulStatusCode);
    }
    const role = ctx.targetOrgRole;
    if (!isValidRole(role) || ROLE_HIERARCHY[role] < minLevel) {
      return c.json(
        D2Result.forbidden({ messages: [TK.middleware.errors.INSUFFICIENT_ROLE] }),
        403 as ContentfulStatusCode,
      );
    }
    await next();
  });
}
