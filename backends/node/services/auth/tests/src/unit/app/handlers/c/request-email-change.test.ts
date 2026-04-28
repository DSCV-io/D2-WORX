import { describe, it, expect, vi, beforeEach } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { D2Result, HttpStatusCode } from "@d2/result";
import { RequestEmailChange } from "@d2/auth-app";
import type { INotifyHandler } from "@d2/comms-client";

const VALID_USER_ID = "01234567-89ab-cdef-0123-456789abcdef";
const VALID_EMAIL = "new@example.com";
const VALID_PASSWORD = "hunter2";

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

interface Mocks {
  passwordVerifier: { verify: ReturnType<typeof vi.fn> };
  otpRateLimit: {
    getCooldownSeconds: ReturnType<typeof vi.fn>;
    recordSend: ReturnType<typeof vi.fn>;
    clearOnSuccess: ReturnType<typeof vi.fn>;
  };
  verificationStore: {
    findByIdentifier: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    deleteById: ReturnType<typeof vi.fn>;
    updateValue: ReturnType<typeof vi.fn>;
  };
  checkEmailAvailability: { handleAsync: ReturnType<typeof vi.fn> };
  updateUserEmail: { handleAsync: ReturnType<typeof vi.fn> };
  getUserById: { handleAsync: ReturnType<typeof vi.fn> };
  notify: { handleAsync: ReturnType<typeof vi.fn> };
}

function makeMocks(): Mocks {
  return {
    passwordVerifier: { verify: vi.fn().mockResolvedValue(true) },
    otpRateLimit: {
      getCooldownSeconds: vi.fn().mockResolvedValue(0),
      recordSend: vi.fn().mockResolvedValue(undefined),
      clearOnSuccess: vi.fn().mockResolvedValue(undefined),
    },
    verificationStore: {
      findByIdentifier: vi.fn().mockResolvedValue(null),
      create: vi.fn().mockResolvedValue(undefined),
      deleteById: vi.fn().mockResolvedValue(undefined),
      updateValue: vi.fn().mockResolvedValue(undefined),
    },
    checkEmailAvailability: {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { available: true } })),
    },
    updateUserEmail: { handleAsync: vi.fn() },
    getUserById: {
      handleAsync: vi.fn().mockResolvedValue(
        D2Result.ok({
          data: {
            user: {
              id: VALID_USER_ID,
              email: "u@example.com",
              emailVerified: true,
              phone: null,
              phoneVerified: false,
              locale: "en-US",
            },
          },
        }),
      ),
    },
    notify: { handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })) },
  };
}

function createMockTranslator() {
  // Echo the key with interpolations applied — lets assertions verify that the
  // OTP code was actually interpolated into the message body (e.g. /\d{6}/).
  return {
    t: vi.fn((_locale: string, key: string, args?: Record<string, unknown>) => {
      if (!args) return key;
      const flat = Object.entries(args)
        .map(([k, v]) => `${k}=${String(v)}`)
        .join(" ");
      return `${key}[${flat}]`;
    }),
  } as never;
}

function makeHandler(mocks: Mocks) {
  return new RequestEmailChange(
    mocks.passwordVerifier as never,
    mocks.otpRateLimit as never,
    mocks.verificationStore as never,
    mocks.checkEmailAvailability as never,
    mocks.updateUserEmail as never,
    mocks.getUserById as never,
    mocks.notify as unknown as INotifyHandler,
    createMockTranslator(),
    createTestContext(),
  );
}

const validInput = () => ({
  userId: VALID_USER_ID,
  newEmail: VALID_EMAIL,
  currentPassword: VALID_PASSWORD,
});

