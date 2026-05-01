/**
 * Server-side auth context module.
 *
 * Lazy singleton initialization for SessionResolver, JwtManager, and AuthProxy.
 *
 * - Production: throws FATAL if SVELTEKIT_AUTH__URL is missing.
 * - Mock mode: returns a mock context with stub session resolver.
 *
 * The `.server.ts` suffix ensures this module is never included in
 * client-side bundles (SvelteKit convention).
 */
import { createLogger, type ILogger } from "@d2/logging";
import { SessionResolver, JwtManager, AuthProxy, type AuthBffConfig } from "@d2/auth-bff-client";
import { createMockAuthContext } from "./middleware.mock.server.js";

export interface AuthContext {
  config: AuthBffConfig;
  sessionResolver: SessionResolver;
  jwtManager: JwtManager;
  authProxy: AuthProxy;
  logger: ILogger;
}

let cached: AuthContext | null | undefined;

/**
 * Whether infrastructure deps should be mocked.
 * Same condition as middleware.server.ts — CI or explicit flag.
 * When set, ALWAYS returns mock context regardless of env vars present.
 */
const MOCK_INFRA = process.env.D2_MOCK_INFRA === "true" || process.env.CI === "true";

/**
 * Returns the shared auth context (lazy singleton).
 *
 * - Production: throws FATAL if SVELTEKIT_AUTH__URL is not set.
 * - Mock mode: returns a mock context with stub session resolver.
 */
export function getAuthContext(): AuthContext | null {
  if (cached !== undefined) return cached;

  // Mock mode: always use stub auth, regardless of env vars present.
  // This ensures `D2_MOCK_INFRA=true` overrides even when .env.local is loaded.
  if (MOCK_INFRA) {
    console.warn(
      "[d2-sveltekit] Auth context mocked. " +
        "Session resolver returns unauthenticated, auth proxy returns 503.",
    );
    cached = createMockAuthContext();
    return cached;
  }

  const authServiceUrl = process.env.SVELTEKIT_AUTH__URL;

  if (!authServiceUrl) {
    throw new Error(
      "[d2-sveltekit] FATAL: Missing required env var SVELTEKIT_AUTH__URL. " +
        "The Auth service URL must be configured. Check your .env.local file.",
    );
  }

  const apiKey = process.env.SVELTEKIT_AUTH__API_KEY;

  const logger = createLogger({ serviceName: process.env.OTEL_SERVICE_NAME ?? "d2-sveltekit" });
  const config: AuthBffConfig = { authServiceUrl, apiKey };

  const sessionResolver = new SessionResolver(config, logger);
  const jwtManager = new JwtManager(config, logger);
  const authProxy = new AuthProxy(config, logger);

  cached = { config, sessionResolver, jwtManager, authProxy, logger };
  logger.info("Auth context initialized.");
  return cached;
}
