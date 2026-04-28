import { serve } from "@hono/node-server";
import { MessageBus, type IMessagePublisher } from "@d2/messaging";
import { createLogger } from "@d2/logging";
import { DEFAULT_AUTH_JOB_OPTIONS } from "@d2/auth-app";
import {
  defineConfig,
  requiredParsed,
  optionalString,
  defaultString,
  defaultInt,
  optionalInt,
  envArray,
  optionalSection,
  parsePostgresUrl,
  parseRedisUrl,
} from "@d2/service-defaults/config";
import { createApp } from "./composition-root.js";

const logger = createLogger({ serviceName: "auth-service" });

// Docker Compose injects connection strings in .NET formats (ADO.NET for PG, StackExchange for Redis).
// Parsers convert to URI format for Node.js clients, passing through URIs unchanged.
const config = defineConfig("auth-service", {
  databaseUrl: requiredParsed(parsePostgresUrl, "AUTH_DATABASE_URL"),
  redisUrl: requiredParsed(parseRedisUrl, "REDIS_URL"),
  rabbitMqUrl: optionalString("RABBITMQ_URL"),
  baseUrl: defaultString("http://localhost:5173", "AUTH_BASE_URL"),
  emailBaseUrl: optionalString("AUTH_EMAIL_BASE_URL"),
  corsOrigins: envArray("AUTH_CORS_ORIGINS"),
  jwtIssuer: defaultString("d2-worx", "AUTH_JWT_ISSUER"),
  jwtAudience: defaultString("d2-services", "AUTH_JWT_AUDIENCE"),
  geoAddress: optionalString("GEO_GRPC_ADDRESS"),
  geoApiKey: optionalString("AUTH_GEO_CLIENT__APIKEY"),
  authApiKeys: envArray("AUTH_API_KEYS"),
  grpcPort: optionalInt("AUTH_GRPC_PORT"),
  port: defaultInt(5100, "AUTH_HTTP_PORT", "PORT"),
  jobOptions: optionalSection("AUTH_APP", DEFAULT_AUTH_JOB_OPTIONS),
});

// Optional: RabbitMQ publisher for auth events (verification emails, password resets).
// When not configured, auth-app notification handlers log events but do not publish.
let messageBus: MessageBus | undefined;
let publisher: IMessagePublisher | undefined;
if (config.rabbitMqUrl) {
  messageBus = new MessageBus({ url: config.rabbitMqUrl, connectionName: "auth-service", logger });
  await messageBus.waitForConnection();
  publisher = messageBus.createPublisher();
  logger.info("RabbitMQ connected");
}

const { app, shutdown } = await createApp(config, publisher, undefined, messageBus);

const server = serve({ fetch: app.fetch, port: config.port }, (info) => {
  logger.info(`Auth service listening on http://localhost:${info.port}`);
});

for (const signal of ["SIGINT", "SIGTERM"] as const) {
  process.on(signal, async () => {
    logger.info(`Received ${signal}, shutting down...`);
    server.close();
    if (publisher) await publisher.close();
    if (messageBus) await messageBus.close();
    await shutdown();
    process.exit(0);
  });
}
