import { describe, it, expect, beforeAll, afterAll, beforeEach, vi } from "vitest";
import { eq, inArray } from "drizzle-orm";
import { generateUuidV7 } from "@d2/utilities";
import { D2Result } from "@d2/result";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { USER_STATUS, USER_DELETION } from "@d2/auth-domain";
import type { DistributedCache } from "@d2/interfaces";
import type { INotifyHandler } from "@d2/comms-client";
import { CleanupDeletedUsers, FinalizeDeletedUser } from "@d2/auth-app";
import {
  AnonymizeUser,
  GetDeletedUsersToPurge,
  user,
  account,
  session,
  signInEvent,
  organization,
  member,
} from "@d2/auth-infra";
import { startPostgres, stopPostgres, getDb, cleanAllTables } from "./postgres-test-helpers.js";

function ctx() {
  const request: IRequestContext = {
    traceId: "trace-cleanup-flow",
    isAuthenticated: false,
    isTrustedService: true,
    isOrgEmulating: false,
    isUserImpersonating: false,
    isAgentStaff: false,
    isAgentAdmin: false,
    isTargetingStaff: false,
    isTargetingAdmin: false,
  };
  return new HandlerContext(request, createLogger({ level: "silent" as never }));
}

function daysAgo(days: number): Date {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d;
}

/**
 * In-memory lock — sufficient for single-process tests. Real prod uses Redis
 * via @d2/cache-redis; we only need the contract honored here.
 */
function makeInMemoryLockHandlers() {
  const heldKeys = new Map<string, string>(); // key -> lockId
  const acquireLock: DistributedCache.IAcquireLockHandler = {
    handleAsync: vi.fn(async (input) => {
      if (heldKeys.has(input.key)) {
        return D2Result.ok({ data: { acquired: false } });
      }
      heldKeys.set(input.key, input.lockId);
      return D2Result.ok({ data: { acquired: true } });
    }),
  };
  const releaseLock: DistributedCache.IReleaseLockHandler = {
    handleAsync: vi.fn(async (input) => {
      const held = heldKeys.get(input.key);
      if (held === input.lockId) {
        heldKeys.delete(input.key);
        return D2Result.ok({ data: { released: true } });
      }
      return D2Result.ok({ data: { released: false } });
    }),
  };
  return { acquireLock, releaseLock, heldKeys };
}

function makeMockNotify() {
  const calls: Array<Parameters<INotifyHandler["handleAsync"]>[0]> = [];
  const notify: INotifyHandler = {
    handleAsync: vi.fn(async (input) => {
      calls.push(input);
      return D2Result.ok();
    }),
  };
  return { notify, calls };
}

const noopTranslator = {
  t: (_locale: string, key: string, _vars?: Record<string, unknown>) => key,
} as unknown as import("@d2/i18n").Translator;

async function seedEligibleUser(opts: { id?: string; deletedAtDays?: number } = {}) {
  const id = opts.id ?? generateUuidV7();
  const email = `eligible-${id}@example.com`;
  await getDb()
    .insert(user)
    .values({
      id,
      name: `Eligible ${id.slice(-6)}`,
      email,
      emailVerified: true,
      username: `el-${id}`,
      displayUsername: `el-${id}`,
      createdAt: daysAgo(60),
      updatedAt: daysAgo(60),
      status: USER_STATUS.PENDING_DELETION,
      deletedAt: daysAgo(opts.deletedAtDays ?? 31),
    });
  // Two sessions
  for (let i = 0; i < 2; i++) {
    await getDb()
      .insert(session)
      .values({
        id: generateUuidV7(),
        userId: id,
        token: `tok-${i}-${generateUuidV7()}`,
        expiresAt: new Date(Date.now() + 86_400_000),
        createdAt: new Date(),
        updatedAt: new Date(),
      });
  }
  // One credential account row
  await getDb()
    .insert(account)
    .values({
      id: generateUuidV7(),
      accountId: email,
      providerId: "credential",
      userId: id,
      createdAt: daysAgo(60),
      updatedAt: daysAgo(60),
    });
  // Five sign-in events with non-null PII
  for (let i = 0; i < 5; i++) {
    await getDb()
      .insert(signInEvent)
      .values({
        id: generateUuidV7(),
        userId: id,
        successful: true,
        ipAddress: `203.0.113.${10 + i}`,
        userAgent: `Mozilla/5.0 attempt-${i}`,
        deviceFingerprint: `fp-${id}-${i}`,
        createdAt: daysAgo(20 - i),
      });
  }
  return { id, originalEmail: email };
}

