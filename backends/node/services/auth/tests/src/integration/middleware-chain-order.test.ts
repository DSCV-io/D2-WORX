import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import { Hono } from "hono";
import { createMiddleware } from "hono/factory";
import { cors } from "hono/cors";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { D2Result, HttpStatusCode } from "@d2/result";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { MemoryCacheStore } from "@d2/cache-memory";
import { enrichRequest } from "@d2/request-enrichment";
import { FindWhoIs, createGeoCircuitBreaker, type GeoClientOptions } from "@d2/geo-client";
import { Singleflight } from "@d2/utilities";
import { CheckRateLimit } from "@d2/ratelimit";
import type { RateLimit } from "@d2/interfaces";
import * as CacheRedis from "@d2/cache-redis";
import { RedisContainer, type StartedRedisContainer } from "@testcontainers/redis";
import Redis from "ioredis";

const REQUEST_CONTEXT_KEY = "requestContext" as const;

/**
 * Tests that auth API middleware runs in the correct order and each layer
 * produces the expected effects.
 *
 * Uses a minimal Hono app with the same middleware chain as the auth
 * composition root, but without BetterAuth/Geo gRPC (not needed for
 * middleware ordering validation).
 */
describe("Middleware chain order", () => {
  let redisContainer: StartedRedisContainer;
  let redis: Redis;
  let app: Hono;

  const logger = createLogger({ level: "silent" as never });

  function createServiceContext(): HandlerContext {
    const request: IRequestContext = {
      traceId: "middleware-test",
      isAuthenticated: false,
      isTrustedService: false,
      isOrgEmulating: false,
      isUserImpersonating: false,
      isAgentStaff: false,
      isAgentAdmin: false,
      isTargetingStaff: false,
      isTargetingAdmin: false,
    };
    return new HandlerContext(request, logger);
  }

  beforeAll(async () => {
    redisContainer = await new RedisContainer("redis:8.2").start();
    redis = new Redis({
      host: redisContainer.getHost(),
      port: redisContainer.getFirstMappedPort(),
      lazyConnect: false,
    });

    const ctx = createServiceContext();

    // Rate limiter with very low threshold for testing
    const getTtl = new CacheRedis.GetTtl(redis, ctx);
    const increment = new CacheRedis.Increment(redis, ctx);
    const set = new CacheRedis.Set<string>(redis, ctx);
    const rateLimitCheck = new CheckRateLimit(
      getTtl,
      increment,
      set,
      {
        deviceFingerprintThreshold: 3,
        ipThreshold: 3,
      },
      ctx,
    );

    // FindWhoIs with stub gRPC client (always returns empty — no real Geo service)
    const whoIsCacheStore = new MemoryCacheStore();
    const geoOptions: GeoClientOptions = {
      whoIsCacheExpirationMs: 28_800_000,
      whoIsCacheMaxEntries: 10_000,
      dataDir: "./data",
      contactCacheMaxEntries: 10_000,
      allowedContextKeys: [],
      apiKey: "",
      grpcTimeoutMs: 30_000,
      whoIsNegativeCacheExpirationMs: 3_600_000,
      circuitBreakerFailureThreshold: 5,
      circuitBreakerCooldownMs: 30_000,
    };
    const stubGeoClient = {
      findWhoIs: (
        _req: unknown,
        _meta: unknown,
        _opts: unknown,
        cb: (err: null, res: unknown) => void,
      ) => {
        cb(null, { result: { success: true }, data: [] });
      },
    } as unknown as Parameters<typeof FindWhoIs>[1];
    const geoCircuitBreaker = createGeoCircuitBreaker(geoOptions, logger);
    const geoSingleflight = new Singleflight();
    const findWhoIs = new FindWhoIs(
      whoIsCacheStore,
      stubGeoClient,
      geoOptions,
      geoCircuitBreaker,
      geoSingleflight,
      ctx,
    );

    // Build Hono app with same middleware order as composition-root.ts
    app = new Hono();

    // 1. CORS (mirrors createCorsMiddleware)
    app.use(
      "*",
      cors({
        origin: "http://localhost:5173",
        credentials: true,
        allowHeaders: [
          "Content-Type",
          "Authorization",
          "X-Requested-With",
          "traceparent",
          "tracestate",
        ],
        allowMethods: ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"],
        maxAge: 86400,
      }),
    );

    // 2. Security headers
    app.use("*", async (c, next) => {
      await next();
      c.res.headers.set("X-Content-Type-Options", "nosniff");
      c.res.headers.set("X-Frame-Options", "DENY");
    });

    // 3. Request enrichment (mirrors createRequestEnrichmentMiddleware)
    app.use(
      "*",
      createMiddleware(async (c, next) => {
        const headers: Record<string, string | undefined> = {};
        c.req.raw.headers.forEach((value, key) => {
          headers[key] = value;
        });
        const requestContext = await enrichRequest(headers, findWhoIs, undefined, logger);
        c.set(REQUEST_CONTEXT_KEY, requestContext);
        await next();
      }),
    );

    // 4. Rate limiting (mirrors createDistributedRateLimitMiddleware)
    app.use(
      "*",
      createMiddleware(async (c, next) => {
        const requestContext = c.get(REQUEST_CONTEXT_KEY) as RateLimit.CheckInput["requestContext"];
        if (!requestContext) {
          await next();
          return;
        }
        const result = await rateLimitCheck.handleAsync({ requestContext });
        if (result.success && result.data?.isBlocked) {
          const retryAfterSec = result.data.retryAfterMs
            ? Math.ceil(result.data.retryAfterMs / 1000)
            : 300;
          c.header("Retry-After", String(retryAfterSec));
          return c.json(
            D2Result.fail({
              messages: ["Too many requests. Please slow down."],
              statusCode: HttpStatusCode.TooManyRequests,
              errorCode: "RATE_LIMITED",
            }),
            429 as ContentfulStatusCode,
          );
        }
        await next();
      }),
    );

    // Test routes
    app.get("/test/public", (c) => c.json({ ok: true }));
  }, 120_000);

  afterAll(async () => {
    await redis?.quit();
    await redisContainer?.stop();
  });

  beforeEach(async () => {
    await redis.flushall();
  });

  it("should return CORS headers on OPTIONS requests (CORS runs first)", async () => {
    const res = await app.request("/test/public", {
      method: "OPTIONS",
      headers: {
        Origin: "http://localhost:5173",
        "Access-Control-Request-Method": "GET",
      },
    });

    expect(res.headers.get("Access-Control-Allow-Origin")).toBe("http://localhost:5173");
  });

  it("should include security headers on all responses", async () => {
    const res = await app.request("/test/public");

    expect(res.headers.get("X-Content-Type-Options")).toBe("nosniff");
    expect(res.headers.get("X-Frame-Options")).toBe("DENY");
  });

  it("should enrich request before rate limiting (rate limiter depends on requestContext)", async () => {
    // If enrichment didn't run before rate limiting, the rate limiter would
    // skip (no requestContext) and never block. Verify the ordering works by
    // flooding requests until we get a 429.
    // Must provide CF-Connecting-IP (the only trusted proxy header by default)
    // so clientIp != "unknown" (which is treated as localhost and skips the
    // IP dimension entirely).
    let got429 = false;
    for (let i = 0; i < 20; i++) {
      const res = await app.request("/test/public", {
        headers: { "CF-Connecting-IP": "203.0.113.1" },
      });
      if (res.status === 429) {
        got429 = true;
        // Rate limit response should still have security headers (they run after)
        expect(res.headers.get("X-Content-Type-Options")).toBe("nosniff");
        // Should have Retry-After header
        expect(res.headers.get("Retry-After")).toBeTruthy();
        break;
      }
    }

    expect(got429).toBe(true);
  });

  it("should return 404 for unknown routes with security headers intact", async () => {
    const res = await app.request("/nonexistent-route");

    expect(res.status).toBe(404);
    expect(res.headers.get("X-Content-Type-Options")).toBe("nosniff");
    expect(res.headers.get("X-Frame-Options")).toBe("DENY");
  });
});
