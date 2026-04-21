import { Hono } from "hono";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { HttpStatusCode } from "@d2/result";
import { TK } from "@d2/i18n";
import { ILoggerKey } from "@d2/logging";
import {
  IUpdateUserRealNameKey,
  IUpdateUsernameKey,
  IUpdateUserLocaleKey,
  IUpdateUserTimezoneKey,
  IGetSignInEventsKey,
  IGetMySessionsKey,
  IUpdateUserImageKey,
  IInvalidateUserSessionCacheKey,
  IPushUserUpdatedKey,
  IRequestEmailChangeKey,
  IVerifyEmailChangeKey,
  IRequestPhoneChangeKey,
  IVerifyPhoneChangeKey,
  IRemovePhoneKey,
  IVerifyUserPasswordKey,
  IRequestUserDeletionKey,
} from "@d2/auth-app";
import type { IRequestContext } from "@d2/handler";
import type { SessionVariables } from "../middleware/session.js";
import type { ScopeVariables } from "../middleware/scope.js";
import { SCOPE_KEY, REQUEST_CONTEXT_KEY } from "../context-keys.js";
import type { Auth } from "@d2/auth-infra";

/** Default and maximum page sizes for list endpoints. */
const DEFAULT_LIMIT = 50;
const MAX_LIMIT = 100;

/**
 * Account routes — user-level, org-agnostic endpoints.
 * All require authentication (session middleware) but no org membership.
 * userId is derived from the request context, never from request body (IDOR prevention).
 */
