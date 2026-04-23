import { describe, it, expect } from "vitest";
import { AsyncLocalStorage } from "node:async_hooks";
import { buildHonoApp, type HonoAppOptions, AUTH_CUSTOM_REQUEST_HEADERS } from "@d2/auth-api";
import { isInfrastructurePath } from "@d2/request-enrichment";
import { checkFingerprint } from "@d2/jwt-auth";

/**
 * §5 Security conformance tests for the auth-api HTTP surface.
 *
 * Each test pins one rule from CLAUDE.md §5 — adding a new route, middleware,
 * or middleware bypass must keep these green. When the parallel-agent
 * compliance review surfaces a new finding, the corresponding rule lands here
 * as a failing test FIRST, the fix lands second.
 *
 * The intent is to make "the auth HTTP server is fail-closed" a CI-enforced
 * claim, not an audit-by-hand snapshot. New rules added to §5 should grow
 * this file (or sibling per-service files) rather than getting tracked as
 * deferred work in PROFILE_PROGRESS.md.
 *
 * Tests here use minimal stubs — they exercise `buildHonoApp` directly
 * rather than the full composition root, so they don't need Postgres/Redis.
 * For end-to-end behavior (signing in, hitting protected routes, etc.) see
 * the integration tests next door.
 */

/**
 * Minimal stub satisfying `HonoAppOptions` — every dependency is a no-op
 * that's never actually invoked because each test's request is rejected
 * before reaching the routes. The point of these tests is the BOUNDARY
 * (startup + outermost middleware), not handler behavior.
 */
function makeMinimalOptions(overrides: Partial<HonoAppOptions["config"]> = {}): HonoAppOptions {
  return {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    auth: {} as any,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    provider: {} as any,
    config: {
      corsOrigins: ["http://localhost:5173"],
      baseUrl: "http://localhost:3333",
      ...overrides,
    },
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    translator: {} as any,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    findWhoIs: {} as any,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    rateLimitCheck: {} as any,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    throttleCheck: {} as any,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    throttleRecord: {} as any,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    checkEmailHandler: {} as any,
    fingerprintStorage: new AsyncLocalStorage<string>(),
    deviceFingerprintStorage: new AsyncLocalStorage<string>(),
    clientFingerprintStorage: new AsyncLocalStorage<string>(),
    serverFingerprintStorage: new AsyncLocalStorage<string>(),
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    sessionFingerprintMiddleware: ((async (_c: unknown, next: () => Promise<void>) => next()) as any),
    logger: {
      debug: () => {},
      info: () => {},
      warn: () => {},
      error: () => {},
      fatal: () => {},
    },
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    db: {} as any,
  };
}

describe("§5 Security — auth-api HTTP conformance", () => {
  describe('"Auth middleware must fail-closed on missing config"', () => {
    it("refuses to build the app when authApiKeys is undefined", () => {
      // The protected non-/api/auth/* routes (account, emulation, org-contacts,
      // invitations) require S2S trust. Skipping the middleware on empty config
      // would leave them open — exactly what the rule forbids.
      expect(() => buildHonoApp(makeMinimalOptions())).toThrow(/AUTH_API_KEYS not configured/);
    });

    it("refuses to build the app when authApiKeys is an empty array", () => {
      expect(() => buildHonoApp(makeMinimalOptions({ authApiKeys: [] }))).toThrow(
        /AUTH_API_KEYS not configured/,
      );
    });

    it("builds successfully when at least one key is configured", () => {
      // Sanity check — the throw is gated on emptiness, not always-on.
      expect(() =>
        buildHonoApp(makeMinimalOptions({ authApiKeys: ["test-key-1"] })),
      ).not.toThrow();
    });
  });

  describe('"CORS allowHeaders must include every custom header any middleware reads"', () => {
    it("includes X-Client-Fingerprint in the exported allowHeaders set", () => {
      // request-enrichment middleware reads `X-Client-Fingerprint` (cross-
      // origin path when the d2-cfp cookie isn't available). Missing the
      // header from CORS = silent preflight rejection.
      expect(AUTH_CUSTOM_REQUEST_HEADERS).toContain("X-Client-Fingerprint");
    });
  });

  describe('"Infrastructure paths must be exempt from ALL business middleware"', () => {
    // The conformance check is whether the shared `isInfrastructurePath()`
    // helper recognizes the standard probe paths. Each business middleware
    // (request-enrichment, rate-limit) consults this helper at its top.
    // Adding a new infrastructure-style path to one service without
    // updating the shared list would silently break parity across stacks.
    it.each(["/health", "/health/db", "/ready", "/alive", "/metrics", "/api/health"])(
      "recognizes %s as infrastructure",
      (path) => {
        expect(isInfrastructurePath(path)).toBe(true);
      },
    );

    it.each(["/api/auth/sign-in", "/api/account/sessions", "/api/v1/files"])(
      "treats %s as a business path (not exempt)",
      (path) => {
        expect(isInfrastructurePath(path)).toBe(false);
      },
    );
  });

  describe('"Auth middleware must fail-closed on missing config (JWT fp claim)"', () => {
    it("rejects a JWT with no `fp` claim by default (no soft fall-through)", async () => {
      // The old behavior was a soft-pass for "backward compat" which let
      // any future issuer/dev token bypass fingerprint binding entirely.
      // New default is fail-closed; opt-in via `allowMissingClaim: true`.
      const result = await checkFingerprint(
        { /* no fp */ },
        "Mozilla/5.0",
        "application/json",
      );
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(401);
    });
  });
});
