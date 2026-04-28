import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import { generateUuidV7 } from "@d2/utilities";
import { createMessage, type Message } from "@d2/comms-domain";
import { createMessageRepoHandlers } from "@d2/comms-infra";
import type { MessageRepoHandlers } from "@d2/comms-app";
import {
  startPostgres,
  stopPostgres,
  getDb,
  cleanAllTables,
} from "./helpers/postgres-test-helpers.js";
import { createTestContext } from "./helpers/test-context.js";

describe("MessageRepository (integration)", () => {
  let repo: MessageRepoHandlers;

  beforeAll(async () => {
    await startPostgres();
    repo = createMessageRepoHandlers(getDb(), createTestContext());
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
  });

  function makeMessage(overrides?: Partial<Message>): Message {
    const base = createMessage({
      content: "Hello **world**",
      plainTextContent: "Hello world",
      senderService: "auth-service",
    });
    return overrides ? { ...base, ...overrides } : base;
  }

  it("should create and retrieve a message by id", async () => {
    const msg = makeMessage();
    const createResult = await repo.create.handleAsync({ message: msg });
    expect(createResult.success).toBe(true);

    const findResult = await repo.findById.handleAsync({ id: msg.id });
    expect(findResult.success).toBe(true);

    const found = findResult.data!.message;
    expect(found.id).toBe(msg.id);
    expect(found.content).toBe("Hello **world**");
    expect(found.plainTextContent).toBe("Hello world");
    expect(found.senderService).toBe("auth-service");
    expect(found.contentFormat).toBe("markdown");
    expect(found.urgency).toBe("normal");
    expect(found.channels).toEqual([]);
    expect(found.threadId).toBeUndefined();
    expect(found.parentMessageId).toBeUndefined();
    expect(found.senderUserId).toBeUndefined();
    expect(found.senderContactId).toBeUndefined();
    expect(found.title).toBeUndefined();
    expect(found.relatedEntityId).toBeUndefined();
    expect(found.relatedEntityType).toBeUndefined();
    expect(found.metadata).toBeUndefined();
    expect(found.editedAt).toBeUndefined();
    expect(found.deletedAt).toBeUndefined();
    expect(found.createdAt).toBeInstanceOf(Date);
    expect(found.updatedAt).toBeInstanceOf(Date);
  });

  it("should return notFound for missing id", async () => {
    const result = await repo.findById.handleAsync({ id: generateUuidV7() });
    expect(result.success).toBe(false);
  });

  it("should store nullable fields as undefined when read back", async () => {
    const msg = makeMessage({
      threadId: undefined,
      title: undefined,
      metadata: undefined,
      senderUserId: undefined,
      senderContactId: undefined,
    });
    await repo.create.handleAsync({ message: msg });

    const result = await repo.findById.handleAsync({ id: msg.id });
    const found = result.data!.message;
    expect(found.threadId).toBeUndefined();
    expect(found.title).toBeUndefined();
    expect(found.metadata).toBeUndefined();
    expect(found.senderUserId).toBeUndefined();
    expect(found.senderContactId).toBeUndefined();
  });

  it("should store jsonb metadata", async () => {
    const metadata = { source: "test", count: 42, nested: { key: "value" } };
    const msg = makeMessage({ metadata });
    await repo.create.handleAsync({ message: msg });

    const result = await repo.findById.handleAsync({ id: msg.id });
    const found = result.data!.message;
    expect(found.metadata).toEqual(metadata);
  });

  it("should store all content formats", async () => {
    for (const format of ["markdown", "plain", "html"] as const) {
      const msg = makeMessage({
        id: generateUuidV7(),
        contentFormat: format,
      });
      await repo.create.handleAsync({ message: msg });

      const result = await repo.findById.handleAsync({ id: msg.id });
      expect(result.data!.message.contentFormat).toBe(format);
    }
  });

  it("should store all urgency levels", async () => {
    for (const urgency of ["normal", "urgent"] as const) {
      const msg = makeMessage({
        id: generateUuidV7(),
        urgency,
      });
      await repo.create.handleAsync({ message: msg });

      const result = await repo.findById.handleAsync({ id: msg.id });
      expect(result.data!.message.urgency).toBe(urgency);
    }
  });

  it("should store optional fields when provided", async () => {
    // Create thread root + parent messages first (FK constraints require valid references).
    const threadRoot = makeMessage({ senderService: "test" });
    const parent = makeMessage({ senderService: "test", threadId: threadRoot.id });
    await repo.create.handleAsync({ message: threadRoot });
    await repo.create.handleAsync({ message: parent });

    const msg = makeMessage({
      threadId: threadRoot.id,
      parentMessageId: parent.id,
      senderUserId: "user-1",
      senderContactId: "contact-1",
      title: "Important Notice",
      relatedEntityId: "entity-1",
      relatedEntityType: "invoice",
      channels: ["email"],
    });
    await repo.create.handleAsync({ message: msg });

    const result = await repo.findById.handleAsync({ id: msg.id });
    const found = result.data!.message;
    expect(found.threadId).toBe(threadRoot.id);
    expect(found.parentMessageId).toBe(parent.id);
    expect(found.senderUserId).toBe("user-1");
    expect(found.senderContactId).toBe("contact-1");
    expect(found.title).toBe("Important Notice");
    expect(found.relatedEntityId).toBe("entity-1");
    expect(found.relatedEntityType).toBe("invoice");
    expect(found.channels).toEqual(["email"]);
  });
});
