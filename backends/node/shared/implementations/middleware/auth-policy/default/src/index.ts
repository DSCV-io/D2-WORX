/**
 * `@d2/auth-policy` — declarative route-level authorization for Hono apps.
 *
 * Mirrors the .NET `D2.Shared.Implementations.Middleware.AuthPolicy.Default`
 * project. Same method names, same enforcement semantics, same status codes
 * and i18n keys. See AUTH_POLICY.md for the parity table.
 *
 * All policies read identity from `c.get("requestContext")` — populate that
 * upstream via either `@d2/jwt-auth` (JWT-validated services) or the auth
 * service's own session middleware (cookie-session-validated routes).
 */
export { REQUEST_CONTEXT_KEY } from "./constants.js";
export { readRequestContext } from "./read-context.js";
export { requireAuth } from "./require-auth.js";
export { requireTrustedService } from "./require-trusted-service.js";
export { requireOrg } from "./require-org.js";
export { requireOrgType } from "./require-org-type.js";
export { requireRole } from "./require-role.js";
export { requireStaff } from "./require-staff.js";
export { requireAdmin } from "./require-admin.js";
