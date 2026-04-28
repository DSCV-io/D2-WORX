import { Hono } from "hono";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { HttpStatusCode } from "@d2/result";
import { SESSION_FIELDS, type OrgType } from "@d2/auth-domain";
import {
  ICreateEmulationConsentKey,
  IRevokeEmulationConsentKey,
  IGetActiveConsentsKey,
} from "@d2/auth-app";
import type { SessionVariables } from "../middleware/session.js";
import { type ScopeVariables } from "../middleware/scope.js";
import { SCOPE_KEY, SESSION_KEY, USER_KEY } from "../context-keys.js";
import { requireOrg, requireStaff, requireRole } from "@d2/auth-policy";

/** Default and maximum page sizes for list endpoints. */
const DEFAULT_LIMIT = 50;
const MAX_LIMIT = 100;

/**
 * Thin routes for emulation consent management.
 * Authorization is visible at route declaration via middleware.
 * All validation and business logic lives in handlers.
 * Handlers are resolved from the per-request DI scope.
 */
export function createEmulationRoutes() {
  const app = new Hono<{ Variables: SessionVariables & ScopeVariables }>();

  // POST — Create consent (staff officer+)
  app.post(
    "/api/emulation/consent",
    requireOrg(),
    requireStaff(),
    requireRole("officer"),
    async (c) => {
      const body = await c.req.json();
      const handler = c.get(SCOPE_KEY).resolve(ICreateEmulationConsentKey);
      const result = await handler.handleAsync({
        userId: c.get(USER_KEY)!.id,
        grantedToOrgId: body.grantedToOrgId,
        activeOrgType: c.get(SESSION_KEY)![SESSION_FIELDS.ACTIVE_ORG_TYPE] as OrgType,
        expiresAt: new Date(body.expiresAt),
      });
      const status = (
        result.success ? HttpStatusCode.Created : (result.statusCode ?? HttpStatusCode.BadRequest)
      ) as ContentfulStatusCode;
      return c.json(result, status);
    },
  );

  // DELETE — Revoke consent (staff officer+)
  app.delete(
    "/api/emulation/consent/:id",
    requireOrg(),
    requireStaff(),
    requireRole("officer"),
    async (c) => {
      const handler = c.get(SCOPE_KEY).resolve(IRevokeEmulationConsentKey);
      const result = await handler.handleAsync({
        consentId: c.req.param("id"),
        userId: c.get(USER_KEY)!.id,
      });
      const status = (
        result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
      ) as ContentfulStatusCode;
      return c.json(result, status);
    },
  );

  // GET — List active consents (staff, any role)
  app.get("/api/emulation/consent", requireOrg(), requireStaff(), async (c) => {
    const limitParam = parseInt(c.req.query("limit") ?? "", 10);
    const offsetParam = parseInt(c.req.query("offset") ?? "", 10);
    const limit = Math.min(
      Number.isFinite(limitParam) && limitParam > 0 ? limitParam : DEFAULT_LIMIT,
      MAX_LIMIT,
    );
    const offset = Number.isFinite(offsetParam) && offsetParam >= 0 ? offsetParam : 0;

    const handler = c.get(SCOPE_KEY).resolve(IGetActiveConsentsKey);
    const result = await handler.handleAsync({
      userId: c.get(USER_KEY)!.id,
      limit,
      offset,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  return app;
}
