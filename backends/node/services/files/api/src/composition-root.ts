import pg from "pg";
import Redis from "ioredis";
import * as grpc from "@grpc/grpc-js";
import { S3Client } from "@aws-sdk/client-s3";
import { drizzle } from "drizzle-orm/node-postgres";
import { ensureDatabase } from "@d2/database-startup-pg";
import { ServiceCollection } from "@d2/di";
import { createLogger, ILoggerKey } from "@d2/logging";
import type { ILogger } from "@d2/logging";
import {
  HandlerContext,
  IHandlerContextKey,
  IRequestContextKey,
  createServiceScope,
} from "@d2/handler";
import * as CacheRedis from "@d2/cache-redis";
import { addRedisCaching } from "@d2/cache-redis";
import * as CacheMemory from "@d2/cache-memory";
import { PingMessageBus, IMessageBusPingKey } from "@d2/messaging";
import type { IMessagePublisher, MessageBus } from "@d2/messaging";
import {
  FindWhoIs,
  createGeoServiceClient,
  createGeoCircuitBreaker,
  DEFAULT_GEO_CLIENT_OPTIONS,
} from "@d2/geo-client";
import type { GeoClientOptions } from "@d2/geo-client";
import { Singleflight } from "@d2/utilities";
import { CheckRateLimit } from "@d2/ratelimit";
import { addFilesApp, parseContextKeyConfigs, DEFAULT_FILES_JOB_OPTIONS } from "@d2/files-app";
import type { FilesJobOptions } from "@d2/files-app";
import { addFilesInfra, runMigrations } from "@d2/files-infra";
import { createFileUploadedConsumer, createFileProcessingConsumer } from "@d2/files-infra";
import { buildHonoApp } from "./setup/hono-app-setup.js";
import { buildGrpcServer } from "./setup/grpc-server-setup.js";
import type { Hono } from "hono";

export interface FilesServiceConfig {
  readonly databaseUrl: string;
  readonly redisUrl: string;
  readonly rabbitMqUrl: string;
  readonly httpPort: number;
  readonly grpcPort?: number;
  readonly filesApiKeys: string[];
  readonly corsOrigins: string[];
  // S3/MinIO
  readonly s3Endpoint: string;
  readonly s3AccessKey: string;
  readonly s3SecretKey: string;
  readonly s3BucketName: string;
  readonly s3Region: string;
  /** Public-facing S3 endpoint for presigned URLs (e.g., cloudflared tunnel to MinIO). */
  readonly s3PublicEndpoint?: string;
  // ClamAV
  readonly clamdHost: string;
  readonly clamdPort: number;
  // JWT auth
  readonly jwksUrl: string;
  readonly jwtIssuer: string;
  readonly jwtAudience: string;
  /** Disable fingerprint check for test environments. Default: true. */
  readonly fingerprintCheck?: boolean;
  // Geo client (for request enrichment + WhoIs)
  readonly geoAddress?: string;
  readonly geoApiKey?: string;
  // SignalR
  readonly signalrGatewayAddress?: string;
  // Outbound API keys (required — fail-closed)
  readonly callbackApiKey: string;
  readonly signalrApiKey: string;
  // Jobs
  readonly jobOptions?: FilesJobOptions;
}

/**
 * Creates pre-auth singletons: FindWhoIs (IP → location) + CheckRateLimit.
 * These operate outside the DI scope — they use the service-level HandlerContext.
 */
function createPreAuthHandlers(
  redis: Redis,
  geoAddress: string,
  geoApiKey: string,
  serviceContext: HandlerContext,
  logger: ILogger,
) {
  // Redis handlers for rate limiting
  const redisGetTtl = new CacheRedis.GetTtl(redis, serviceContext);
  const redisIncrement = new CacheRedis.Increment(redis, serviceContext);
  const redisSet = new CacheRedis.Set<string>(redis, serviceContext);

  const rateLimitCheck = new CheckRateLimit(
    redisGetTtl,
    redisIncrement,
    redisSet,
    {},
    serviceContext,
  );

  // FindWhoIs for request enrichment
  const geoOptions: GeoClientOptions = {
    ...DEFAULT_GEO_CLIENT_OPTIONS,
    apiKey: geoApiKey,
  };
  const geoClient = createGeoServiceClient(geoAddress, geoApiKey);
  const whoIsCacheStore = new CacheMemory.MemoryCacheStore();
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

  return { findWhoIs, rateLimitCheck };
}

/**
 * Creates and wires the complete files service application.
 *
 * This is the composition root (mirrors .NET Program.cs):
 *   1. Create singletons: pg.Pool, S3Client, Redis, logger
 *   2. Run Drizzle migrations
 *   3. Create pre-auth handlers (FindWhoIs, rate limiting)
 *   4. Register all services in ServiceCollection
 *   5. Build ServiceProvider
 *   6. Create Hono app (REST endpoints with full middleware pipeline)
 *   7. Create gRPC server (health + jobs)
 *   8. Start RabbitMQ consumers (intake + processing pipeline)
 */
