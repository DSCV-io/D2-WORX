import { createMiddleware } from "hono/factory";
import { getServerSpan, enrichSpan } from "@d2/service-defaults";
import type { ServiceProvider, ServiceScope } from "@d2/di";
import {
  IRequestContextKey,
  IHandlerContextKey,
  HandlerContext,
  OrgType,
  requestContextStorage,
  requestLoggerStorage,
} from "@d2/handler";
import { ILoggerKey } from "@d2/logging";
import type { IRequestContext } from "@d2/handler";
import { SESSION_FIELDS } from "@d2/auth-domain";
import { IFindActiveConsentByUserIdAndOrgKey } from "@d2/auth-app";
import { SCOPE_KEY, USER_KEY, SESSION_KEY, REQUEST_CONTEXT_KEY } from "../context-keys.js";
import { REQUEST_LOGGER_KEY } from "./request-context-logging.js";
import type { SessionVariables } from "./session.js";

export { SCOPE_KEY };

/**
 * Hono Variables set by the scope middleware.
 */
export interface ScopeVariables {
  [SCOPE_KEY]: ServiceScope;
}

/**
 * The `OrgType` enum values are now identical to the lowercase wire strings
 * stored in the session / DB / JWT, so this is a typed membership check
 * rather than a translation. Returns undefined for unknown / missing values
 * so consumers can fail-closed.
 */
function toHandlerOrgType(domainOrgType: string | undefined): OrgType | undefined {
  if (!domainOrgType) return undefined;
  return (Object.values(OrgType) as string[]).includes(domainOrgType)
    ? (domainOrgType as OrgType)
    : undefined;
}

/**
 * Builds an IRequestContext by merging enrichment data (network/fingerprint)
 * with session/auth data (identity/org). If a base context from enrichment
 * middleware is available, its network fields are preserved.
 *
 * Mirrors .NET's MutableRequestContext progressive population.
 */
function buildRequestContext(
  user: SessionVariables["user"],
  session: SessionVariables["session"],
  base?: IRequestContext,
): IRequestContext {
  const domainOrgType = session?.[SESSION_FIELDS.ACTIVE_ORG_TYPE] as string | undefined;
  const orgType = toHandlerOrgType(domainOrgType);
  const domainOrgRole = session?.[SESSION_FIELDS.ACTIVE_ORG_ROLE] as string | undefined;
  const emulatedOrgId = session?.[SESSION_FIELDS.EMULATED_ORG_ID] as string | undefined;
  const domainEmulatedOrgType = session?.[SESSION_FIELDS.EMULATED_ORG_TYPE] as string | undefined;
  const emulatedOrgType = toHandlerOrgType(domainEmulatedOrgType);
  const isOrgEmulating = !!emulatedOrgId;

  return {
    // Preserve network/enrichment fields from base context
    ...(base ?? {}),
    traceId: base?.traceId ?? crypto.randomUUID(),
    isAuthenticated: !!user,
    userId: user?.id,
    username: user?.username,
    email: user?.email,
    agentOrgId: session?.[SESSION_FIELDS.ACTIVE_ORG_ID] as string | undefined,
    agentOrgType: orgType,
    agentOrgRole: domainOrgRole,
    targetOrgId: isOrgEmulating
      ? emulatedOrgId
      : (session?.[SESSION_FIELDS.ACTIVE_ORG_ID] as string | undefined),
    targetOrgType: isOrgEmulating ? emulatedOrgType : orgType,
    targetOrgRole: isOrgEmulating ? "auditor" : domainOrgRole,
    isOrgEmulating,
    isUserImpersonating: false,
    isTrustedService: base?.isTrustedService ?? false,
    isAgentStaff: domainOrgType === "admin" || domainOrgType === "support",
    isAgentAdmin: domainOrgType === "admin",
    isTargetingStaff: isOrgEmulating
      ? domainEmulatedOrgType === "admin" || domainEmulatedOrgType === "support"
      : domainOrgType === "admin" || domainOrgType === "support",
    isTargetingAdmin: isOrgEmulating
      ? domainEmulatedOrgType === "admin"
      : domainOrgType === "admin",
  };
}

/**
 * Creates Hono middleware that establishes a DI scope per request.
 *
 * The scope contains:
 * - IRequestContext (built from session data)
 * - IHandlerContext (wraps IRequestContext + ILogger)
 *
 * Routes resolve handlers from `c.get(SCOPE_KEY)`.
 * The scope is disposed after the request completes.
 */
