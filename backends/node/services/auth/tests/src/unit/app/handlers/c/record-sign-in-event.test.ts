import { describe, it, expect, vi, beforeEach } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { D2Result, HttpStatusCode } from "@d2/result";
import { RecordSignInEvent } from "@d2/auth-app";
import type { ICreateSignInEventHandler } from "@d2/auth-app";

function createTestContext() {
  const request: IRequestContext = {
    traceId: "trace-test",
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

function createMockCreateRecord() {
  return { handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })) };
}

describe("RecordSignInEvent", () => {
  let createRecord: ReturnType<typeof createMockCreateRecord>;
  let handler: RecordSignInEvent;

  beforeEach(() => {
    createRecord = createMockCreateRecord();
    handler = new RecordSignInEvent(
      createRecord as unknown as ICreateSignInEventHandler,
      createTestContext(),
    );
  });

  it("should record a sign-in event and return success", async () => {
    const result = await handler.handleAsync({
      userId: "01234567-89ab-cdef-0123-456789abcdef",
      successful: true,
      ipAddress: "192.168.1.1",
      userAgent: "Mozilla/5.0",
      whoIsId: "abcdef0123456789abcdef0123456789",
    });

    expect(result.success).toBe(true);
    expect(result.data?.event).toBeDefined();
    expect(result.data?.event.userId).toBe("01234567-89ab-cdef-0123-456789abcdef");
    expect(result.data?.event.successful).toBe(true);
    expect(result.data?.event.ipAddress).toBe("192.168.1.1");
    expect(result.data?.event.userAgent).toBe("Mozilla/5.0");
    expect(result.data?.event.whoIsId).toBe("abcdef0123456789abcdef0123456789");
    expect(createRecord.handleAsync).toHaveBeenCalledOnce();
  });

  it("should set whoIsId to undefined when not provided", async () => {
    const result = await handler.handleAsync({
      userId: "01234567-89ab-cdef-0123-456789abcdef",
      successful: false,
      ipAddress: "10.0.0.1",
      userAgent: "curl/7.0",
    });

    expect(result.success).toBe(true);
    expect(result.data?.event.whoIsId).toBeUndefined();
  });

  it("should record deviceFingerprint when provided", async () => {
    const result = await handler.handleAsync({
      userId: "01234567-89ab-cdef-0123-456789abcdef",
      successful: true,
      ipAddress: "192.168.1.1",
      userAgent: "Mozilla/5.0",
      deviceFingerprint: "a".repeat(64),
    });

    expect(result.success).toBe(true);
    expect(result.data?.event.deviceFingerprint).toBe("a".repeat(64));
  });

  it("should record clientFingerprint and serverFingerprint when provided", async () => {
    const result = await handler.handleAsync({
      userId: "01234567-89ab-cdef-0123-456789abcdef",
      successful: true,
      ipAddress: "192.168.1.1",
      userAgent: "Mozilla/5.0",
      deviceFingerprint: "d".repeat(64),
      clientFingerprint: "c".repeat(64),
      serverFingerprint: "s".repeat(64),
    });

    expect(result.success).toBe(true);
    expect(result.data?.event.deviceFingerprint).toBe("d".repeat(64));
    expect(result.data?.event.clientFingerprint).toBe("c".repeat(64));
    expect(result.data?.event.serverFingerprint).toBe("s".repeat(64));
    // The repo handler must receive all three so they land in the
    // sign_in_event row. Without this, the security tab can't render the
    // identicon (clientFp) or display the per-network forensic chip
    // (serverFp).
    expect(createRecord.handleAsync).toHaveBeenCalledWith({
      event: expect.objectContaining({
        deviceFingerprint: "d".repeat(64),
        clientFingerprint: "c".repeat(64),
        serverFingerprint: "s".repeat(64),
      }),
    });
  });

  it("should reject clientFingerprint exceeding 64 characters", async () => {
    const result = await handler.handleAsync({
      userId: "01234567-89ab-cdef-0123-456789abcdef",
      successful: true,
      ipAddress: "10.0.0.1",
      userAgent: "TestAgent",
      clientFingerprint: "x".repeat(65),
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(createRecord.handleAsync).not.toHaveBeenCalled();
  });

  it("should reject serverFingerprint exceeding 64 characters", async () => {
    const result = await handler.handleAsync({
      userId: "01234567-89ab-cdef-0123-456789abcdef",
      successful: true,
      ipAddress: "10.0.0.1",
      userAgent: "TestAgent",
      serverFingerprint: "x".repeat(65),
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(createRecord.handleAsync).not.toHaveBeenCalled();
  });

  it("should set deviceFingerprint to undefined when not provided", async () => {
    const result = await handler.handleAsync({
      userId: "01234567-89ab-cdef-0123-456789abcdef",
      successful: true,
      ipAddress: "10.0.0.1",
      userAgent: "TestAgent",
    });

    expect(result.success).toBe(true);
    expect(result.data?.event.deviceFingerprint).toBeUndefined();
  });

  it("should reject deviceFingerprint exceeding 64 characters", async () => {
    const result = await handler.handleAsync({
      userId: "01234567-89ab-cdef-0123-456789abcdef",
      successful: true,
      ipAddress: "10.0.0.1",
      userAgent: "TestAgent",
      deviceFingerprint: "x".repeat(65),
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(createRecord.handleAsync).not.toHaveBeenCalled();
  });

  it("should generate a UUIDv7 id for the event", async () => {
    const result = await handler.handleAsync({
      userId: "01234567-89ab-cdef-0123-456789abcdef",
      successful: true,
      ipAddress: "10.0.0.1",
      userAgent: "TestAgent",
    });

    expect(result.success).toBe(true);
    expect(result.data?.event.id).toBeDefined();
    expect(result.data?.event.id.length).toBeGreaterThan(0);
  });

  it("should pass the domain event to the repository", async () => {
    await handler.handleAsync({
      userId: "abcdef01-2345-6789-abcd-ef0123456789",
      successful: true,
      ipAddress: "172.16.0.1",
      userAgent: "Bot/1.0",
    });

    expect(createRecord.handleAsync).toHaveBeenCalledWith({
      event: expect.objectContaining({
        userId: "abcdef01-2345-6789-abcd-ef0123456789",
        ipAddress: "172.16.0.1",
      }),
    });
  });

  // -----------------------------------------------------------------------
  // Zod validation tests (gap #6)
  // -----------------------------------------------------------------------

  it("should return validationFailed when userId is not a valid UUID", async () => {
    const result = await handler.handleAsync({
      userId: "not-a-uuid",
      successful: true,
      ipAddress: "10.0.0.1",
      userAgent: "TestAgent",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(createRecord.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when ipAddress exceeds 45 characters", async () => {
    const result = await handler.handleAsync({
      userId: "01234567-89ab-cdef-0123-456789abcdef",
      successful: true,
      ipAddress: "x".repeat(46),
      userAgent: "TestAgent",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(createRecord.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when userAgent exceeds 512 characters", async () => {
    const result = await handler.handleAsync({
      userId: "01234567-89ab-cdef-0123-456789abcdef",
      successful: true,
      ipAddress: "10.0.0.1",
      userAgent: "x".repeat(513),
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(createRecord.handleAsync).not.toHaveBeenCalled();
  });

  it("should bubble failure when repo create returns error", async () => {
    createRecord.handleAsync = vi.fn().mockResolvedValue(
      D2Result.fail({
        messages: ["connection lost"],
        statusCode: HttpStatusCode.InternalServerError,
      }),
    );

    const result = await handler.handleAsync({
      userId: "01234567-89ab-cdef-0123-456789abcdef",
      successful: true,
      ipAddress: "10.0.0.1",
      userAgent: "TestAgent",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.InternalServerError);
    expect(createRecord.handleAsync).toHaveBeenCalledOnce();
  });

  it("should accept valid input at max field lengths", async () => {
    const result = await handler.handleAsync({
      userId: "01234567-89ab-cdef-0123-456789abcdef",
      successful: false,
      ipAddress: "x".repeat(45),
      userAgent: "x".repeat(512),
      whoIsId: "x".repeat(64),
    });

    expect(result.success).toBe(true);
    expect(createRecord.handleAsync).toHaveBeenCalledOnce();
  });

  it("should define redaction spec that redacts PII fields", () => {
    expect(handler.redaction).toBeDefined();
    expect(handler.redaction?.inputFields).toContain("ipAddress");
    expect(handler.redaction?.inputFields).toContain("userAgent");
    expect(handler.redaction?.suppressOutput).toBe(true);
  });
});
