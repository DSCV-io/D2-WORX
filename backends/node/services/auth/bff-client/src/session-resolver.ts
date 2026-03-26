/**
 * Session resolver — resolves the current user session from the Auth service.
 *
 * Forwards the incoming request's cookies and fingerprint header to the Auth
 * service's GET /api/auth/get-session endpoint and maps the response to typed
 * AuthSession + AuthUser.
 *
 * Fail-closed: any error (timeout, 5xx, network) returns null session/user
 * with a warning log. The caller can then treat the request as unauthenticated.
 *
 * Cookie signing awareness:
 * BetterAuth cookie values use the format `TOKEN.SIGNATURE`. If a request
 * contains a `better-auth.session_token` cookie but get-session returns null
 * (200 with null body), the cookie was likely unsigned/tampered. BetterAuth
 * does not distinguish this from "no session" — both return silent null.
 * We log a warning for this case as a monitoring signal.
 */
import type { ILogger } from "@d2/logging";
import type { AuthBffConfig, AuthSession, AuthUser, BetterAuthSessionResponse } from "./types.js";

export interface SessionResolveResult {
  session: AuthSession | null;
  user: AuthUser | null;
}

const DEFAULT_TIMEOUT = 5000;

export class SessionResolver {
  private readonly config: AuthBffConfig;
  private readonly logger: ILogger;

  constructor(config: AuthBffConfig, logger: ILogger) {
    this.config = {
      ...config,
      authServiceUrl: config.authServiceUrl.replace(/\/+$/, ""),
    };
    this.logger = logger;
  }

  /**
   * Resolves the session for the given request.
   * Forwards cookie and x-client-fingerprint headers to the Auth service.
   */
  async resolve(request: Request): Promise<SessionResolveResult> {
    const cookie = request.headers.get("cookie");
    if (!cookie) {
      return { session: null, user: null };
    }

    // Track whether the request carried a session token cookie.
    // If it did but resolution returns null, BetterAuth silently rejected it
    // (likely unsigned/tampered — see module doc comment).
    const hasSessionCookie = cookie.includes("better-auth.session_token=");

    const headers: Record<string, string> = { cookie };

    if (this.config.apiKey) {
      headers["x-api-key"] = this.config.apiKey;
    }

    const fingerprint = request.headers.get("x-client-fingerprint");
    if (fingerprint) {
      headers["x-client-fingerprint"] = fingerprint;
    }

    const timeout = this.config.timeout ?? DEFAULT_TIMEOUT;

    try {
      const response = await fetch(`${this.config.authServiceUrl}/api/auth/get-session`, {
        method: "GET",
        headers,
        signal: AbortSignal.timeout(timeout),
      });

      if (!response.ok) {
        // 401 = not authenticated, which is a normal response (not an error)
        if (response.status === 401) {
          return { session: null, user: null };
        }

        this.logger.warn("Auth service returned non-OK status during session resolution", {
          status: response.status,
        });
        return { session: null, user: null };
      }

      const data = (await response.json()) as BetterAuthSessionResponse;

      if (!data?.session || !data?.user) {
        if (hasSessionCookie) {
          this.logger.warn(
            "Session cookie present but auth service returned no session — " +
              "cookie may be unsigned, tampered, expired, or revoked",
          );
        }
        return { session: null, user: null };
      }

      // Validate critical identity fields — treat empty strings as unauthenticated
      if (!data.session.userId || !data.user.id || !data.user.email) {
        this.logger.warn("Auth service returned session with missing identity fields", {
          hasUserId: Boolean(data.session.userId),
          hasUserObjectId: Boolean(data.user.id),
          hasEmail: Boolean(data.user.email),
        });
        return { session: null, user: null };
      }

      const session = Object.freeze(mapSession(data.session));
      const user = Object.freeze(mapUser(data.user));

      return { session, user };
    } catch (error) {
      this.logger.warn("Failed to resolve session from Auth service", {
        error: error instanceof Error ? error.message : String(error),
      });
      return { session: null, user: null };
    }
  }
}

function mapSession(raw: BetterAuthSessionResponse["session"]): AuthSession {
  return {
    userId: raw.userId,
    activeOrganizationId: raw.activeOrganizationId ?? null,
    activeOrganizationType: raw.activeOrganizationType ?? null,
    activeOrganizationRole: raw.activeOrganizationRole ?? null,
    emulatedOrganizationId: raw.emulatedOrganizationId ?? null,
    emulatedOrganizationType: raw.emulatedOrganizationType ?? null,
  };
}

function mapUser(raw: BetterAuthSessionResponse["user"]): AuthUser {
  return {
    id: raw.id,
    email: raw.email,
    name: raw.name,
    username: raw.username ?? undefined,
    displayUsername: raw.displayUsername ?? undefined,
    image: raw.image ?? undefined,
    locale: raw.locale ?? undefined,
    timezone: raw.timezone ?? undefined,
  };
}
