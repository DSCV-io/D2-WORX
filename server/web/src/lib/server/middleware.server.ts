/**
 * Server-side middleware composition module.
 *
 * Lazy singleton initialization for Redis, Geo gRPC, cache handlers,
 * FindWhoIs, CheckRateLimit, and idempotency handlers. Pattern mirrors
 * auth/api/src/composition-root.ts but simplified — no DI container,
 * just module-level singletons.
 *
 * Throws if required env vars are missing — infrastructure (Redis, Geo service)
 * must be running. Matches auth.server.ts and gateway.server.ts behavior.
 *
 * The `.server.ts` suffix ensures this module is never included in
 * client-side bundles (SvelteKit convention).
 */
import Redis from "ioredis";
import { createLogger, type ILogger } from "@d2/logging";
import { HandlerContext } from "@d2/handler";
import { parseRedisUrl } from "@d2/service-defaults/config";
import * as CacheRedis from "@d2/cache-redis";
import { MemoryCacheStore } from "@d2/cache-memory";
import { Singleflight } from "@d2/utilities";
import {
  createGeoServiceClient,
  createGeoCircuitBreaker,
  FindWhoIs,
  Get as GetGeoRefData,
  GetFromMem,
  GetFromDist,
  GetFromDisk,
  ReqUpdate,
  SetInMem,
  SetOnDisk,
  GeoRefDataSerializer,
  DEFAULT_GEO_CLIENT_OPTIONS,
  type GeoClientOptions,
} from "@d2/geo-client";
import type { GeoRefData } from "@d2/protos";
import { CheckRateLimit } from "@d2/ratelimit";
import { CheckIdempotency, checkIdempotency } from "@d2/idempotency";
import { enrichRequest } from "@d2/request-enrichment";
import type { RateLimit, Idempotency, DistributedCache } from "@d2/interfaces";
import { createMockMiddlewareContext } from "./middleware.mock.server.js";

// Re-export for use in hook wrappers.
export { enrichRequest };
export { checkIdempotency };
export type { IdempotencyResult } from "@d2/idempotency";

export interface MiddlewareContext {
  logger: ILogger;
  findWhoIs: FindWhoIs;
  rateLimitCheck: RateLimit.ICheckHandler;
  idempotencyCheck: Idempotency.ICheckHandler;
  redisGet: DistributedCache.IGetHandler<string>;
  redisSet: DistributedCache.ISetHandler<string>;
  redisSetNx: DistributedCache.ISetNxHandler<string>;
  redisRemove: DistributedCache.IRemoveHandler;
  getGeoRefData: GetGeoRefData;
}

let cached: MiddlewareContext | null | undefined;

/**
 * Whether infrastructure deps should be mocked when env vars are missing.
 *
 * Enabled in CI (`CI=true`) or explicitly via `D2_MOCK_INFRA=true`.
 * Returns a mock context with stub handlers instead of throwing FATAL —
 * the full middleware pipeline runs with no-op implementations so pages
 * render normally (e.g. mocked Playwright tests).
 *
 * In production (neither flag set), missing env vars always FATAL.
 */
const MOCK_INFRA = process.env.D2_MOCK_INFRA === "true" || process.env.CI === "true";

/**
 * Returns the shared middleware context (lazy singleton).
 *
 * - Production: throws FATAL if required env vars are missing.
 * - Mock mode: returns a mock context with stub handlers.
 */
