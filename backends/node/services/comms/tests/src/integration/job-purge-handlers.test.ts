import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import { generateUuidV7 } from "@d2/utilities";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import {
  PurgeDeletedMessages,
  PurgeDeliveryHistory,
  message,
  deliveryRequest,
  deliveryAttempt,
} from "@d2/comms-infra";
import {
  startPostgres,
  stopPostgres,
  getDb,
  cleanAllTables,
} from "./helpers/postgres-test-helpers.js";

function createTestContext() {
  const request: IRequestContext = {
    traceId: "trace-purge-integration",
    isAuthenticated: false,
    isTrustedService: false,
    isAgentStaff: false,
    isAgentAdmin: false,
    isTargetingStaff: false,
    isTargetingAdmin: false,
    isOrgEmulating: false,
    isUserImpersonating: false,
  };
  return new HandlerContext(request, createLogger({ level: "silent" as never }));
}

function daysAgo(days: number): Date {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d;
}

function makeMessage(overrides?: Partial<Record<string, unknown>>) {
  return {
    id: generateUuidV7(),
    content: "test content",
    plainTextContent: "test content",
    contentFormat: "markdown",
    channels: [],
    urgency: "normal",
    senderService: "test",
    createdAt: new Date(),
    updatedAt: new Date(),
    ...overrides,
  };
}

function makeDeliveryRequest(overrides?: Partial<Record<string, unknown>>) {
  return {
    id: generateUuidV7(),
    messageId: generateUuidV7(),
    correlationId: generateUuidV7(),
    recipientContactId: generateUuidV7(),
    createdAt: new Date(),
    ...overrides,
  };
}

function makeDeliveryAttempt(requestId: string, overrides?: Partial<Record<string, unknown>>) {
  return {
    id: generateUuidV7(),
    requestId,
    channel: "email",
    recipientAddress: "test@example.com",
    status: "delivered",
    attemptNumber: 1,
    createdAt: new Date(),
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// PurgeDeletedMessages
// ---------------------------------------------------------------------------
describe("PurgeDeletedMessages (integration)", () => {
  let handler: PurgeDeletedMessages;

  beforeAll(async () => {
    await startPostgres();
    handler = new PurgeDeletedMessages(getDb(), createTestContext());
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
  });

  it("should return 0 when no messages exist", async () => {
    const result = await handler.handleAsync({ cutoffDate: new Date() });

    expect(result.success).toBe(true);
    expect(result.data?.rowsAffected).toBe(0);
  });

  it("should delete only soft-deleted messages before cutoff", async () => {
    const cutoff = daysAgo(90);

    await getDb()
      .insert(message)
      .values([
        // Soft-deleted long ago — should be purged
        makeMessage({ deletedAt: daysAgo(100) }),
        // Soft-deleted before cutoff — should be purged
        makeMessage({ deletedAt: daysAgo(91) }),
        // Soft-deleted after cutoff — should NOT be purged
        makeMessage({ deletedAt: daysAgo(30) }),
        // Not deleted — should NOT be purged
        makeMessage(),
      ]);

    const result = await handler.handleAsync({ cutoffDate: cutoff });

    expect(result.success).toBe(true);
    expect(result.data?.rowsAffected).toBe(2);

    const remaining = await getDb().select().from(message);
    expect(remaining).toHaveLength(2);
  });

  it("should not delete messages that are not soft-deleted", async () => {
    await getDb().insert(message).values([makeMessage(), makeMessage(), makeMessage()]);

    const result = await handler.handleAsync({ cutoffDate: new Date() });

    expect(result.success).toBe(true);
    expect(result.data?.rowsAffected).toBe(0);

    const remaining = await getDb().select().from(message);
    expect(remaining).toHaveLength(3);
  });
});

// ---------------------------------------------------------------------------
// PurgeDeliveryHistory
// ---------------------------------------------------------------------------
describe("PurgeDeliveryHistory (integration)", () => {
  let handler: PurgeDeliveryHistory;

  beforeAll(async () => {
    await startPostgres();
    handler = new PurgeDeliveryHistory(getDb(), createTestContext());
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
  });

  it("should return 0 when no delivery requests exist", async () => {
    const result = await handler.handleAsync({ cutoffDate: new Date() });

    expect(result.success).toBe(true);
    expect(result.data?.rowsAffected).toBe(0);
  });

  it("should delete old delivery requests and keep recent ones", async () => {
    const cutoff = daysAgo(365);

    // Create parent messages first (FK constraint requires valid messageId).
    const msg1 = makeMessage();
    const msg2 = makeMessage();
    await getDb().insert(message).values([msg1, msg2]);

    const oldReq = makeDeliveryRequest({ messageId: msg1.id, createdAt: daysAgo(400) });
    const recentReq = makeDeliveryRequest({ messageId: msg2.id, createdAt: daysAgo(100) });

    await getDb().insert(deliveryRequest).values([oldReq, recentReq]);

    const result = await handler.handleAsync({ cutoffDate: cutoff });

    expect(result.success).toBe(true);
    expect(result.data?.rowsAffected).toBe(1);

    const remaining = await getDb().select().from(deliveryRequest);
    expect(remaining).toHaveLength(1);
    expect(remaining[0].id).toBe(recentReq.id);
  });

  it("should delete associated delivery attempts when deleting requests", async () => {
    const cutoff = daysAgo(365);

    const msg1 = makeMessage();
    const msg2 = makeMessage();
    await getDb().insert(message).values([msg1, msg2]);

    const oldReq = makeDeliveryRequest({ messageId: msg1.id, createdAt: daysAgo(400) });
    const recentReq = makeDeliveryRequest({ messageId: msg2.id, createdAt: daysAgo(100) });

    await getDb().insert(deliveryRequest).values([oldReq, recentReq]);

    // Add attempts for both requests
    await getDb()
      .insert(deliveryAttempt)
      .values([
        makeDeliveryAttempt(oldReq.id as string),
        makeDeliveryAttempt(oldReq.id as string, { attemptNumber: 2 }),
        makeDeliveryAttempt(recentReq.id as string),
      ]);

    const result = await handler.handleAsync({ cutoffDate: cutoff });

    expect(result.success).toBe(true);
    expect(result.data?.rowsAffected).toBe(1);

    // Old request's attempts should be deleted
    const remainingAttempts = await getDb().select().from(deliveryAttempt);
    expect(remainingAttempts).toHaveLength(1);
    expect(remainingAttempts[0].requestId).toBe(recentReq.id);

    const remainingRequests = await getDb().select().from(deliveryRequest);
    expect(remainingRequests).toHaveLength(1);
  });

  it("should handle requests without attempts", async () => {
    const cutoff = daysAgo(365);

    const msg = makeMessage();
    await getDb().insert(message).values([msg]);

    await getDb()
      .insert(deliveryRequest)
      .values([makeDeliveryRequest({ messageId: msg.id, createdAt: daysAgo(400) })]);

    const result = await handler.handleAsync({ cutoffDate: cutoff });

    expect(result.success).toBe(true);
    expect(result.data?.rowsAffected).toBe(1);

    const remaining = await getDb().select().from(deliveryRequest);
    expect(remaining).toHaveLength(0);
  });
});
