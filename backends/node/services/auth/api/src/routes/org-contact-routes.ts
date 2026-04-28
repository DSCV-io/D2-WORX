import { Hono } from "hono";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { HttpStatusCode } from "@d2/result";
import { cleanDisplayStr } from "@d2/utilities";
import { SESSION_FIELDS } from "@d2/auth-domain";
import {
  ICreateOrgContactKey,
  IUpdateOrgContactKey,
  IDeleteOrgContactKey,
  IGetOrgContactsKey,
} from "@d2/auth-app";
import type { SessionVariables } from "../middleware/session.js";
import { type ScopeVariables } from "../middleware/scope.js";
import { SCOPE_KEY, SESSION_KEY } from "../context-keys.js";
import { requireOrg, requireRole } from "@d2/auth-policy";

/** Default and maximum page sizes for list endpoints. */
const DEFAULT_LIMIT = 50;
const MAX_LIMIT = 100;

/**
 * Thin routes for org contact junction management.
 * Authorization is visible at route declaration via middleware.
 * All validation, IDOR checks, and business logic live in handlers.
 * Handlers are resolved from the per-request DI scope.
 */
export function createOrgContactRoutes() {
  const app = new Hono<{ Variables: SessionVariables & ScopeVariables }>();

  // POST — Create contact (org member, officer+)
  // Body: { label, isPrimary?, contact: { contactMethods?, personalDetails?, ... } }
  app.post("/api/org-contacts", requireOrg(), requireRole("officer"), async (c) => {
    const body = await c.req.json();
    const handler = c.get(SCOPE_KEY).resolve(ICreateOrgContactKey);
    const result = await handler.handleAsync({
      organizationId: c.get(SESSION_KEY)![SESSION_FIELDS.ACTIVE_ORG_ID] as string,
      label: cleanDisplayStr(body.label as string) ?? body.label,
      isPrimary: body.isPrimary,
      contact: body.contact ?? {},
    });
    const status = (
      result.success ? HttpStatusCode.Created : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // PATCH — Update contact (org member, officer+)
  // Body: { label?, isPrimary?, contact?: { ... } }
  app.patch("/api/org-contacts/:id", requireOrg(), requireRole("officer"), async (c) => {
    const body = await c.req.json();
    const handler = c.get(SCOPE_KEY).resolve(IUpdateOrgContactKey);
    const result = await handler.handleAsync({
      id: c.req.param("id"),
      organizationId: c.get(SESSION_KEY)![SESSION_FIELDS.ACTIVE_ORG_ID] as string,
      updates: {
        label:
          body.label != null ? (cleanDisplayStr(body.label as string) ?? body.label) : undefined,
        isPrimary: body.isPrimary,
        contact: body.contact,
      },
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // DELETE — Delete contact (org member, officer+)
  app.delete("/api/org-contacts/:id", requireOrg(), requireRole("officer"), async (c) => {
    const handler = c.get(SCOPE_KEY).resolve(IDeleteOrgContactKey);
    const result = await handler.handleAsync({
      id: c.req.param("id"),
      organizationId: c.get(SESSION_KEY)![SESSION_FIELDS.ACTIVE_ORG_ID] as string,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // GET — List contacts (org member, any role)
  app.get("/api/org-contacts", requireOrg(), async (c) => {
    const limitParam = parseInt(c.req.query("limit") ?? "", 10);
    const offsetParam = parseInt(c.req.query("offset") ?? "", 10);
    const limit = Math.min(
      Number.isFinite(limitParam) && limitParam > 0 ? limitParam : DEFAULT_LIMIT,
      MAX_LIMIT,
    );
    const offset = Number.isFinite(offsetParam) && offsetParam >= 0 ? offsetParam : 0;

    const handler = c.get(SCOPE_KEY).resolve(IGetOrgContactsKey);
    const result = await handler.handleAsync({
      organizationId: c.get(SESSION_KEY)![SESSION_FIELDS.ACTIVE_ORG_ID] as string,
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