describe("RequestEmailChange", () => {
  let mocks: Mocks;
  beforeEach(() => {
    mocks = makeMocks();
  });

  // -----------------------------------------------------------------------
  // Validation
  // -----------------------------------------------------------------------

  it("rejects invalid userId", async () => {
    const result = await makeHandler(mocks).handleAsync({
      ...validInput(),
      userId: "not-a-uuid",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(mocks.passwordVerifier.verify).not.toHaveBeenCalled();
  });

  it("rejects malformed email", async () => {
    const result = await makeHandler(mocks).handleAsync({
      ...validInput(),
      newEmail: "not-an-email",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(mocks.passwordVerifier.verify).not.toHaveBeenCalled();
  });

  it("rejects empty password", async () => {
    const result = await makeHandler(mocks).handleAsync({
      ...validInput(),
      currentPassword: "",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  it("rejects email exceeding 254 chars", async () => {
    const result = await makeHandler(mocks).handleAsync({
      ...validInput(),
      newEmail: "a".repeat(250) + "@x.com", // 256 chars total — exceeds 254 limit
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  // -----------------------------------------------------------------------
  // Password gate (CRITICAL — wrong password = no state changes, no OTP)
  // -----------------------------------------------------------------------

  it("returns 401 when password is incorrect AND does not touch any other infra", async () => {
    mocks.passwordVerifier.verify.mockResolvedValue(false);

    const result = await makeHandler(mocks).handleAsync(validInput());

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.Unauthorized);
    // No OTP issued, no rate-limit consumed, no email sent.
    expect(mocks.otpRateLimit.getCooldownSeconds).not.toHaveBeenCalled();
    expect(mocks.otpRateLimit.recordSend).not.toHaveBeenCalled();
    expect(mocks.verificationStore.create).not.toHaveBeenCalled();
    expect(mocks.notify.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Rate limiting
  // -----------------------------------------------------------------------

  it("returns 429 when cooldown is active", async () => {
    mocks.otpRateLimit.getCooldownSeconds.mockResolvedValue(15);

    const result = await makeHandler(mocks).handleAsync(validInput());

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(429);
    expect(result.errorCode).toBe("OTP_RATE_LIMITED");
    expect(mocks.checkEmailAvailability.handleAsync).not.toHaveBeenCalled();
    expect(mocks.notify.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Email availability
  // -----------------------------------------------------------------------

  it("returns 409 when email is already in use", async () => {
    mocks.checkEmailAvailability.handleAsync.mockResolvedValue(
      D2Result.ok({ data: { available: false } }),
    );

    const result = await makeHandler(mocks).handleAsync(validInput());

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.Conflict);
    expect(mocks.verificationStore.create).not.toHaveBeenCalled();
    expect(mocks.notify.handleAsync).not.toHaveBeenCalled();
  });

  it("bubbles upstream failures from checkEmailAvailability", async () => {
    mocks.checkEmailAvailability.handleAsync.mockResolvedValue(
      D2Result.fail({ messages: ["db down"], statusCode: 500 }),
    );

    const result = await makeHandler(mocks).handleAsync(validInput());

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(500);
  });

  // -----------------------------------------------------------------------
  // Replace-on-request semantics
  // -----------------------------------------------------------------------

  it("deletes any existing pending record before creating a new one", async () => {
    mocks.verificationStore.findByIdentifier.mockResolvedValue({
      id: "v-old",
      identifier: "x",
      value: "{}",
      expiresAt: new Date(Date.now() + 100000),
    });

    const result = await makeHandler(mocks).handleAsync(validInput());

    expect(result.success).toBe(true);
    expect(mocks.verificationStore.deleteById).toHaveBeenCalledWith("v-old");
    expect(mocks.verificationStore.create).toHaveBeenCalledTimes(1);
  });

  // -----------------------------------------------------------------------
  // Happy path
  // -----------------------------------------------------------------------

  it("creates a verification record, records send, sends OTP, returns expiresAt", async () => {
    const before = Date.now();
    const result = await makeHandler(mocks).handleAsync(validInput());
    const after = Date.now();

    expect(result.success).toBe(true);
    expect(result.data?.expiresAt).toBeInstanceOf(Date);
    // 15-minute expiry.
    const expiresMs = result.data!.expiresAt.getTime();
    expect(expiresMs).toBeGreaterThanOrEqual(before + 14 * 60 * 1000);
    expect(expiresMs).toBeLessThanOrEqual(after + 16 * 60 * 1000);

    expect(mocks.verificationStore.create).toHaveBeenCalledTimes(1);
    const created = mocks.verificationStore.create.mock.calls[0][0];
    expect(created.identifier).toBe(`account-change:email:${VALID_USER_ID}`);

    expect(mocks.otpRateLimit.recordSend).toHaveBeenCalledWith(VALID_USER_ID, "email");

    expect(mocks.notify.handleAsync).toHaveBeenCalledTimes(1);
    const notifyCall = mocks.notify.handleAsync.mock.calls[0][0];
    expect(notifyCall.channels).toEqual(["email"]);
    expect(notifyCall.alternativeContactInfo).toEqual({ email: VALID_EMAIL });
    expect(notifyCall.senderService).toBe("auth");
    expect(notifyCall.content).toMatch(/\d{6}/); // contains 6-digit code
  });

  // -----------------------------------------------------------------------
  // Comms publish failure → rollback verification record
  // -----------------------------------------------------------------------

  it("cleans up the verification record if Comms publish fails", async () => {
    mocks.notify.handleAsync.mockResolvedValue(
      D2Result.fail({ messages: ["publish failed"], statusCode: 500 }),
    );
    mocks.verificationStore.findByIdentifier
      .mockResolvedValueOnce(null) // first call: existing check
      .mockResolvedValueOnce({
        // second call after publish failure: cleanup lookup
        id: "v-new",
        identifier: "x",
        value: "{}",
        expiresAt: new Date(),
      });

    const result = await makeHandler(mocks).handleAsync(validInput());

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(mocks.verificationStore.deleteById).toHaveBeenCalledWith("v-new");
  });

  // -----------------------------------------------------------------------
  // Redaction
  // -----------------------------------------------------------------------

  it("declares redaction on newEmail and currentPassword", () => {
    const handler = makeHandler(mocks);
    expect(handler.redaction.inputFields).toContain("newEmail");
    expect(handler.redaction.inputFields).toContain("currentPassword");
  });
});