export async function createFilesApp(
  config: FilesServiceConfig,
  publisher: IMessagePublisher,
  messageBus?: MessageBus,
) {
  // 1. Singletons
  const pool = new pg.Pool({
    connectionString: config.databaseUrl,
    max: 20,
    idleTimeoutMillis: 30_000,
    connectionTimeoutMillis: 5_000,
  });
  const redis = new Redis(config.redisUrl);
  const logger = createLogger({ serviceName: "files-service" });

  const s3 = new S3Client({
    endpoint: config.s3Endpoint,
    region: config.s3Region,
    credentials: {
      accessKeyId: config.s3AccessKey,
      secretAccessKey: config.s3SecretKey,
    },
    forcePathStyle: true,
  });

  // Public S3 client for presigned URLs (browser → MinIO via tunnel).
  // Falls back to the internal client when not configured.
  const s3Public = config.s3PublicEndpoint
    ? new S3Client({
        endpoint: config.s3PublicEndpoint,
        region: config.s3Region,
        credentials: {
          accessKeyId: config.s3AccessKey,
          secretAccessKey: config.s3SecretKey,
        },
        forcePathStyle: true,
      })
    : undefined;

  const serviceContext = new HandlerContext(
    {
      isAuthenticated: null,
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

  // 3. Parse context key configs from env
  const contextKeyConfigs = parseContextKeyConfigs(
    process.env as Record<string, string | undefined>,
  );
  logger.info(`Loaded ${contextKeyConfigs.size} context key config(s)`);

  // 4. Pre-auth handlers (request enrichment + rate limiting)
  let findWhoIs: FindWhoIs | undefined;
  let rateLimitCheck: CheckRateLimit | undefined;
  if (config.geoAddress && config.geoApiKey) {
    const preAuth = createPreAuthHandlers(
      redis,
      config.geoAddress,
      config.geoApiKey,
      serviceContext,
      logger,
    );
    findWhoIs = preAuth.findWhoIs;
    rateLimitCheck = preAuth.rateLimitCheck;
    logger.info("Request enrichment + rate limiting enabled (Geo client connected)");
  } else {
    logger.warn(
      "GEO_GRPC_ADDRESS or FILES_GEO_CLIENT__APIKEY not configured — request enrichment + rate limiting disabled",
    );
  }

  // 5. Build ServiceCollection
  const services = new ServiceCollection();

  services.addInstance(ILoggerKey, logger);
  services.addScoped(
    IHandlerContextKey,
    (sp) => new HandlerContext(sp.resolve(IRequestContextKey), sp.resolve(ILoggerKey)),
  );

  // Infra layer (repo, storage, providers, outbound, realtime, messaging)
  const infraDisposable = addFilesInfra(services, {
    db,
    s3,
    s3Public,
    bucketName: config.s3BucketName,
    clamd: { host: config.clamdHost, port: config.clamdPort },
    publisher,
    signalrGatewayAddress: config.signalrGatewayAddress ?? "localhost:5200",
    callbackApiKey: config.callbackApiKey,
    signalrApiKey: config.signalrApiKey,
  });

  // App layer (CQRS handlers)
  addFilesApp(services, contextKeyConfigs, config.jobOptions ?? DEFAULT_FILES_JOB_OPTIONS);

  // Distributed cache handlers (Get, Set, Remove, Lock, Ping, etc.)
  addRedisCaching(services, redis, serviceContext);
  if (messageBus) {
    services.addInstance(IMessageBusPingKey, new PingMessageBus(messageBus, serviceContext));
  }

  // 6. Build ServiceProvider
  const provider = services.build();

  // 7. Build Hono app
  const app: Hono = buildHonoApp({
    provider,
    config: {
      corsOrigins: config.corsOrigins,
      jwksUrl: config.jwksUrl,
      jwtIssuer: config.jwtIssuer,
      jwtAudience: config.jwtAudience,
      fingerprintCheck: config.fingerprintCheck,
    },
    contextKeyConfigs,
    findWhoIs,
    rateLimitCheck,
    logger,
  });

  // 8. gRPC server
  let grpcServer: grpc.Server | undefined;
  if (config.grpcPort) {
    grpcServer = await buildGrpcServer({
      provider,
      grpcPort: config.grpcPort,
      filesApiKeys: config.filesApiKeys,
      logger,
    });
  }

  // 9. RabbitMQ consumers
  if (messageBus) {
    createFileUploadedConsumer({
      messageBus,
      provider,
      createScope: createServiceScope,
      logger,
    });
    logger.info("File upload consumer started");

    createFileProcessingConsumer({
      messageBus,
      provider,
      createScope: createServiceScope,
      logger,
    });
    logger.info("File processing consumer started");
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
    try {
      infraDisposable.dispose();
    } catch (e) {
      logger.error("Shutdown: infraDisposable.dispose failed", { error: String(e) });
    }
    try {
      s3.destroy();
    } catch (e) {
      logger.error("Shutdown: s3.destroy failed", { error: String(e) });
    }
    try {
      s3Public?.destroy();
    } catch (e) {
      logger.error("Shutdown: s3Public.destroy failed", { error: String(e) });
    }
    try {
      provider.dispose();
    } catch (e) {
      logger.error("Shutdown: provider.dispose failed", { error: String(e) });
    }
    try {
      redis.disconnect();
    } catch (e) {
      logger.error("Shutdown: redis.disconnect failed", { error: String(e) });
    }
    try {
      await pool.end();
    } catch (e) {
      logger.error("Shutdown: pool.end failed", { error: String(e) });
    }
  }

  return { app, grpcServer, provider, shutdown };
}
