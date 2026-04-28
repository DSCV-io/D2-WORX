import { serve } from "@hono/node-server";
import { MessageBus } from "@d2/messaging";
import { createLogger } from "@d2/logging";
import {
  defineConfig,
  requiredParsed,
  requiredString,
  defaultString,
  defaultInt,
  optionalInt,
  optionalString,
  envArray,
  optionalSection,
  parsePostgresUrl,
  parseRedisUrl,
} from "@d2/service-defaults/config";
import { DEFAULT_FILES_JOB_OPTIONS } from "@d2/files-app";
import { createFilesApp } from "./composition-root.js";

const logger = createLogger({ serviceName: "files-service" });

// Docker Compose injects connection strings in .NET formats (ADO.NET for PG, StackExchange for Redis).
// Parsers convert to URI format for Node.js clients, passing through URIs unchanged.
const config = defineConfig("files-service", {
  databaseUrl: requiredParsed(parsePostgresUrl, "FILES_DATABASE_URL"),
  redisUrl: requiredParsed(parseRedisUrl, "REDIS_URL"),
  rabbitMqUrl: requiredString("RABBITMQ_URL"),
  httpPort: defaultInt(5300, "FILES_HTTP_PORT", "PORT"),
  grpcPort: optionalInt("FILES_GRPC_PORT"),
  filesApiKeys: envArray("FILES_API_KEYS"),
  corsOrigins: envArray("FILES_CORS_ORIGINS"),
  // S3/MinIO
  s3Endpoint: requiredString("FILES_S3_ENDPOINT"),
  s3AccessKey: requiredString("FILES_S3_ACCESS_KEY"),
  s3SecretKey: requiredString("FILES_S3_SECRET_KEY"),
  s3BucketName: defaultString("d2-files", "FILES_S3_BUCKET"),
  s3Region: defaultString("us-east-1", "FILES_S3_REGION"),
  s3PublicEndpoint: optionalString("FILES_S3_PUBLIC_ENDPOINT"),
  // ClamAV
  clamdHost: defaultString("localhost", "CLAMAV_HOST"),
  clamdPort: defaultInt(3310, "CLAMAV_PORT"),
  // JWT auth (for REST endpoints)
  jwksUrl: requiredString("FILES_JWKS_URL"),
  jwtIssuer: defaultString("d2-worx", "FILES_JWT_ISSUER"),
  jwtAudience: defaultString("d2-services", "FILES_JWT_AUDIENCE"),
  // Geo client (for request enrichment + WhoIs lookup)
  geoAddress: optionalString("GEO_GRPC_ADDRESS"),
  geoApiKey: optionalString("FILES_GEO_CLIENT__APIKEY"),
  // SignalR gateway
  signalrGatewayAddress: optionalString("SIGNALR_GRPC_ADDRESS"),
  // Outbound API keys (required — fail-closed, no empty-string fallback)
  callbackApiKey: requiredString("FILES_CALLBACK_API_KEY"),
  signalrApiKey: requiredString("FILES_SIGNALR_API_KEY"),
  // Job options
  jobOptions: optionalSection("FILES_APP", DEFAULT_FILES_JOB_OPTIONS),
});

// RabbitMQ (required — files service needs consumers for the processing pipeline)
const messageBus = new MessageBus({
  url: config.rabbitMqUrl,
  connectionName: "files-service",
  logger,
});
await messageBus.waitForConnection();
logger.info("RabbitMQ connected");

const publisher = messageBus.createPublisher();

const { app, shutdown } = await createFilesApp(config, publisher, messageBus);

const server = serve({ fetch: app.fetch, port: config.httpPort }, (info) => {
  logger.info(`Files service listening on http://localhost:${info.port}`);
});

for (const signal of ["SIGINT", "SIGTERM"] as const) {
  process.on(signal, async () => {
    logger.info(`Received ${signal}, shutting down...`);
    server.close();
    await publisher.close();
    await messageBus.close();
    await shutdown();
    process.exit(0);
  });
}
