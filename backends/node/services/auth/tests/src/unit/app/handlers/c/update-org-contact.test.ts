import { describe, it, expect, vi, beforeEach } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { D2Result, HttpStatusCode, ErrorCodes } from "@d2/result";
import { UpdateOrgContactHandler } from "@d2/auth-app";
import type { IFindOrgContactByIdHandler, IUpdateOrgContactRecordHandler } from "@d2/auth-app";
import type { OrgContact } from "@d2/auth-domain";
import type { ContactDTO } from "@d2/protos";
import type { Complex, Queries as GeoQueries } from "@d2/geo-client";

const VALID_CONTACT_ID = "01234567-89ab-cdef-0123-456789abcdef";
const VALID_ORG_ID = "abcdef01-2345-6789-abcd-ef0123456789";
const OTHER_ORG_ID = "11111111-1111-1111-1111-111111111111";

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

function createMockFindById() {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.notFound()),
  };
}

function createMockUpdateRecord() {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })),
  };
}

function createMockGetContactsByExtKeys(): GeoQueries.IGetContactsByExtKeysHandler {
  return {
    handleAsync: vi.fn().mockResolvedValue(
      D2Result.ok({
        data: {
          data: new Map([
            [
              "auth_org_contact:" + VALID_CONTACT_ID,
              [
                {
                  id: "old-geo-contact-001",
                  contextKey: "auth_org_contact",
                  relatedEntityId: VALID_CONTACT_ID,
                  ietfBcp47Tag: "en-US",
                },
              ],
            ],
          ]),
        },
      }),
    ),
  } as unknown as GeoQueries.IGetContactsByExtKeysHandler;
}

function createMockUpdateContactsByExtKeys(): Complex.IUpdateContactsByExtKeysHandler {
  return {
    handleAsync: vi.fn().mockResolvedValue(
      D2Result.ok({
        data: {
          replacements: [
            {
              key: {
                contextKey: "auth_org_contact",
                relatedEntityId: VALID_CONTACT_ID,
                oldContactId: "old-geo-contact-001",
              },
              newContact: {
                id: "new-geo-contact-001",
                createdAt: new Date("2026-02-10"),
                contextKey: "auth_org_contact",
                relatedEntityId: VALID_CONTACT_ID,
              } as ContactDTO,
            },
          ],
        },
      }),
    ),
    redaction: { suppressInput: true, suppressOutput: true },
  } as unknown as Complex.IUpdateContactsByExtKeysHandler;
}

function createExistingContact(overrides?: Partial<OrgContact>): OrgContact {
  return {
    id: VALID_CONTACT_ID,
    organizationId: VALID_ORG_ID,
    label: "Billing",
    isPrimary: false,
    createdAt: new Date("2026-02-01"),
    updatedAt: new Date("2026-02-01"),
    ...overrides,
  };
}

