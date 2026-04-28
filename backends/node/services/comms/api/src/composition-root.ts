import pg from "pg";
import { ensureDatabase } from "@d2/database-startup-pg";
import Redis from "ioredis";
import { drizzle } from "drizzle-orm/node-postgres";
import { createLogger, type ILogger } from "@d2/logging";
import { ILoggerKey } from "@d2/logging";
import { HandlerContext, IHandlerContextKey, IRequestContextKey } from "@d2/handler";
import { ServiceCollection } from "@d2/di";
import { addRedisCaching } from "@d2/cache-redis";
import { MessageBus, PingMessageBus, IMessageBusPingKey } from "@d2/messaging";
import { addCommsApp } from "@d2/comms-app";
import { DEFAULT_COMMS_JOB_OPTIONS, type CommsJobOptions } from "@d2/comms-app";
import { addCommsInfra, runMigrations } from "@d2/comms-infra";
import { wireGeoClientConsumers } from "@d2/geo-client";
import {
  addGeoClientHandlers,
  addDeliveryProviders,
  buildGrpcServer,
  startNotificationConsumer,
} from "./setup/index.js";

export interface CommsServiceConfig {
  databaseUrl: string;
  rabbitMqUrl: string;
  grpcPort: number;
  redisUrl?: string;
  resendApiKey?: string;
  resendFromAddress?: string;
  twilioAccountSid?: string;
  twilioAuthToken?: string;
  twilioPhoneNumber?: string;
  /** "twilio" | "mock" — when omitted, auto-detects based on Twilio creds. Read from env as a string; provider-setup narrows. */
  smsProvider?: string;
  /** JSONL log file path used by the mock SMS provider. */
  smsMockLogPath?: string;
  geoAddress?: string;
  geoApiKey?: string;
  commsApiKeys?: string[];
  /** When true, allow startup without API key auth. Default false. */
  allowUnauthenticated?: boolean;
  /** Job options (retention periods, lock TTL). */
  jobOptions?: CommsJobOptions;
  /** Brand text for automated email footers (e.g., "DCSV WORX"). */
  emailFooterText?: string;
  /** Path to a custom HTML email template file (overrides built-in template). */
  emailTemplatePath?: string;
}

/**
 * Creates and wires the complete comms service application.
 *
 * This is the composition root (mirrors .NET Program.cs):
 *   1. Create singletons: pg.Pool, logger
 *   2. Run Drizzle migrations
 *   3. Register all services in ServiceCollection
 *   4. Build ServiceProvider
 *   5. Create gRPC server (per-RPC scope)
 *   6. Create RabbitMQ consumer (per-message scope + DLX retry topology)
 */
export async function createCommsService(config: CommsServiceConfig) {
  // 1. Singletons
  const pool = new pg.Pool({
    connectionString: config.databaseUrl,
    max: 20,
    idleTimeoutMillis: 30_000,
    connectionTimeoutMillis: 5_000,
  });
  const logger: ILogger = createLogger({ serviceName: "comms-service" });

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

  // 3. Build ServiceCollection
  const services = new ServiceCollection();

  services.addInstance(ILoggerKey, logger);
  services.addScoped(
    IHandlerContextKey,
    (sp) => new HandlerContext(sp.resolve(IRequestContextKey), sp.resolve(ILoggerKey)),
  );

  // Geo client for recipient resolution + ext-key contact lookup. Returns
  // the cache store and eviction handler so we can wire the cross-process
  // cache invalidation consumer once the message bus is up.
  const geoClientSetup = addGeoClientHandlers(services, config, serviceContext);

  // Layer registrations
  addCommsInfra(services, db);
  addCommsApp(
    services,
    config.jobOptions ?? DEFAULT_COMMS_JOB_OPTIONS,
    config.emailFooterText,
    config.emailTemplatePath,
  );

  // Delivery providers (email + SMS)
  addDeliveryProviders(services, config, serviceContext, logger);

  // 4. MessageBus (connect early so PingMessageBus can be registered)
  let messageBus: MessageBus | undefined;
  if (config.rabbitMqUrl) {
    messageBus = new MessageBus({
      url: config.rabbitMqUrl,
      connectionName: "comms-service",
      logger,
    });
    await messageBus.waitForConnection();
    logger.info("RabbitMQ connected");
    services.addInstance(IMessageBusPingKey, new PingMessageBus(messageBus, serviceContext));
  } else {
    logger.warn("No RabbitMQ URL configured — event consumption disabled");
  }

  // Distributed locks (Redis required for jobs)
  if (config.jobOptions && !config.redisUrl) {
    throw new Error(
      "Job options are configured but no Redis URL provided. Distributed locks require Redis.",
    );
  }

  let redis: Redis | undefined;
  if (config.redisUrl) {
    redis = new Redis(config.redisUrl);
    addRedisCaching(services, redis, serviceContext);
    logger.info("Redis connected (distributed cache handlers registered)");
  }

  // 5. Build ServiceProvider
  const provider = services.build();

  // 6. gRPC server
  const server = await buildGrpcServer({
    provider,
    grpcPort: config.grpcPort,
    commsApiKeys: config.commsApiKeys,
    allowUnauthenticated: config.allowUnauthenticated,
    redis,
    logger,
  });

  // 7. RabbitMQ notification consumer + geo-client cache invalidation
  if (messageBus) {
    await startNotificationConsumer(messageBus, provider, logger);

    // Wire the geo-client's cross-process cache invalidation. Every service
    // with a geo-client cache must do this; the helper subscribes to the
    // `events.geo.contacts` fanout and evicts on any contact mutation
    // anywhere in the cluster.
    await wireGeoClientConsumers({
      bus: messageBus,
      cacheStore: geoClientSetup.contactCacheStore,
      context: serviceContext,
      logger,
    });
  }

  // 8. Shutdown
  async function shutdown() {
    if (messageBus) await messageBus.close();
    if (redis) redis.disconnect();
    provider.dispose();
    await pool.end();
  }

  return { server, provider, shutdown };
}
