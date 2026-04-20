/**
 * Hono context-variable key holding the populated `IRequestContext` for the
 * current request. By convention every D2 service populates this key — the
 * `@d2/jwt-auth` middleware does it for JWT-validated services, and the auth
 * service's own session middleware does it for cookie-session-validated
 * routes. `@d2/auth-policy` reads identity exclusively from this key so the
 * policies are agnostic to whichever auth mechanism populated it.
 */
export const REQUEST_CONTEXT_KEY = "requestContext" as const;
