// @d2/auth-api — Hono server entry point and DI composition root.

export { createApp } from "./composition-root.js";
export type { AppOverrides } from "./composition-root.js";
export type { SessionVariables } from "./middleware/session.js";

// Context key constants
export { USER_KEY, SESSION_KEY, SCOPE_KEY, REQUEST_CONTEXT_KEY } from "./context-keys.js";

// Middleware factories (exported for testing)
export { createCsrfMiddleware } from "./middleware/csrf.js";
export { createRequestEnrichmentMiddleware } from "./middleware/request-enrichment.js";
export { createDistributedRateLimitMiddleware } from "./middleware/distributed-rate-limit.js";
export { createSessionMiddleware } from "./middleware/session.js";
export {
  createSessionFingerprintMiddleware,
  computeFingerprint,
  type SessionFingerprintMiddlewareOptions,
  type StoreFingerprint,
  type GetFingerprint,
  type RevokeSession,
} from "./middleware/session-fingerprint.js";
export { createErrorHandler } from "./middleware/error-handler.js";
export {
  createServiceKeyMiddleware,
  type ServiceKeyMiddlewareOptions,
} from "./middleware/service-key.js";
export { createAmbientScopeMiddleware } from "./middleware/ambient-scope.js";
export { createScopeMiddleware } from "./middleware/scope.js";
export {
  requireOrg,
  requireOrgType,
  requireRole,
  requireStaff,
  requireAdmin,
} from "./middleware/authorization.js";

// Route factories (exported for testing)
export { createAuthRoutes } from "./routes/auth-routes.js";
export { createEmulationRoutes } from "./routes/emulation-routes.js";
export { createOrgContactRoutes } from "./routes/org-contact-routes.js";
export { createInvitationRoutes } from "./routes/invitation-routes.js";
export type { InvitationRoutesOptions } from "./routes/invitation-routes.js";
export { createCheckEmailRoutes } from "./routes/check-email-routes.js";

// gRPC service factories (exported for testing)
export { createAuthGrpcService } from "./services/auth-grpc-service.js";
