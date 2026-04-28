// NOTE: OpenTelemetry is bootstrapped via the `--import @d2/service-defaults/register`
// Node.js loader flag in package.json scripts (dev + start). The register module:
//   1. Calls `createAddHookMessageChannel()` + `register()` to install ESM loader hooks
//      BEFORE any application code loads, enabling auto-instrumentation of Pino, pg,
//      @grpc/grpc-js, etc.
//   2. Calls `setupTelemetry({ serviceName })` using OTEL_SERVICE_NAME from env vars
//      (set by Docker Compose env vars).
//
// This means OTel is fully active before this file even executes. There is no need
// for an explicit `setupTelemetry()` call here. The `--import` approach is required
// for ESM environments (vs. the older `--require` for CJS) so that instrumentation
// patches are applied before module imports are resolved.

import { createLogger } from "@d2/logging";
import { DEFAULT_COMMS_JOB_OPTIONS } from "@d2/comms-app";
import {
  defineConfig,
  requiredParsed,
  requiredString,
  optionalString,
  optionalParsed,
  defaultInt,
  envArray,
  optionalSection,
  parsePostgresUrl,
  parseRedisUrl,
} from "@d2/service-defaults/config";
import { createCommsService } from "./composition-root.js";

const logger = createLogger({ serviceName: "comms-service" });

// Docker Compose injects connection strings in .NET formats (ADO.NET for PG).
// Parser converts to URI format for Node.js clients, passing through URIs unchanged.
const config = defineConfig("comms-service", {
  databaseUrl: requiredParsed(parsePostgresUrl, "COMMS_DATABASE_URL"),
  rabbitMqUrl: requiredString("RABBITMQ_URL"),
  redisUrl: optionalParsed(parseRedisUrl, "REDIS_URL"),
  grpcPort: defaultInt(5200, "COMMS_GRPC_PORT"),
  resendApiKey: optionalString("RESEND_API_KEY"),
  resendFromAddress: optionalString("RESEND_FROM_ADDRESS"),
  twilioAccountSid: optionalString("TWILIO_ACCOUNT_SID"),
  twilioAuthToken: optionalString("TWILIO_AUTH_TOKEN"),
  twilioPhoneNumber: optionalString("TWILIO_PHONE_NUMBER"),
  smsProvider: optionalString("COMMS_SMS_PROVIDER"),
  smsMockLogPath: optionalString("COMMS_SMS_MOCK_LOG_PATH"),
  geoAddress: optionalString("GEO_GRPC_ADDRESS"),
  geoApiKey: optionalString("COMMS_GEO_CLIENT__APIKEY"),
  commsApiKeys: envArray("COMMS_API_KEYS"),
  jobOptions: optionalSection("COMMS_APP", DEFAULT_COMMS_JOB_OPTIONS),
  emailFooterText: optionalString("COMMS_APP__EMAILFOOTERTEXT"),
  emailTemplatePath: optionalString("COMMS_APP__EMAILTEMPLATEPATH"),
});

const { server, shutdown } = await createCommsService(config);

for (const signal of ["SIGINT", "SIGTERM"] as const) {
  process.on(signal, async () => {
    logger.info(`Received ${signal}, shutting down...`);
    await new Promise<void>((resolve) => {
      const timeout = setTimeout(() => {
        server.forceShutdown();
        resolve();
      }, 5_000);
      server.tryShutdown(() => {
        clearTimeout(timeout);
        resolve();
      });
    });
    await shutdown();
    process.exit(0);
  });
}
