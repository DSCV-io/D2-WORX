import { describe, it, expect, beforeAll, afterAll, beforeEach, vi } from "vitest";
import { betterAuth } from "better-auth";
import { drizzleAdapter } from "better-auth/adapters/drizzle";
import { bearer } from "better-auth/plugins/bearer";
import { organization } from "better-auth/plugins/organization";
import { username } from "better-auth/plugins/username";
import { generateUuidV7 } from "@d2/utilities";
import { D2Result } from "@d2/result";
import type { Complex } from "@d2/geo-client";
import { generateId, ensureUsername } from "@d2/auth-infra";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import * as CacheMemory from "@d2/cache-memory";
import {
  createSignInEventRepoHandlers,
  createEmulationConsentRepoHandlers,
  CheckOrgExists,
  user as userTable,
  session as sessionTable,
  account as accountTable,
  verification as verificationTable,
  jwks as jwksTable,
  organization as organizationTable,
  member as memberTable,
  invitation as invitationTable,
} from "@d2/auth-infra";
import { createSignInEventHandlers, createEmulationConsentHandlers } from "@d2/auth-app";
import {
  startPostgres,
  stopPostgres,
  getPool,
  getDb,
  cleanAllTables,
} from "./postgres-test-helpers.js";

function createTestContext() {
  const request: IRequestContext = {
    traceId: "trace-app-handler-integration",
    isAuthenticated: true,
    isTrustedService: false,
    isOrgEmulating: false,
    isUserImpersonating: false,
    isAgentStaff: false,
    isAgentAdmin: false,
    isTargetingStaff: false,
    isTargetingAdmin: false,
  };
  return new HandlerContext(request, createLogger({ level: "silent" as never }));
}

/**
 * Tests app-layer CQRS handlers wired to real repo handlers against real
 * PostgreSQL. Validates cross-layer data flow (domain factory -> handler ->
 * repo -> DB -> repo -> handler -> output).
 */

// ---------------------------------------------------------------------------
// Sign-In Event Handlers
// ---------------------------------------------------------------------------
describe("Sign-in event handlers (integration)", () => {
  let handlers: ReturnType<typeof createSignInEventHandlers>;
  let cacheStore: CacheMemory.MemoryCacheStore;

  type CachedEvents = {
    events: import("@d2/auth-app").AuthQueries.EnrichedSignInEvent[];
    total: number;
    latestDate?: string;
  };

  beforeAll(async () => {
    await startPostgres();
    const ctx = createTestContext();
    const repo = createSignInEventRepoHandlers(getDb(), ctx);

    cacheStore = new CacheMemory.MemoryCacheStore();
    // No-op WhoIs resolver: integration tests for the cache/pagination behavior
    // don't need real Geo lookups. WhoIs hydration is itself fail-open.
    const noopFindWhoIs: Complex.IFindWhoIsHandler = {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { whoIs: undefined } })),
      redaction: { inputFields: ["ipAddress"], suppressOutput: true },
    } as unknown as Complex.IFindWhoIsHandler;
    handlers = createSignInEventHandlers(repo, noopFindWhoIs, ctx, {
      get: new CacheMemory.Get<CachedEvents>(cacheStore, ctx),
      set: new CacheMemory.Set<CachedEvents>(cacheStore, ctx),
    });
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
    cacheStore.clear();
  });

  const userId = generateUuidV7();

  it("should record and retrieve a sign-in event", async () => {
    const recordResult = await handlers.record.handleAsync({
      userId,
      successful: true,
      ipAddress: "10.0.0.1",
      userAgent: "TestBrowser/1.0",
    });
    expect(recordResult.success).toBe(true);

    const getResult = await handlers.getByUser.handleAsync({ userId });
    expect(getResult.success).toBe(true);
    expect(getResult.data!.events).toHaveLength(1);
    expect(getResult.data!.events[0].event.userId).toBe(userId);
    expect(getResult.data!.events[0].event.ipAddress).toBe("10.0.0.1");
    expect(getResult.data!.events[0].event.userAgent).toBe("TestBrowser/1.0");
    expect(getResult.data!.events[0].event.successful).toBe(true);
    expect(getResult.data!.total).toBe(1);
  });

  it("should return events in reverse chronological order", async () => {
    // Record 3 events with slight delays to ensure ordering
    for (let i = 0; i < 3; i++) {
      await handlers.record.handleAsync({
        userId,
        successful: i % 2 === 0,
        ipAddress: `10.0.0.${i + 1}`,
        userAgent: `Agent-${i}`,
      });
    }

    const result = await handlers.getByUser.handleAsync({ userId });
    expect(result.success).toBe(true);

    const events = result.data!.events;
    expect(events).toHaveLength(3);

    // Newest first
    for (let i = 0; i < events.length - 1; i++) {
      expect(events[i].event.createdAt.getTime()).toBeGreaterThanOrEqual(
        events[i + 1].event.createdAt.getTime(),
      );
    }
  });

  it("should return correct total count", async () => {
    for (let i = 0; i < 5; i++) {
      await handlers.record.handleAsync({
        userId,
        successful: true,
        ipAddress: "10.0.0.1",
        userAgent: "Agent",
      });
    }

    const result = await handlers.getByUser.handleAsync({ userId, limit: 2 });
    expect(result.success).toBe(true);
    expect(result.data!.events).toHaveLength(2); // limited by limit
    expect(result.data!.total).toBe(5); // full count
  });

  it("should paginate with limit and offset", async () => {
    for (let i = 0; i < 5; i++) {
      await handlers.record.handleAsync({
        userId,
        successful: true,
        ipAddress: "10.0.0.1",
        userAgent: `Agent-${i}`,
      });
    }

    const page1 = await handlers.getByUser.handleAsync({ userId, limit: 2, offset: 0 });
    const page2 = await handlers.getByUser.handleAsync({ userId, limit: 2, offset: 2 });

    expect(page1.data!.events).toHaveLength(2);
    expect(page2.data!.events).toHaveLength(2);
    expect(page1.data!.events[0].event.id).not.toBe(page2.data!.events[0].event.id);
  });

  it("should serve second call from memory cache (same data)", async () => {
    await handlers.record.handleAsync({
      userId,
      successful: true,
      ipAddress: "10.0.0.1",
      userAgent: "Agent",
    });

    const first = await handlers.getByUser.handleAsync({ userId });
    expect(first.success).toBe(true);

    // Second call should be served from cache — same data
    const second = await handlers.getByUser.handleAsync({ userId });
    expect(second.success).toBe(true);
    expect(second.data!.events).toHaveLength(1);
    expect(second.data!.events[0].event.id).toBe(first.data!.events[0].event.id);
  });

  it("should invalidate cache when new event is recorded", async () => {
    await handlers.record.handleAsync({
      userId,
      successful: true,
      ipAddress: "10.0.0.1",
      userAgent: "Agent",
    });

    // First query populates cache
    const first = await handlers.getByUser.handleAsync({ userId });
    expect(first.data!.events).toHaveLength(1);

    // Record a new event — this changes latestEventDate
    await handlers.record.handleAsync({
      userId,
      successful: false,
      ipAddress: "10.0.0.2",
      userAgent: "Agent-2",
    });

    // Next query should see the new event (cache invalidated by staleness check)
    const second = await handlers.getByUser.handleAsync({ userId });
    expect(second.data!.events).toHaveLength(2);
    expect(second.data!.total).toBe(2);
  });
});

