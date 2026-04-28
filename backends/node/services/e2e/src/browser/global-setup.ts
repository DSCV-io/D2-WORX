/**
 * Playwright global setup for Tier 2 browser E2E tests.
 *
 * Starts all infrastructure (Testcontainers) + services (Geo, Auth, Comms)
 * + SvelteKit dev server. Exports service URLs via env vars so Playwright
 * config and tests can access them.
 *
 * Lifecycle: runs ONCE before all browser test files.
 */
import {
  startContainers,
  getGeoPgUrl,
  getAuthPgUrl,
  getCommsPgUrl,
  getRedisUrl,
  getRabbitUrl,
} from "../helpers/containers.js";
import { startGeoService } from "../helpers/geo-dotnet-service.js";
import {
  startAuthService,
  E2E_AUTH_API_KEY,
  type AuthServiceHandle,
} from "../helpers/auth-service.js";
import { startAuthHttpServer, type AuthHttpServer } from "../helpers/auth-http-server.js";
import { startCommsService, type CommsServiceHandle } from "../helpers/comms-service.js";
import { startSvelteKitServer, getAvailablePort } from "../helpers/sveltekit-server.js";

const GEO_API_KEY = "e2e-test-key";

// Store handles for teardown (module-level state persists across setup/teardown)
let authHandle: AuthServiceHandle | undefined;
let authHttp: AuthHttpServer | undefined;
let commsHandle: CommsServiceHandle | undefined;

export default async function globalSetup() {
  console.log("[Browser E2E] Starting infrastructure...");

  // 1. Start containers (PG + RabbitMQ + Redis)
  await startContainers();
  console.log("[Browser E2E] Containers ready.");

  // 2. Pre-allocate the SvelteKit port so we can add it to auth's trusted origins.
  //    Auth starts before SvelteKit (SvelteKit needs the auth URL), but BetterAuth's
  //    CSRF/CORS checks need to trust the SvelteKit origin for browser requests.
  const svelteKitPort = await getAvailablePort();
  const svelteKitOrigin = `http://localhost:${svelteKitPort}`;

  // 3. Start .NET Geo service
  const geoAddress = await startGeoService({
    pgUrl: getGeoPgUrl(),
    redisUrl: getRedisUrl(),
    rabbitUrl: getRabbitUrl(),
    apiKey: GEO_API_KEY,
  });
  console.log(`[Browser E2E] Geo service ready at ${geoAddress}`);

  // 4. Start auth service (in-process) + wrap in HTTP server
  authHandle = await startAuthService({
    databaseUrl: getAuthPgUrl(),
    redisUrl: getRedisUrl(),
    rabbitMqUrl: getRabbitUrl(),
    geoAddress,
    geoApiKey: GEO_API_KEY,
    corsOrigins: [svelteKitOrigin],
  });
  authHttp = await startAuthHttpServer(authHandle.app);
  console.log(`[Browser E2E] Auth service ready at ${authHttp.baseUrl}`);

  // 5. Start comms service (in-process with stub email)
  commsHandle = await startCommsService({
    databaseUrl: getCommsPgUrl(),
    rabbitMqUrl: getRabbitUrl(),
    geoAddress,
    geoApiKey: GEO_API_KEY,
  });
  console.log("[Browser E2E] Comms service ready.");

  // 6. Start SvelteKit dev server on the pre-allocated port
  const svelteKitUrl = await startSvelteKitServer({
    authUrl: authHttp.baseUrl,
    authApiKey: E2E_AUTH_API_KEY,
    redisUrl: getRedisUrl(),
    geoAddress,
    geoApiKey: GEO_API_KEY,
    port: svelteKitPort,
  });
  console.log(`[Browser E2E] SvelteKit ready at ${svelteKitUrl}`);

  // Export URLs so Playwright config and tests can use them
  process.env.SVELTEKIT_BASE_URL = svelteKitUrl;
  process.env.AUTH_BASE_URL = authHttp.baseUrl;
  // Export DB URL so browser tests can verify users' emails directly
  process.env.AUTH_DB_URL = getAuthPgUrl();
}

export { authHandle, authHttp, commsHandle };
