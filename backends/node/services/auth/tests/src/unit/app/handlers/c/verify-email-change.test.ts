import { describe, it, expect, vi, beforeEach } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { D2Result, HttpStatusCode } from "@d2/result";
import { VerifyEmailChange } from "@d2/auth-app";
import {
  encodePendingValue,
  hashOtpCode,
  pendingChangeIdentifier,
  OTP_VERIFY,
} from "@d2/auth-domain";
import type { INotifyHandler } from "@d2/comms-client";
import type { Complex, Queries as GeoQueries } from "@d2/geo-client";

const VALID_USER_ID = "01234567-89ab-cdef-0123-456789abcdef";
const NEW_EMAIL = "new@example.com";
const OLD_EMAIL = "old@example.com";
const VALID_CODE = "123456";

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
  verificationStore: {
    findByIdentifier: ReturnType<typeof vi.fn>;
    deleteById: ReturnType<typeof vi.fn>;
    updateValue: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
  };
  otpRateLimit: { clearOnSuccess: ReturnType<typeof vi.fn> };
  updateUserEmailRepo: { handleAsync: ReturnType<typeof vi.fn> };
  getUserById: { handleAsync: ReturnType<typeof vi.fn> };
  getContactsByExtKeys: GeoQueries.IGetContactsByExtKeysHandler;
  updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler;
  notify: { handleAsync: ReturnType<typeof vi.fn> };
}

function makeMocks(opts?: { storedValue?: string; expiresAt?: Date }): Mocks {
  const validStored = encodePendingValue({
    codeHash: hashOtpCode(VALID_CODE),
    pendingValue: NEW_EMAIL,
    attempts: 0,
  });
  return {
    verificationStore: {
      findByIdentifier: vi.fn().mockResolvedValue({
        id: "v-1",
        identifier: pendingChangeIdentifier("email", VALID_USER_ID),
        value: opts?.storedValue ?? validStored,
        expiresAt: opts?.expiresAt ?? new Date(Date.now() + 5 * 60 * 1000),
      }),
      deleteById: vi.fn().mockResolvedValue(undefined),
      updateValue: vi.fn().mockResolvedValue(undefined),
      create: vi.fn().mockResolvedValue(undefined),
    },
    otpRateLimit: { clearOnSuccess: vi.fn().mockResolvedValue(undefined) },
    updateUserEmailRepo: {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })),
    },
    getUserById: {
      handleAsync: vi.fn().mockResolvedValue(
        D2Result.ok({
          data: {
            user: {
              id: VALID_USER_ID,
              email: OLD_EMAIL,
              emailVerified: true,
              phone: null,
              phoneVerified: false,
              locale: "en-US",
            },
          },
        }),
      ),
    },
    getContactsByExtKeys: {
      handleAsync: vi.fn().mockResolvedValue(
        D2Result.ok({
          data: {
            data: new Map([
              [
                `auth_user:${VALID_USER_ID}`,
                [
                  {
                    id: "geo-001",
                    contextKey: "auth_user",
                    relatedEntityId: VALID_USER_ID,
                    contactMethods: {
                      emails: [{ value: OLD_EMAIL, labels: [] }],
                      phoneNumbers: [],
                    },
                  },
                ],
              ],
            ]),
          },
        }),
      ),
    } as unknown as GeoQueries.IGetContactsByExtKeysHandler,
    updateContactsByExtKeys: {
      handleAsync: vi.fn().mockResolvedValue(
        D2Result.ok({ data: { replacements: [{ newContact: { id: "geo-001" } }] } }),
      ),
    } as unknown as Complex.IUpdateContactsByExtKeysHandler,
    notify: { handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })) },
  };
}

function createMockTranslator() {
  return { t: vi.fn((_locale: string, key: string) => key) } as never;
}

function makeHandler(mocks: Mocks) {
  return new VerifyEmailChange(
    mocks.verificationStore as never,
    mocks.otpRateLimit as never,
    mocks.updateUserEmailRepo as never,
    mocks.getUserById as never,
    mocks.getContactsByExtKeys,
    mocks.updateContactsByExtKeys,
    mocks.notify as unknown as INotifyHandler,
    createMockTranslator(),
    createTestContext(),
  );
}

