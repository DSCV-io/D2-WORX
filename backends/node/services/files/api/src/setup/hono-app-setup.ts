import { Hono } from "hono";
import { cors } from "hono/cors";
import { bodyLimit } from "hono/body-limit";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import type { ServiceProvider } from "@d2/di";
import type { ILogger } from "@d2/logging";
import { ILoggerKey } from "@d2/logging";
import { D2Result } from "@d2/result";
import { jwtAuth } from "@d2/jwt-auth";
import {
  HandlerContext,
  IHandlerContextKey,
  IRequestContextKey,
  requestContextStorage,
  requestLoggerStorage,
} from "@d2/handler";
import type { IRequestContext } from "@d2/handler";
import type { FindWhoIs } from "@d2/geo-client";
import type { CheckRateLimit } from "@d2/ratelimit";
import type { ContextKeyConfigMap } from "@d2/files-app";
import { createRequestEnrichmentMiddleware } from "../middleware/request-enrichment.js";
import { createDistributedRateLimitMiddleware } from "../middleware/distributed-rate-limit.js";
import { createRequestContextLoggingMiddleware } from "../middleware/request-context-logging.js";
import { createAmbientScopeMiddleware } from "../middleware/ambient-scope.js";
import { REQUEST_CONTEXT_KEY, ENRICHED_CONTEXT_KEY, SCOPE_KEY } from "../context-keys.js";
import { createHealthRoutes } from "../routes/health-routes.js";
import { createUploadRoutes } from "../routes/upload-routes.js";
import { createDownloadRoutes } from "../routes/download-routes.js";
import { createListRoutes } from "../routes/list-routes.js";
import { createVariantUrlRoutes } from "../routes/variant-url-routes.js";

export interface HonoAppOptions {
  provider: ServiceProvider;
  config: {
    corsOrigins: string[];
    jwksUrl: string;
    jwtIssuer: string;
    jwtAudience: string;
    /** Disable fingerprint check for test environments. Default: true. */
    fingerprintCheck?: boolean;
  };
  contextKeyConfigs: ContextKeyConfigMap;
  findWhoIs?: FindWhoIs;
  rateLimitCheck?: CheckRateLimit;
  logger: ILogger;
}

/**
 * Builds the Hono app with full middleware pipeline and route composition.
 *
 * Middleware order (matches Auth API):
 *   CORS → security headers → body limit → request enrichment → logging →
 *   ambient scope → rate limiting → error handler
 *   → /health (public)
 *   → /api/v1/* (protected): JWT auth → DI scope (merges enriched + JWT) → routes
 */
