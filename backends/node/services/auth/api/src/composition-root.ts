import { ensureDatabase } from "@d2/database-startup-pg";
import { AsyncLocalStorage } from "node:async_hooks";
import { dirname, resolve as pathResolve } from "node:path";
import { fileURLToPath } from "node:url";
import pg from "pg";
import Redis from "ioredis";
import * as grpc from "@grpc/grpc-js";
import { drizzle } from "drizzle-orm/node-postgres";
import { ServiceCollection } from "@d2/di";
import { createLogger } from "@d2/logging";
import { ILoggerKey } from "@d2/logging";
import {
  HandlerContext,
  IHandlerContextKey,
  IRequestContextKey,
  createServiceScope,
} from "@d2/handler";
import { addRedisCaching } from "@d2/cache-redis";
import { PingMessageBus, IMessageBusPingKey } from "@d2/messaging";
import type { IMessagePublisher } from "@d2/messaging";
import { addCommsClient } from "@d2/comms-client";
import { wireGeoClientConsumers, IFindWhoIsKey } from "@d2/geo-client";
import {
  createAuth,
  runMigrations,
  addAuthInfra,
  createWhoIsResolutionConsumer,
  BetterAuthPasswordVerifier,
  BetterAuthVerificationStore,
  type AuthServiceConfig,
  type PasswordFunctions,
} from "@d2/auth-infra";
import {
  addAuthApp,
  ISignInThrottleStoreKey,
  IOtpRateLimitStoreKey,
  IVerificationStoreKey,
  IVerifyUserPasswordKey,
  ITranslatorKey,
  DEFAULT_AUTH_JOB_OPTIONS,
  type AuthJobOptions,
} from "@d2/auth-app";
import { createTranslator } from "@d2/i18n";
import { createSessionFingerprintMiddleware } from "./middleware/session-fingerprint.js";
import {
  createRedisSetup,
  addGeoClientHandlers,
  createPreAuthHandlers,
  createAuthCallbacks,
  createRecordFailedSignIn,
  buildHonoApp,
  buildGrpcServer,
} from "./setup/index.js";

/**
 * Optional overrides for the composition root.
 * Primarily used by tests to replace infrastructure dependencies.
 */
export interface AppOverrides {
  /** Custom password hash/verify — skips HIBP breach check when provided. */
  passwordFunctions?: PasswordFunctions;
}

/**
 * Creates and wires the complete auth service application.
 *
 * This is the composition root (mirrors .NET Program.cs):
 *   1. Create singletons: pg.Pool, Redis, logger
 *   2. Run Drizzle migrations + create repos via DI
 *   3. Register all services in ServiceCollection
 *   4. Build ServiceProvider
 *   5. Create pre-auth singletons (FindWhoIs, RateLimit, Throttle)
 *   6. Create BetterAuth with scoped callbacks
 *   7. Build Hono app with scope middleware on protected routes
 */