// ---------------------------------------------------------------------------
// Emulation Consent Handlers
// ---------------------------------------------------------------------------
describe("Emulation consent handlers (integration)", () => {
  let handlers: ReturnType<typeof createEmulationConsentHandlers>;
  let auth: ReturnType<typeof betterAuth>;

  beforeAll(async () => {
    await startPostgres();
    const ctx = createTestContext();
    const repo = createEmulationConsentRepoHandlers(getDb(), ctx);

    // Create a minimal BetterAuth instance for creating orgs
    const schema = {
      user: userTable,
      session: sessionTable,
      account: accountTable,
      verification: verificationTable,
      jwks: jwksTable,
      organization: organizationTable,
      member: memberTable,
      invitation: invitationTable,
    };
    auth = betterAuth({
      baseURL: "http://localhost:3333",
      basePath: "/api/auth",
      emailAndPassword: { enabled: true, autoSignIn: true },
      database: drizzleAdapter(getDb(), { provider: "pg", schema }),
      advanced: { database: { generateId } },
      databaseHooks: {
        user: {
          create: {
            before: async (user) => {
              let data = ensureUsername(user as Record<string, unknown>);
              if (user.id) {
                data = { ...data, id: user.id };
              }
              return { data };
            },
          },
        },
      },
      plugins: [
        bearer(),
        username(),
        organization({
          creatorRole: "owner",
          allowUserToCreateOrganization: true,
        }),
      ],
    });

    const checkOrgExists = new CheckOrgExists(getDb(), ctx);

    handlers = createEmulationConsentHandlers(repo, ctx, checkOrgExists);
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
  });

  /** Sign up a user and create an org. Returns userId, orgId, and token. */
  async function setupUserWithOrg() {
    const res = await auth.api.signUpEmail({
      body: {
        email: `user-${Date.now()}@example.com`,
        password: "SecurePass123!",
        name: "Test User",
      },
    });
    const token = res.token ?? (res as Record<string, unknown>).session?.token;

    const org = await auth.api.createOrganization({
      body: { name: "Test Org", slug: `org-${Date.now()}` },
      headers: new Headers({ Authorization: `Bearer ${token}` }),
    });

    return { userId: res.user.id, orgId: org.id, token: token as string };
  }

  it("should create an emulation consent when org exists", async () => {
    const { userId, orgId } = await setupUserWithOrg();

    const result = await handlers.create.handleAsync({
      userId,
      grantedToOrgId: orgId,
      activeOrgType: "support",
      expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000),
    });

    expect(result.success).toBe(true);
    expect(result.data!.consent.userId).toBe(userId);
    expect(result.data!.consent.grantedToOrgId).toBe(orgId);
  });

  it("should reject when org does not exist", async () => {
    const userId = generateUuidV7();
    const fakeOrgId = generateUuidV7();

    const result = await handlers.create.handleAsync({
      userId,
      grantedToOrgId: fakeOrgId,
      activeOrgType: "support",
      expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000),
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(404);
  });

  it("should create → revoke → getActive returns empty", async () => {
    const { userId, orgId } = await setupUserWithOrg();

    // Create
    const createResult = await handlers.create.handleAsync({
      userId,
      grantedToOrgId: orgId,
      activeOrgType: "admin",
      expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000),
    });
    expect(createResult.success).toBe(true);

    // Revoke
    const revokeResult = await handlers.revoke.handleAsync({
      consentId: createResult.data!.consent.id,
      userId,
    });
    expect(revokeResult.success).toBe(true);

    // GetActive — should be empty
    const activeResult = await handlers.getActive.handleAsync({ userId });
    expect(activeResult.success).toBe(true);
    expect(activeResult.data!.consents).toHaveLength(0);
  });
});