describe("UpdateOrgContactHandler", () => {
  let findById: ReturnType<typeof createMockFindById>;
  let updateRecord: ReturnType<typeof createMockUpdateRecord>;
  let getContactsByExtKeys: GeoQueries.IGetContactsByExtKeysHandler;
  let updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler;
  let handler: UpdateOrgContactHandler;

  beforeEach(() => {
    findById = createMockFindById();
    updateRecord = createMockUpdateRecord();
    getContactsByExtKeys = createMockGetContactsByExtKeys();
    updateContactsByExtKeys = createMockUpdateContactsByExtKeys();
    handler = new UpdateOrgContactHandler(
      findById as unknown as IFindOrgContactByIdHandler,
      updateRecord as unknown as IUpdateOrgContactRecordHandler,
      createTestContext(),
      getContactsByExtKeys,
      updateContactsByExtKeys,
    );
  });

  // -----------------------------------------------------------------------
  // Validation tests (Zod schema)
  // -----------------------------------------------------------------------

  it("should return validationFailed when id is not a valid UUID", async () => {
    const result = await handler.handleAsync({
      id: "not-a-uuid",
      organizationId: VALID_ORG_ID,
      updates: { label: "Shipping" },
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(Array.isArray(result.inputErrors)).toBe(true);
    expect(result.inputErrors.length).toBeGreaterThanOrEqual(1);
    expect(findById.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when organizationId is not a valid UUID", async () => {
    const result = await handler.handleAsync({
      id: VALID_CONTACT_ID,
      organizationId: "bad-org",
      updates: { label: "Shipping" },
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(Array.isArray(result.inputErrors)).toBe(true);
    expect(findById.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when no updates are provided", async () => {
    const result = await handler.handleAsync({
      id: VALID_CONTACT_ID,
      organizationId: VALID_ORG_ID,
      updates: {},
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(Array.isArray(result.inputErrors)).toBe(true);
    expect(findById.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // IDOR check
  // -----------------------------------------------------------------------

  it("should return Forbidden when contact belongs to a different organization", async () => {
    const existing = createExistingContact({ organizationId: OTHER_ORG_ID });
    findById.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { contact: existing } }));

    const result = await handler.handleAsync({
      id: VALID_CONTACT_ID,
      organizationId: VALID_ORG_ID,
      updates: { label: "Shipping" },
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.Forbidden);
    expect(result.errorCode).toBe(ErrorCodes.FORBIDDEN);
    expect(updateRecord.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Metadata-only updates (no Geo calls)
  // -----------------------------------------------------------------------

  it("should update label and return success without calling Geo handlers", async () => {
    const existing = createExistingContact();
    findById.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { contact: existing } }));

    const result = await handler.handleAsync({
      id: VALID_CONTACT_ID,
      organizationId: VALID_ORG_ID,
      updates: { label: "Shipping" },
    });

    expect(result.success).toBe(true);
    expect(result.data?.contact.label).toBe("Shipping");
    expect(result.data?.contact.isPrimary).toBe(false);
    expect(result.data?.geoContact).toBeUndefined();
    expect(updateContactsByExtKeys.handleAsync).not.toHaveBeenCalled();
    expect(updateRecord.handleAsync).toHaveBeenCalledOnce();
  });

  it("should update isPrimary and return success", async () => {
    const existing = createExistingContact();
    findById.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { contact: existing } }));

    const result = await handler.handleAsync({
      id: VALID_CONTACT_ID,
      organizationId: VALID_ORG_ID,
      updates: { isPrimary: true },
    });

    expect(result.success).toBe(true);
    expect(result.data?.contact.isPrimary).toBe(true);
    expect(result.data?.contact.label).toBe("Billing");
    expect(updateContactsByExtKeys.handleAsync).not.toHaveBeenCalled();
  });

  it("should update both label and isPrimary", async () => {
    const existing = createExistingContact();
    findById.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { contact: existing } }));

    const result = await handler.handleAsync({
      id: VALID_CONTACT_ID,
      organizationId: VALID_ORG_ID,
      updates: { label: "Main", isPrimary: true },
    });

    expect(result.success).toBe(true);
    expect(result.data?.contact.label).toBe("Main");
    expect(result.data?.contact.isPrimary).toBe(true);
  });

  // -----------------------------------------------------------------------
  // Contact replacement flow (via updateContactsByExtKeys)
  // -----------------------------------------------------------------------

  it("should call updateContactsByExtKeys when contact is provided", async () => {
    const existing = createExistingContact();
    findById.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { contact: existing } }));

    const result = await handler.handleAsync({
      id: VALID_CONTACT_ID,
      organizationId: VALID_ORG_ID,
      updates: {
        contact: {
          personalDetails: { firstName: "Jane", lastName: "Smith" },
        },
      },
    });

    expect(result.success).toBe(true);
    expect(updateContactsByExtKeys.handleAsync).toHaveBeenCalledOnce();
    expect(result.data?.geoContact).toBeDefined();
    expect(result.data?.geoContact?.id).toBe("new-geo-contact-001");

    // Verify the call uses ext key pattern
    const call = vi.mocked(updateContactsByExtKeys.handleAsync).mock.calls[0][0];
    expect(call.contacts).toHaveLength(1);
    expect(call.contacts[0].contextKey).toBe("auth_org_contact");
    expect(call.contacts[0].relatedEntityId).toBe(VALID_CONTACT_ID);
  });

  it("should bubble Geo failure when updateContactsByExtKeys fails", async () => {
    const existing = createExistingContact();
    findById.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { contact: existing } }));
    updateContactsByExtKeys.handleAsync = vi.fn().mockResolvedValue(
      D2Result.fail({
        messages: ["Geo service unavailable."],
        statusCode: HttpStatusCode.ServiceUnavailable,
      }),
    );

    const result = await handler.handleAsync({
      id: VALID_CONTACT_ID,
      organizationId: VALID_ORG_ID,
      updates: {
        contact: { personalDetails: { firstName: "Updated" } },
      },
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(updateRecord.handleAsync).not.toHaveBeenCalled();
  });

  it("should update metadata AND replace contact in same call", async () => {
    const existing = createExistingContact();
    findById.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { contact: existing } }));

    const result = await handler.handleAsync({
      id: VALID_CONTACT_ID,
      organizationId: VALID_ORG_ID,
      updates: {
        label: "New Label",
        isPrimary: true,
        contact: { personalDetails: { firstName: "Replaced" } },
      },
    });

    expect(result.success).toBe(true);
    expect(result.data?.contact.label).toBe("New Label");
    expect(result.data?.contact.isPrimary).toBe(true);
    expect(result.data?.geoContact?.id).toBe("new-geo-contact-001");
  });

  // -----------------------------------------------------------------------
  // Edge cases
  // -----------------------------------------------------------------------

  it("should return NotFound when contact does not exist", async () => {
    findById.handleAsync = vi.fn().mockResolvedValue(D2Result.notFound());

    const result = await handler.handleAsync({
      id: VALID_CONTACT_ID,
      organizationId: VALID_ORG_ID,
      updates: { label: "New Label" },
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.NotFound);
    expect(updateRecord.handleAsync).not.toHaveBeenCalled();
  });

  it("should bubble failure when repo updateRecord returns error", async () => {
    const existing = createExistingContact();
    findById.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { contact: existing } }));
    updateRecord.handleAsync = vi.fn().mockResolvedValue(
      D2Result.fail({
        messages: ["connection lost"],
        statusCode: HttpStatusCode.InternalServerError,
      }),
    );

    const result = await handler.handleAsync({
      id: VALID_CONTACT_ID,
      organizationId: VALID_ORG_ID,
      updates: { label: "Shipping" },
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.InternalServerError);
    expect(updateRecord.handleAsync).toHaveBeenCalledOnce();
  });

  it("should bubble failure when Geo returns success with empty data array", async () => {
    const existing = createExistingContact();
    findById.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { contact: existing } }));
    updateContactsByExtKeys.handleAsync = vi
      .fn()
      .mockResolvedValue(D2Result.ok({ data: { replacements: [] } }));

    const result = await handler.handleAsync({
      id: VALID_CONTACT_ID,
      organizationId: VALID_ORG_ID,
      updates: {
        contact: { personalDetails: { firstName: "New" } },
      },
    });

    expect(result.success).toBe(false);
    // Should not proceed to update the junction since Geo returned no contact
    // (saga aborts auth update via the no-newGeoContact guard, then attempts rollback)
    expect(updateRecord.handleAsync).not.toHaveBeenCalled();
  });

  it("should set updatedAt to a new date", async () => {
    const existing = createExistingContact({
      updatedAt: new Date("2026-01-01"),
    });
    findById.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { contact: existing } }));

    const result = await handler.handleAsync({
      id: VALID_CONTACT_ID,
      organizationId: VALID_ORG_ID,
      updates: { label: "Updated" },
    });

    expect(result.success).toBe(true);
    expect(result.data?.contact.updatedAt.getTime()).toBeGreaterThan(
      new Date("2026-01-01").getTime(),
    );
  });
});
