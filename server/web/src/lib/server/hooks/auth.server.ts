/**
 * SvelteKit Handle wrapper for auth session resolution.
 *
 * Resolves the user session by forwarding cookies to the Auth service's
 * GET /api/auth/get-session endpoint. Sets event.locals.session and
 * event.locals.user for downstream route guards and layouts.
 *
 * Also sets OTel span attributes and baggage for downstream propagation.
 */
import { propagation, context } from "@opentelemetry/api";
import type { Span } from "@opentelemetry/api";
import type { Handle } from "@sveltejs/kit";
import { getServerSpan } from "../../../instrumentation.server.js";
import { getAuthContext } from "../auth.server.js";

/** Sets non-null/undefined values as span attributes in bulk. */
function enrichSpan(
  span: Span | undefined,
  attrs: Record<string, string | number | boolean | undefined | null>,
): void {
  if (!span) return;
  for (const [key, value] of Object.entries(attrs)) {
    if (value != null) span.setAttribute(key, value);
  }
}

export function createAuthHandle(): Handle {
  return async ({ event, resolve }) => {
    const ctx = getAuthContext();

    // CI / skip mode: auth context unavailable — treat as unauthenticated.
    if (!ctx) {
      event.locals.session = null;
      event.locals.user = null;
      if (event.locals.requestContext) {
        event.locals.requestContext.isAuthenticated = false;
      }
      return resolve(event);
    }

    const { session, user } = await ctx.sessionResolver.resolve(event.request);
    event.locals.session = session;
    event.locals.user = user;

    // Update requestContext with auth state so downstream middleware
    // (idempotency, rate limiting user dimension) can see it.
    // Always set isAuthenticated to a concrete boolean — session resolution
    // is the auth boundary, so after this point the value is known.
    if (event.locals.requestContext) {
      event.locals.requestContext.isAuthenticated = !!session;
      if (session) {
        event.locals.requestContext.userId = session.userId;
        if (user?.username) event.locals.requestContext.username = user.username;
      }
    }

    // Set OTel span attributes for Tempo trace queries.
    // Network fields (deviceFingerprint, city, countryCode) are set by
    // the request-enrichment hook; auth fields are added here.
    const rc = event.locals.requestContext;
    enrichSpan(getServerSpan(event.request), {
      userId: rc?.userId,
      username: user?.username,
      deviceFingerprint: rc?.deviceFingerprint,
      whoIsHashId: rc?.whoIsHashId,
      city: rc?.city,
      countryCode: rc?.countryCode,
      isAuthenticated: rc?.isAuthenticated,
      isVpn: rc?.isVpn,
      isProxy: rc?.isProxy,
      isTor: rc?.isTor,
      isHosting: rc?.isHosting,
    });

    // Set OTel baggage so downstream HTTP calls (to .NET gateway, etc.)
    // propagate context via the standard W3C baggage header.
    // SECURITY: Always create fresh baggage to overwrite any browser-injected
    // spoofed values. Network fields propagate for all requests (enables WhoIs
    // correlation even for anonymous traffic); auth fields only for authenticated.
    const entries: Record<string, { value: string }> = {};

    // Network/enrichment fields — always propagated (available pre-auth).
    if (rc?.deviceFingerprint) entries.deviceFingerprint = { value: rc.deviceFingerprint };
    if (rc?.city) entries.city = { value: rc.city };
    if (rc?.countryCode) entries.countryCode = { value: rc.countryCode };
    if (rc?.whoIsHashId) entries.whoIsHashId = { value: rc.whoIsHashId };

    // Auth/identity fields — only for authenticated users.
    if (session && rc?.userId) {
      entries.userId = { value: rc.userId };
      if (user?.username) entries.username = { value: user.username };
      if (rc.agentOrgId) entries.agentOrgId = { value: rc.agentOrgId };
      if (rc.agentOrgType != null) entries.agentOrgType = { value: String(rc.agentOrgType) };
    }

    const baggage = propagation.createBaggage(entries);
    const ctxWithBaggage = propagation.setBaggage(context.active(), baggage);
    return context.with(ctxWithBaggage, () => resolve(event));
  };
}
