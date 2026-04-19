import { Hono } from "hono";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { HttpStatusCode } from "@d2/result";
import {
  IUpdateUserRealNameKey,
  IUpdateUsernameKey,
  IUpdateUserLocaleKey,
  IUpdateUserTimezoneKey,
  IGetSignInEventsKey,
  IUpdateUserImageKey,
  IInvalidateUserSessionCacheKey,
  IPushUserUpdatedKey,
  IRequestEmailChangeKey,
  IVerifyEmailChangeKey,
  IRequestPhoneChangeKey,
  IVerifyPhoneChangeKey,
  IRemovePhoneKey,
} from "@d2/auth-app";
import type { SessionVariables } from "../middleware/session.js";
import type { ScopeVariables } from "../middleware/scope.js";
import { SCOPE_KEY, SESSION_KEY } from "../context-keys.js";
import type { Auth } from "@d2/auth-infra";

/** Default and maximum page sizes for list endpoints. */
const DEFAULT_LIMIT = 50;
const MAX_LIMIT = 100;

/**
 * Account routes — user-level, org-agnostic endpoints.
 * All require authentication (session middleware) but no org membership.
 * userId is derived from session, never from request body (IDOR prevention).
 */
export function createAccountRoutes(auth: Auth) {
  const app = new Hono<{ Variables: SessionVariables & ScopeVariables }>();

  // PATCH /api/account/name — Update user's real name (firstName + lastName)
  app.patch("/api/account/name", async (c) => {
    const body = await c.req.json();
    const session = c.get(SESSION_KEY);
    if (!session) return c.json({ success: false, statusCode: 401 }, 401 as ContentfulStatusCode);

    const handler = c.get(SCOPE_KEY).resolve(IUpdateUserRealNameKey);
    const result = await handler.handleAsync({
      userId: session.userId as string,
      firstName: body.firstName as string,
      lastName: body.lastName as string,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // PATCH /api/account/username — Update username
  app.patch("/api/account/username", async (c) => {
    const body = await c.req.json();
    const session = c.get(SESSION_KEY);
    if (!session) return c.json({ success: false, statusCode: 401 }, 401 as ContentfulStatusCode);

    const handler = c.get(SCOPE_KEY).resolve(IUpdateUsernameKey);
    const result = await handler.handleAsync({
      userId: session.userId as string,
      username: body.username as string,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // PATCH /api/account/locale — Update user's locale preference
  app.patch("/api/account/locale", async (c) => {
    const body = await c.req.json();
    const session = c.get(SESSION_KEY);
    if (!session) return c.json({ success: false, statusCode: 401 }, 401 as ContentfulStatusCode);

    const handler = c.get(SCOPE_KEY).resolve(IUpdateUserLocaleKey);
    const result = await handler.handleAsync({
      userId: session.userId as string,
      locale: body.locale as string,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // PATCH /api/account/timezone — Update user's timezone preference
  app.patch("/api/account/timezone", async (c) => {
    const body = await c.req.json();
    const session = c.get(SESSION_KEY);
    if (!session) return c.json({ success: false, statusCode: 401 }, 401 as ContentfulStatusCode);

    const handler = c.get(SCOPE_KEY).resolve(IUpdateUserTimezoneKey);
    const result = await handler.handleAsync({
      userId: session.userId as string,
      timezone: body.timezone as string,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // DELETE /api/account/avatar — Remove user's profile picture
  app.delete("/api/account/avatar", async (c) => {
    const session = c.get(SESSION_KEY);
    if (!session) return c.json({ success: false, statusCode: 401 }, 401 as ContentfulStatusCode);

    const scope = c.get(SCOPE_KEY);
    const userId = session.userId as string;

    const handler = scope.resolve(IUpdateUserImageKey);
    const invalidate = scope.tryResolve(IInvalidateUserSessionCacheKey);
    const push = scope.tryResolve(IPushUserUpdatedKey);

    const result = await handler.handleAsync({ userId, image: null });

    if (result.success) {
      // Fire-and-forget: invalidate cache → then push SignalR event.
      // Client relies on the user:updated event for session refresh, not the API response.
      invalidate
        ?.handleAsync({ userId })
        .then(() => push?.handleAsync({ userId }))
        .catch(() => {});
    }

    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // GET /api/account/sessions — List active sessions (BetterAuth native)
  app.get("/api/account/sessions", async (c) => {
    try {
      const sessions = await auth.api.listSessions({ headers: c.req.raw.headers });
      return c.json({ success: true, statusCode: 200, data: { sessions } });
    } catch {
      return c.json(
        { success: false, statusCode: 500, messages: ["Failed to retrieve sessions."] },
        500 as ContentfulStatusCode,
      );
    }
  });

  // POST /api/account/sessions/revoke — Revoke a specific session by token
  app.post("/api/account/sessions/revoke", async (c) => {
    const body = await c.req.json();
    const token = body.token as string;
    if (!token) {
      return c.json(
        { success: false, statusCode: 400, messages: ["Session token is required."] },
        400 as ContentfulStatusCode,
      );
    }
    try {
      await auth.api.revokeSession({
        headers: c.req.raw.headers,
        body: { token },
      });
      return c.json({ success: true, statusCode: 200, data: {} });
    } catch {
      return c.json(
        { success: false, statusCode: 500, messages: ["Failed to revoke session."] },
        500 as ContentfulStatusCode,
      );
    }
  });

  // POST /api/account/sessions/revoke-others — Revoke all sessions except current
  app.post("/api/account/sessions/revoke-others", async (c) => {
    try {
      await auth.api.revokeSessions({ headers: c.req.raw.headers });
      return c.json({ success: true, statusCode: 200, data: {} });
    } catch {
      return c.json(
        { success: false, statusCode: 500, messages: ["Failed to revoke sessions."] },
        500 as ContentfulStatusCode,
      );
    }
  });

  // GET /api/account/sign-in-events — Paginated sign-in event history
  app.get("/api/account/sign-in-events", async (c) => {
    const session = c.get(SESSION_KEY);
    if (!session) return c.json({ success: false, statusCode: 401 }, 401 as ContentfulStatusCode);

    const limitParam = parseInt(c.req.query("limit") ?? "", 10);
    const offsetParam = parseInt(c.req.query("offset") ?? "", 10);
    const limit = Math.min(
      Number.isFinite(limitParam) && limitParam > 0 ? limitParam : DEFAULT_LIMIT,
      MAX_LIMIT,
    );
    const offset = Number.isFinite(offsetParam) && offsetParam >= 0 ? offsetParam : 0;

    const handler = c.get(SCOPE_KEY).resolve(IGetSignInEventsKey);
    const result = await handler.handleAsync({
      userId: session.userId as string,
      limit,
      offset,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // ========================================================================
  // Email & Phone change (OTP flows)
  // ========================================================================
  // userId always derived from session — never accepted from request body (IDOR).
  // Password is in the SAME request body as the new value (atomic — bypass-proof).

  // POST /api/account/email/request-change — initiate email change (sends OTP to NEW email)
  app.post("/api/account/email/request-change", async (c) => {
    const body = await c.req.json();
    const session = c.get(SESSION_KEY);
    if (!session) return c.json({ success: false, statusCode: 401 }, 401 as ContentfulStatusCode);

    const handler = c.get(SCOPE_KEY).resolve(IRequestEmailChangeKey);
    const result = await handler.handleAsync({
      userId: session.userId as string,
      newEmail: body.newEmail as string,
      currentPassword: body.currentPassword as string,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // POST /api/account/email/verify-change — verify OTP and apply email change
  app.post("/api/account/email/verify-change", async (c) => {
    const body = await c.req.json();
    const session = c.get(SESSION_KEY);
    if (!session) return c.json({ success: false, statusCode: 401 }, 401 as ContentfulStatusCode);

    const handler = c.get(SCOPE_KEY).resolve(IVerifyEmailChangeKey);
    const result = await handler.handleAsync({
      userId: session.userId as string,
      code: body.code as string,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // POST /api/account/phone/request-change — initiate phone change/add (sends OTP via SMS)
  app.post("/api/account/phone/request-change", async (c) => {
    const body = await c.req.json();
    const session = c.get(SESSION_KEY);
    if (!session) return c.json({ success: false, statusCode: 401 }, 401 as ContentfulStatusCode);

    const handler = c.get(SCOPE_KEY).resolve(IRequestPhoneChangeKey);
    const result = await handler.handleAsync({
      userId: session.userId as string,
      newPhone: body.newPhone as string,
      currentPassword: body.currentPassword as string,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // POST /api/account/phone/verify-change — verify OTP and apply phone change
  app.post("/api/account/phone/verify-change", async (c) => {
    const body = await c.req.json();
    const session = c.get(SESSION_KEY);
    if (!session) return c.json({ success: false, statusCode: 401 }, 401 as ContentfulStatusCode);

    const handler = c.get(SCOPE_KEY).resolve(IVerifyPhoneChangeKey);
    const result = await handler.handleAsync({
      userId: session.userId as string,
      code: body.code as string,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // DELETE /api/account/phone — remove user's phone (password-gated, no OTP)
  app.delete("/api/account/phone", async (c) => {
    const body = await c.req.json().catch(() => ({}));
    const session = c.get(SESSION_KEY);
    if (!session) return c.json({ success: false, statusCode: 401 }, 401 as ContentfulStatusCode);

    const handler = c.get(SCOPE_KEY).resolve(IRemovePhoneKey);
    const result = await handler.handleAsync({
      userId: session.userId as string,
      currentPassword: body.currentPassword as string,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  return app;
}
