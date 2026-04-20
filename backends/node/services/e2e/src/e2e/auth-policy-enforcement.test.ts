import { describe, it, expect, beforeAll, afterAll } from "vitest";
import {
  startContainers,
  stopContainers,
  getAuthPgUrl,
  getAuthPool,
  getGeoPgUrl,
  getRedisUrl,
  getRabbitUrl,
} from "../helpers/containers.js";
import { startGeoService, stopGeoService } from "../helpers/geo-dotnet-service.js";
import {
  startAuthService,
  stopAuthService,
  type AuthServiceHandle,
} from "../helpers/auth-service.js";
import { startAuthHttpServer, type AuthHttpServer } from "../helpers/auth-http-server.js";

const GEO_API_KEY = "e2e-test-key";
const TEST_PASSWORD = "SecurePass123!@#";

/**
 * E2E proof that the new `@d2/auth-policy` route gates actually fire.
 *
 * Hits a protected route (`PATCH /api/account/locale`) twice:
 *   1. **Without** a session cookie → expects 401 (the gate runs and rejects).
 *   2. **With** a valid session cookie → expects 2xx (the gate passes and the
 *      handler runs).
 *
 * If `requireAuth()` were silently broken — say, missing from the route or
 * misreading the request context — the unauthenticated request would either
 * 200 (catastrophic) or 500 (handler runs and crashes on missing userId).
 * Both failure modes are caught here.
 */
describe("E2E: auth policy enforcement", () => {
  let geoAddress: string;
  let authHandle: AuthServiceHandle;
  let httpServer: AuthHttpServer;

  beforeAll(async () => {
    await startContainers();
    geoAddress = await startGeoService({
      pgUrl: getGeoPgUrl(),
      redisUrl: getRedisUrl(),
      rabbitUrl: getRabbitUrl(),
      apiKey: GEO_API_KEY,
    });
    authHandle = await startAuthService({
      databaseUrl: getAuthPgUrl(),
      redisUrl: getRedisUrl(),
      rabbitMqUrl: getRabbitUrl(),
      geoAddress,
      geoApiKey: GEO_API_KEY,
    });
    httpServer = await startAuthHttpServer(authHandle.app);
  }, 180_000);

  afterAll(async () => {
    await httpServer?.close();
    await stopAuthService(authHandle);
    await stopGeoService();
    await stopContainers();
  });

  /**
   * Sign up + sign in via HTTP, returning the signed cookie header value.
   * Mirrors the helper in `bff-client.test.ts` — bypasses email verification
   * (sign-in throttle/email-verified gate would otherwise 403).
   */
  async function signUpAndGetCookie(email: string, name: string): Promise<string> {
    const signUpRes = await authHandle.auth.api.signUpEmail({
      body: { email, password: TEST_PASSWORD, name },
    });
    expect(signUpRes.user).toBeDefined();

    // Manually mark email verified (skips the verification-email flow).
    await getAuthPool().query('UPDATE "user" SET email_verified = true WHERE id = $1', [
      signUpRes.user.id,
    ]);

    const signInRes = await fetch(`${httpServer.baseUrl}/api/auth/sign-in/email`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password: TEST_PASSWORD }),
    });
    if (!signInRes.ok) throw new Error(`Sign-in failed: ${signInRes.status}`);

    const setCookieHeader = signInRes.headers.get("set-cookie");
    if (!setCookieHeader) throw new Error("No set-cookie on sign-in response");

    // Set-cookie may contain multiple cookies (better-auth issues both
    // session_token and session_data). Parse "name=value" pairs and join them
    // for use as a Cookie request header.
    const cookiePairs = setCookieHeader
      .split(/,(?=\s*better-auth\.)/)
      .map((c) => c.split(";")[0].trim());
    return cookiePairs.join("; ");
  }

  it("rejects an unauthenticated PATCH /api/account/locale with 401", async () => {
    const res = await fetch(`${httpServer.baseUrl}/api/account/locale`, {
      method: "PATCH",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ locale: "en-US" }),
    });

    expect(res.status).toBe(401);
  });

  it("accepts the same request with a valid session cookie", async () => {
    const cookie = await signUpAndGetCookie("policy-allowed@example.com", "Test User");

    const res = await fetch(`${httpServer.baseUrl}/api/account/locale`, {
      method: "PATCH",
      headers: {
        "content-type": "application/json",
        cookie,
      },
      body: JSON.stringify({ locale: "en-US" }),
    });

    // 200 = handler ran and locale update succeeded.
    // The point is that the gate did NOT 401 — the handler was reached.
    expect(res.status).toBeGreaterThanOrEqual(200);
    expect(res.status).toBeLessThan(400);
  });
});
