import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import { generateUuidV7 } from "@d2/utilities";
import { createMessage, createDeliveryRequest, type DeliveryRequest } from "@d2/comms-domain";
import { createMessageRepoHandlers, createDeliveryRequestRepoHandlers } from "@d2/comms-infra";
import type { MessageRepoHandlers, DeliveryRequestRepoHandlers } from "@d2/comms-app";
import {
  startPostgres,
  stopPostgres,
  getDb,
  cleanAllTables,
} from "./helpers/postgres-test-helpers.js";
import { createTestContext } from "./helpers/test-context.js";

describe("DeliveryRequestRepository (integration)", () => {
  let msgRepo: MessageRepoHandlers;
  let repo: DeliveryRequestRepoHandlers;
  let testMessageId: string;

  beforeAll(async () => {
    await startPostgres();
    const ctx = createTestContext();
    msgRepo = createMessageRepoHandlers(getDb(), ctx);
    repo = createDeliveryRequestRepoHandlers(getDb(), ctx);
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
    // Insert a parent message for FK reference
    const msg = createMessage({
      content: "Test message",
      plainTextContent: "Test message",
      senderService: "test",
    });
    await msgRepo.create.handleAsync({ message: msg });
    testMessageId = msg.id;
  });

  function makeRequest(overrides?: Partial<DeliveryRequest>): DeliveryRequest {
    const base = createDeliveryRequest({
      messageId: testMessageId,
      correlationId: generateUuidV7(),
      recipientContactId: "contact-1",
    });
    return overrides ? { ...base, ...overrides } : base;
  }

  it("should create and retrieve a delivery request by id", async () => {
    const req = makeRequest();
    const createResult = await repo.create.handleAsync({ request: req });
    expect(createResult.success).toBe(true);

    const findResult = await repo.findById.handleAsync({ id: req.id });
    expect(findResult.success).toBe(true);

    const found = findResult.data!.request;
    expect(found.id).toBe(req.id);
    expect(found.messageId).toBe(testMessageId);
    expect(found.correlationId).toBe(req.correlationId);
    expect(found.recipientContactId).toBe("contact-1");
    expect(found.callbackTopic).toBeUndefined();
    expect(found.processedAt).toBeUndefined();
    expect(found.createdAt).toBeInstanceOf(Date);
  });

  it("should return notFound for missing id", async () => {
    const result = await repo.findById.handleAsync({ id: generateUuidV7() });
    expect(result.success).toBe(false);
  });

  it("should find by correlationId", async () => {
    const req = makeRequest();
    await repo.create.handleAsync({ request: req });

    const result = await repo.findByCorrelationId.handleAsync({
      correlationId: req.correlationId,
    });
    expect(result.success).toBe(true);
    expect(result.data!.request).toBeDefined();
    expect(result.data!.request!.id).toBe(req.id);
  });

  it("should return undefined for missing correlationId", async () => {
    const result = await repo.findByCorrelationId.handleAsync({
      correlationId: "nonexistent",
    });
    expect(result.success).toBe(true);
    expect(result.data!.request).toBeUndefined();
  });

  it("should markProcessed set processedAt", async () => {
    const req = makeRequest();
    await repo.create.handleAsync({ request: req });

    await repo.markProcessed.handleAsync({ id: req.id });

    const result = await repo.findById.handleAsync({ id: req.id });
    expect(result.data!.request.processedAt).toBeInstanceOf(Date);
  });

  it("should return 409 Conflict on duplicate correlationId", async () => {
    const correlationId = generateUuidV7();
    const req1 = makeRequest({ correlationId });
    const req2 = makeRequest({ id: generateUuidV7(), correlationId });

    await repo.create.handleAsync({ request: req1 });
    const result = await repo.create.handleAsync({ request: req2 });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(409);
    expect(result.errorCode).toBe("CONFLICT");
  });

  it("should store optional fields when provided", async () => {
    const req = makeRequest({
      callbackTopic: "delivery.callback",
    });
    await repo.create.handleAsync({ request: req });

    const result = await repo.findById.handleAsync({ id: req.id });
    const found = result.data!.request;
    expect(found.callbackTopic).toBe("delivery.callback");
  });

  it("should return notFound when markProcessed targets nonexistent id", async () => {
    const result = await repo.markProcessed.handleAsync({
      id: "00000000-0000-0000-0000-000000000000",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(404);
  });
});
