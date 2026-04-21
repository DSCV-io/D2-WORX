import { describe, it, expect, vi, beforeEach } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { D2Result, HttpStatusCode } from "@d2/result";
import { VerifyPhoneChange } from "@d2/auth-app";
import {
  encodePendingValue,
  hashOtpCode,
  pendingChangeIdentifier,
  OTP_VERIFY,
} from "@d2/auth-domain";
import type { INotifyHandler } from "@d2/comms-client";
import type { Complex, Queries as GeoQueries } from "@d2/geo-client";

const VALID_USER_ID = "01234567-89ab-cdef-0123-456789abcdef";
const NEW_PHONE = "13213214321";
const USER_EMAIL = "user@example.com";
const VALID_CODE = "654321";

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
  updateUserPhoneRepo: { handleAsync: ReturnType<typeof vi.fn> };
  getUserById: { handleAsync: ReturnType<typeof vi.fn> };
  getContactsByExtKeys: GeoQueries.IGetContactsByExtKeysHandler;
  updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler;
  notify: { handleAsync: ReturnType<typeof vi.fn> };
}

function makeMocks(opts?: { storedValue?: string; expiresAt?: Date }): Mocks {
  const validStored = encodePendingValue({
    codeHash: hashOtpCode(VALID_CODE),
    pendingValue: NEW_PHONE,
    attempts: 0,
  });
  return {
    verificationStore: {
      findByIdentifier: vi.fn().mockResolvedValue({
        id: "v-1",
        identifier: pendingChangeIdentifier("phone", VALID_USER_ID),
        value: opts?.storedValue ?? validStored,
        expiresAt: opts?.expiresAt ?? new Date(Date.now() + 5 * 60 * 1000),
      }),
      deleteById: vi.fn().mockResolvedValue(undefined),
      updateValue: vi.fn().mockResolvedValue(undefined),
      create: vi.fn().mockResolvedValue(undefined),
    },
    otpRateLimit: { clearOnSuccess: vi.fn().mockResolvedValue(undefined) },
    updateUserPhoneRepo: {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })),
    },
    getUserById: {
      handleAsync: vi.fn().mockResolvedValue(
        D2Result.ok({
          data: {
            user: {
              id: VALID_USER_ID,
              email: USER_EMAIL,
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
                    contactMethods: { emails: [], phoneNumbers: [] },
                  },
                ],
              ],
            ]),
          },
        }),
      ),
    } as unknown as GeoQueries.IGetContactsByExtKeysHandler,
    updateContactsByExtKeys: {
      handleAsync: vi
        .fn()
        .mockResolvedValue(
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
  return new VerifyPhoneChange(
    mocks.verificationStore as never,
    mocks.otpRateLimit as never,
    mocks.updateUserPhoneRepo as never,
    mocks.getUserById as never,
    mocks.getContactsByExtKeys,
    mocks.updateContactsByExtKeys,
    mocks.notify as unknown as INotifyHandler,
    createMockTranslator(),
    createTestContext(),
  );
}

describe("VerifyPhoneChange", () => {
  let mocks: Mocks;
  beforeEach(() => {
    mocks = makeMocks();
  });

  it("rejects malformed code", async () => {
    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: "abc123",
    });
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  it("returns 404 when no pending record", async () => {
    mocks.verificationStore.findByIdentifier.mockResolvedValue(null);
    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: VALID_CODE,
    });
    expect(result.statusCode).toBe(HttpStatusCode.NotFound);
  });

  it("burns expired record + returns 404", async () => {
    mocks = makeMocks({ expiresAt: new Date(Date.now() - 1000) });
    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: VALID_CODE,
    });
    expect(result.statusCode).toBe(HttpStatusCode.NotFound);
    expect(mocks.verificationStore.deleteById).toHaveBeenCalledWith("v-1");
  });

  it("returns 429 when attempts already at max", async () => {
    mocks = makeMocks({
      storedValue: encodePendingValue({
        codeHash: hashOtpCode(VALID_CODE),
        pendingValue: NEW_PHONE,
        attempts: OTP_VERIFY.MAX_ATTEMPTS,
      }),
    });
    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: VALID_CODE,
    });
    expect(result.statusCode).toBe(429);
    expect(result.errorCode).toBe("OTP_MAX_ATTEMPTS");
  });

  it("increments attempts on wrong code", async () => {
    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: "000000",
    });
    expect(result.statusCode).toBe(HttpStatusCode.Unauthorized);
    expect(mocks.verificationStore.updateValue).toHaveBeenCalledTimes(1);
    expect(mocks.updateUserPhoneRepo.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Success — SAGA + security email goes to USER'S EMAIL (not the new phone)
  // -----------------------------------------------------------------------

  it("updates phone via saga, deletes record, clears rate limit", async () => {
    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: VALID_CODE,
    });

    expect(result.success).toBe(true);
    expect(result.data?.phone).toBe(NEW_PHONE);
    expect(mocks.updateUserPhoneRepo.handleAsync).toHaveBeenCalledWith({
      userId: VALID_USER_ID,
      phone: NEW_PHONE,
      phoneVerified: true,
    });
    expect(mocks.otpRateLimit.clearOnSuccess).toHaveBeenCalledWith(VALID_USER_ID, "phone");
  });

  it("sends security notification to user's EMAIL (not the phone)", async () => {
    await makeHandler(mocks).handleAsync({ userId: VALID_USER_ID, code: VALID_CODE });
    await new Promise((r) => setImmediate(r));
    expect(mocks.notify.handleAsync).toHaveBeenCalledTimes(1);
    const call = mocks.notify.handleAsync.mock.calls[0][0];
    expect(call.channels).toEqual(["email"]);
    expect(call.alternativeContactInfo?.email).toBe(USER_EMAIL);
    expect(call.alternativeContactInfo?.phone).toBeUndefined();
  });

  // -----------------------------------------------------------------------
  // SAGA failure
  // -----------------------------------------------------------------------

  it("rolls Geo back when auth update fails", async () => {
    mocks.updateUserPhoneRepo.handleAsync.mockResolvedValue(
      D2Result.fail({ messages: ["db conflict"], statusCode: 409 }),
    );

    const result = await makeHandler(mocks).handleAsync({
      userId: VALID_USER_ID,
      code: VALID_CODE,
    });

    expect(result.success).toBe(false);
    expect(mocks.updateContactsByExtKeys.handleAsync).toHaveBeenCalledTimes(2);
    expect(mocks.verificationStore.deleteById).not.toHaveBeenCalled();
  });
});
