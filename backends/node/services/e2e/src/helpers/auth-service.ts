import { MessageBus, type IMessagePublisher } from "@d2/messaging";
import { createApp } from "@d2/auth-api";
import { COMMS_EVENTS } from "@d2/comms-client";
import { createPasswordFunctions, type AuthServiceConfig, type PrefixCache } from "@d2/auth-infra";

/**
 * Test-fixture API key. Anywhere E2E code calls a non-`/api/auth/*` route
 * (account routes, invitations, etc.) it MUST send this as `x-api-key`,
 * otherwise the auth API's fail-closed S2S middleware rejects with 401.
 *
 * The same key is plumbed into the SvelteKit dev server via
 * `SVELTEKIT_AUTH__API_KEY` so its auth proxy can identify as a trusted
 * service when forwarding browser requests.
 */
export const E2E_AUTH_API_KEY = "e2e-test-auth-key";

let messageBus: MessageBus | undefined;
let publisher: IMessagePublisher | undefined;
let shutdownFn: (() => Promise<void>) | undefined;

export interface AuthServiceHandle {
  /** BetterAuth API for direct testing (no HTTP needed). */
  auth: Awaited<ReturnType<typeof createApp>>["auth"];
  /** Hono app for HTTP route testing (invitation routes, etc.). */
  app: Awaited<ReturnType<typeof createApp>>["app"];
  /** gRPC address (e.g., "localhost:54321") — only set when grpcPort is provided. */
  grpcAddress?: string;
}

/**
 * No-breach HIBP cache: returns an empty response for all prefix lookups.
 * Domain validation (length, complexity, common blocklist) still runs —
 * only the external HIBP API call is skipped.
 */
const noBreachCache = {
  get: () => "",
  set: () => {},
} as PrefixCache;

/**
 * Starts the auth service in-process using `createApp()` from `@d2/auth-api`.
 * Connects to RabbitMQ for publishing auth events (verification, password reset, invitation).
 */
export async function startAuthService(opts: {
  databaseUrl: string;
  redisUrl: string;
  rabbitMqUrl: string;
  geoAddress: string;
  geoApiKey: string;
  corsOrigins?: string[];
  /** Enable Auth gRPC server on this port (for FileCallback, etc.). */
  grpcPort?: number;
  /**
   * API keys for S2S authentication. Defaults to a single test key so the
   * HTTP server's fail-closed posture (commit 95ca94c7) doesn't block E2E
   * setup. Override only when a test specifically asserts on key contents.
   */
  authApiKeys?: string[];
}): Promise<AuthServiceHandle> {
  // Create RabbitMQ publisher for auth events
  messageBus = new MessageBus({
    url: opts.rabbitMqUrl,
    connectionName: "e2e-auth",
  });
  await messageBus.waitForConnection();

  publisher = await messageBus.createPublisher({
    exchanges: [{ exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, type: "fanout" }],
  });

  const config: AuthServiceConfig & {
    authApiKeys?: string[];
    grpcPort?: number;
  } = {
    databaseUrl: opts.databaseUrl,
    redisUrl: opts.redisUrl,
    baseUrl: "http://localhost:3333",
    corsOrigins: opts.corsOrigins ?? ["http://localhost:5173"],
    jwtIssuer: "e2e-test-issuer",
    jwtAudience: "e2e-test-audience",
    jwtExpirationSeconds: 900,
    passwordMinLength: 12,
    passwordMaxLength: 128,
    geoAddress: opts.geoAddress,
    geoApiKey: opts.geoApiKey,
    grpcPort: opts.grpcPort,
    authApiKeys: opts.authApiKeys ?? [E2E_AUTH_API_KEY],
  };

  // Skip HIBP API in E2E tests — domain validation still runs
  const passwordFunctions = createPasswordFunctions(noBreachCache);

  const { app, auth, shutdown } = await createApp(config, publisher, { passwordFunctions });
  shutdownFn = shutdown;

  const grpcAddress = opts.grpcPort ? `localhost:${opts.grpcPort}` : undefined;
  return { auth, app, grpcAddress };
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
 * Stops the auth service and closes RabbitMQ connections.
 */
export async function stopAuthService(): Promise<void> {
  if (shutdownFn) await withTimeout(shutdownFn(), 5_000, "auth shutdown");
  if (messageBus) await withTimeout(messageBus.close(), 5_000, "auth messageBus.close");
  shutdownFn = undefined;
  messageBus = undefined;
  publisher = undefined;
}
