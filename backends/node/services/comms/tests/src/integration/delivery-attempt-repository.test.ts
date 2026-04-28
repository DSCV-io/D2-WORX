import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import { generateUuidV7 } from "@d2/utilities";
import { createDeliveryAttempt, type DeliveryAttempt } from "@d2/comms-domain";
import { createDeliveryAttemptRepoHandlers, message, deliveryRequest } from "@d2/comms-infra";
import type { DeliveryAttemptRepoHandlers } from "@d2/comms-app";
import {
  startPostgres,
  stopPostgres,
  getDb,
  cleanAllTables,
} from "./helpers/postgres-test-helpers.js";
import { createTestContext } from "./helpers/test-context.js";

describe("DeliveryAttemptRepository (integration)", () => {
  let repo: DeliveryAttemptRepoHandlers;
  const testRequestId = generateUuidV7();

  beforeAll(async () => {
    await startPostgres();
    repo = createDeliveryAttemptRepoHandlers(getDb(), createTestContext());
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();

    // FK constraints require a valid message → delivery_request chain.
    const msgId = generateUuidV7();
    await getDb().insert(message).values({
      id: msgId,
      content: "test",
      plainTextContent: "test",
      contentFormat: "markdown",
      channels: [],
      urgency: "normal",
      senderService: "test",
      createdAt: new Date(),
      updatedAt: new Date(),
    });
    await getDb().insert(deliveryRequest).values({
      id: testRequestId,
      messageId: msgId,
      correlationId: generateUuidV7(),
      recipientContactId: generateUuidV7(),
      createdAt: new Date(),
    });
  });

  function makeAttempt(overrides?: Partial<DeliveryAttempt>): DeliveryAttempt {
    const base = createDeliveryAttempt({
      requestId: testRequestId,
      channel: "email",
      recipientAddress: "user@example.com",
      attemptNumber: 1,
    });
    return overrides ? { ...base, ...overrides } : base;
  }

  it("should create and retrieve attempts by requestId", async () => {
    const attempt = makeAttempt();
    const createResult = await repo.create.handleAsync({ attempt });
    expect(createResult.success).toBe(true);

    const findResult = await repo.findByRequestId.handleAsync({ requestId: testRequestId });
    expect(findResult.success).toBe(true);

    const attempts = findResult.data!.attempts;
    expect(attempts).toHaveLength(1);

    const found = attempts[0];
    expect(found.id).toBe(attempt.id);
    expect(found.requestId).toBe(testRequestId);
    expect(found.channel).toBe("email");
    expect(found.recipientAddress).toBe("user@example.com");
    expect(found.status).toBe("pending");
    expect(found.providerMessageId).toBeUndefined();
    expect(found.error).toBeUndefined();
    expect(found.attemptNumber).toBe(1);
    expect(found.createdAt).toBeInstanceOf(Date);
    expect(found.nextRetryAt).toBeUndefined();
  });

  it("should return empty array when no attempts exist", async () => {
    const result = await repo.findByRequestId.handleAsync({ requestId: "nonexistent" });
    expect(result.success).toBe(true);
    expect(result.data!.attempts).toHaveLength(0);
  });

  it("should return multiple attempts for the same request", async () => {
    const attempt1 = makeAttempt({ attemptNumber: 1 });
    const attempt2 = makeAttempt({
      id: generateUuidV7(),
      channel: "sms",
      recipientAddress: "+15551234567",
      attemptNumber: 1,
    });
    const attempt3 = makeAttempt({
      id: generateUuidV7(),
      attemptNumber: 2,
    });

    await repo.create.handleAsync({ attempt: attempt1 });
    await repo.create.handleAsync({ attempt: attempt2 });
    await repo.create.handleAsync({ attempt: attempt3 });

    const result = await repo.findByRequestId.handleAsync({ requestId: testRequestId });
    expect(result.data!.attempts).toHaveLength(3);
  });

  it("should updateStatus with providerMessageId on success", async () => {
    const attempt = makeAttempt();
    await repo.create.handleAsync({ attempt });

    await repo.updateStatus.handleAsync({
      id: attempt.id,
      status: "sent",
      providerMessageId: "resend-msg-123",
    });

    const result = await repo.findByRequestId.handleAsync({ requestId: testRequestId });
    const updated = result.data!.attempts[0];
    expect(updated.status).toBe("sent");
    expect(updated.providerMessageId).toBe("resend-msg-123");
  });

  it("should updateStatus with error and nextRetryAt on failure", async () => {
    const attempt = makeAttempt();
    await repo.create.handleAsync({ attempt });

    const nextRetry = new Date(Date.now() + 60_000);
    await repo.updateStatus.handleAsync({
      id: attempt.id,
      status: "failed",
      error: "Connection timeout",
      nextRetryAt: nextRetry,
    });

    const result = await repo.findByRequestId.handleAsync({ requestId: testRequestId });
    const updated = result.data!.attempts[0];
    expect(updated.status).toBe("failed");
    expect(updated.error).toBe("Connection timeout");
    expect(updated.nextRetryAt).toBeInstanceOf(Date);
    expect(updated.nextRetryAt!.getTime()).toBe(nextRetry.getTime());
  });

  it("should updateStatus with only status field", async () => {
    const attempt = makeAttempt();
    await repo.create.handleAsync({ attempt });

    await repo.updateStatus.handleAsync({
      id: attempt.id,
      status: "sent",
    });

    const result = await repo.findByRequestId.handleAsync({ requestId: testRequestId });
    const updated = result.data!.attempts[0];
    expect(updated.status).toBe("sent");
    expect(updated.providerMessageId).toBeUndefined();
    expect(updated.error).toBeUndefined();
  });

  it("should return notFound when updateStatus targets nonexistent id", async () => {
    const result = await repo.updateStatus.handleAsync({
      id: "00000000-0000-0000-0000-000000000000",
      status: "sent",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(404);
  });
});