export function buildHonoApp(options: HonoAppOptions): Hono {
  const { provider, config, contextKeyConfigs, findWhoIs, rateLimitCheck, logger } = options;

  const app = new Hono();

  // --- Global middleware (order matters — matches Auth API) ---

  // 1. CORS
  app.use(
    "*",
    cors({
      origin: config.corsOrigins,
      allowHeaders: ["Content-Type", "Authorization", "Accept", "X-Client-Fingerprint"],
      allowMethods: ["GET", "POST", "DELETE", "OPTIONS"],
      exposeHeaders: ["Content-Disposition"],
      credentials: true,
      maxAge: 86400,
    }),
  );

  // 2. Security headers
  app.use("*", async (c, next) => {
    await next();
    c.res.headers.set("X-Content-Type-Options", "nosniff");
    c.res.headers.set("X-Frame-Options", "DENY");
    c.res.headers.set("Content-Security-Policy", "frame-ancestors 'none'");
    c.res.headers.set("Referrer-Policy", "strict-origin-when-cross-origin");
    c.res.headers.set("X-Permitted-Cross-Domain-Policies", "none");
  });

  // 3. Body limit (metadata only — files upload direct to MinIO via presigned URL)
  app.use(
    "*",
    bodyLimit({
      maxSize: 1024 * 1024, // 1 MB
      onError: (c) => c.json(D2Result.payloadTooLarge(), 413 as ContentfulStatusCode),
    }),
  );

  // 4. Request enrichment (IP resolution, fingerprinting, WhoIs)
  if (findWhoIs) {
    app.use("*", createRequestEnrichmentMiddleware(findWhoIs, undefined, logger));
  }

  // 5. Request context logging (child logger with traceId, userId, etc.)
  app.use("*", createRequestContextLoggingMiddleware(logger));

  // 6. Ambient scope (AsyncLocalStorage for per-request context on singletons)
  app.use("*", createAmbientScopeMiddleware());

  // 7. Rate limiting (multi-dimensional sliding window)
  if (rateLimitCheck) {
    app.use("*", createDistributedRateLimitMiddleware(rateLimitCheck));
  }

  // 8. Error handler — never leak internal details
  app.onError((err, c) => {
    logger.error("Unhandled error", {
      error: err.message,
      path: c.req.path,
      method: c.req.method,
    });
    return c.json(
      {
        success: false,
        statusCode: 500,
        messages: ["Internal server error"],
        inputErrors: [],
        data: null,
      },
      500,
    );
  });

  // --- Public routes (no auth) ---
  app.route("/", createHealthRoutes(provider));

  // --- Protected routes (JWT auth + DI scope) ---
  const protectedApp = new Hono();

  protectedApp.use(
    "*",
    jwtAuth({
      jwksUrl: config.jwksUrl,
      issuer: config.jwtIssuer,
      audience: config.jwtAudience,
      fingerprintCheck: config.fingerprintCheck !== false,
    }),
  );

  // Per-request DI scope middleware — merges JWT context with enriched context.
  // Enrichment middleware stores context under ENRICHED_CONTEXT_KEY (preserved)
  // because JWT middleware overwrites REQUEST_CONTEXT_KEY with claims-only context.
  protectedApp.use("*", async (c, next) => {
    // JWT middleware populated requestContext from claims
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const jwtContext = (c as any).get(REQUEST_CONTEXT_KEY) as IRequestContext;
    // Request enrichment saved its context under a separate key before JWT ran
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const enrichedContext = (c as any).get(ENRICHED_CONTEXT_KEY) as IRequestContext | undefined;

    // Merge: JWT identity claims take priority, enriched network fields preserved
    const requestContext: IRequestContext = {
      ...jwtContext,
      // Network fields from enrichment (JWT doesn't set these)
      traceId: enrichedContext?.traceId ?? jwtContext.traceId,
      clientIp: enrichedContext?.clientIp,
      serverFingerprint: enrichedContext?.serverFingerprint,
      clientFingerprint: enrichedContext?.clientFingerprint,
      deviceFingerprint: enrichedContext?.deviceFingerprint,
      whoIsHashId: enrichedContext?.whoIsHashId,
      city: enrichedContext?.city,
      countryCode: enrichedContext?.countryCode,
      subdivisionCode: enrichedContext?.subdivisionCode,
      isVpn: enrichedContext?.isVpn,
      isProxy: enrichedContext?.isProxy,
      isTor: enrichedContext?.isTor,
      isHosting: enrichedContext?.isHosting,
    };

    // Build scope manually (NOT createServiceScope which injects unauthenticated defaults).
    // The JWT-derived context must be injected BEFORE HandlerContext is created.
    const scope = provider.createScope();
    const resolvedLogger = provider.resolve(ILoggerKey);
    scope.setInstance(IRequestContextKey, requestContext);
    scope.setInstance(IHandlerContextKey, new HandlerContext(requestContext, resolvedLogger));

    // Set ambient scope so pre-auth singletons see per-request context
    requestContextStorage.enterWith(requestContext);
    requestLoggerStorage.enterWith(resolvedLogger);

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (c as any).set(SCOPE_KEY, scope);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (c as any).set(REQUEST_CONTEXT_KEY, requestContext);
    try {
      await next();
    } finally {
      scope.dispose();
    }
  });

  protectedApp.route("/", createUploadRoutes(contextKeyConfigs));
  protectedApp.route("/", createDownloadRoutes(contextKeyConfigs));
  protectedApp.route("/", createVariantUrlRoutes(contextKeyConfigs));
  protectedApp.route("/", createListRoutes());

  app.route("/api/v1", protectedApp);

  return app;
}
