import { describe, it, expect, beforeEach, vi } from "vitest";
import { D2Result } from "@d2/result";
import { Deliver, EmailDispatcher, SmsDispatcher } from "@d2/comms-app";
import { RecipientResolver } from "@d2/comms-app";
import {
  createMockContext,
  createMockMessageRepo,
  createMockRequestRepo,
  createMockAttemptRepo,
  createMockChannelPrefRepo,
  createMockEmailProvider,
  createMockGetContactsByIds,
} from "../helpers/mock-handlers.js";

// Stable UUIDs for test data (deterministic, avoid random generation in tests)
const CONTACT_1 = "a0000000-0000-0000-0000-000000000001";
const CONTACT_SMS = "a0000000-0000-0000-0000-000000000002";
const CONTACT_PREF = "a0000000-0000-0000-0000-000000000003";
const CONTACT_DUAL = "a0000000-0000-0000-0000-000000000004";
const CONTACT_999 = "a0000000-0000-0000-0000-000000000999";
const CORR_1 = "b0000000-0000-0000-0000-000000000001";
const CORR_DUP = "b0000000-0000-0000-0000-000000000002";
const CORR_NO_ADDR = "b0000000-0000-0000-0000-000000000003";
const CORR_FAIL = "b0000000-0000-0000-0000-000000000004";
const CORR_PERSIST = "b0000000-0000-0000-0000-000000000005";
const CORR_SMS = "b0000000-0000-0000-0000-000000000006";
const CORR_CONTACT_PREF = "b0000000-0000-0000-0000-000000000007";
const CORR_RESOLVER_FAIL = "b0000000-0000-0000-0000-000000000008";
const CORR_MSG_FAIL = "b0000000-0000-0000-0000-000000000009";
const CORR_REQ_FAIL = "b0000000-0000-0000-0000-00000000000a";
const CORR_DUAL = "b0000000-0000-0000-0000-00000000000b";
const CORR_PROVIDER_THROW = "b0000000-0000-0000-0000-00000000000c";

