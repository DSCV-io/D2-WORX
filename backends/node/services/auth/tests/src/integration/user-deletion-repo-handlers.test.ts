import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import { eq } from "drizzle-orm";
import { generateUuidV7 } from "@d2/utilities";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { USER_STATUS } from "@d2/auth-domain";
import {
  AnonymizeUser,
  CheckSoleOwnerOrgs,
  FindDeletedUsersToPurge,
  UpdateUserStatus,
  DeleteAllUserSessions,
  user,
  account,
  session,
  organization,
  member,
  signInEvent,
} from "@d2/auth-infra";
import { startPostgres, stopPostgres, getDb, cleanAllTables } from "./postgres-test-helpers.js";

function ctx() {
  const request: IRequestContext = {
    traceId: "trace-user-deletion-repo",
    isAuthenticated: false,
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

function daysAgo(days: number): Date {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d;
}

async function seedUser(opts: {
  id?: string;
  status?: string;
  deletedAt?: Date;
  email?: string;
  name?: string;
}) {
  const id = opts.id ?? generateUuidV7();
  await getDb()
    .insert(user)
    .values({
      id,
      name: opts.name ?? "Test User",
      email: opts.email ?? `${id}@test.local`,
      emailVerified: true,
      username: `u-${id}`,
      displayUsername: `u-${id}`,
      createdAt: daysAgo(60),
      updatedAt: daysAgo(60),
      status: opts.status ?? USER_STATUS.ACTIVE,
      deletedAt: opts.deletedAt,
    });
  return id;
}

describe("AnonymizeUser (integration)", () => {
  let handler: AnonymizeUser;

  beforeAll(async () => {
    await startPostgres();
    handler = new AnonymizeUser(getDb(), ctx());
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
  });

  it("should no-op when status is not pending_deletion (active user)", async () => {
    const userId = await seedUser({ status: USER_STATUS.ACTIVE });

    const result = await handler.handleAsync({ userId });

    expect(result.success).toBe(true);
    expect(result.data?.anonymized).toBe(false);
    expect(result.data?.originalEmail).toBeUndefined();

    // Confirm row was not mutated.
    const [row] = await getDb().select().from(user).where(eq(user.id, userId)).limit(1);
    expect(row?.status).toBe(USER_STATUS.ACTIVE);
    expect(row?.email).not.toMatch(/^deleted-/);
  });

  it("should anonymize a pending_deletion user and capture originalEmail/Name", async () => {
    const userId = await seedUser({
      status: USER_STATUS.PENDING_DELETION,
      deletedAt: daysAgo(31),
      email: "real-user@example.com",
      name: "Alice Cooper",
    });
    // Seed an account row + session + sign_in_event to validate cascade scrub.
    await getDb()
      .insert(account)
      .values({
        id: generateUuidV7(),
        accountId: "google-sub-12345",
        providerId: "google",
        userId,
        createdAt: daysAgo(60),
        updatedAt: daysAgo(60),
      });
    await getDb()
      .insert(session)
      .values({
        id: generateUuidV7(),
        userId,
        token: `tok-${generateUuidV7()}`,
        expiresAt: new Date(Date.now() + 86_400_000),
        createdAt: new Date(),
        updatedAt: new Date(),
      });
    await getDb()
      .insert(signInEvent)
      .values({
        id: generateUuidV7(),
        userId,
        successful: true,
        ipAddress: "203.0.113.42",
        userAgent: "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)",
        deviceFingerprint: "fp-abcdef",
        createdAt: daysAgo(5),
      });

    const result = await handler.handleAsync({ userId });

    expect(result.success).toBe(true);
    expect(result.data?.anonymized).toBe(true);
    expect(result.data?.originalEmail).toBe("real-user@example.com");
    expect(result.data?.originalName).toBe("Alice Cooper");

    // user row scrubbed
    const [row] = await getDb().select().from(user).where(eq(user.id, userId)).limit(1);
    expect(row?.status).toBe(USER_STATUS.DELETED);
    expect(row?.email).toBe(`deleted-${userId}@deleted.local`);
    expect(row?.name).toBe("Deleted user");
    expect(row?.username).toBe(`deleted_${userId}`);
    expect(row?.displayUsername).toBe(`deleted_${userId}`);
    expect(row?.image).toBeNull();
    expect(row?.phone).toBeNull();
    expect(row?.phoneVerified).toBe(false);

    // account rows deleted (frees google sub for re-registration)
    const accounts = await getDb().select().from(account).where(eq(account.userId, userId));
    expect(accounts).toHaveLength(0);

    // session rows deleted
    const sessions = await getDb().select().from(session).where(eq(session.userId, userId));
    expect(sessions).toHaveLength(0);

    // sign_in_event rows preserved but PII scrubbed
    const events = await getDb()
      .select()
      .from(signInEvent)
      .where(eq(signInEvent.userId, userId));
    expect(events).toHaveLength(1);
    expect(events[0]?.ipAddress).toBe("[anonymized]");
    expect(events[0]?.userAgent).toBe("[anonymized]");
    expect(events[0]?.deviceFingerprint).toBeNull();
    expect(events[0]?.whoIsId).toBeNull();
    expect(events[0]?.successful).toBe(true); // audit-relevant — preserved
  });

  it("re-anonymizing an already-deleted user is a no-op (guard miss)", async () => {
    const userId = await seedUser({
      status: USER_STATUS.PENDING_DELETION,
      deletedAt: daysAgo(31),
    });

    const first = await handler.handleAsync({ userId });
    expect(first.data?.anonymized).toBe(true);

    const second = await handler.handleAsync({ userId });
    expect(second.success).toBe(true);
    expect(second.data?.anonymized).toBe(false);
    expect(second.data?.originalEmail).toBeUndefined();
  });
});

describe("CheckSoleOwnerOrgs (integration)", () => {
  let handler: CheckSoleOwnerOrgs;

  beforeAll(async () => {
    await startPostgres();
    handler = new CheckSoleOwnerOrgs(getDb(), ctx());
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
  });

  async function seedOrg(opts?: { id?: string; name?: string }) {
    const id = opts?.id ?? generateUuidV7();
    await getDb()
      .insert(organization)
      .values({
        id,
        name: opts?.name ?? `org-${id}`,
        slug: `slug-${id}`,
        createdAt: new Date(),
      });
    return id;
  }

  async function addMember(userId: string, orgId: string, role: string) {
    await getDb()
      .insert(member)
      .values({
        id: generateUuidV7(),
        organizationId: orgId,
        userId,
        role,
        createdAt: new Date(),
      });
  }

  it("returns empty when user is not a member of any org", async () => {
    const userId = await seedUser({});

    const result = await handler.handleAsync({ userId });

    expect(result.success).toBe(true);
    expect(result.data?.soleOwnerOrgIds).toEqual([]);
  });

  it("returns empty when user is owner alongside co-owners (not sole)", async () => {
    const userId = await seedUser({});
    const coOwnerId = await seedUser({});
    const orgId = await seedOrg();
    await addMember(userId, orgId, "owner");
    await addMember(coOwnerId, orgId, "owner");

    const result = await handler.handleAsync({ userId });

    expect(result.success).toBe(true);
    expect(result.data?.soleOwnerOrgIds).toEqual([]);
  });

  it("returns the orgId when user is the sole owner of one org", async () => {
    const userId = await seedUser({});
    const orgId = await seedOrg();
    await addMember(userId, orgId, "owner");
    // An agent in the same org doesn't count as an owner.
    const otherUser = await seedUser({});
    await addMember(otherUser, orgId, "agent");

    const result = await handler.handleAsync({ userId });

    expect(result.success).toBe(true);
    expect(result.data?.soleOwnerOrgIds).toEqual([orgId]);
  });

  it("returns multiple orgIds when user is sole owner of several orgs", async () => {
    const userId = await seedUser({});
    const org1 = await seedOrg();
    const org2 = await seedOrg();
    const org3 = await seedOrg();
    await addMember(userId, org1, "owner");
    await addMember(userId, org2, "owner");
    await addMember(userId, org3, "owner");

    const result = await handler.handleAsync({ userId });

    expect(result.success).toBe(true);
    expect(new Set(result.data?.soleOwnerOrgIds)).toEqual(new Set([org1, org2, org3]));
  });
});

describe("FindDeletedUsersToPurge (integration)", () => {
  let handler: FindDeletedUsersToPurge;

  beforeAll(async () => {
    await startPostgres();
    handler = new FindDeletedUsersToPurge(getDb(), ctx());
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
  });

  it("returns empty when no users are eligible", async () => {
    await seedUser({ status: USER_STATUS.ACTIVE });
    await seedUser({ status: USER_STATUS.PENDING_DELETION, deletedAt: daysAgo(5) });

    const result = await handler.handleAsync({ graceCutoff: daysAgo(30) });

    expect(result.success).toBe(true);
    expect(result.data?.userIds).toEqual([]);
  });

  it("returns only pending_deletion users with deletedAt < graceCutoff", async () => {
    const eligible1 = await seedUser({
      status: USER_STATUS.PENDING_DELETION,
      deletedAt: daysAgo(31),
    });
    const eligible2 = await seedUser({
      status: USER_STATUS.PENDING_DELETION,
      deletedAt: daysAgo(45),
    });
    // Not eligible — within grace.
    await seedUser({ status: USER_STATUS.PENDING_DELETION, deletedAt: daysAgo(5) });
    // Not eligible — already deleted.
    await seedUser({ status: USER_STATUS.DELETED, deletedAt: daysAgo(60) });
    // Not eligible — active.
    await seedUser({ status: USER_STATUS.ACTIVE });

    const result = await handler.handleAsync({ graceCutoff: daysAgo(30) });

    expect(result.success).toBe(true);
    expect(new Set(result.data?.userIds)).toEqual(new Set([eligible1, eligible2]));
  });
});

describe("UpdateUserStatus (integration)", () => {
  let handler: UpdateUserStatus;

  beforeAll(async () => {
    await startPostgres();
    handler = new UpdateUserStatus(getDb(), ctx());
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
  });

  it("flips status to pending_deletion and sets deletedAt + feedback", async () => {
    const userId = await seedUser({ status: USER_STATUS.ACTIVE });
    const now = new Date();

    const result = await handler.handleAsync({
      userId,
      status: USER_STATUS.PENDING_DELETION,
      deletedAt: now,
      deletionFeedback: { reason: "leaving", comment: "test comment" },
    });

    expect(result.success).toBe(true);
    expect(result.data?.updated).toBe(true);

    const [row] = await getDb().select().from(user).where(eq(user.id, userId)).limit(1);
    expect(row?.status).toBe(USER_STATUS.PENDING_DELETION);
    expect(row?.deletedAt?.toISOString()).toBe(now.toISOString());
    expect(row?.deletionFeedback).toMatchObject({ reason: "leaving", comment: "test comment" });
  });

  it("flips status back to active and clears deletedAt on cancel", async () => {
    const userId = await seedUser({
      status: USER_STATUS.PENDING_DELETION,
      deletedAt: new Date(),
    });

    const result = await handler.handleAsync({
      userId,
      status: USER_STATUS.ACTIVE,
      deletedAt: null,
    });

    expect(result.success).toBe(true);
    const [row] = await getDb().select().from(user).where(eq(user.id, userId)).limit(1);
    expect(row?.status).toBe(USER_STATUS.ACTIVE);
    expect(row?.deletedAt).toBeNull();
  });

  it("returns updated:false when no row matches", async () => {
    const result = await handler.handleAsync({
      userId: generateUuidV7(),
      status: USER_STATUS.ACTIVE,
    });

    expect(result.success).toBe(true);
    expect(result.data?.updated).toBe(false);
  });

  it("expectedStatus guard: no-ops if the row's current status doesn't match (cancel-vs-anonymize race defense)", async () => {
    // Simulate the race window: AnonymizeUser already committed (status now
    // 'deleted'), and a fire-and-forget CancelUserDeletion lands afterward.
    const userId = await seedUser({
      status: USER_STATUS.DELETED,
      deletedAt: new Date(),
    });

    const result = await handler.handleAsync({
      userId,
      status: USER_STATUS.ACTIVE,
      deletedAt: null,
      expectedStatus: USER_STATUS.PENDING_DELETION,
    });

    expect(result.success).toBe(true);
    expect(result.data?.updated).toBe(false);

    // Tombstone row stays put — user is NOT resurrected.
    const [row] = await getDb().select().from(user).where(eq(user.id, userId)).limit(1);
    expect(row?.status).toBe(USER_STATUS.DELETED);
  });

  it("expectedStatus guard: applies the update when current status matches", async () => {
    const userId = await seedUser({
      status: USER_STATUS.PENDING_DELETION,
      deletedAt: new Date(),
    });

    const result = await handler.handleAsync({
      userId,
      status: USER_STATUS.ACTIVE,
      deletedAt: null,
      expectedStatus: USER_STATUS.PENDING_DELETION,
    });

    expect(result.success).toBe(true);
    expect(result.data?.updated).toBe(true);

    const [row] = await getDb().select().from(user).where(eq(user.id, userId)).limit(1);
    expect(row?.status).toBe(USER_STATUS.ACTIVE);
    expect(row?.deletedAt).toBeNull();
  });
});

describe("DeleteAllUserSessions (integration)", () => {
  let handler: DeleteAllUserSessions;

  beforeAll(async () => {
    await startPostgres();
    handler = new DeleteAllUserSessions(getDb(), ctx());
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
  });

  it("deletes every session for a user and returns rowsAffected", async () => {
    const userId = await seedUser({});
    const otherId = await seedUser({});
    for (let i = 0; i < 3; i++) {
      await getDb()
        .insert(session)
        .values({
          id: generateUuidV7(),
          userId,
          token: `tok-${i}-${generateUuidV7()}`,
          expiresAt: new Date(Date.now() + 86_400_000),
          createdAt: new Date(),
          updatedAt: new Date(),
        });
    }
    // A session belonging to a different user — must NOT be deleted.
    await getDb()
      .insert(session)
      .values({
        id: generateUuidV7(),
        userId: otherId,
        token: `tok-other-${generateUuidV7()}`,
        expiresAt: new Date(Date.now() + 86_400_000),
        createdAt: new Date(),
        updatedAt: new Date(),
      });

    const result = await handler.handleAsync({ userId });

    expect(result.success).toBe(true);
    expect(result.data?.rowsAffected).toBe(3);

    const remaining = await getDb()
      .select()
      .from(session)
      .where(eq(session.userId, userId));
    expect(remaining).toHaveLength(0);

    const otherRemaining = await getDb()
      .select()
      .from(session)
      .where(eq(session.userId, otherId));
    expect(otherRemaining).toHaveLength(1);
  });
});
