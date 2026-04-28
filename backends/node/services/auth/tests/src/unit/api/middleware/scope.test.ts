import { describe, it, expect, vi } from "vitest";
import { Hono } from "hono";
import { createScopeMiddleware, SCOPE_KEY } from "@d2/auth-api";
import { IRequestContextKey } from "@d2/handler";
import type { IRequestContext } from "@d2/handler";
import { ILoggerKey } from "@d2/logging";
import { SESSION_FIELDS } from "@d2/auth-domain";
import { IFindActiveConsentByUserIdAndOrgKey } from "@d2/auth-app";
import { D2Result } from "@d2/result";

/**
 * Creates a mock consent handler that returns the specified result.
 * Defaults to returning a valid (active) consent.
 */
function createMockConsentHandler(hasActiveConsent = true) {
  return {
    handleAsync: vi.fn(async () =>
      hasActiveConsent
        ? D2Result.ok({ data: { consent: { id: "consent-1" } } })
        : D2Result.ok({ data: { consent: undefined } }),
    ),
  };
}

/**
 * Creates a mock ServiceProvider that returns controllable scopes.
 * Tracks setInstance calls and supports resolve for ILoggerKey.
 *
 * @param hasActiveConsent - Whether the mock consent handler returns a valid consent (default: true).
 */
function createMockProvider(hasActiveConsent = true) {
  const disposeFn = vi.fn();
  const instances = new Map<unknown, unknown>();
  const consentHandler = createMockConsentHandler(hasActiveConsent);

  const scope = {
    setInstance: vi.fn((key: unknown, value: unknown) => {
      instances.set(key, value);
    }),
    resolve: vi.fn((key: unknown) => {
      if (instances.has(key)) return instances.get(key);
      if (key === IFindActiveConsentByUserIdAndOrgKey) return consentHandler;
      throw new Error(`Key not registered in scope: ${String(key)}`);
    }),
    dispose: disposeFn,
    getInstance: (key: unknown) => instances.get(key),
  };

  const logger: Record<string, unknown> = {
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
    debug: vi.fn(),
    child: vi.fn(() => ({
      info: vi.fn(),
      warn: vi.fn(),
      error: vi.fn(),
      debug: vi.fn(),
      child: vi.fn(),
    })),
  };

  const provider = {
    createScope: vi.fn(() => scope),
    resolve: vi.fn((key: unknown) => {
      if (key === ILoggerKey) return logger;
      throw new Error(`Key not registered in provider: ${String(key)}`);
    }),
  };

  return { provider, scope, disposeFn, logger, consentHandler };
}

type User = { id: string; email: string; name: string } | null;
type Session = Record<string, unknown> | null;

/**
 * Creates a Hono app with scope middleware and a test route that captures IRequestContext.
 */
function createTestApp(
  mockProvider: ReturnType<typeof createMockProvider>,
  user: User,
  session: Session,
) {
  const app = new Hono();

  // Simulate session middleware (sets user/session vars)
  app.use("*", async (c, next) => {
    if (user) c.set("user" as never, user as never);
    if (session) c.set("session" as never, session as never);
    await next();
  });

  app.use("*", createScopeMiddleware(mockProvider.provider as any));

  // Test route that captures the IRequestContext from the scope
  app.get("/test", (c) => {
    const scope = c.get(SCOPE_KEY as never) as any;
    const ctx = scope.getInstance(IRequestContextKey) as IRequestContext;
    return c.json(ctx);
  });

  return app;
}

function get(app: Hono) {
  return app.request("/test", { method: "GET" });
}