export function getMiddlewareContext(): MiddlewareContext | null {
  if (cached !== undefined) return cached;

  // Mock mode: always use stub handlers, regardless of env vars present.
  // This ensures `D2_MOCK_INFRA=true` overrides even when .env.local is loaded.
  if (MOCK_INFRA) {
    console.warn(
      "[d2-sveltekit] Infrastructure mocked. Middleware pipeline runs with stub handlers.",
    );
    cached = createMockMiddlewareContext();
    return cached;
  }

  const redisConnectionString = process.env.REDIS_URL;
  const geoAddress = process.env.GEO_GRPC_ADDRESS;
  const geoApiKey = process.env.SVELTEKIT_GEO_CLIENT__APIKEY;

  if (!redisConnectionString || !geoAddress || !geoApiKey) {
    const missing: string[] = [];
    if (!redisConnectionString) missing.push("REDIS_URL");
    if (!geoAddress) missing.push("GEO_GRPC_ADDRESS");
    if (!geoApiKey) missing.push("SVELTEKIT_GEO_CLIENT__APIKEY");

    throw new Error(
      `[d2-sveltekit] FATAL: Missing required env vars: ${missing.join(", ")}. ` +
        "Infrastructure services must be running. Check your .env.local file.",
    );
  }

  const logger = createLogger({ serviceName: "d2-sveltekit" });

  // Redis (lazyConnect: no startup blocking, connects on first operation)
  const redisUrl = parseRedisUrl(redisConnectionString);
  const redis = new Redis(redisUrl, { lazyConnect: true });

  // Service-level HandlerContext (pre-auth: no per-request user info)
  const serviceContext = new HandlerContext(
    {
      isAuthenticated: false,
      isTrustedService: null,
      isAgentStaff: false,
      isAgentAdmin: false,
      isTargetingStaff: false,
      isTargetingAdmin: false,
      isOrgEmulating: false,
      isUserImpersonating: false,
    },
    logger,
  );

  // Redis cache handlers (singletons — shared Redis connection)
  const redisGet = new CacheRedis.Get<string>(redis, serviceContext);
  const redisSet = new CacheRedis.Set<string>(redis, serviceContext);
  const redisSetNx = new CacheRedis.SetNx<string>(redis, serviceContext);
  const redisRemove = new CacheRedis.Remove(redis, serviceContext);
  const _redisExists = new CacheRedis.Exists(redis, serviceContext);
  const redisGetTtl = new CacheRedis.GetTtl(redis, serviceContext);
  const redisIncrement = new CacheRedis.Increment(redis, serviceContext);

  // Geo gRPC client + FindWhoIs handler (memory-cached WhoIs lookups)
  const geoOptions: GeoClientOptions = {
    ...DEFAULT_GEO_CLIENT_OPTIONS,
    allowedContextKeys: [],
    apiKey: geoApiKey,
  };
  const geoClient = createGeoServiceClient(geoAddress, geoApiKey);
  const whoIsCacheStore = new MemoryCacheStore();
  const geoCircuitBreaker = createGeoCircuitBreaker(geoOptions, logger);
  const geoSingleflight = new Singleflight();
  const findWhoIs = new FindWhoIs(
    whoIsCacheStore,
    geoClient,
    geoOptions,
    geoCircuitBreaker,
    geoSingleflight,
    serviceContext,
  );

  // Geo reference data handler chain (Memory → Redis → Disk → gRPC)
  const refDataMemCache = new MemoryCacheStore();
  const getFromMem = new GetFromMem(refDataMemCache, serviceContext);
  const getFromDist = new GetFromDist(
    new CacheRedis.Get<GeoRefData>(redis, serviceContext, new GeoRefDataSerializer()),
    serviceContext,
  );
  const getFromDisk = new GetFromDisk(geoOptions, serviceContext);
  const setInMem = new SetInMem(refDataMemCache, serviceContext);
  const setOnDisk = new SetOnDisk(geoOptions, serviceContext);
  const reqUpdate = new ReqUpdate(geoClient, geoOptions, serviceContext);

  const getGeoRefData = new GetGeoRefData(
    { getFromMem, getFromDist, getFromDisk, reqUpdate, setInMem, setOnDisk },
    serviceContext,
  );

  // Rate limit check handler (Redis-backed sliding window)
  const rateLimitCheck = new CheckRateLimit(
    redisGetTtl,
    redisIncrement,
    redisSet,
    {},
    serviceContext,
  );

  // Idempotency check handler (Redis-backed SET NX)
  const idempotencyCheck = new CheckIdempotency(redisSetNx, redisGet, {}, serviceContext);

  cached = {
    logger,
    findWhoIs,
    rateLimitCheck,
    idempotencyCheck,
    redisGet,
    redisSet,
    redisSetNx,
    redisRemove,
    getGeoRefData,
  };

  logger.info("Server middleware initialized (request enrichment, rate limiting, idempotency).");
  return cached;
}
