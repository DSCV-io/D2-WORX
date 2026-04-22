import { AsyncLocalStorage } from "node:async_hooks";
import { Hono } from "hono";
import { bodyLimit } from "hono/body-limit";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { D2Result } from "@d2/result";
import type { ServiceProvider } from "@d2/di";
import type { ILogger } from "@d2/logging";
import type { FindWhoIs } from "@d2/geo-client";
import type { CheckRateLimit } from "@d2/ratelimit";
import type {
  CheckSignInThrottle,
  RecordSignInOutcome,
  CheckEmailAvailability,
} from "@d2/auth-app";
import type { Translator } from "@d2/i18n";
import type { Auth } from "@d2/auth-infra";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { createCorsMiddleware } from "../middleware/cors.js";
import { createSessionMiddleware } from "../middleware/session.js";
import { createCsrfMiddleware } from "../middleware/csrf.js";
import { createRequestEnrichmentMiddleware } from "../middleware/request-enrichment.js";
import { createDistributedRateLimitMiddleware } from "../middleware/distributed-rate-limit.js";
import { createServiceKeyMiddleware } from "../middleware/service-key.js";
import {
  createSessionFingerprintMiddleware,
  computeFingerprint,
} from "../middleware/session-fingerprint.js";
import { createScopeMiddleware } from "../middleware/scope.js";
import { createRequestContextLoggingMiddleware } from "../middleware/request-context-logging.js";
import { createAmbientScopeMiddleware } from "../middleware/ambient-scope.js";
import { createErrorHandler } from "../middleware/error-handler.js";
import { REQUEST_CONTEXT_KEY } from "../context-keys.js";
import { createAuthRoutes } from "../routes/auth-routes.js";
import { createEmulationRoutes } from "../routes/emulation-routes.js";
import { createOrgContactRoutes } from "../routes/org-contact-routes.js";
import { createInvitationRoutes } from "../routes/invitation-routes.js";
import { createCheckEmailRoutes } from "../routes/check-email-routes.js";
import { createAccountRoutes } from "../routes/account-routes.js";

export interface HonoAppOptions {
  auth: Auth;
  provider: ServiceProvider;
  config: {
    corsOrigins: string[];
    authApiKeys?: string[];
    baseUrl: string;
    emailBaseUrl?: string;
  };
  translator: Translator;
  findWhoIs: FindWhoIs;
  rateLimitCheck: CheckRateLimit;
  throttleCheck: CheckSignInThrottle;
  throttleRecord: RecordSignInOutcome;
  /** Audit-record failed sign-in attempts (resolves userId, writes sign_in_event row, enqueues WhoIs lookup). Optional. */
  recordFailedSignIn?: import("../routes/auth-routes.js").RecordFailedSignIn;
  checkEmailHandler: CheckEmailAvailability;
  fingerprintStorage: AsyncLocalStorage<string>;
  deviceFingerprintStorage: AsyncLocalStorage<string>;
  clientFingerprintStorage: AsyncLocalStorage<string>;
  serverFingerprintStorage: AsyncLocalStorage<string>;
  sessionFingerprintMiddleware: ReturnType<typeof createSessionFingerprintMiddleware>;
  logger: ILogger;
  db: NodePgDatabase;
}

/**
 * Builds the Hono app with all middleware and route composition.
 */
