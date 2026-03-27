import { describe, it, expect, beforeAll, afterAll, beforeEach, vi } from "vitest";
import { D2Result } from "@d2/result";
import { HandlerContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { generateUuidV7 } from "@d2/utilities";
import { createDeliveryHandlers, type DeliveryHandlers } from "@d2/comms-app";

/** Generates a fresh UUID to use as a contactId in tests. */
const newContactId = () => generateUuidV7();

// Stable UUIDs for tests that don't need fresh IDs per run
const CONTACT_1 = "c0000000-0000-0000-0000-000000000001";
const CONTACT_2 = "c0000000-0000-0000-0000-000000000002";
const CONTACT_IDEM = "c0000000-0000-0000-0000-000000000003";
const CONTACT_FAIL = "c0000000-0000-0000-0000-000000000004";
const CONTACT_NOEXIST = "c0000000-0000-0000-0000-000000000005";
const CONTACT_PROC = "c0000000-0000-0000-0000-000000000006";
import {
  createMessageRepoHandlers,
  createDeliveryRequestRepoHandlers,
  createDeliveryAttemptRepoHandlers,
  createChannelPreferenceRepoHandlers,
  deliveryRequest as deliveryRequestTable,
  deliveryAttempt as deliveryAttemptTable,
  message as messageTable,
} from "@d2/comms-infra";
import { eq } from "drizzle-orm";
import {
  startPostgres,
  stopPostgres,
  getDb,
  cleanAllTables,
} from "./helpers/postgres-test-helpers.js";
import {
  createStubEmailProvider,
  createMockGeoHandlers,
  type StubEmailProvider,
  type MockGeoHandlers,
} from "./helpers/handler-test-helpers.js";

/**
 * Integration tests for the Deliver handler.
 *
 * Uses real Postgres (Testcontainers), real repo handlers, a stub email provider,
 * and mock geo-client handlers — validates actual DB records, D2Result outcomes,
 * and the full delivery orchestration pipeline.
 */
describe("Deliver handler (integration)", () => {
  let handlers: DeliveryHandlers;
  let stubEmail: StubEmailProvider;
  let mockGeo: MockGeoHandlers;
  let context: HandlerContext;

  beforeAll(async () => {
    await startPostgres();

    const logger = createLogger({ level: "silent" as never });
    context = new HandlerContext(
      {
        traceId: "trace-deliver-integration",
        isAuthenticated: false,
        isTrustedService: null,
        isAgentStaff: false,
        isAgentAdmin: false,
        isTargetingStaff: false,
        isTargetingAdmin: false,
        isOrgEmulating: false,
        isUserImpersonating: false,
      },
      logger,
    );

    const db = getDb();
    const repos = {
      message: createMessageRepoHandlers(db, context),
      request: createDeliveryRequestRepoHandlers(db, context),
      attempt: createDeliveryAttemptRepoHandlers(db, context),
      channelPref: createChannelPreferenceRepoHandlers(db, context),
    };

    stubEmail = createStubEmailProvider(context);
    mockGeo = createMockGeoHandlers();

    handlers = createDeliveryHandlers(
      repos,
      { email: stubEmail },
      mockGeo.getContactsByIds,
      context,
    );
  }, 120_000);

  afterAll(stopPostgres);

  beforeEach(async () => {
    await cleanAllTables();
    stubEmail.clear();
    mockGeo.reset();
  });

  // ---------------------------------------------------------------------------
  // Deliver — Core pipeline
  // ---------------------------------------------------------------------------

  it("should create message, request, and attempt records in the database", async () => {
    mockGeo.setContactEmail(CONTACT_1, "user@example.com");

    const result = await handlers.deliver.handleAsync({
      senderService: "auth",
      title: "Test Email",
      content: "<p>Hello</p>",
      plainTextContent: "Hello",
      channels: ["email"],
      recipientContactId: CONTACT_1,
      correlationId: generateUuidV7(),
    });

    expect(result.success).toBe(true);
    expect(result.data).toBeDefined();

    const { messageId, requestId, attempts } = result.data!;
    expect(messageId).toBeDefined();
    expect(requestId).toBeDefined();
    expect(attempts).toHaveLength(1);
    expect(attempts[0].status).toBe("sent");

    // Verify actual DB records
    const db = getDb();
    const [msgRow] = await db.select().from(messageTable).where(eq(messageTable.id, messageId));
    expect(msgRow).toBeDefined();
    expect(msgRow.title).toBe("Test Email");
    expect(msgRow.senderService).toBe("auth");
    expect(msgRow.channels).toEqual(["email"]);

    const [reqRow] = await db
      .select()
      .from(deliveryRequestTable)
      .where(eq(deliveryRequestTable.id, requestId));
    expect(reqRow).toBeDefined();
    expect(reqRow.messageId).toBe(messageId);
    expect(reqRow.recipientContactId).toBe(CONTACT_1);

    const attemptRows = await db
      .select()
      .from(deliveryAttemptTable)
      .where(eq(deliveryAttemptTable.requestId, requestId));
    expect(attemptRows).toHaveLength(1);
    expect(attemptRows[0].channel).toBe("email");
    expect(attemptRows[0].status).toBe("sent");
    expect(attemptRows[0].recipientAddress).toBe("user@example.com");
  });

  it("should send email via stub provider with correct content", async () => {
    mockGeo.setContactEmail(CONTACT_2, "verify@example.com");

    await handlers.deliver.handleAsync({
      senderService: "auth",
      title: "Verify email",
      content: "<p>Click to verify</p>",
      plainTextContent: "Click to verify",
      recipientContactId: CONTACT_2,
      correlationId: generateUuidV7(),
    });

    expect(stubEmail.sentCount()).toBe(1);
    const email = stubEmail.getLastEmail()!;
    expect(email.to).toBe("verify@example.com");
    expect(email.plainText).toBe("Click to verify");
  });

  it("should enforce idempotency via correlationId — same ID returns existing data", async () => {
    mockGeo.setContactEmail(CONTACT_IDEM, "idem@example.com");

    const correlationId = generateUuidV7();
    const first = await handlers.deliver.handleAsync({
      senderService: "auth",
      title: "Idempotent",
      content: "<p>First call</p>",
      plainTextContent: "First call",
      recipientContactId: CONTACT_IDEM,
      correlationId,
    });

    expect(first.success).toBe(true);

    // Second call with same correlationId — should return existing data, not create new records
    const second = await handlers.deliver.handleAsync({
      senderService: "auth",
      title: "Idempotent — should be ignored",
      content: "<p>Should not be created</p>",
      plainTextContent: "Should not be created",
      recipientContactId: CONTACT_IDEM,
      correlationId,
    });

    expect(second.success).toBe(true);
    expect(second.data!.messageId).toBe(first.data!.messageId);
    expect(second.data!.requestId).toBe(first.data!.requestId);
    // Only 1 email sent, not 2
    expect(stubEmail.sentCount()).toBe(1);
  });

  it("should return DELIVERY_FAILED when email provider fails", async () => {
    mockGeo.setContactEmail(CONTACT_FAIL, "fail@example.com");

    // Create a failing email provider by overriding executeAsync
    const failingEmail = createStubEmailProvider(context);
    vi.spyOn(failingEmail, "handleAsync").mockResolvedValue(
      D2Result.fail({ messages: ["SMTP timeout"], statusCode: 503 }),
    );

    const db = getDb();
    const repos = {
      message: createMessageRepoHandlers(db, context),
      request: createDeliveryRequestRepoHandlers(db, context),
      attempt: createDeliveryAttemptRepoHandlers(db, context),
      channelPref: createChannelPreferenceRepoHandlers(db, context),
    };
    const failHandlers = createDeliveryHandlers(
      repos,
      { email: failingEmail },
      mockGeo.getContactsByIds,
      context,
    );

    const result = await failHandlers.deliver.handleAsync({
      senderService: "auth",
      title: "Will Fail",
      content: "<p>Fail</p>",
      plainTextContent: "Fail",
      recipientContactId: CONTACT_FAIL,
      correlationId: generateUuidV7(),
    });

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe("DELIVERY_FAILED");
    expect(result.statusCode).toBe(503);

    // Attempt should be persisted with "failed" status
    const attemptRows = await db.select().from(deliveryAttemptTable);
    const failedAttempt = attemptRows.find((a) => a.recipientAddress === "fail@example.com");
    expect(failedAttempt).toBeDefined();
    expect(failedAttempt!.status).toBe("failed");
    expect(failedAttempt!.nextRetryAt).not.toBeNull();
  });

  it("should return not-found when recipient has no deliverable address", async () => {
    // Don't register any geo contact — RecipientResolver returns empty
    const result = await handlers.deliver.handleAsync({
      senderService: "auth",
      title: "No recipient",
      content: "<p>Nowhere to send</p>",
      plainTextContent: "Nowhere to send",
      recipientContactId: CONTACT_NOEXIST,
      correlationId: generateUuidV7(),
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(404);
  });

  it("should mark delivery request as processed on success", async () => {
    mockGeo.setContactEmail(CONTACT_PROC, "proc@example.com");

    const result = await handlers.deliver.handleAsync({
      senderService: "auth",
      title: "Processed",
      content: "<p>Done</p>",
      plainTextContent: "Done",
      recipientContactId: CONTACT_PROC,
      correlationId: generateUuidV7(),
    });

    expect(result.success).toBe(true);

    const db = getDb();
    const [reqRow] = await db
      .select()
      .from(deliveryRequestTable)
      .where(eq(deliveryRequestTable.id, result.data!.requestId));
    expect(reqRow.processedAt).not.toBeNull();
  });

  // ---------------------------------------------------------------------------
  // Channel preference integration
  // ---------------------------------------------------------------------------

  it("should respect channel preferences — email disabled blocks email delivery", async () => {
    const contactId = generateUuidV7();
    mockGeo.setContactEmail(contactId, "pref@example.com");

    // Disable email for this contact (contactId must be valid UUID for Zod validation)
    const prefResult = await handlers.setChannelPreference.handleAsync({
      contactId,
      emailEnabled: false,
      smsEnabled: true,
    });
    expect(prefResult.success).toBe(true);

    // No channel override, non-urgent message — system-decided channels
    // Email is disabled, SMS is enabled but contact has no phone -> no deliverable channels
    const deliverResult = await handlers.deliver.handleAsync({
      senderService: "system",
      title: "Preference Test",
      content: "<p>test</p>",
      plainTextContent: "test",
      recipientContactId: contactId,
      correlationId: generateUuidV7(),
    });

    expect(deliverResult.success).toBe(false);
    expect(deliverResult.statusCode).toBe(404);
    expect(stubEmail.sentCount()).toBe(0);
  });

  it("should read back channel preference via GetChannelPreference", async () => {
    const contactId = generateUuidV7();

    await handlers.setChannelPreference.handleAsync({
      contactId,
      emailEnabled: true,
      smsEnabled: false,
    });

    const result = await handlers.getChannelPreference.handleAsync({
      contactId,
    });

    expect(result.success).toBe(true);
    expect(result.data?.pref).toBeDefined();
    expect(result.data!.pref!.emailEnabled).toBe(true);
    expect(result.data!.pref!.smsEnabled).toBe(false);
  });
});