export function createAccountRoutes(auth: Auth) {
  const app = new Hono<{ Variables: SessionVariables & ScopeVariables }>();

  // Local helper: returns the authenticated user's id from requestContext.
  // Routes are gated by sessionMiddleware upstream, so userId is guaranteed present.
  const uid = (c: import("hono").Context): string =>
    (c.get(REQUEST_CONTEXT_KEY as never) as IRequestContext).userId!;

  // PATCH /api/account/name — Update user's real name (firstName + lastName)
  app.patch("/api/account/name", async (c) => {
    const body = await c.req.json();
    const handler = c.get(SCOPE_KEY).resolve(IUpdateUserRealNameKey);
    const result = await handler.handleAsync({
      userId: uid(c),
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
    const handler = c.get(SCOPE_KEY).resolve(IUpdateUsernameKey);
    const result = await handler.handleAsync({
      userId: uid(c),
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
    const handler = c.get(SCOPE_KEY).resolve(IUpdateUserLocaleKey);
    const result = await handler.handleAsync({
      userId: uid(c),
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
    const handler = c.get(SCOPE_KEY).resolve(IUpdateUserTimezoneKey);
    const result = await handler.handleAsync({
      userId: uid(c),
      timezone: body.timezone as string,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // DELETE /api/account/avatar — Remove user's profile picture
  app.delete("/api/account/avatar", async (c) => {
    const scope = c.get(SCOPE_KEY);
    const id = uid(c);

    const handler = scope.resolve(IUpdateUserImageKey);
    const invalidate = scope.tryResolve(IInvalidateUserSessionCacheKey);
    const push = scope.tryResolve(IPushUserUpdatedKey);

    const result = await handler.handleAsync({ userId: id, image: null });

    if (result.success) {
      // Fire-and-forget: invalidate cache → then push SignalR event.
      // Client relies on the user:updated event for session refresh, not the API response.
      invalidate
        ?.handleAsync({ userId: id })
        .then(() => push?.handleAsync({ userId: id }))
        .catch(() => {});
    }

    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // GET /api/account/sessions — List active sessions, enriched with Geo WhoIs
  // (city/country/ASN/network flags) and an `isCurrent` flag for the active row.
  app.get("/api/account/sessions", async (c) => {
    const scope = c.get(SCOPE_KEY);
    const handler = scope.resolve(IGetMySessionsKey);

    // Resolve the current session token so the handler can flag the active row.
    // Failure to resolve is non-fatal — every row just gets isCurrent=false.
    let currentSessionToken: string | undefined;
    try {
      const current = await auth.api.getSession({ headers: c.req.raw.headers });
      currentSessionToken = current?.session?.token;
    } catch {
      currentSessionToken = undefined;
    }

    const result = await handler.handleAsync({
      userId: uid(c),
      currentSessionToken,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // POST /api/account/sessions/revoke — Revoke a specific session by token.
  // Password-gated: caller must supply currentPassword in the SAME request body
  // (atomic — bypass-proof, mirroring the email/phone change pattern).
  app.post("/api/account/sessions/revoke", async (c) => {
    const body = await c.req.json().catch(() => ({}));
    const token = body.token as string;
    const currentPassword = body.currentPassword as string;
    if (!token) {
      return c.json(
        { success: false, statusCode: 400, messages: [TK.auth.errors.SESSION_TOKEN_REQUIRED] },
        400 as ContentfulStatusCode,
      );
    }
    if (!currentPassword) {
      return c.json(
        {
          success: false,
          statusCode: 400,
          messages: [TK.auth.errors.PASSWORD_REQUIRED_FOR_CHANGE],
        },
        400 as ContentfulStatusCode,
      );
    }
    const passwordOk = await c
      .get(SCOPE_KEY)
      .resolve(IVerifyUserPasswordKey)
      .verify(uid(c), currentPassword);
    if (!passwordOk) {
      return c.json(
        { success: false, statusCode: 401, messages: [TK.auth.errors.INCORRECT_PASSWORD] },
        401 as ContentfulStatusCode,
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
        { success: false, statusCode: 500, messages: [TK.auth.errors.SESSION_REVOKE_FAILED] },
        500 as ContentfulStatusCode,
      );
    }
  });

  // POST /api/account/sessions/revoke-others — Revoke all sessions except current.
  // Password-gated, same atomic-request rule as the single-session revoke.
  app.post("/api/account/sessions/revoke-others", async (c) => {
    const body = await c.req.json().catch(() => ({}));
    const currentPassword = body.currentPassword as string;
    if (!currentPassword) {
      return c.json(
        {
          success: false,
          statusCode: 400,
          messages: [TK.auth.errors.PASSWORD_REQUIRED_FOR_CHANGE],
        },
        400 as ContentfulStatusCode,
      );
    }
    const passwordOk = await c
      .get(SCOPE_KEY)
      .resolve(IVerifyUserPasswordKey)
      .verify(uid(c), currentPassword);
    if (!passwordOk) {
      return c.json(
        { success: false, statusCode: 401, messages: [TK.auth.errors.INCORRECT_PASSWORD] },
        401 as ContentfulStatusCode,
      );
    }
    try {
      await auth.api.revokeSessions({ headers: c.req.raw.headers });
      return c.json({ success: true, statusCode: 200, data: {} });
    } catch {
      return c.json(
        { success: false, statusCode: 500, messages: [TK.auth.errors.SESSION_REVOKE_FAILED] },
        500 as ContentfulStatusCode,
      );
    }
  });

  // POST /api/account/change-password — Change password (BetterAuth-native).
  // Atomic: currentPassword + newPassword in the SAME body. By default revokes
  // ALL other sessions to invalidate any stolen cookies; the security email
  // notification fires automatically via the publishPasswordChanged hook
  // (databaseHooks.account.update.after).
  app.post("/api/account/change-password", async (c) => {
    const body = await c.req.json().catch(() => ({}));
    const currentPassword = body.currentPassword as string | undefined;
    const newPassword = body.newPassword as string | undefined;
    const revokeOtherSessions = body.revokeOtherSessions !== false; // default true

    if (!currentPassword || !newPassword) {
      return c.json(
        {
          success: false,
          statusCode: 400,
          messages: [TK.auth.errors.CHANGE_PASSWORD_REQUIRED_FIELDS],
        },
        400 as ContentfulStatusCode,
      );
    }

    try {
      await auth.api.changePassword({
        headers: c.req.raw.headers,
        body: { currentPassword, newPassword, revokeOtherSessions },
      });
      return c.json({ success: true, statusCode: 200, data: {} });
    } catch (err) {
      const status = (err as { statusCode?: number })?.statusCode ?? 400;
      // Log the raw provider error for ops, but never propagate it to the user —
      // emit a translated TK key instead so we don't leak provider/English copy.
      const rawMessage = (err as { message?: string })?.message;
      c.get(SCOPE_KEY)
        .resolve(ILoggerKey)
        .warn("Change password failed", { status, error: rawMessage });
      return c.json(
        {
          success: false,
          statusCode: status,
          messages: [TK.auth.errors.CHANGE_PASSWORD_FAILED],
        },
        status as ContentfulStatusCode,
      );
    }
  });

  // GET /api/account/sign-in-events — Paginated sign-in event history
  app.get("/api/account/sign-in-events", async (c) => {
    const limitParam = parseInt(c.req.query("limit") ?? "", 10);
    const offsetParam = parseInt(c.req.query("offset") ?? "", 10);
    const limit = Math.min(
      Number.isFinite(limitParam) && limitParam > 0 ? limitParam : DEFAULT_LIMIT,
      MAX_LIMIT,
    );
    const offset = Number.isFinite(offsetParam) && offsetParam >= 0 ? offsetParam : 0;

    const handler = c.get(SCOPE_KEY).resolve(IGetSignInEventsKey);
    const result = await handler.handleAsync({
      userId: uid(c),
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
    const handler = c.get(SCOPE_KEY).resolve(IRequestEmailChangeKey);
    const result = await handler.handleAsync({
      userId: uid(c),
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
    const handler = c.get(SCOPE_KEY).resolve(IVerifyEmailChangeKey);
    const result = await handler.handleAsync({
      userId: uid(c),
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
    const handler = c.get(SCOPE_KEY).resolve(IRequestPhoneChangeKey);
    const result = await handler.handleAsync({
      userId: uid(c),
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
    const handler = c.get(SCOPE_KEY).resolve(IVerifyPhoneChangeKey);
    const result = await handler.handleAsync({
      userId: uid(c),
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
    const handler = c.get(SCOPE_KEY).resolve(IRemovePhoneKey);
    const result = await handler.handleAsync({
      userId: uid(c),
      currentPassword: body.currentPassword as string,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  // POST /api/account/delete — Initiate self-service user deletion.
  //
  // Atomic password-gated. On success the user is logged out everywhere
  // (status flipped to `pending_deletion`, all sessions revoked, BetterAuth
  // Redis cookie cache busted) and the FE redirects to the public
  // `/account/delete-scheduled` landing page. The cancellation path is just
  // signing back in — handled by the BetterAuth `session.create.before` hook.
  //
  //   200 → { scheduledFor: ISO string }
  //   401 → wrong password
  //   409 → sole owner of one or more orgs (must transfer ownership first)
  app.post("/api/account/delete", async (c) => {
    const body = await c.req.json().catch(() => ({}));
    const handler = c.get(SCOPE_KEY).resolve(IRequestUserDeletionKey);
    const result = await handler.handleAsync({
      userId: uid(c),
      currentPassword: body.currentPassword as string,
      feedback: body.feedback as { reason?: string; comment?: string } | undefined,
    });
    const status = (
      result.success ? HttpStatusCode.OK : (result.statusCode ?? HttpStatusCode.BadRequest)
    ) as ContentfulStatusCode;
    return c.json(result, status);
  });

  return app;
}