describe("Scope Middleware", () => {
  describe("buildRequestContext (via middleware)", () => {
    it("should set isAuthenticated false for unauthenticated request", async () => {
      const mock = createMockProvider();
      const app = createTestApp(mock, null, null);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      expect(ctx.isAuthenticated).toBe(false);
      expect(ctx.userId).toBeUndefined();
      expect(ctx.email).toBeUndefined();
    });

    it("should set isAuthenticated true with userId and email for authenticated request", async () => {
      const mock = createMockProvider();
      const user = { id: "user-1", email: "user@example.com", name: "Test" };
      const session = { [SESSION_FIELDS.ACTIVE_ORG_TYPE]: "customer" };
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      expect(ctx.isAuthenticated).toBe(true);
      expect(ctx.userId).toBe("user-1");
      expect(ctx.email).toBe("user@example.com");
    });

    it("should map admin org type to isAgentStaff true and isAgentAdmin true", async () => {
      const mock = createMockProvider();
      const user = { id: "u1", email: "admin@test.com", name: "Admin" };
      const session = {
        [SESSION_FIELDS.ACTIVE_ORG_TYPE]: "admin",
        [SESSION_FIELDS.ACTIVE_ORG_ID]: "org-admin",
      };
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      expect(ctx.isAgentStaff).toBe(true);
      expect(ctx.isAgentAdmin).toBe(true);
      expect(ctx.agentOrgType).toBe("admin");
    });

    it("should map support org type to isAgentStaff true and isAgentAdmin false", async () => {
      const mock = createMockProvider();
      const user = { id: "u1", email: "support@test.com", name: "Support" };
      const session = {
        [SESSION_FIELDS.ACTIVE_ORG_TYPE]: "support",
        [SESSION_FIELDS.ACTIVE_ORG_ID]: "org-support",
      };
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      expect(ctx.isAgentStaff).toBe(true);
      expect(ctx.isAgentAdmin).toBe(false);
      expect(ctx.agentOrgType).toBe("support");
    });

    it("should map customer org type to isAgentStaff false and isAgentAdmin false", async () => {
      const mock = createMockProvider();
      const user = { id: "u1", email: "cust@test.com", name: "Cust" };
      const session = {
        [SESSION_FIELDS.ACTIVE_ORG_TYPE]: "customer",
        [SESSION_FIELDS.ACTIVE_ORG_ID]: "org-cust",
      };
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      expect(ctx.isAgentStaff).toBe(false);
      expect(ctx.isAgentAdmin).toBe(false);
      expect(ctx.agentOrgType).toBe("customer");
    });

    it("should map third_party org type correctly (Bug 1 fix)", async () => {
      const mock = createMockProvider();
      const user = { id: "u1", email: "tp@test.com", name: "TP" };
      const session = {
        [SESSION_FIELDS.ACTIVE_ORG_TYPE]: "third_party",
        [SESSION_FIELDS.ACTIVE_ORG_ID]: "org-tp",
      };
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      expect(ctx.isAgentStaff).toBe(false);
      expect(ctx.isAgentAdmin).toBe(false);
      expect(ctx.agentOrgType).toBe("third_party");
    });

    it("should map affiliate org type correctly", async () => {
      const mock = createMockProvider();
      const user = { id: "u1", email: "aff@test.com", name: "Aff" };
      const session = {
        [SESSION_FIELDS.ACTIVE_ORG_TYPE]: "affiliate",
        [SESSION_FIELDS.ACTIVE_ORG_ID]: "org-aff",
      };
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      expect(ctx.isAgentStaff).toBe(false);
      expect(ctx.agentOrgType).toBe("affiliate");
    });

    it("should set emulation fields when emulating", async () => {
      const mock = createMockProvider();
      const user = { id: "u1", email: "admin@test.com", name: "Admin" };
      const session = {
        [SESSION_FIELDS.ACTIVE_ORG_TYPE]: "admin",
        [SESSION_FIELDS.ACTIVE_ORG_ID]: "org-admin",
        [SESSION_FIELDS.EMULATED_ORG_ID]: "org-customer",
        [SESSION_FIELDS.EMULATED_ORG_TYPE]: "customer",
      };
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      expect(ctx.isOrgEmulating).toBe(true);
      expect(ctx.targetOrgId).toBe("org-customer");
      expect(ctx.agentOrgId).toBe("org-admin");
      expect(ctx.targetOrgType).toBe("customer");
      expect(ctx.agentOrgType).toBe("admin");
    });

    it("should strip emulation when consent is expired or revoked", async () => {
      const mock = createMockProvider(false);
      const user = { id: "u1", email: "admin@test.com", name: "Admin" };
      const session = {
        [SESSION_FIELDS.ACTIVE_ORG_TYPE]: "admin",
        [SESSION_FIELDS.ACTIVE_ORG_ID]: "org-admin",
        [SESSION_FIELDS.EMULATED_ORG_ID]: "org-customer",
        [SESSION_FIELDS.EMULATED_ORG_TYPE]: "customer",
      };
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      // Emulation should be stripped — falls back to agent's own org
      expect(ctx.isOrgEmulating).toBe(false);
      expect(ctx.targetOrgId).toBe("org-admin");
      expect(ctx.targetOrgType).toBe("admin");
      expect(ctx.agentOrgId).toBe("org-admin");
      expect(ctx.agentOrgType).toBe("admin");
      expect(ctx.isTargetingStaff).toBe(true);
      expect(ctx.isTargetingAdmin).toBe(true);
    });

    it("should preserve emulation when consent is active", async () => {
      const mock = createMockProvider(true);
      const user = { id: "u1", email: "admin@test.com", name: "Admin" };
      const session = {
        [SESSION_FIELDS.ACTIVE_ORG_TYPE]: "admin",
        [SESSION_FIELDS.ACTIVE_ORG_ID]: "org-admin",
        [SESSION_FIELDS.EMULATED_ORG_ID]: "org-customer",
        [SESSION_FIELDS.EMULATED_ORG_TYPE]: "customer",
      };
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      // Emulation should be preserved — consent is active
      expect(ctx.isOrgEmulating).toBe(true);
      expect(ctx.targetOrgId).toBe("org-customer");
      expect(ctx.targetOrgType).toBe("customer");
      expect(ctx.agentOrgId).toBe("org-admin");
      expect(mock.consentHandler.handleAsync).toHaveBeenCalledOnce();
    });

    it("should set targetOrgId equal to agentOrgId when not emulating", async () => {
      const mock = createMockProvider();
      const user = { id: "u1", email: "cust@test.com", name: "Cust" };
      const session = {
        [SESSION_FIELDS.ACTIVE_ORG_TYPE]: "customer",
        [SESSION_FIELDS.ACTIVE_ORG_ID]: "org-cust",
      };
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      expect(ctx.isOrgEmulating).toBe(false);
      expect(ctx.targetOrgId).toBe("org-cust");
      expect(ctx.agentOrgId).toBe("org-cust");
    });

    it("should set isTargetingStaff false when admin emulates customer", async () => {
      const mock = createMockProvider();
      const user = { id: "u1", email: "admin@test.com", name: "Admin" };
      const session = {
        [SESSION_FIELDS.ACTIVE_ORG_TYPE]: "admin",
        [SESSION_FIELDS.ACTIVE_ORG_ID]: "org-admin",
        [SESSION_FIELDS.EMULATED_ORG_ID]: "org-customer",
        [SESSION_FIELDS.EMULATED_ORG_TYPE]: "customer",
      };
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      expect(ctx.isAgentStaff).toBe(true);
      expect(ctx.isTargetingStaff).toBe(false);
    });

    it("should generate a unique traceId per request", async () => {
      const mock = createMockProvider();
      const app = createTestApp(mock, null, null);

      const res1 = await get(app);
      const ctx1 = (await res1.json()) as IRequestContext;
      const res2 = await get(app);
      const ctx2 = (await res2.json()) as IRequestContext;

      expect(ctx1.traceId).toBeDefined();
      expect(ctx2.traceId).toBeDefined();
      expect(ctx1.traceId).not.toBe(ctx2.traceId);
    });
  });

  describe("buildRequestContext — defensive edge cases", () => {
    it("should return undefined agentOrgType for unknown org type string", async () => {
      const mock = createMockProvider();
      const user = { id: "u1", email: "hack@test.com", name: "Hack" };
      const session = {
        [SESSION_FIELDS.ACTIVE_ORG_TYPE]: "hacker",
        [SESSION_FIELDS.ACTIVE_ORG_ID]: "org-hack",
      };
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      // Unknown org type maps to undefined — downstream middleware must guard
      expect(ctx.agentOrgType).toBeUndefined();
      expect(ctx.isAgentStaff).toBe(false);
      expect(ctx.isAgentAdmin).toBe(false);
    });

    it("should return undefined targetOrgType when emulated org type is unknown", async () => {
      const mock = createMockProvider();
      const user = { id: "u1", email: "admin@test.com", name: "Admin" };
      const session = {
        [SESSION_FIELDS.ACTIVE_ORG_TYPE]: "admin",
        [SESSION_FIELDS.ACTIVE_ORG_ID]: "org-admin",
        [SESSION_FIELDS.EMULATED_ORG_ID]: "org-evil",
        [SESSION_FIELDS.EMULATED_ORG_TYPE]: "fake_type",
      };
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      expect(ctx.isOrgEmulating).toBe(true);
      expect(ctx.agentOrgType).toBe("admin");
      // Emulated type doesn't map — undefined propagates
      expect(ctx.targetOrgType).toBeUndefined();
      expect(ctx.isTargetingStaff).toBe(false);
      expect(ctx.isTargetingAdmin).toBe(false);
    });

    it("should NOT set isAgentStaff true for PascalCase 'Admin' org type", async () => {
      const mock = createMockProvider();
      const user = { id: "u1", email: "a@test.com", name: "A" };
      const session = {
        [SESSION_FIELDS.ACTIVE_ORG_TYPE]: "Admin",
        [SESSION_FIELDS.ACTIVE_ORG_ID]: "org-1",
      };
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      // Wire format is lowercase ("admin"); PascalCase is treated as unknown
      // and falls through to undefined orgType (fail-closed).
      expect(ctx.isAgentStaff).toBe(false);
      expect(ctx.isAgentAdmin).toBe(false);
      expect(ctx.agentOrgType).toBeUndefined();
    });

    it("should set isOrgEmulating false when emulatedOrgId is empty string", async () => {
      const mock = createMockProvider();
      const user = { id: "u1", email: "a@test.com", name: "A" };
      const session = {
        [SESSION_FIELDS.ACTIVE_ORG_TYPE]: "customer",
        [SESSION_FIELDS.ACTIVE_ORG_ID]: "org-1",
        [SESSION_FIELDS.EMULATED_ORG_ID]: "",
        [SESSION_FIELDS.EMULATED_ORG_TYPE]: "admin",
      };
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      // Empty string is falsy — should NOT be treated as emulating
      expect(ctx.isOrgEmulating).toBe(false);
      expect(ctx.targetOrgId).toBe("org-1");
    });

    it("should handle session with all undefined org fields (no active org)", async () => {
      const mock = createMockProvider();
      const user = { id: "u1", email: "new@test.com", name: "New User" };
      const session = {}; // Freshly created user with no org
      const app = createTestApp(mock, user, session);

      const res = await get(app);
      const ctx = (await res.json()) as IRequestContext;

      expect(ctx.isAuthenticated).toBe(true);
      expect(ctx.agentOrgId).toBeUndefined();
      expect(ctx.agentOrgType).toBeUndefined();
      expect(ctx.targetOrgId).toBeUndefined();
      expect(ctx.isAgentStaff).toBe(false);
      expect(ctx.isAgentAdmin).toBe(false);
      expect(ctx.isOrgEmulating).toBe(false);
    });

    it("should dispose scope even when route handler throws", async () => {
      const mock = createMockProvider();
      const app = new Hono();
      app.use("*", createScopeMiddleware(mock.provider as any));
      app.get("/test", () => {
        throw new Error("Route handler exploded");
      });

      // The error handler may catch this; the important thing is dispose is called
      try {
        await app.request("/test", { method: "GET" });
      } catch {
        // Expected
      }

      expect(mock.disposeFn).toHaveBeenCalledOnce();
    });
  });

  describe("createScopeMiddleware lifecycle", () => {
    it("should dispose scope after request completes", async () => {
      const mock = createMockProvider();
      const app = createTestApp(mock, null, null);

      await get(app);

      expect(mock.disposeFn).toHaveBeenCalledOnce();
    });
  });
});
