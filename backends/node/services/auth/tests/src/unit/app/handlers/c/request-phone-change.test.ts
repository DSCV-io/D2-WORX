import { describe, it, expect, vi, beforeEach } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { D2Result, HttpStatusCode } from "@d2/result";
import { RequestPhoneChange } from "@d2/auth-app";
import type { INotifyHandler } from "@d2/comms-client";

const VALID_USER_ID = "01234567-89ab-cdef-0123-456789abcdef";
const VALID_PHONE = "13213214321";
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
  checkPhoneAvailability: { handleAsync: ReturnType<typeof vi.fn> };
  getUserById: { handleAsync: ReturnType<typeof vi.fn> };
  notify: { handleAsync: ReturnType<typeof vi.fn> };
}

function makeMocks(currentPhone: string | null = null): Mocks {
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
    checkPhoneAvailability: {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { available: true } })),
    },
    getUserById: {
      handleAsync: vi.fn().mockResolvedValue(
        D2Result.ok({
          data: {
            user: {
              id: VALID_USER_ID,
              email: "u@example.com",
              emailVerified: true,
              phone: currentPhone,
              phoneVerified: !!currentPhone,
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
  return { t: vi.fn((_locale: string, key: string) => key) } as never;
}

function makeHandler(mocks: Mocks) {
  return new RequestPhoneChange(
    mocks.passwordVerifier as never,
    mocks.otpRateLimit as never,
    mocks.verificationStore as never,
    mocks.checkPhoneAvailability as never,
    mocks.getUserById as never,
    mocks.notify as unknown as INotifyHandler,
    createMockTranslator(),
    createTestContext(),
  );
}

const validInput = () => ({
  userId: VALID_USER_ID,
  newPhone: VALID_PHONE,
  currentPassword: VALID_PASSWORD,
});

describe("RequestPhoneChange", () => {
  let mocks: Mocks;
  beforeEach(() => {
    mocks = makeMocks();
  });

  // -----------------------------------------------------------------------
  // Validation — phone format strictness
  // -----------------------------------------------------------------------

  it("rejects phone with formatting characters and surfaces a TK key (not raw English)", async () => {
    const result = await makeHandler(mocks).handleAsync({
      ...validInput(),
      newPhone: "+1 (321) 321-4321",
    });
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    // Zod regex error must use a TK key — never leak free-form English to clients.
    const phoneError = result.inputErrors.find(([field]) => field === "newPhone");
    expect(phoneError).toBeDefined();
    expect(phoneError?.[1]).toBe("auth_errors_PHONE_INVALID_FORMAT");
  });

  it("rejects phone shorter than 7 digits", async () => {
    const result = await makeHandler(mocks).handleAsync({
      ...validInput(),
      newPhone: "123456",
    });
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  it("rejects phone longer than 15 digits", async () => {
    const result = await makeHandler(mocks).handleAsync({
      ...validInput(),
      newPhone: "1234567890123456",
    });
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  it("rejects empty password", async () => {
    const result = await makeHandler(mocks).handleAsync({
      ...validInput(),
      currentPassword: "",
    });
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  // -----------------------------------------------------------------------
  // Password gate
  // -----------------------------------------------------------------------

  it("returns 401 when password incorrect, no other state changes", async () => {
    mocks.passwordVerifier.verify.mockResolvedValue(false);
    const result = await makeHandler(mocks).handleAsync(validInput());
    expect(result.statusCode).toBe(HttpStatusCode.Unauthorized);
    expect(mocks.otpRateLimit.recordSend).not.toHaveBeenCalled();
    expect(mocks.notify.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // No-change semantics
  // -----------------------------------------------------------------------

  it("returns PHONE_NO_CHANGE when newPhone equals user's current phone", async () => {
    mocks = makeMocks(VALID_PHONE);
    const result = await makeHandler(mocks).handleAsync(validInput());
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.errorCode).toBe("PHONE_NO_CHANGE");
    expect(mocks.checkPhoneAvailability.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Availability + uniqueness
  // -----------------------------------------------------------------------

  it("returns 409 when phone is already in use", async () => {
    mocks.checkPhoneAvailability.handleAsync.mockResolvedValue(
      D2Result.ok({ data: { available: false } }),
    );
    const result = await makeHandler(mocks).handleAsync(validInput());
    expect(result.statusCode).toBe(HttpStatusCode.Conflict);
    expect(mocks.notify.handleAsync).not.toHaveBeenCalled();
  });

  it("excludes own user from phone uniqueness check", async () => {
    await makeHandler(mocks).handleAsync(validInput());
    expect(mocks.checkPhoneAvailability.handleAsync).toHaveBeenCalledWith({
      phone: VALID_PHONE,
      excludeUserId: VALID_USER_ID,
    });
  });

  // -----------------------------------------------------------------------
  // Rate limiting
  // -----------------------------------------------------------------------

  it("returns 429 when cooldown active", async () => {
    mocks.otpRateLimit.getCooldownSeconds.mockResolvedValue(30);
    const result = await makeHandler(mocks).handleAsync(validInput());
    expect(result.statusCode).toBe(429);
    expect(result.errorCode).toBe("OTP_RATE_LIMITED");
  });

  // -----------------------------------------------------------------------
  // Happy path — SMS specifics
  // -----------------------------------------------------------------------

  it("sends via SMS channel with 5min expiry", async () => {
    const before = Date.now();
    const result = await makeHandler(mocks).handleAsync(validInput());
    const after = Date.now();

    expect(result.success).toBe(true);
    const expiresMs = result.data!.expiresAt.getTime();
    expect(expiresMs).toBeGreaterThanOrEqual(before + 4 * 60 * 1000);
    expect(expiresMs).toBeLessThanOrEqual(after + 6 * 60 * 1000);

    expect(mocks.notify.handleAsync).toHaveBeenCalledTimes(1);
    const call = mocks.notify.handleAsync.mock.calls[0][0];
    expect(call.channels).toEqual(["sms"]);
    expect(call.alternativeContactInfo).toEqual({ phone: VALID_PHONE });
  });

  it("uses 'phone' identifier prefix for verification record", async () => {
    await makeHandler(mocks).handleAsync(validInput());
    const created = mocks.verificationStore.create.mock.calls[0][0];
    expect(created.identifier).toBe(`account-change:phone:${VALID_USER_ID}`);
  });

  // -----------------------------------------------------------------------
  // Comms publish failure
  // -----------------------------------------------------------------------

  it("cleans up the verification record if SMS publish fails", async () => {
    mocks.notify.handleAsync.mockResolvedValue(
      D2Result.fail({ messages: ["sms publish failed"], statusCode: 500 }),
    );
    mocks.verificationStore.findByIdentifier.mockResolvedValueOnce(null).mockResolvedValueOnce({
      id: "v-strand",
      identifier: "x",
      value: "{}",
      expiresAt: new Date(),
    });

    const result = await makeHandler(mocks).handleAsync(validInput());

    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(mocks.verificationStore.deleteById).toHaveBeenCalledWith("v-strand");
  });

  // -----------------------------------------------------------------------
  // Redaction
  // -----------------------------------------------------------------------

  it("declares redaction on newPhone and currentPassword", () => {
    const handler = makeHandler(mocks);
    expect(handler.redaction.inputFields).toContain("newPhone");
    expect(handler.redaction.inputFields).toContain("currentPassword");
  });
});