async function seedNonEligibleUser(status: string, deletedAtDays?: number) {
  const id = generateUuidV7();
  await getDb()
    .insert(user)
    .values({
      id,
      name: `Non-eligible ${id.slice(-6)}`,
      email: `non-${id}@example.com`,
      emailVerified: true,
      username: `n-${id}`,
      displayUsername: `n-${id}`,
      createdAt: daysAgo(60),
      updatedAt: daysAgo(60),
      status,
      deletedAt: deletedAtDays !== undefined ? daysAgo(deletedAtDays) : undefined,
    });
  return id;
}

describe("CleanupDeletedUsers — full job flow (integration)", () => {
  let cleanup: CleanupDeletedUsers;
  let mockNotify: ReturnType<typeof makeMockNotify>;
  let mockLocks: ReturnType<typeof makeInMemoryLockHandlers>;

  beforeAll(async () => {
    await startPostgres();

    mockLocks = makeInMemoryLockHandlers();
    mockNotify = makeMockNotify();

    const anonymize = new AnonymizeUser(getDb(), ctx());
    const getPurgeList = new GetDeletedUsersToPurge(getDb(), ctx());

    // FinalizeDeletedUser without a publisher — fanout publish is best-effort
    // and out of scope for this DB-flow assertion suite (the per-user notify
    // call is the user-visible artifact).
    const finalize = new FinalizeDeletedUser(
      anonymize,
      mockNotify.notify,
      noopTranslator,
      ctx(),
      undefined,
    );

    cleanup = new CleanupDeletedUsers(
      mockLocks.acquireLock,
      mockLocks.releaseLock,
      getPurgeList,
      finalize,
      {
        jobLockTtlMs: 5 * 60 * 1000,
        signInEventRetentionDays: 90,
        invitationRetentionDays: 7,
        userDeletionGracePeriodMs: USER_DELETION.GRACE_PERIOD_MS,
        userPurgeBatchSize: 50_000,
      },
      ctx(),
    );
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
    mockNotify.calls.length = 0;
    mockLocks.heldKeys.clear();
    (mockLocks.acquireLock.handleAsync as ReturnType<typeof vi.fn>).mockClear();
    (mockLocks.releaseLock.handleAsync as ReturnType<typeof vi.fn>).mockClear();
    (mockNotify.notify.handleAsync as ReturnType<typeof vi.fn>).mockClear();
  });

  it("anonymizes only eligible users in a single run + leaves others untouched + idempotent on re-run", async () => {
    // Seed mix: 5 eligible, 3 not-yet-eligible (within grace), 4 active.
    const eligibleSeeds = await Promise.all([
      seedEligibleUser(),
      seedEligibleUser(),
      seedEligibleUser({ deletedAtDays: 45 }),
      seedEligibleUser({ deletedAtDays: 60 }),
      seedEligibleUser({ deletedAtDays: 31 }),
    ]);
    const eligibleIds = new Set(eligibleSeeds.map((u) => u.id));
    const nonEligibleIds = await Promise.all([
      seedNonEligibleUser(USER_STATUS.PENDING_DELETION, 5),
      seedNonEligibleUser(USER_STATUS.PENDING_DELETION, 14),
      seedNonEligibleUser(USER_STATUS.PENDING_DELETION, 29),
    ]);
    const activeIds = await Promise.all([
      seedNonEligibleUser(USER_STATUS.ACTIVE),
      seedNonEligibleUser(USER_STATUS.ACTIVE),
      seedNonEligibleUser(USER_STATUS.ACTIVE),
      seedNonEligibleUser(USER_STATUS.ACTIVE),
    ]);

    // Run the orchestrator.
    const result = await cleanup.handleAsync({});

    expect(result.success).toBe(true);
    expect(result.data?.processed).toBe(5);
    expect(result.data?.anonymized).toBe(5);
    expect(result.data?.skipped).toBe(0);
    expect(result.data?.lockAcquired).toBe(true);
    expect(result.data?.rowsAffected).toBe(5);

    // Per eligible user: status DELETED, email scrubbed, account/session gone,
    // sign_in_event rows preserved with PII anonymized.
    for (const seed of eligibleSeeds) {
      const [row] = await getDb().select().from(user).where(eq(user.id, seed.id)).limit(1);
      expect(row?.status).toBe(USER_STATUS.DELETED);
      expect(row?.email).toBe(`deleted-${seed.id}@deleted.local`);
      expect(row?.name).toBe("Deleted user");

      const accounts = await getDb().select().from(account).where(eq(account.userId, seed.id));
      expect(accounts).toHaveLength(0);

      const sessions = await getDb().select().from(session).where(eq(session.userId, seed.id));
      expect(sessions).toHaveLength(0);

      const events = await getDb()
        .select()
        .from(signInEvent)
        .where(eq(signInEvent.userId, seed.id));
      expect(events).toHaveLength(5);
      for (const ev of events) {
        expect(ev.ipAddress).toBe("[anonymized]");
        expect(ev.userAgent).toBe("[anonymized]");
        expect(ev.deviceFingerprint).toBeNull();
        expect(ev.whoIsId).toBeNull();
      }
    }

    // Non-eligible pending_deletion users: untouched.
    if (nonEligibleIds.length > 0) {
      const rows = await getDb().select().from(user).where(inArray(user.id, nonEligibleIds));
      expect(rows).toHaveLength(nonEligibleIds.length);
      for (const r of rows) {
        expect(r.status).toBe(USER_STATUS.PENDING_DELETION);
        expect(r.email).not.toMatch(/^deleted-/);
      }
    }

    // Active users: untouched.
    if (activeIds.length > 0) {
      const rows = await getDb().select().from(user).where(inArray(user.id, activeIds));
      expect(rows).toHaveLength(activeIds.length);
      for (const r of rows) {
        expect(r.status).toBe(USER_STATUS.ACTIVE);
      }
    }

    // Mock notify: exactly 5 calls, all using alternativeContactInfo (the
    // contact may have been torn down by Geo, so we must route by email
    // directly — security-relevant, mirrors phoneRemoved).
    expect(mockNotify.calls).toHaveLength(5);
    const emailedAddresses = new Set<string>();
    for (const call of mockNotify.calls) {
      expect(call.alternativeContactInfo).toBeDefined();
      expect(call.alternativeContactInfo?.email).toBeTruthy();
      // Should NOT route via the contact pipeline.
      expect((call as { recipientContactId?: string }).recipientContactId).toBeUndefined();
      emailedAddresses.add(call.alternativeContactInfo!.email!);
    }
    expect(emailedAddresses).toEqual(new Set(eligibleSeeds.map((s) => s.originalEmail)));

    // Sanity: `eligibleIds` matches what was anonymized.
    const deletedRows = await getDb()
      .select({ id: user.id })
      .from(user)
      .where(eq(user.status, USER_STATUS.DELETED));
    expect(new Set(deletedRows.map((r) => r.id))).toEqual(eligibleIds);

    // Idempotency: a second run finds no eligible users (all deleted now).
    mockNotify.calls.length = 0;
    const second = await cleanup.handleAsync({});
    expect(second.success).toBe(true);
    expect(second.data?.processed).toBe(0);
    expect(second.data?.anonymized).toBe(0);
    expect(second.data?.skipped).toBe(0);
    expect(second.data?.lockAcquired).toBe(true);
    expect(mockNotify.calls).toHaveLength(0);
  }, 60_000);

  it("publishes user-anonymize fanout WITHOUT PII (only userId + anonymizedAt)", async () => {
    // Regression for D3: the fanout payload previously included `email` and
    // `name`. That PII durably persisted in every bound queue (and any DLX
    // capture) until consumed — defeating the point of the anonymization
    // event itself. Consumers that need PII for their own teardown should
    // look it up by userId from their own user-keyed tables.
    const seed = await seedEligibleUser();
    const publishedMessages: Array<{ exchange: string; routingKey: string; body: unknown }> = [];
    const mockPublisher = {
      send: vi.fn(
        async (
          target: { exchange: string; routingKey: string; headers?: Record<string, unknown> },
          body: unknown,
        ) => {
          publishedMessages.push({
            exchange: target.exchange,
            routingKey: target.routingKey,
            body,
          });
        },
      ),
    } as unknown as import("@d2/messaging").IMessagePublisher;

    const finalizeWithPublisher = new FinalizeDeletedUser(
      new AnonymizeUser(getDb(), ctx()),
      mockNotify.notify,
      noopTranslator,
      ctx(),
      mockPublisher,
    );

    const result = await finalizeWithPublisher.handleAsync({ userId: seed.id });
    expect(result.success).toBe(true);
    expect(result.data?.anonymized).toBe(true);

    // The send is fire-and-forget (best-effort) — give the queued promise a
    // microtask tick to settle.
    await new Promise((r) => setTimeout(r, 50));

    expect(publishedMessages).toHaveLength(1);
    const msg = publishedMessages[0]!;
    expect(msg.exchange).toBe("auth.user-anonymize");
    expect(msg.body).toEqual({
      userId: seed.id,
      anonymizedAt: expect.any(String),
    });
    // Critically: PII must NOT appear in the payload.
    const body = msg.body as Record<string, unknown>;
    expect(body).not.toHaveProperty("email");
    expect(body).not.toHaveProperty("name");
  });

  it("auto-cancels deletion + notifies user when they became sole owner during the grace window (E2 TOCTOU)", async () => {
    // Regression: RequestUserDeletion blocks the initial request when the
    // user is sole owner of any org, but the grace window is days long. A
    // co-owner can leave before anonymization runs. Without this fix the
    // anonymization would tombstone the user while the org still references
    // them, leaving an org with zero owners. The fix re-checks inside the
    // anonymization tx and atomically auto-cancels (flips back to ACTIVE)
    // if sole-owner-now, and FinalizeDeletedUser sends an explanatory email.
    const seed = await seedEligibleUser();

    // Seed an org owned solely by this user (simulating "co-owner left
    // during grace"). The seed uses a minimal org row + one member row.
    const orgId = generateUuidV7();
    await getDb().insert(organization).values({
      id: orgId,
      name: `org-${orgId}`,
      slug: `org-${orgId}`,
      orgType: "customer",
      createdAt: daysAgo(60),
    });
    await getDb().insert(member).values({
      id: generateUuidV7(),
      organizationId: orgId,
      userId: seed.id,
      role: "owner",
      createdAt: daysAgo(60),
    });

    mockNotify.calls.length = 0;
    const result = await cleanup.handleAsync({});
    expect(result.success).toBe(true);
    // Processed but not anonymized — auto-cancelled instead.
    expect(result.data?.processed).toBe(1);
    expect(result.data?.anonymized).toBe(0);
    expect(result.data?.skipped).toBe(1);

    // The user MUST be flipped back to ACTIVE atomically (no tombstone left
    // behind), with deletedAt cleared.
    const [row] = await getDb().select().from(user).where(eq(user.id, seed.id));
    expect(row?.status).toBe(USER_STATUS.ACTIVE);
    expect(row?.deletedAt).toBeNull();
    // Email + name preserved (NOT scrubbed) so the user can recover normally.
    expect(row?.email).toBe(seed.originalEmail);

    // Notify MUST fire with the auto-cancel email template (subject TK key)
    // and route via alternativeContactInfo (the contact pipeline isn't torn
    // down on auto-cancel, but the handler routes by email directly to keep
    // the path consistent with the deletion-complete email).
    expect(mockNotify.calls).toHaveLength(1);
    const call = mockNotify.calls[0]!;
    expect(call.alternativeContactInfo?.email).toBe(seed.originalEmail);
    expect(call.title).toBe("auth_email_user_deletion_auto_cancelled_sole_owner_subject");
  }, 60_000);

  it("returns lockAcquired:false and zero counts when the lock is held by another instance", async () => {
    // Force the lock into the held state.
    mockLocks.heldKeys.set("lock:job:cleanup-deleted-users", "another-worker");

    await seedEligibleUser();

    const result = await cleanup.handleAsync({});

    expect(result.success).toBe(true);
    expect(result.data?.lockAcquired).toBe(false);
    expect(result.data?.processed).toBe(0);
    expect(result.data?.anonymized).toBe(0);
    expect(mockNotify.calls).toHaveLength(0);

    // The user is still pending_deletion — next instance to acquire the lock
    // will pick them up.
    const [row] = await getDb()
      .select()
      .from(user)
      .where(eq(user.status, USER_STATUS.PENDING_DELETION))
      .limit(1);
    expect(row?.status).toBe(USER_STATUS.PENDING_DELETION);
  });
});