describe("VerifyEmailChange", () => {
  let mocks: Mocks;
  beforeEach(() => {
    mocks = makeMocks();
  });

  // -----------------------------------------------------------------------
  // Validation
  // -----------------------------------------------------------------------

  it("rejects invalid userId", async () => {
    const result = await makeHandler(mocks).handleAsync({
      userId: "not-uuid",
      code: VALID_CODE,
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(mocks.verificationStore.findByIdentifier).not.toHaveBeenCalled();
  });

  it("rejects code with wrong length", async () => {
    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: "12345",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  it("rejects non-numeric code", async () => {
    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: "abcdef",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  // -----------------------------------------------------------------------
  // Verification record lifecycle
  // -----------------------------------------------------------------------

  it("returns 404 when no pending record exists", async () => {
    mocks.verificationStore.findByIdentifier.mockResolvedValue(null);
    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: VALID_CODE,
    });
    expect(result.statusCode).toBe(HttpStatusCode.NotFound);
  });

  it("deletes and returns 404 when record is expired", async () => {
    mocks = makeMocks({ expiresAt: new Date(Date.now() - 1) });
    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: VALID_CODE,
    });
    expect(result.statusCode).toBe(HttpStatusCode.NotFound);
    expect(mocks.verificationStore.deleteById).toHaveBeenCalledWith("v-1");
  });

  it("deletes and returns 404 when record value is malformed", async () => {
    mocks = makeMocks({ storedValue: "not-json{" });
    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: VALID_CODE,
    });
    expect(result.statusCode).toBe(HttpStatusCode.NotFound);
    expect(mocks.verificationStore.deleteById).toHaveBeenCalledWith("v-1");
  });

  // -----------------------------------------------------------------------
  // Code verification + attempt tracking
  // -----------------------------------------------------------------------

  it("returns 429 when stored attempts already at max", async () => {
    mocks = makeMocks({
      storedValue: encodePendingValue({
        codeHash: hashOtpCode(VALID_CODE),
        pendingValue: NEW_EMAIL,
        attempts: OTP_VERIFY.MAX_ATTEMPTS,
      }),
    });
    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: VALID_CODE,
    });
    expect(result.statusCode).toBe(429);
    expect(result.errorCode).toBe("OTP_MAX_ATTEMPTS");
    expect(mocks.verificationStore.deleteById).toHaveBeenCalledWith("v-1");
  });

  it("increments attempts on wrong code (below max)", async () => {
    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: "999999",
    });
    expect(result.statusCode).toBe(HttpStatusCode.Unauthorized);
    expect(mocks.verificationStore.updateValue).toHaveBeenCalledTimes(1);
    const updatedValue = mocks.verificationStore.updateValue.mock.calls[0][1];
    expect(JSON.parse(updatedValue).attempts).toBe(1);
    expect(mocks.updateUserEmailRepo.handleAsync).not.toHaveBeenCalled();
  });

  it("burns the record on the final wrong attempt", async () => {
    mocks = makeMocks({
      storedValue: encodePendingValue({
        codeHash: hashOtpCode(VALID_CODE),
        pendingValue: NEW_EMAIL,
        attempts: OTP_VERIFY.MAX_ATTEMPTS - 1,
      }),
    });
    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: "999999",
    });
    expect(result.statusCode).toBe(HttpStatusCode.Unauthorized);
    expect(mocks.verificationStore.deleteById).toHaveBeenCalledWith("v-1");
    expect(mocks.verificationStore.updateValue).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Success — SAGA execution
  // -----------------------------------------------------------------------

  it("updates Geo + Auth via saga, deletes record, clears rate limit on success", async () => {
    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: VALID_CODE,
    });

    expect(result.success).toBe(true);
    expect(result.data?.newEmail).toBe(NEW_EMAIL);
    expect(mocks.updateContactsByExtKeys.handleAsync).toHaveBeenCalledTimes(1);
    expect(mocks.updateUserEmailRepo.handleAsync).toHaveBeenCalledWith({
      userId: VALID_USER_ID,
      email: NEW_EMAIL,
      emailVerified: true,
    });
    expect(mocks.verificationStore.deleteById).toHaveBeenCalledWith("v-1");
    expect(mocks.otpRateLimit.clearOnSuccess).toHaveBeenCalledWith(VALID_USER_ID, "email");
  });

  it("sends a security notification to the OLD email", async () => {
    await makeHandler(mocks).handleAsync({ userId: VALID_USER_ID, code: VALID_CODE });
    // Best-effort fire-and-forget — yield once to let the .catch chain settle.
    await new Promise((r) => setImmediate(r));
    expect(mocks.notify.handleAsync).toHaveBeenCalledTimes(1);
    const call = mocks.notify.handleAsync.mock.calls[0][0];
    expect(call.alternativeContactInfo?.email).toBe(OLD_EMAIL);
    expect(call.channels).toEqual(["email"]);
  });

  // -----------------------------------------------------------------------
  // SAGA failure paths
  // -----------------------------------------------------------------------

  it("rolls Geo back when auth update fails (saga compensation)", async () => {
    mocks.updateUserEmailRepo.handleAsync.mockResolvedValue(
      D2Result.fail({ messages: ["db conflict"], statusCode: 409 }),
    );

    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: VALID_CODE,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(409);
    // Geo called twice: forward + rollback.
    expect(mocks.updateContactsByExtKeys.handleAsync).toHaveBeenCalledTimes(2);
    // Verification record NOT deleted on failure (user can retry).
    expect(mocks.verificationStore.deleteById).not.toHaveBeenCalled();
    expect(mocks.otpRateLimit.clearOnSuccess).not.toHaveBeenCalled();
  });

  it("returns serviceUnavailable when fetching the contact fails", async () => {
    vi.mocked(mocks.getContactsByExtKeys.handleAsync).mockResolvedValue(
      D2Result.fail({ messages: ["geo down"], statusCode: 500 }),
    );

    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: VALID_CODE,
    });

    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(mocks.updateUserEmailRepo.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Redaction
  // -----------------------------------------------------------------------

  it("declares redaction on code with output suppression", () => {
    const handler = makeHandler(mocks);
    expect(handler.redaction.inputFields).toContain("code");
    expect(handler.redaction.suppressOutput).toBe(true);
  });
});
