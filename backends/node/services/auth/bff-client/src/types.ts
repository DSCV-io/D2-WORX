/**
 * Shared types for the auth BFF client.
 *
 * These types represent the session and user data resolved from the Auth service
 * and made available throughout SvelteKit via event.locals.
 */

/** Resolved session data available throughout the app. */
export interface AuthSession {
  userId: string;
  activeOrganizationId: string | null;
  activeOrganizationType: string | null;
  activeOrganizationRole: string | null;
  emulatedOrganizationId: string | null;
  emulatedOrganizationType: string | null;
}

/** Resolved user data. */
export interface AuthUser {
  id: string;
  email: string;
  name: string;
  username?: string;
  displayUsername?: string;
  image?: string;
  locale?: string;
  timezone?: string;
}

/** Configuration for the BFF client. */
export interface AuthBffConfig {
  /** Auth service base URL (e.g., "http://localhost:5100"). */
  authServiceUrl: string;
  /** Request timeout in ms (default: 5000). */
  timeout?: number;
  /** S2S API key for trusted service identification. When set, sent as X-Api-Key header. */
  apiKey?: string;
}

/**
 * The shape returned by BetterAuth's GET /api/auth/get-session endpoint.
 * Used internally for deserialization — not part of the public API.
 */
export interface BetterAuthSessionResponse {
  session: {
    id: string;
    userId: string;
    token: string;
    expiresAt: string;
    activeOrganizationId?: string | null;
    activeOrganizationType?: string | null;
    activeOrganizationRole?: string | null;
    emulatedOrganizationId?: string | null;
    emulatedOrganizationType?: string | null;
  };
  user: {
    id: string;
    email: string;
    name: string;
    username?: string;
    displayUsername?: string;
    image?: string | null;
    locale?: string | null;
    timezone?: string | null;
  };
}