export function createScopeMiddleware(provider: ServiceProvider) {
  return createMiddleware<{ Variables: SessionVariables & ScopeVariables }>(async (c, next) => {
    const scope = provider.createScope();
    try {
      // Build IRequestContext by merging enrichment data with session/auth data.
      const user = c.get(USER_KEY) as SessionVariables["user"] | undefined;
      const session = c.get(SESSION_KEY) as SessionVariables["session"] | undefined;
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const enrichedContext = (c as any).get(REQUEST_CONTEXT_KEY) as IRequestContext | undefined;
      let requestContext = buildRequestContext(user ?? null, session ?? null, enrichedContext);

      scope.setInstance(IRequestContextKey, requestContext);

      // Build IHandlerContext — scoped (new per request).
      // Start from the request-level child logger (already enriched with network fields
      // by RequestContextLoggingMiddleware), then add auth-specific bindings.
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const requestLogger = (c as any).get(REQUEST_LOGGER_KEY) as
        | import("@d2/logging").ILogger
        | undefined;
      const baseLogger = requestLogger ?? provider.resolve(ILoggerKey);
      const authBindings: Record<string, unknown> = {};
      if (requestContext.userId) authBindings.userId = requestContext.userId;
      if (requestContext.username) authBindings.username = requestContext.username;
      if (requestContext.agentOrgId) authBindings.agentOrgId = requestContext.agentOrgId;
      if (requestContext.agentOrgType) authBindings.agentOrgType = requestContext.agentOrgType;
      if (requestContext.agentOrgRole) authBindings.agentOrgRole = requestContext.agentOrgRole;
      if (requestContext.targetOrgId) authBindings.targetOrgId = requestContext.targetOrgId;
      if (requestContext.targetOrgType) authBindings.targetOrgType = requestContext.targetOrgType;
      const logger =
        Object.keys(authBindings).length > 0 ? baseLogger.child(authBindings) : baseLogger;
      scope.setInstance(IHandlerContextKey, new HandlerContext(requestContext, logger));

      // Validate emulation consent if emulating
      if (requestContext.isOrgEmulating && requestContext.userId && requestContext.targetOrgId) {
        const consentHandler = scope.resolve(IFindActiveConsentByUserIdAndOrgKey);
        const consentResult = await consentHandler.handleAsync({
          userId: requestContext.userId,
          grantedToOrgId: requestContext.targetOrgId,
        });

        // If no active consent found, strip emulation from context
        if (!consentResult.success || !consentResult.data?.consent) {
          requestContext = {
            ...requestContext,
            targetOrgId: requestContext.agentOrgId,
            targetOrgType: requestContext.agentOrgType,
            isOrgEmulating: false,
            isTargetingStaff: requestContext.isAgentStaff,
            isTargetingAdmin: requestContext.isAgentAdmin,
          };
          scope.setInstance(IRequestContextKey, requestContext);
          scope.setInstance(IHandlerContextKey, new HandlerContext(requestContext, logger));
        }
      }

      // Enrich the active span with auth-specific attributes now that session
      // data has been merged into the request context. The request-context-logging
      // middleware already set network fields; this adds/updates identity/org fields.
      enrichSpan(getServerSpan(c), {
        userId: requestContext.userId,
        username: requestContext.username,
        agentOrgId: requestContext.agentOrgId,
        agentOrgType:
          requestContext.agentOrgType != null ? String(requestContext.agentOrgType) : undefined,
        agentOrgRole: requestContext.agentOrgRole,
        targetOrgId: requestContext.targetOrgId,
        targetOrgType:
          requestContext.targetOrgType != null ? String(requestContext.targetOrgType) : undefined,
        isAuthenticated: requestContext.isAuthenticated ?? false,
        isOrgEmulating: requestContext.isOrgEmulating ?? false,
      });

      // Upgrade ambient storage so all handlers (including singletons) see
      // the auth-enriched context and logger for the rest of this request.
      // Use the locally-built requestContext directly — IRequestContextKey
      // is set via setInstance (not registered in ServiceCollection), so
      // scope.resolve() would fail. The logger comes from what we just built.
      requestContextStorage.enterWith(requestContext);
      requestLoggerStorage.enterWith(logger);

      // Re-publish the merged requestContext into c.var so policy middleware
      // (`@d2/auth-policy` requireAuth/requireOrg/etc.) can read it.
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (c as any).set(REQUEST_CONTEXT_KEY, requestContext);

      c.set(SCOPE_KEY, scope);
      await next();
    } finally {
      scope.dispose();
    }
  });
}