describe("Deliver", () => {
  let deliver: Deliver;
  let messageRepo: ReturnType<typeof createMockMessageRepo>;
  let requestRepo: ReturnType<typeof createMockRequestRepo>;
  let attemptRepo: ReturnType<typeof createMockAttemptRepo>;
  let channelPrefRepo: ReturnType<typeof createMockChannelPrefRepo>;
  let emailProvider: ReturnType<typeof createMockEmailProvider>;
  let recipientResolver: RecipientResolver;

  beforeEach(() => {
    const context = createMockContext();
    messageRepo = createMockMessageRepo();
    requestRepo = createMockRequestRepo();
    attemptRepo = createMockAttemptRepo();
    channelPrefRepo = createMockChannelPrefRepo();
    emailProvider = createMockEmailProvider();

    const geoByIdHandler = createMockGetContactsByIds();
    recipientResolver = new RecipientResolver(geoByIdHandler as any, context);

    deliver = new Deliver(
      {
        message: messageRepo,
        request: requestRepo,
        attempt: attemptRepo,
        channelPref: channelPrefRepo,
      },
      [new EmailDispatcher(emailProvider)],
      recipientResolver,
      context,
    );
  });

  it("should deliver email successfully", async () => {
    const result = await deliver.handleAsync({
      senderService: "auth",
      title: "Test Email",
      content: "<p>Hello</p>",
      plainTextContent: "Hello",
      channels: ["email"],
      recipientContactId: CONTACT_1,
      correlationId: CORR_1,
    });

    expect(result.success).toBe(true);
    expect(result.data).toBeDefined();
    expect(result.data!.messageId).toBeDefined();
    expect(result.data!.requestId).toBeDefined();
    expect(result.data!.attempts).toHaveLength(1);
    expect(result.data!.attempts[0].status).toBe("sent");
    expect(result.data!.attempts[0].recipientAddress).toBe("user@example.com");
    expect(result.data!.attempts[0].providerMessageId).toBe("resend-msg-123");
  });

  it("should return existing result for duplicate correlationId", async () => {
    // Mock findByCorrelationId returning an existing request
    (requestRepo.findByCorrelationId.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({
        data: {
          request: {
            id: "existing-req",
            messageId: "existing-msg",
            correlationId: CORR_DUP,
            recipientContactId: CONTACT_1,
            callbackTopic: null,
            createdAt: new Date(),
            processedAt: null,
          },
        },
      }),
    );

    const result = await deliver.handleAsync({
      senderService: "auth",
      title: "Test",
      content: "test",
      plainTextContent: "test",
      recipientContactId: CONTACT_1,
      correlationId: CORR_DUP,
    });

    expect(result.success).toBe(true);
    expect(result.data!.requestId).toBe("existing-req");
    // Should NOT create new message or request
    expect(messageRepo.create.handleAsync).not.toHaveBeenCalled();
  });

  it("should return NOT_FOUND when no deliverable address", async () => {
    // Mock geo returning empty contacts
    const emptyGeoById = {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { data: new Map() } })),
    };
    const context = createMockContext();
    const resolver = new RecipientResolver(emptyGeoById as any, context);
    const deliverNoAddr = new Deliver(
      {
        message: messageRepo,
        request: requestRepo,
        attempt: attemptRepo,
        channelPref: channelPrefRepo,
      },
      [new EmailDispatcher(emailProvider)],
      resolver,
      context,
    );

    const result = await deliverNoAddr.handleAsync({
      senderService: "auth",
      title: "Test",
      content: "test",
      plainTextContent: "test",
      recipientContactId: CONTACT_999,
      correlationId: CORR_NO_ADDR,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(404);
  });

  it("should return DELIVERY_FAILED when email send fails with retryable error", async () => {
    (emailProvider.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.fail({ messages: ["SMTP timeout"], statusCode: 502 }),
    );

    const result = await deliver.handleAsync({
      senderService: "auth",
      title: "Test",
      content: "test",
      plainTextContent: "test",
      channels: ["email"],
      recipientContactId: CONTACT_1,
      correlationId: CORR_FAIL,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(503);
    expect(result.errorCode).toBe("DELIVERY_FAILED");
  });

  it("should persist message and delivery request", async () => {
    await deliver.handleAsync({
      senderService: "auth",
      title: "Test",
      content: "test",
      plainTextContent: "test",
      recipientContactId: CONTACT_1,
      correlationId: CORR_PERSIST,
    });

    expect(messageRepo.create.handleAsync).toHaveBeenCalledOnce();
    expect(requestRepo.create.handleAsync).toHaveBeenCalledOnce();
    // contact-1 has both email + phone; empty channels + normal urgency -> email + sms resolved,
    // but SMS dispatcher not registered so only email attempt is created
    expect(attemptRepo.create.handleAsync).toHaveBeenCalledTimes(1);
  });

  it("should skip SMS attempt entirely when no SMS dispatcher configured", async () => {
    // Default Deliver has no SMS dispatcher — only email
    // Mock geo to return a contact with phone only (no email)
    const contactsWithPhone = new Map();
    contactsWithPhone.set(CONTACT_SMS, {
      id: CONTACT_SMS,
      contextKey: "auth_user",
      relatedEntityId: "user-sms",
      contactMethods: {
        emails: [],
        phoneNumbers: [{ value: "+15551234567", labels: [] }],
      },
      personalDetails: undefined,
      professionalDetails: undefined,
      location: undefined,
      createdAt: new Date(),
    });

    const geoById = {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { data: contactsWithPhone } })),
    };

    const context = createMockContext();
    const resolverSms = new RecipientResolver(geoById as any, context);

    const smsDeliver = new Deliver(
      {
        message: messageRepo,
        request: requestRepo,
        attempt: attemptRepo,
        channelPref: channelPrefRepo,
      },
      [new EmailDispatcher(emailProvider)], // No SMS dispatcher
      resolverSms,
      context,
    );

    const result = await smsDeliver.handleAsync({
      senderService: "auth",
      title: "SMS Test",
      content: "test",
      plainTextContent: "test",
      recipientContactId: CONTACT_SMS,
      correlationId: CORR_SMS,
    });

    // SMS channel skipped entirely — no dispatcher configured
    // No deliverable channels remain → NOT_FOUND
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(404);
    // No attempt persisted
    expect(attemptRepo.create.handleAsync).not.toHaveBeenCalled();
  });

  it("should look up channel prefs by contactId", async () => {
    const contactMap = new Map();
    contactMap.set(CONTACT_PREF, {
      id: CONTACT_PREF,
      contextKey: "auth_org_invitation",
      relatedEntityId: "inv-1",
      contactMethods: {
        emails: [{ value: "pref@example.com", labels: [] }],
        phoneNumbers: [],
      },
      personalDetails: undefined,
      professionalDetails: undefined,
      location: undefined,
      createdAt: new Date(),
    });

    const geoById = {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { data: contactMap } })),
    };

    const context = createMockContext();
    const resolverContact = new RecipientResolver(geoById as any, context);

    const contactDeliver = new Deliver(
      {
        message: messageRepo,
        request: requestRepo,
        attempt: attemptRepo,
        channelPref: channelPrefRepo,
      },
      [new EmailDispatcher(emailProvider)],
      resolverContact,
      context,
    );

    await contactDeliver.handleAsync({
      senderService: "auth",
      title: "Test",
      content: "test",
      plainTextContent: "test",
      recipientContactId: CONTACT_PREF,
      correlationId: CORR_CONTACT_PREF,
    });

    // Should call findByContactId
    expect(channelPrefRepo.findByContactId.handleAsync).toHaveBeenCalledWith({
      contactId: CONTACT_PREF,
    });
  });

  // -------------------------------------------------------------------------
  // Input Validation tests
  // -------------------------------------------------------------------------

  it("should reject missing correlationId with validation failure", async () => {
    const result = await deliver.handleAsync({
      senderService: "auth",
      title: "Test",
      content: "test",
      plainTextContent: "test",
      recipientContactId: CONTACT_1,
      correlationId: undefined as any,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
    expect(result.inputErrors.length).toBeGreaterThan(0);
    // Should NOT attempt any repo operations
    expect(messageRepo.create.handleAsync).not.toHaveBeenCalled();
  });

  it("should reject non-UUID correlationId with validation failure", async () => {
    const result = await deliver.handleAsync({
      senderService: "auth",
      title: "Test",
      content: "test",
      plainTextContent: "test",
      recipientContactId: CONTACT_1,
      correlationId: "not-a-uuid",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
    expect(result.inputErrors.length).toBeGreaterThan(0);
    const fields = result.inputErrors.map(([field]) => field);
    expect(fields).toContain("correlationId");
    expect(messageRepo.create.handleAsync).not.toHaveBeenCalled();
  });

  it("should reject non-UUID recipientContactId with validation failure", async () => {
    const result = await deliver.handleAsync({
      senderService: "auth",
      title: "Test",
      content: "test",
      plainTextContent: "test",
      recipientContactId: "not-a-uuid",
      correlationId: CORR_1,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
    const fields = result.inputErrors.map(([field]) => field);
    expect(fields).toContain("recipientContactId");
  });

  it("should reject empty title with validation failure", async () => {
    const result = await deliver.handleAsync({
      senderService: "auth",
      title: "",
      content: "test",
      plainTextContent: "test",
      recipientContactId: CONTACT_1,
      correlationId: CORR_1,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
    const fields = result.inputErrors.map(([field]) => field);
    expect(fields).toContain("title");
  });

  it("should reject empty content with validation failure", async () => {
    const result = await deliver.handleAsync({
      senderService: "auth",
      title: "Test",
      content: "",
      plainTextContent: "test",
      recipientContactId: CONTACT_1,
      correlationId: CORR_1,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
    const fields = result.inputErrors.map(([field]) => field);
    expect(fields).toContain("content");
  });

  it("should pass validation with valid UUID inputs", async () => {
    const result = await deliver.handleAsync({
      senderService: "auth",
      title: "Valid Test",
      content: "valid content",
      plainTextContent: "valid content",
      recipientContactId: CONTACT_1,
      correlationId: CORR_1,
    });

    // Validation passes, delivery proceeds
    expect(result.success).toBe(true);
    expect(result.data).toBeDefined();
  });

  // -------------------------------------------------------------------------
  // Defensive / Security tests
  // -------------------------------------------------------------------------

  it("should propagate resolver failure (e.g. geo 503) to caller", async () => {
    const geoIdsFailing = {
      handleAsync: vi
        .fn()
        .mockResolvedValue(D2Result.fail({ messages: ["Geo down"], statusCode: 503 })),
    };

    const context = createMockContext();
    const failingResolver = new RecipientResolver(geoIdsFailing as any, context);

    const failDeliver = new Deliver(
      {
        message: messageRepo,
        request: requestRepo,
        attempt: attemptRepo,
        channelPref: channelPrefRepo,
      },
      [new EmailDispatcher(emailProvider)],
      failingResolver,
      context,
    );

    const result = await failDeliver.handleAsync({
      senderService: "auth",
      title: "Test",
      content: "test",
      plainTextContent: "test",
      recipientContactId: CONTACT_1,
      correlationId: CORR_RESOLVER_FAIL,
    });

    // Resolver bubbles geo failure through — Deliver propagates it
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(503);
  });

  it("should propagate message repo failure as a fail result", async () => {
    // Override message create to fail
    (messageRepo.create.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.fail({ messages: ["DB write failed"], statusCode: 503 }),
    );

    const result = await deliver.handleAsync({
      senderService: "auth",
      title: "Test",
      content: "test",
      plainTextContent: "test",
      recipientContactId: CONTACT_1,
      correlationId: CORR_MSG_FAIL,
    });

    expect(result.success).toBe(false);
    // Should NOT attempt delivery if message creation failed
    expect(emailProvider.handleAsync).not.toHaveBeenCalled();
  });

  it("should propagate request repo failure as a fail result", async () => {
    // Override request create to fail
    (requestRepo.create.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.fail({ messages: ["DB write failed"], statusCode: 503 }),
    );

    const result = await deliver.handleAsync({
      senderService: "auth",
      title: "Test",
      content: "test",
      plainTextContent: "test",
      recipientContactId: CONTACT_1,
      correlationId: CORR_REQ_FAIL,
    });

    expect(result.success).toBe(false);
    expect(emailProvider.handleAsync).not.toHaveBeenCalled();
  });

  it("should return DELIVERY_FAILED when email provider throws an Error", async () => {
    // Mock email provider that throws (simulates SMTP timeout / network failure)
    (emailProvider.handleAsync as ReturnType<typeof vi.fn>).mockRejectedValue(
      new Error("SMTP timeout"),
    );

    const result = await deliver.handleAsync({
      senderService: "auth",
      title: "Test",
      content: "test",
      plainTextContent: "test",
      channels: ["email"],
      recipientContactId: CONTACT_1,
      correlationId: CORR_PROVIDER_THROW,
    });

    // Deliver handler catches the rejected promise via Promise.allSettled
    // and returns DELIVERY_FAILED for retryable failures
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(503);
    expect(result.errorCode).toBe("DELIVERY_FAILED");

    // Attempt should still be persisted with failed status and the error message
    expect(attemptRepo.create.handleAsync).toHaveBeenCalledOnce();
    expect(attemptRepo.updateStatus.handleAsync).toHaveBeenCalledWith(
      expect.objectContaining({
        status: "failed",
        error: "SMTP timeout",
      }),
    );
  });

  it("should handle both email and sms channels in single request", async () => {
    // Mock geo to return contact with both email and phone
    const contactMap = new Map();
    contactMap.set(CONTACT_DUAL, {
      id: CONTACT_DUAL,
      contextKey: "auth_user",
      relatedEntityId: "user-dual",
      contactMethods: {
        emails: [{ value: "dual@example.com", labels: [] }],
        phoneNumbers: [{ value: "+15551234567", labels: [] }],
      },
      personalDetails: undefined,
      professionalDetails: undefined,
      location: undefined,
      createdAt: new Date(),
    });

    const geoById = {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { data: contactMap } })),
    };

    const context = createMockContext();
    const dualResolver = new RecipientResolver(geoById as any, context);

    const mockSmsProvider = {
      handleAsync: vi
        .fn()
        .mockResolvedValue(D2Result.ok({ data: { providerMessageId: "twilio-123" } })),
    };

    const dualDeliver = new Deliver(
      {
        message: messageRepo,
        request: requestRepo,
        attempt: attemptRepo,
        channelPref: channelPrefRepo,
      },
      [new EmailDispatcher(emailProvider), new SmsDispatcher(mockSmsProvider as any)],
      dualResolver,
      context,
    );

    const result = await dualDeliver.handleAsync({
      senderService: "auth",
      title: "Dual Channel",
      content: "test",
      plainTextContent: "test",
      recipientContactId: CONTACT_DUAL,
      correlationId: CORR_DUAL,
    });

    expect(result.success).toBe(true);
    expect(result.data!.attempts).toHaveLength(2);
    const channels = result.data!.attempts.map((a) => a.channel);
    expect(channels).toContain("email");
    expect(channels).toContain("sms");
  });

  // -------------------------------------------------------------------------
  // alternativeContactInfo (transient sends)
  // -------------------------------------------------------------------------

  describe("alternativeContactInfo", () => {
    const CORR_ALT_EMAIL = "b0000000-0000-0000-0000-00000000ae01";
    const CORR_ALT_SMS = "b0000000-0000-0000-0000-00000000ae02";
    const CORR_ALT_BOTH = "b0000000-0000-0000-0000-00000000ae03";

    it("should deliver to alternativeContactInfo email without calling Geo resolver", async () => {
      const resolverSpy = vi.spyOn(recipientResolver, "handleAsync");

      const result = await deliver.handleAsync({
        senderService: "auth",
        title: "Verify your new email",
        content: "Code: 123456",
        plainTextContent: "Code: 123456",
        channels: ["email"],
        alternativeContactInfo: { email: "new@example.com" },
        correlationId: CORR_ALT_EMAIL,
      });

      expect(result.success).toBe(true);
      expect(result.data!.attempts).toHaveLength(1);
      expect(result.data!.attempts[0].channel).toBe("email");
      expect(result.data!.attempts[0].recipientAddress).toBe("new@example.com");
      expect(result.data!.attempts[0].status).toBe("sent");

      // Geo lookup must NOT happen for transient sends
      expect(resolverSpy).not.toHaveBeenCalled();
      // Channel preferences must NOT be looked up either
      expect(channelPrefRepo.findByContactId.handleAsync).not.toHaveBeenCalled();
    });

    it("should persist DeliveryRequest with null recipientContactId for transient sends", async () => {
      await deliver.handleAsync({
        senderService: "auth",
        title: "Test",
        content: "Hi",
        plainTextContent: "Hi",
        channels: ["email"],
        alternativeContactInfo: { email: "transient@example.com" },
        correlationId: CORR_ALT_BOTH,
      });

      const createCall = (requestRepo.create.handleAsync as ReturnType<typeof vi.fn>).mock.calls[0];
      expect(createCall[0].request.recipientContactId).toBeUndefined();
    });

    it("should reject when both recipientContactId and alternativeContactInfo are provided", async () => {
      const result = await deliver.handleAsync({
        senderService: "auth",
        title: "Test",
        content: "Hi",
        plainTextContent: "Hi",
        recipientContactId: CONTACT_1,
        alternativeContactInfo: { email: "new@example.com" },
        correlationId: CORR_ALT_SMS,
      });

      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(400);
      expect(result.errorCode).toBe("VALIDATION_FAILED");
    });

    it("should reject when neither recipientContactId nor alternativeContactInfo is provided", async () => {
      const result = await deliver.handleAsync({
        senderService: "auth",
        title: "Test",
        content: "Hi",
        plainTextContent: "Hi",
        correlationId: "b0000000-0000-0000-0000-00000000ae04",
      });

      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(400);
      expect(result.errorCode).toBe("VALIDATION_FAILED");
    });

    it("should return notFound when alternativeContactInfo provides no usable address for requested channel", async () => {
      const result = await deliver.handleAsync({
        senderService: "auth",
        title: "Test",
        content: "Hi",
        plainTextContent: "Hi",
        channels: ["sms"], // SMS requested
        alternativeContactInfo: { email: "only-email@example.com" }, // but only email provided
        correlationId: "b0000000-0000-0000-0000-00000000ae05",
      });

      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(404);
    });
  });
});
