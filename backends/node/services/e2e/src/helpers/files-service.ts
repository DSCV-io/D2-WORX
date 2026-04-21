import pg from "pg";
import { serve } from "@hono/node-server";
import { MessageBus, type IMessagePublisher } from "@d2/messaging";
import { createFilesApp, type FilesServiceConfig } from "@d2/files-api";
import { FILES_MESSAGING } from "@d2/files-domain";

let messageBus: MessageBus | undefined;
let publisher: IMessagePublisher | undefined;
let intakePublisher: IMessagePublisher | undefined;
let shutdownFn: (() => Promise<void>) | undefined;
let filesPool: pg.Pool | undefined;
let httpServerClose: (() => void) | undefined;

export interface FilesServiceHandle {
  /** Base URL of the Files HTTP server (e.g., "http://127.0.0.1:54321"). */
  baseUrl: string;
  /** Hono app for direct request testing (bypasses HTTP server). */
  app: import("hono").Hono;
  /** Files database pool for assertions. */
  pool: pg.Pool;
  /** Publisher for simulating MinIO intake messages (bypasses MinIO notifications). */
  intakePublisher: IMessagePublisher;
}

/**
 * Sets context key configuration via environment variables.
 *
 * Must be called BEFORE `createFilesApp()` because the composition root
 * reads `FILES_CK__*` env vars at startup via `parseContextKeyConfigs()`.
 */
function setContextKeyEnvVars(authGrpcAddress: string): void {
  const env = process.env;

  // Context key 0: user_avatar
  env["FILES_CK__0__KEY"] = "user_avatar";
  env["FILES_CK__0__UPLOAD_RESOLUTION"] = "jwt_owner";
  env["FILES_CK__0__READ_RESOLUTION"] = "jwt_owner";
  env["FILES_CK__0__CALLBACK_ADDR"] = authGrpcAddress;
  env["FILES_CK__0__CATEGORY__0"] = "image";
  env["FILES_CK__0__MAX_SIZE_BYTES"] = String(5 * 1024 * 1024); // 5 MB
  env["FILES_CK__0__VARIANT__0__NAME"] = "thumb";
  env["FILES_CK__0__VARIANT__0__MAX_DIM"] = "64";
  env["FILES_CK__0__VARIANT__1__NAME"] = "original";
}

/**
 * Cleans up context key environment variables.
 */
function clearContextKeyEnvVars(): void {
  const env = process.env;
  const keys = Object.keys(env).filter((k) => k.startsWith("FILES_CK__"));
  for (const key of keys) {
    delete env[key];
  }
}

/**
 * Starts the Files service in-process using `createFilesApp()` from `@d2/files-api`.
 * Mirrors the composition root with test-specific overrides (no fingerprint check,
 * no Geo/rate-limiting, context keys set programmatically).
 */
export async function startFilesService(opts: {
  databaseUrl: string;
  redisUrl: string;
  rabbitMqUrl: string;
  s3Endpoint: string;
  s3AccessKey: string;
  s3SecretKey: string;
  s3BucketName: string;
  clamdHost: string;
  clamdPort: number;
  jwksUrl: string;
  signalrGatewayAddress: string;
  callbackApiKey: string;
  authGrpcAddress: string;
}): Promise<FilesServiceHandle> {
  // Set context key configs via env vars (composition root reads these)
  setContextKeyEnvVars(opts.authGrpcAddress);

  // Create RabbitMQ publisher for files messaging (intake → processing pipeline)
  messageBus = new MessageBus({
    url: opts.rabbitMqUrl,
    connectionName: "e2e-files",
  });
  await messageBus.waitForConnection();

  // Publisher used by the Files service internally (PublishFileForProcessing handler).
  // Declares the exchange to ensure it exists before consumers are ready.
  // `durable: true` MUST match the consumer's declaration in
  // file-{uploaded,processing}-consumer.ts. RabbitMQ rejects a redeclare
  // with PRECONDITION_FAILED if `durable` differs from the existing exchange,
  // so the publisher and consumer must agree.
  publisher = messageBus.createPublisher({
    exchanges: [
      {
        exchange: FILES_MESSAGING.EVENTS_EXCHANGE,
        type: FILES_MESSAGING.EVENTS_EXCHANGE_TYPE,
        durable: true,
      },
    ],
  });

  // Separate publisher for tests to simulate MinIO intake messages
  intakePublisher = messageBus.createPublisher({
    exchanges: [
      {
        exchange: FILES_MESSAGING.EVENTS_EXCHANGE,
        type: FILES_MESSAGING.EVENTS_EXCHANGE_TYPE,
        durable: true,
      },
    ],
  });

  // Database pool for test assertions
  filesPool = new pg.Pool({ connectionString: opts.databaseUrl });

  const config: FilesServiceConfig = {
    databaseUrl: opts.databaseUrl,
    redisUrl: opts.redisUrl,
    rabbitMqUrl: opts.rabbitMqUrl,
    httpPort: 0, // Not used — we test via Hono app directly
    corsOrigins: ["http://localhost:5173"],
    filesApiKeys: ["e2e-files-key"],
    // S3/MinIO
    s3Endpoint: opts.s3Endpoint,
    s3AccessKey: opts.s3AccessKey,
    s3SecretKey: opts.s3SecretKey,
    s3BucketName: opts.s3BucketName,
    s3Region: "us-east-1",
    // ClamAV
    clamdHost: opts.clamdHost,
    clamdPort: opts.clamdPort,
    // JWT auth
    jwksUrl: opts.jwksUrl,
    jwtIssuer: "e2e-test-issuer",
    jwtAudience: "e2e-test-audience",
    fingerprintCheck: false, // Avoids UA/Accept header coordination in tests
    // SignalR (stub)
    signalrGatewayAddress: opts.signalrGatewayAddress,
    // Outbound API keys
    callbackApiKey: opts.callbackApiKey,
    signalrApiKey: "e2e-signalr-key", // Stub doesn't validate keys
  };

  const { app, shutdown } = await createFilesApp(config, publisher, messageBus);
  shutdownFn = shutdown;

  // Start real HTTP server (JWT middleware needs real HTTP for JWKS fetch)
  const baseUrl = await new Promise<string>((resolve) => {
    const server = serve({ fetch: app.fetch, port: 0 }, (info) => {
      resolve(`http://127.0.0.1:${info.port}`);
    });
    httpServerClose = () => server.close();
  });

  // Allow RabbitMQ consumers to fully subscribe (exchange/queue binding is async)
  await new Promise((r) => setTimeout(r, 1_500));

  return { baseUrl, app, pool: filesPool, intakePublisher };
}

/** Race a promise against a timeout (resolves even if inner hangs). */
function withTimeout(promise: Promise<void>, ms: number, label: string): Promise<void> {
  return Promise.race([
    promise,
    new Promise<void>((resolve) =>
      setTimeout(() => {
        console.warn(`[E2E] ${label} timed out after ${ms}ms — forcing continue`);
        resolve();
      }, ms),
    ),
  ]);
}

/**
 * Stops the Files service and closes connections.
 */
export async function stopFilesService(): Promise<void> {
  if (httpServerClose) httpServerClose();
  if (shutdownFn) await withTimeout(shutdownFn(), 5_000, "files shutdown");
  if (messageBus) await withTimeout(messageBus.close(), 5_000, "files messageBus.close");
  if (filesPool) await withTimeout(filesPool.end(), 3_000, "files pool.end");
  clearContextKeyEnvVars();
  shutdownFn = undefined;
  httpServerClose = undefined;
  messageBus = undefined;
  publisher = undefined;
  intakePublisher = undefined;
  filesPool = undefined;
}
