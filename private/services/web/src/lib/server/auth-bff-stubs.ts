// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Local BFF auth stubs — the former `@dcsv-io/d2-auth-bff-client` package is not on the
 * public workspace surface after the public/private reorg. These stubs keep
 * layout server loads type-checkable until a private auth-bff package lands.
 * Runtime behavior is fail-closed (redirect to login / home) when session is
 * absent; product auth wiring remains private host ownership.
 */
import { redirect } from "@sveltejs/kit";

/** Minimal session shape used by App.Locals + layout guards. */
export interface AuthSession {
  userId?: string;
  activeOrganizationId?: string;
  activeOrganizationType?: string;
  activeOrganizationRole?: string;
}

/** Minimal user shape used by App.Locals + layout guards. */
export interface AuthUser {
  id?: string;
  email?: string;
  displayName?: string;
}

export interface AuthLocals {
  session?: AuthSession;
  user?: AuthUser;
}

/**
 * Requires an authenticated session; redirects to login when absent.
 */
export function requireAuth(
  locals: AuthLocals,
  url: URL,
): { session: AuthSession; user: AuthUser } {
  if (!locals.session || !locals.user) {
    const returnTo = encodeURIComponent(`${url.pathname}${url.search}`);
    redirect(303, `/login?returnTo=${returnTo}`);
  }

  return { session: locals.session, user: locals.user };
}

/**
 * Requires an active organization on the session.
 */
export function requireOrg(locals: AuthLocals, url: URL): { session: AuthSession; user: AuthUser } {
  const auth = requireAuth(locals, url);

  if (!auth.session.activeOrganizationId) {
    redirect(303, "/onboarding");
  }

  return auth;
}

/**
 * Redirects authenticated callers away from guest-only routes.
 */
export function redirectIfAuthenticated(locals: AuthLocals): void {
  if (locals.session && locals.user) {
    redirect(303, "/");
  }
}
