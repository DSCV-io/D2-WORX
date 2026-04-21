import { describe, it, expect, vi, beforeEach } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { D2Result, HttpStatusCode } from "@d2/result";
import { RemovePhone } from "@d2/auth-app";
import type { INotifyHandler } from "@d2/comms-client";
import type { Complex, Queries as GeoQueries } from "@d2/geo-client";

const VALID_USER_ID = "01234567-89ab-cdef-0123-456789abcdef";
const OLD_PHONE = "13213214321";
const USER_EMAIL = "user@example.com";

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
  getUserById: { handleAsync: ReturnType<typeof vi.fn> };
  updateUserPhoneRepo: { handleAsync: ReturnType<typeof vi.fn> };
  getContactsByExtKeys: GeoQueries.IGetContactsByExtKeysHandler;
  updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler;
  notify: { handleAsync: ReturnType<typeof vi.fn> };
}

function makeMocks(currentPhone: string | null = OLD_PHONE): Mocks {
  return {
    passwordVerifier: { verify: vi.fn().mockResolvedValue(true) },
    getUserById: {
      handleAsync: vi.fn().mockResolvedValue(
        D2Result.ok({
          data: {
            user: {
              id: VALID_USER_ID,
              email: USER_EMAIL,
              emailVerified: true,
              phone: currentPhone,
              phoneVerified: !!currentPhone,
              locale: "en-US",
            },
          },
        }),
      ),
    },
    updateUserPhoneRepo: {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })),
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
                      emails: [{ value: USER_EMAIL, labels: [] }],
                      phoneNumbers: [{ value: OLD_PHONE, labels: [] }],
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
  return new RemovePhone(
    mocks.passwordVerifier as never,
    mocks.getUserById as never,
    mocks.updateUserPhoneRepo as never,
    mocks.getContactsByExtKeys,
    mocks.updateContactsByExtKeys,
    mocks.notify as unknown as INotifyHandler,
    createMockTranslator(),
    createTestContext(),
  );
}

const validInput = () => ({
  userId: VALID_USER_ID,
  currentPassword: "hunter2",
});

describe("RemovePhone", () => {
  let mocks: Mocks;
  beforeEach(() => {
    mocks = makeMocks();
  });

  // -----------------------------------------------------------------------
  // Validation + password gate
  // -----------------------------------------------------------------------

  it("rejects empty password", async () => {
    const result = await makeHandler(mocks).handleAsync({
      ...validInput(),
      currentPassword: "",
    });
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(mocks.passwordVerifier.verify).not.toHaveBeenCalled();
  });

  it("rejects invalid userId", async () => {
    const result = await makeHandler(mocks).handleAsync({
      ...validInput(),
      userId: "bad",
    });
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  it("returns 401 when password incorrect — no state changes, no notify", async () => {
    mocks.passwordVerifier.verify.mockResolvedValue(false);
    const result = await makeHandler(mocks).handleAsync(validInput());

    expect(result.statusCode).toBe(HttpStatusCode.Unauthorized);
    expect(mocks.updateUserPhoneRepo.handleAsync).not.toHaveBeenCalled();
    expect(mocks.updateContactsByExtKeys.handleAsync).not.toHaveBeenCalled();
    expect(mocks.notify.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Idempotent — no phone set
  // -----------------------------------------------------------------------

  it("returns ok without state changes when user has no phone (idempotent)", async () => {
    mocks = makeMocks(null);
    const result = await makeHandler(mocks).handleAsync(validInput());

    expect(result.success).toBe(true);
    expect(mocks.updateUserPhoneRepo.handleAsync).not.toHaveBeenCalled();
    expect(mocks.updateContactsByExtKeys.handleAsync).not.toHaveBeenCalled();
    expect(mocks.notify.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Happy path — SAGA + notification
  // -----------------------------------------------------------------------

  it("clears phone on user via saga + sends security email", async () => {
    const result = await makeHandler(mocks).handleAsync(validInput());

    expect(result.success).toBe(true);
    expect(mocks.updateUserPhoneRepo.handleAsync).toHaveBeenCalledWith({
      userId: VALID_USER_ID,
      phone: null,
      phoneVerified: false,
    });
    expect(mocks.updateContactsByExtKeys.handleAsync).toHaveBeenCalledTimes(1);

    await new Promise((r) => setImmediate(r));
    expect(mocks.notify.handleAsync).toHaveBeenCalledTimes(1);
    const call = mocks.notify.handleAsync.mock.calls[0][0];
    expect(call.channels).toEqual(["email"]);
    expect(call.alternativeContactInfo?.email).toBe(USER_EMAIL);
  });

  it("strips the first phone entry from the Geo contact", async () => {
    await makeHandler(mocks).handleAsync(validInput());
    const call = vi.mocked(mocks.updateContactsByExtKeys.handleAsync).mock.calls[0][0];
    const phones = call.contacts[0].contactMethods?.phoneNumbers ?? [];
    expect(phones).toEqual([]); // only one was present, dropping it leaves none
  });

  // -----------------------------------------------------------------------
  // SAGA compensation
  // -----------------------------------------------------------------------

  it("rolls Geo back when auth update fails", async () => {
    mocks.updateUserPhoneRepo.handleAsync.mockResolvedValue(
      D2Result.fail({ messages: ["db error"], statusCode: 500 }),
    );

    const result = await makeHandler(mocks).handleAsync(validInput());

    expect(result.success).toBe(false);
    expect(mocks.updateContactsByExtKeys.handleAsync).toHaveBeenCalledTimes(2);
    // Rollback restores the OLD phone.
    const rollback = vi.mocked(mocks.updateContactsByExtKeys.handleAsync).mock.calls[1][0];
    expect(rollback.contacts[0].contactMethods?.phoneNumbers?.[0]?.value).toBe(OLD_PHONE);
  });

  // -----------------------------------------------------------------------
  // Geo contact fetch failure — abort before any mutation
  // -----------------------------------------------------------------------

  it("returns serviceUnavailable when fetching the contact fails", async () => {
    vi.mocked(mocks.getContactsByExtKeys.handleAsync).mockResolvedValue(
      D2Result.fail({ messages: ["geo down"], statusCode: 500 }),
    );

    const result = await makeHandler(mocks).handleAsync(validInput());

    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(mocks.updateUserPhoneRepo.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Redaction
  // -----------------------------------------------------------------------

  it("declares redaction on currentPassword", () => {
    const handler = makeHandler(mocks);
    expect(handler.redaction.inputFields).toContain("currentPassword");
  });
});
