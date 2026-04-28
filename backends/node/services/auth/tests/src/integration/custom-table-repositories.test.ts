import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import { generateUuidV7 } from "@d2/utilities";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import {
  createSignInEventRepoHandlers,
  createEmulationConsentRepoHandlers,
  createOrgContactRepoHandlers,
} from "@d2/auth-infra";
import type {
  SignInEventRepoHandlers,
  EmulationConsentRepoHandlers,
  OrgContactRepoHandlers,
} from "@d2/auth-app";
import type { SignInEvent, EmulationConsent, OrgContact } from "@d2/auth-domain";
import { startPostgres, stopPostgres, getDb, cleanCustomTables } from "./postgres-test-helpers.js";

function createTestContext() {
  const request: IRequestContext = {
    traceId: "trace-integration",
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

// ---------------------------------------------------------------------------
// SignInEventRepository
// ---------------------------------------------------------------------------
describe("SignInEventRepository (integration)", () => {
  let repo: SignInEventRepoHandlers;

  beforeAll(async () => {
    await startPostgres();
    repo = createSignInEventRepoHandlers(getDb(), createTestContext());
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanCustomTables();
  });

  function makeEvent(overrides?: Partial<SignInEvent>): SignInEvent {
    return {
      id: generateUuidV7(),
      userId: "user-1",
      successful: true,
      ipAddress: "127.0.0.1",
      userAgent: "test-agent",
      whoIsId: undefined,
      createdAt: new Date(),
      ...overrides,
    };
  }

  it("should create and retrieve a sign-in event", async () => {
    const event = makeEvent();
    await repo.create.handleAsync({ event });

    const result = await repo.findByUserId.handleAsync({
      userId: event.userId,
      limit: 10,
      offset: 0,
    });
    expect(result.success).toBe(true);
    const events = result.data!.events;
    expect(events).toHaveLength(1);
    expect(events[0].id).toBe(event.id);
    expect(events[0].userId).toBe(event.userId);
    expect(events[0].successful).toBe(true);
    expect(events[0].ipAddress).toBe("127.0.0.1");
    expect(events[0].userAgent).toBe("test-agent");
    expect(events[0].whoIsId).toBeUndefined();
  });

  it("should create an event with whoIsId", async () => {
    const event = makeEvent({ whoIsId: "abc123" });
    await repo.create.handleAsync({ event });

    const result = await repo.findByUserId.handleAsync({
      userId: event.userId,
      limit: 10,
      offset: 0,
    });
    expect(result.data!.events[0].whoIsId).toBe("abc123");
  });

  it("should return events ordered by created_at desc", async () => {
    const older = makeEvent({ createdAt: new Date("2025-01-01") });
    const newer = makeEvent({ createdAt: new Date("2025-06-01") });
    await repo.create.handleAsync({ event: older });
    await repo.create.handleAsync({ event: newer });

    const result = await repo.findByUserId.handleAsync({
      userId: "user-1",
      limit: 10,
      offset: 0,
    });
    const events = result.data!.events;
    expect(events).toHaveLength(2);
    expect(events[0].id).toBe(newer.id);
    expect(events[1].id).toBe(older.id);
  });

  it("should paginate with limit and offset", async () => {
    for (let i = 0; i < 5; i++) {
      await repo.create.handleAsync({
        event: makeEvent({ createdAt: new Date(2025, 0, i + 1) }),
      });
    }

    const page1 = await repo.findByUserId.handleAsync({
      userId: "user-1",
      limit: 2,
      offset: 0,
    });
    const page2 = await repo.findByUserId.handleAsync({
      userId: "user-1",
      limit: 2,
      offset: 2,
    });
    expect(page1.data!.events).toHaveLength(2);
    expect(page2.data!.events).toHaveLength(2);
    expect(page1.data!.events[0].id).not.toBe(page2.data!.events[0].id);
  });

  it("should count events by user", async () => {
    await repo.create.handleAsync({ event: makeEvent() });
    await repo.create.handleAsync({ event: makeEvent() });

    const result = await repo.countByUserId.handleAsync({ userId: "user-1" });
    expect(result.success).toBe(true);
    expect(result.data!.count).toBe(2);
  });

  it("should return 0 count for unknown user", async () => {
    const result = await repo.countByUserId.handleAsync({ userId: "nonexistent" });
    expect(result.success).toBe(true);
    expect(result.data!.count).toBe(0);
  });

  it("should return latest event date", async () => {
    const d1 = new Date("2025-01-01T00:00:00Z");
    const d2 = new Date("2025-06-15T00:00:00Z");
    await repo.create.handleAsync({ event: makeEvent({ createdAt: d1 }) });
    await repo.create.handleAsync({ event: makeEvent({ createdAt: d2 }) });

    const result = await repo.getLatestEventDate.handleAsync({ userId: "user-1" });
    expect(result.success).toBe(true);
    expect(result.data!.date).toBeInstanceOf(Date);
    expect(result.data!.date!.getTime()).toBe(d2.getTime());
  });

  it("should return undefined latest date for unknown user", async () => {
    const result = await repo.getLatestEventDate.handleAsync({ userId: "nonexistent" });
    expect(result.success).toBe(true);
    expect(result.data!.date).toBeUndefined();
  });

  it("should not cross-contaminate between users", async () => {
    await repo.create.handleAsync({ event: makeEvent({ userId: "user-A" }) });
    await repo.create.handleAsync({ event: makeEvent({ userId: "user-B" }) });

    const resultA = await repo.findByUserId.handleAsync({
      userId: "user-A",
      limit: 10,
      offset: 0,
    });
    const resultB = await repo.findByUserId.handleAsync({
      userId: "user-B",
      limit: 10,
      offset: 0,
    });
    expect(resultA.data!.events).toHaveLength(1);
    expect(resultB.data!.events).toHaveLength(1);
    expect(resultA.data!.events[0].userId).toBe("user-A");
    expect(resultB.data!.events[0].userId).toBe("user-B");
  });

  it("should update whoIsId on an existing event", async () => {
    const event = makeEvent();
    await repo.create.handleAsync({ event });

    const updateResult = await repo.updateWhoIsId.handleAsync({
      id: event.id,
      whoIsId: "a".repeat(64),
    });
    expect(updateResult.success).toBe(true);

    const findResult = await repo.findByUserId.handleAsync({
      userId: event.userId,
      limit: 10,
      offset: 0,
    });
    expect(findResult.data!.events[0].whoIsId).toBe("a".repeat(64));
  });

  it("should return notFound when updating whoIsId for nonexistent event", async () => {
    const result = await repo.updateWhoIsId.handleAsync({
      id: generateUuidV7(),
      whoIsId: "b".repeat(64),
    });
    expect(result.success).toBe(false);
  });

  it("should overwrite an existing whoIsId value", async () => {
    const event = makeEvent({ whoIsId: "old-hash" });
    await repo.create.handleAsync({ event });

    await repo.updateWhoIsId.handleAsync({
      id: event.id,
      whoIsId: "c".repeat(64),
    });

    const findResult = await repo.findByUserId.handleAsync({
      userId: event.userId,
      limit: 10,
      offset: 0,
    });
    expect(findResult.data!.events[0].whoIsId).toBe("c".repeat(64));
  });
});

// ---------------------------------------------------------------------------
// EmulationConsentRepository
// ---------------------------------------------------------------------------
describe("EmulationConsentRepository (integration)", () => {
  let repo: EmulationConsentRepoHandlers;

  beforeAll(async () => {
    await startPostgres();
    repo = createEmulationConsentRepoHandlers(getDb(), createTestContext());
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanCustomTables();
  });

  function makeConsent(overrides?: Partial<EmulationConsent>): EmulationConsent {
    return {
      id: generateUuidV7(),
      userId: "user-1",
      grantedToOrgId: "org-1",
      expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000), // +1 day
      revokedAt: undefined,
      createdAt: new Date(),
      ...overrides,
    };
  }

  it("should create and retrieve a consent by id", async () => {
    const consent = makeConsent();
    await repo.create.handleAsync({ consent });

    const result = await repo.findById.handleAsync({ id: consent.id });
    expect(result.success).toBe(true);
    const found = result.data!.consent;
    expect(found).toBeDefined();
    expect(found!.id).toBe(consent.id);
    expect(found!.userId).toBe(consent.userId);
    expect(found!.grantedToOrgId).toBe(consent.grantedToOrgId);
    expect(found!.revokedAt).toBeUndefined();
  });

  it("should return not-found for nonexistent id", async () => {
    const result = await repo.findById.handleAsync({ id: "nonexistent" });
    expect(result.success).toBe(false);
  });

  it("should find active consents by user", async () => {
    const active = makeConsent();
    const expired = makeConsent({
      id: generateUuidV7(),
      grantedToOrgId: "org-2",
      expiresAt: new Date(Date.now() - 1000), // already expired
    });
    const revoked = makeConsent({
      id: generateUuidV7(),
      grantedToOrgId: "org-3",
      revokedAt: new Date(),
    });

    await repo.create.handleAsync({ consent: active });
    await repo.create.handleAsync({ consent: expired });
    await repo.create.handleAsync({ consent: revoked });

    const result = await repo.findActiveByUserId.handleAsync({
      userId: "user-1",
      limit: 50,
      offset: 0,
    });
    expect(result.success).toBe(true);
    expect(result.data!.consents).toHaveLength(1);
    expect(result.data!.consents[0].id).toBe(active.id);
  });

  it("should paginate active consents", async () => {
    for (let i = 0; i < 5; i++) {
      await repo.create.handleAsync({
        consent: makeConsent({
          id: generateUuidV7(),
          grantedToOrgId: `org-${i}`,
          createdAt: new Date(2025, 0, i + 1),
        }),
      });
    }

    const result = await repo.findActiveByUserId.handleAsync({
      userId: "user-1",
      limit: 2,
      offset: 0,
    });
    expect(result.success).toBe(true);
    expect(result.data!.consents).toHaveLength(2);
  });

  it("should find active consent by user and org", async () => {
    const consent = makeConsent();
    await repo.create.handleAsync({ consent });

    const result = await repo.findActiveByUserIdAndOrg.handleAsync({
      userId: "user-1",
      grantedToOrgId: "org-1",
    });
    expect(result.success).toBe(true);
    expect(result.data!.consent).toBeDefined();
    expect(result.data!.consent!.id).toBe(consent.id);
  });

  it("should return undefined consent for nonexistent user+org combo", async () => {
    const result = await repo.findActiveByUserIdAndOrg.handleAsync({
      userId: "user-1",
      grantedToOrgId: "org-999",
    });
    expect(result.success).toBe(true);
    expect(result.data!.consent).toBeUndefined();
  });

  it("should revoke a consent", async () => {
    const consent = makeConsent();
    await repo.create.handleAsync({ consent });

    await repo.revoke.handleAsync({ id: consent.id });

    const findResult = await repo.findById.handleAsync({ id: consent.id });
    expect(findResult.success).toBe(true);
    expect(findResult.data!.consent!.revokedAt).toBeInstanceOf(Date);

    const activeResult = await repo.findActiveByUserId.handleAsync({
      userId: "user-1",
      limit: 50,
      offset: 0,
    });
    expect(activeResult.data!.consents).toHaveLength(0);
  });

  it("should return notFound when revoking a nonexistent consent", async () => {
    const result = await repo.revoke.handleAsync({
      id: "00000000-0000-0000-0000-000000000000",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(404);
  });

  it("should enforce partial unique index on active consents", async () => {
    const consent1 = makeConsent();
    await repo.create.handleAsync({ consent: consent1 });

    const consent2 = makeConsent({ id: generateUuidV7() }); // same user+org
    const result = await repo.create.handleAsync({ consent: consent2 });
    expect(result.success).toBe(false);
  });

  it("should allow duplicate user+org after revocation", async () => {
    const consent1 = makeConsent();
    await repo.create.handleAsync({ consent: consent1 });
    await repo.revoke.handleAsync({ id: consent1.id });

    const consent2 = makeConsent({ id: generateUuidV7() });
    const result = await repo.create.handleAsync({ consent: consent2 });
    expect(result.success).toBe(true);
  });

  it("should not cross-contaminate between users", async () => {
    await repo.create.handleAsync({ consent: makeConsent({ userId: "user-A" }) });
    await repo.create.handleAsync({
      consent: makeConsent({ id: generateUuidV7(), userId: "user-B" }),
    });

    const resultA = await repo.findActiveByUserId.handleAsync({
      userId: "user-A",
      limit: 50,
      offset: 0,
    });
    const resultB = await repo.findActiveByUserId.handleAsync({
      userId: "user-B",
      limit: 50,
      offset: 0,
    });
    expect(resultA.data!.consents).toHaveLength(1);
    expect(resultB.data!.consents).toHaveLength(1);
    expect(resultA.data!.consents[0].userId).toBe("user-A");
    expect(resultB.data!.consents[0].userId).toBe("user-B");
  });
});

// ---------------------------------------------------------------------------
// OrgContactRepository
// ---------------------------------------------------------------------------
describe("OrgContactRepository (integration)", () => {
  let repo: OrgContactRepoHandlers;

  beforeAll(async () => {
    await startPostgres();
    repo = createOrgContactRepoHandlers(getDb(), createTestContext());
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanCustomTables();
  });

  function makeContact(overrides?: Partial<OrgContact>): OrgContact {
    return {
      id: generateUuidV7(),
      organizationId: "org-1",
      label: "Main Office",
      isPrimary: false,
      createdAt: new Date(),
      updatedAt: new Date(),
      ...overrides,
    };
  }

  it("should create and retrieve a contact by id", async () => {
    const contact = makeContact();
    await repo.create.handleAsync({ contact });

    const result = await repo.findById.handleAsync({ id: contact.id });
    expect(result.success).toBe(true);
    const found = result.data!.contact;
    expect(found).toBeDefined();
    expect(found!.id).toBe(contact.id);
    expect(found!.organizationId).toBe(contact.organizationId);
    expect(found!.label).toBe("Main Office");
    expect(found!.isPrimary).toBe(false);
  });

  it("should return not-found for nonexistent id", async () => {
    const result = await repo.findById.handleAsync({ id: "nonexistent" });
    expect(result.success).toBe(false);
  });

  it("should find contacts by org with primary first", async () => {
    const secondary = makeContact({
      label: "Branch",
      isPrimary: false,
      createdAt: new Date("2025-01-01"),
    });
    const primary = makeContact({
      id: generateUuidV7(),
      label: "HQ",
      isPrimary: true,
      createdAt: new Date("2025-06-01"),
    });

    await repo.create.handleAsync({ contact: secondary });
    await repo.create.handleAsync({ contact: primary });

    const result = await repo.findByOrgId.handleAsync({
      organizationId: "org-1",
      limit: 50,
      offset: 0,
    });
    expect(result.success).toBe(true);
    const contacts = result.data!.contacts;
    expect(contacts).toHaveLength(2);
    expect(contacts[0].isPrimary).toBe(true);
    expect(contacts[0].label).toBe("HQ");
    expect(contacts[1].isPrimary).toBe(false);
  });

  it("should paginate contacts by org", async () => {
    for (let i = 0; i < 5; i++) {
      await repo.create.handleAsync({
        contact: makeContact({
          id: generateUuidV7(),
          label: `Contact ${i}`,
          createdAt: new Date(2025, 0, i + 1),
        }),
      });
    }

    const result = await repo.findByOrgId.handleAsync({
      organizationId: "org-1",
      limit: 2,
      offset: 0,
    });
    expect(result.success).toBe(true);
    expect(result.data!.contacts).toHaveLength(2);
  });

  it("should update label and isPrimary", async () => {
    const contact = makeContact();
    await repo.create.handleAsync({ contact });

    const updated: OrgContact = {
      ...contact,
      label: "Updated Office",
      isPrimary: true,
      updatedAt: new Date(),
    };
    await repo.update.handleAsync({ contact: updated });

    const result = await repo.findById.handleAsync({ id: contact.id });
    const found = result.data!.contact!;
    expect(found.label).toBe("Updated Office");
    expect(found.isPrimary).toBe(true);
    expect(found.organizationId).toBe(contact.organizationId);
  });

  it("should delete a contact", async () => {
    const contact = makeContact();
    await repo.create.handleAsync({ contact });

    await repo.delete.handleAsync({ id: contact.id });

    const result = await repo.findById.handleAsync({ id: contact.id });
    expect(result.success).toBe(false);
  });

  it("should return notFound when updating a nonexistent contact", async () => {
    const contact: OrgContact = {
      id: "00000000-0000-0000-0000-000000000000",
      organizationId: "org-1",
      label: "Ghost Office",
      isPrimary: false,
      createdAt: new Date(),
      updatedAt: new Date(),
    };
    const result = await repo.update.handleAsync({ contact });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(404);
  });

  it("should return notFound when deleting a nonexistent contact", async () => {
    const result = await repo.delete.handleAsync({
      id: "00000000-0000-0000-0000-000000000000",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(404);
  });

  it("should not cross-contaminate between orgs", async () => {
    await repo.create.handleAsync({
      contact: makeContact({ organizationId: "org-A" }),
    });
    await repo.create.handleAsync({
      contact: makeContact({ id: generateUuidV7(), organizationId: "org-B" }),
    });

    const resultA = await repo.findByOrgId.handleAsync({
      organizationId: "org-A",
      limit: 50,
      offset: 0,
    });
    const resultB = await repo.findByOrgId.handleAsync({
      organizationId: "org-B",
      limit: 50,
      offset: 0,
    });
    expect(resultA.data!.contacts).toHaveLength(1);
    expect(resultB.data!.contacts).toHaveLength(1);
    expect(resultA.data!.contacts[0].organizationId).toBe("org-A");
    expect(resultB.data!.contacts[0].organizationId).toBe("org-B");
  });
});