export function buildHonoApp(options: HonoAppOptions): Hono {
  const {
    auth,
    provider,
    config,
    findWhoIs,
    rateLimitCheck,
    throttleCheck,
    throttleRecord,
    recordFailedSignIn,
    checkEmailHandler,
    fingerprintStorage,
    deviceFingerprintStorage,
    clientFingerprintStorage,
    serverFingerprintStorage,
    sessionFingerprintMiddleware,
    translator,
    logger,
    db,
  } = options;

  const app = new Hono();

  // Global middleware
  app.use("*", createCorsMiddleware(config.corsOrigins));
  app.use("*", async (c, next) => {
    await next();
    c.res.headers.set("X-Content-Type-Options", "nosniff");
    c.res.headers.set("X-Frame-Options", "DENY");
  });
  app.use(
    "*",
    bodyLimit({
      maxSize: 256 * 1024, // 256 KB — auth payloads are small JSON
      onError: (c) => c.json(D2Result.payloadTooLarge(), 413 as ContentfulStatusCode),
    }),
  );
  app.use(
    "*",
    createRequestEnrichmentMiddleware(
      findWhoIs,
      // Auth sits behind the gateway / SvelteKit BFF. The BFF forwards the
      // browser IP via `x-forwarded-for`; in prod the gateway uses
      // `cf-connecting-ip`. Trusting all three keeps `clientIp` accurate
      // across both paths so failed-sign-in audit + throttle bucket the right
      // address. Anything reaching auth without one of these headers (i.e.
      // direct internal traffic) intentionally falls back to "unknown".
      { trustedProxyHeaders: ["cf-connecting-ip", "x-real-ip", "x-forwarded-for"] },
      logger,
    ),
  );
  if (config.authApiKeys?.length) {
    // BetterAuth routes (/api/auth/*) are public (JWKS, sign-in, sign-up, etc.) —
    // other services must fetch JWKS without an API key. Only non-auth routes
    // (custom endpoints: emulation, org contacts, invitations) require S2S trust.
    const serviceKeyMiddleware = createServiceKeyMiddleware(config.authApiKeys, { require: true });
    app.use("*", async (c, next) => {
      if (c.req.path.startsWith("/api/auth/")) {
        return next();
      }
      return serviceKeyMiddleware(c, next);
    });
    logger.info(
      `Auth API service key authentication enabled (${config.authApiKeys.length} key(s), required — /api/auth/* exempt)`,
    );
  } else {
    logger.warn("Auth API started WITHOUT service key requirement — all requests accepted");
  }
  app.use("*", createRequestContextLoggingMiddleware(logger));
  app.use("*", createAmbientScopeMiddleware());
  app.use("*", createDistributedRateLimitMiddleware(rateLimitCheck));
  app.onError(createErrorHandler(logger));

  // Email availability check (public, pre-auth — rate limited but no session/CSRF)
  app.route("/", createCheckEmailRoutes(checkEmailHandler));

  // BetterAuth routes (handles its own auth + CSRF via origin check)
  // AsyncLocalStorage for JWT `fp` claim — no session fingerprint binding here
  // because BetterAuth routes are called both by the SvelteKit proxy (browser
  // headers) and by SessionResolver (Node.js server headers). The fingerprint
  // middleware would store a hash from the server-side get-session call (no UA,
  // no Accept) and then reject browser-proxied requests (different hash) → 401.
  // BetterAuth has its own session security (signed cookies, origin checks).
  const authApp = new Hono();
  authApp.use("*", async (c, next) => {
    const fp = computeFingerprint(c.req.raw.headers);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const rc = (c as any).get(REQUEST_CONTEXT_KEY) as
      | { deviceFingerprint?: string; clientFingerprint?: string; serverFingerprint?: string }
      | undefined;
    // Bridge fingerprints from request enrichment middleware → AsyncLocalStorage
    // so BetterAuth's session.create.before hook (which has no Hono context)
    // can stamp them onto the new session row.
    const runWith = (
      store: AsyncLocalStorage<string>,
      value: string | undefined,
      next: () => Promise<void>,
    ) => (value ? store.run(value, next) : next());
    await fingerprintStorage.run(fp, () =>
      runWith(deviceFingerprintStorage, rc?.deviceFingerprint, () =>
        runWith(clientFingerprintStorage, rc?.clientFingerprint, () =>
          runWith(serverFingerprintStorage, rc?.serverFingerprint, () => next()),
        ),
      ),
    );
  });
  authApp.route(
    "/",
    createAuthRoutes(
      auth,
      { check: throttleCheck, record: throttleRecord },
      logger,
      recordFailedSignIn,
    ),
  );
  app.route("/", authApp);

  // Protected custom routes: session + fingerprint + scope + CSRF → routes resolve from scope
  const protectedRoutes = new Hono();
  protectedRoutes.use("*", createSessionMiddleware(auth));
  protectedRoutes.use("*", sessionFingerprintMiddleware);
  protectedRoutes.use("*", createScopeMiddleware(provider));
  protectedRoutes.use("*", createCsrfMiddleware(config.corsOrigins));
  protectedRoutes.route("/", createEmulationRoutes());
  protectedRoutes.route("/", createOrgContactRoutes());
  protectedRoutes.route("/", createAccountRoutes(auth));
  protectedRoutes.route(
    "/",
    createInvitationRoutes({
      auth,
      db,
      baseUrl: config.baseUrl,
      translator,
      emailBaseUrl: config.emailBaseUrl,
    }),
  );
  app.route("/", protectedRoutes);

  return app;
}