export async function createApp(
  config: AuthServiceConfig & {
    authApiKeys?: string[];
    grpcPort?: number;
    jobOptions?: AuthJobOptions;
  },
  publisher?: IMessagePublisher,
  overrides?: AppOverrides,
  messageBus?: import("@d2/messaging").MessageBus,
) {
  // 1. Singletons (infrastructure)
  const pool = new pg.Pool({
    connectionString: config.databaseUrl,
    max: 20,
    idleTimeoutMillis: 30_000,
    connectionTimeoutMillis: 5_000,
  });
  const redis = new Redis(config.redisUrl);
  const logger = createLogger({
    serviceName: config.appName ?? "auth-service",
  });

  const serviceContext = new HandlerContext(
    {
      isAuthenticated: false,
      isTrustedService: null,
      isAgentStaff: false,
      isAgentAdmin: false,
      isTargetingStaff: false,
      isTargetingAdmin: false,
      isOrgEmulating: null,
      isUserImpersonating: null,
    },
    logger,
  );

  // 2. Ensure database exists + run Drizzle migrations
  await ensureDatabase(config.databaseUrl, logger);
  await runMigrations(pool);
  const db = drizzle(pool);

  // 3. Redis setup (secondary storage, throttle store, cache handlers)
  const redisSetup = createRedisSetup(redis, serviceContext);

  // 4. Build ServiceCollection
  const services = new ServiceCollection();

  services.addInstance(ILoggerKey, logger);
  services.addScoped(
    IHandlerContextKey,
    (sp) => new HandlerContext(sp.resolve(IRequestContextKey), sp.resolve(ILoggerKey)),
  );

  services.addInstance(ISignInThrottleStoreKey, redisSetup.throttleStore);
  services.addInstance(IOtpRateLimitStoreKey, redisSetup.otpRateLimitStore);

  // Geo client handlers (gRPC-backed with local caching)
  const geoSetup = addGeoClientHandlers(services, config, serviceContext);

  // Distributed cache handlers (Get, Set, Remove, Lock, Ping, etc.)
  addRedisCaching(services, redis, serviceContext);

  // Layer registrations
  const jobOptions = config.jobOptions ?? DEFAULT_AUTH_JOB_OPTIONS;
  addAuthInfra(services, db, {
    signalrGatewayAddress: process.env.AUTH_SIGNALR_GATEWAY_ADDRESS,
    signalrApiKey: process.env.AUTH_SIGNALR_API_KEY,
    userPurgeBatchSize: jobOptions.userPurgeBatchSize,
  });
  addAuthApp(services, jobOptions, publisher);
  addCommsClient(services, { publisher });

  if (messageBus) {
    services.addInstance(IMessageBusPingKey, new PingMessageBus(messageBus, serviceContext));
  }

  // i18n translator (loads contracts/messages/*.json at startup) — needs to be
  // registered BEFORE build so handlers can resolve it.
  const messagesDir = pathResolve(
    dirname(fileURLToPath(import.meta.url)),
    "../../../../../../contracts/messages",
  );
  const translator = createTranslator({ messagesDir });
  services.addInstance(ITranslatorKey, translator);

  // BetterAuth-backed stores depend on `auth`, which is created AFTER `build()`
  // (because `createAuth` needs `provider` via callbacks → cyclic dependency).
  // Register lazy singleton factories that capture the eventual `auth` reference
  // — first resolution happens during request handling, well after auth is set.
  let authInstance: ReturnType<typeof createAuth> | undefined = undefined;
  services.addSingleton(IVerificationStoreKey, () => {
    if (!authInstance) throw new Error("auth instance not initialized");
    return new BetterAuthVerificationStore(authInstance);
  });
  services.addSingleton(IVerifyUserPasswordKey, () => {
    if (!authInstance) throw new Error("auth instance not initialized");
    return new BetterAuthPasswordVerifier(authInstance);
  });

  // FindWhoIs is constructed by `createPreAuthHandlers` AFTER `services.build()`
  // (it depends on the geo singleflight + circuit breaker that live with the
  // service-level HandlerContext). Register a lazy singleton that captures the
  // eventual instance — same pattern as the BetterAuth-backed stores above.
  let findWhoIsInstance: import("@d2/geo-client").FindWhoIs | undefined = undefined;
  services.addSingleton(IFindWhoIsKey, () => {
    if (!findWhoIsInstance) throw new Error("FindWhoIs not initialized");
    return findWhoIsInstance;
  });

  // 5. Build ServiceProvider
  const provider = services.build();

  // 6. Pre-auth singletons (outside DI scope)
  const preAuth = createPreAuthHandlers(
    redisSetup,
    geoSetup.geoClient,
    geoSetup.geoOptions,
    redisSetup.throttleStore,
    db,
    serviceContext,
    logger,
    overrides?.passwordFunctions,
  );
  // Bind the FindWhoIs lazy singleton registered earlier so DI consumers
  // (GetMySessions, GetSignInEvents) can resolve it.
  findWhoIsInstance = preAuth.findWhoIs;

  // 7. Session fingerprint binding (stolen token detection)
  const fingerprintStorage = new AsyncLocalStorage<string>();
  const deviceFingerprintStorage = new AsyncLocalStorage<string>();
  const clientFingerprintStorage = new AsyncLocalStorage<string>();
  const serverFingerprintStorage = new AsyncLocalStorage<string>();
  const SESSION_FP_PREFIX = "session:fp:";
  const SESSION_FP_TTL_SECONDS = 7 * 24 * 60 * 60;

  const callbacks = createAuthCallbacks(
    provider,
    logger,
    geoSetup.getContactsByExtKeys,
    translator,
    publisher,
  );
  const recordFailedSignIn = createRecordFailedSignIn(provider, logger, publisher);

  const auth = createAuth(config, db, redisSetup.secondaryStorage, {
    ...callbacks,
    logger,
    getFingerprintForCurrentRequest: () => fingerprintStorage.getStore(),
    getDeviceFingerprintForCurrentRequest: () => deviceFingerprintStorage.getStore(),
    getClientFingerprintForCurrentRequest: () => clientFingerprintStorage.getStore(),
    getServerFingerprintForCurrentRequest: () => serverFingerprintStorage.getStore(),
    passwordFunctions: preAuth.passwordFns,
  });
  authInstance = auth; // unblock the lazy factories registered earlier

  const sessionFingerprintMiddleware = createSessionFingerprintMiddleware({
    storeFingerprint: async (token, fp) => {
      await redis.set(`${SESSION_FP_PREFIX}${token}`, fp, "EX", SESSION_FP_TTL_SECONDS);
    },
    getFingerprint: async (token) => {
      return redis.get(`${SESSION_FP_PREFIX}${token}`);
    },
    revokeSession: async (token) => {
      await redis.del(`${SESSION_FP_PREFIX}${token}`);
      await auth.api.revokeSession({
        headers: new Headers(),
        body: { token },
      });
    },
  });

  // 8. Build Hono app
  const app = buildHonoApp({
    auth,
    provider,
    config: {
      corsOrigins: config.corsOrigins,
      authApiKeys: config.authApiKeys,
      baseUrl: config.baseUrl,
      emailBaseUrl: config.emailBaseUrl,
    },
    findWhoIs: preAuth.findWhoIs,
    rateLimitCheck: preAuth.rateLimitCheck,
    throttleCheck: preAuth.throttleCheck,
    throttleRecord: preAuth.throttleRecord,
    recordFailedSignIn,
    checkEmailHandler: preAuth.checkEmailHandler,
    fingerprintStorage,
    deviceFingerprintStorage,
    clientFingerprintStorage,
    serverFingerprintStorage,
    sessionFingerprintMiddleware,
    translator,
    logger,
    db,
  });

  // 9. WhoIs resolution consumer + geo-client cache invalidation
  if (messageBus) {
    createWhoIsResolutionConsumer({
      messageBus,
      provider,
      createScope: createServiceScope,
      findWhoIs: preAuth.findWhoIs,
      logger,
    });

    // Cross-process geo-client cache invalidation. Auth's local cache evicts
    // when auth itself mutates a contact (via the cacheRemove handler injected
    // into UpdateContactsByExtKeys/DeleteContactsByExtKeys), but only this
    // subscription keeps it consistent when ANOTHER service mutates contacts.
    await wireGeoClientConsumers({
      bus: messageBus,
      cacheStore: geoSetup.contactCacheStore,
      context: serviceContext,
      logger,
    });
  }

  // 10. gRPC server
  let grpcServer: grpc.Server | undefined;
  if (config.grpcPort) {
    grpcServer = await buildGrpcServer({
      provider,
      grpcPort: config.grpcPort,
      authApiKeys: config.authApiKeys,
      logger,
    });
  }

  // Cleanup function for graceful shutdown
  async function shutdown() {
    if (grpcServer) {
      await new Promise<void>((resolve) => {
        const timeout = setTimeout(() => {
          grpcServer!.forceShutdown();
          resolve();
        }, 5_000);
        grpcServer!.tryShutdown(() => {
          clearTimeout(timeout);
          resolve();
        });
      });
    }
    provider.dispose();
    redis.disconnect();
    await pool.end();
  }

  return { app, auth, grpcServer, shutdown };
}
