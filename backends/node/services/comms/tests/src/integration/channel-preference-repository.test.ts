import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import { generateUuidV7 } from "@d2/utilities";
import { createChannelPreference, updateChannelPreference } from "@d2/comms-domain";
import { createChannelPreferenceRepoHandlers } from "@d2/comms-infra";
import type { ChannelPreferenceRepoHandlers } from "@d2/comms-app";
import {
  startPostgres,
  stopPostgres,
  getDb,
  cleanAllTables,
} from "./helpers/postgres-test-helpers.js";
import { createTestContext } from "./helpers/test-context.js";

describe("ChannelPreferenceRepository (integration)", () => {
  let repo: ChannelPreferenceRepoHandlers;

  beforeAll(async () => {
    await startPostgres();
    repo = createChannelPreferenceRepoHandlers(getDb(), createTestContext());
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
  });

  it("should create a preference and retrieve it by contactId", async () => {
    const contactId = generateUuidV7();
    const pref = createChannelPreference({ contactId });

    const createResult = await repo.create.handleAsync({ pref });
    expect(createResult.success).toBe(true);

    const findResult = await repo.findByContactId.handleAsync({ contactId });
    expect(findResult.success).toBe(true);

    const found = findResult.data!.pref;
    expect(found).not.toBeNull();
    expect(found!.id).toBe(pref.id);
    expect(found!.contactId).toBe(contactId);
    expect(found!.emailEnabled).toBe(true);
    expect(found!.smsEnabled).toBe(true);
    expect(found!.createdAt).toBeInstanceOf(Date);
    expect(found!.updatedAt).toBeInstanceOf(Date);
  });

  it("should return undefined for unknown contactId", async () => {
    const result = await repo.findByContactId.handleAsync({ contactId: generateUuidV7() });
    expect(result.success).toBe(true);
    expect(result.data!.pref).toBeUndefined();
  });

  it("should update emailEnabled and smsEnabled", async () => {
    const contactId = generateUuidV7();
    const pref = createChannelPreference({ contactId });
    await repo.create.handleAsync({ pref });

    const updated = updateChannelPreference(pref, {
      emailEnabled: false,
      smsEnabled: false,
    });

    const updateResult = await repo.update.handleAsync({ pref: updated });
    expect(updateResult.success).toBe(true);

    const findResult = await repo.findByContactId.handleAsync({ contactId });
    const found = findResult.data!.pref!;
    expect(found.emailEnabled).toBe(false);
    expect(found.smsEnabled).toBe(false);
    expect(found.updatedAt.getTime()).toBeGreaterThanOrEqual(pref.updatedAt.getTime());
  });

  it("should isolate preferences by contactId", async () => {
    const contactId1 = generateUuidV7();
    const contactId2 = generateUuidV7();
    const pref1 = createChannelPreference({ contactId: contactId1, emailEnabled: false });
    const pref2 = createChannelPreference({ contactId: contactId2, smsEnabled: false });

    await repo.create.handleAsync({ pref: pref1 });
    await repo.create.handleAsync({ pref: pref2 });

    const result1 = await repo.findByContactId.handleAsync({ contactId: contactId1 });
    const result2 = await repo.findByContactId.handleAsync({ contactId: contactId2 });

    expect(result1.data!.pref!.emailEnabled).toBe(false);
    expect(result1.data!.pref!.smsEnabled).toBe(true);
    expect(result2.data!.pref!.emailEnabled).toBe(true);
    expect(result2.data!.pref!.smsEnabled).toBe(false);
  });

  it("should create with defaults (both channels enabled)", async () => {
    const contactId = generateUuidV7();
    const pref = createChannelPreference({ contactId });
    await repo.create.handleAsync({ pref });

    const result = await repo.findByContactId.handleAsync({ contactId });
    const found = result.data!.pref!;
    expect(found.emailEnabled).toBe(true);
    expect(found.smsEnabled).toBe(true);
  });

  it("should return notFound when update targets nonexistent id", async () => {
    const pref = createChannelPreference({ contactId: generateUuidV7() });
    // Do NOT create the preference — update a record that doesn't exist
    const updated = updateChannelPreference(pref, { emailEnabled: false });

    const result = await repo.update.handleAsync({ pref: updated });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(404);
  });

  it("should handle concurrent updates to the same preference without error", async () => {
    const contactId = generateUuidV7();
    const pref = createChannelPreference({ contactId });
    await repo.create.handleAsync({ pref });

    // Fire 5 concurrent updates with different values
    const updates = await Promise.all(
      Array.from({ length: 5 }, (_, i) => {
        const toggle = updateChannelPreference(pref, {
          emailEnabled: i % 2 === 0,
          smsEnabled: i % 2 !== 0,
        });
        return repo.update.handleAsync({ pref: toggle });
      }),
    );

    // All promises should resolve without error
    for (const result of updates) {
      expect(result.success).toBe(true);
    }

    // Exactly 1 row should exist for that contactId
    const findResult = await repo.findByContactId.handleAsync({ contactId });
    expect(findResult.success).toBe(true);
    expect(findResult.data!.pref).not.toBeNull();
    expect(findResult.data!.pref!.contactId).toBe(contactId);
  });
});
