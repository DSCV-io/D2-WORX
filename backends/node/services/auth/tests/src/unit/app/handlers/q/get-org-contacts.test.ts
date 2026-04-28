import { describe, it, expect, vi, beforeEach } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { D2Result, HttpStatusCode } from "@d2/result";
import { GetOrgContacts } from "@d2/auth-app";
import type { IFindOrgContactsByOrgIdHandler } from "@d2/auth-app";
import type { OrgContact } from "@d2/auth-domain";
import type { ContactDTO } from "@d2/protos";
import type { Queries } from "@d2/geo-client";

const VALID_ORG_ID = "01234567-89ab-cdef-0123-456789abcdef";

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

function createMockFindByOrgId() {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { contacts: [] } })),
  };
}

function createMockGetContactsByExtKeys(): Queries.IGetContactsByExtKeysHandler {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { data: new Map() } })),
    redaction: { suppressOutput: true },
  } as unknown as Queries.IGetContactsByExtKeysHandler;
}

function createContact(id: string): OrgContact {
  return {
    id,
    organizationId: VALID_ORG_ID,
    label: "Office",
    isPrimary: false,
    createdAt: new Date("2026-02-01"),
    updatedAt: new Date("2026-02-01"),
  };
}

function createContactDTO(id: string): ContactDTO {
  return {
    id,
    createdAt: new Date("2026-02-01"),
    contextKey: "auth_org_contact",
    relatedEntityId: "some-oc-id",
    personalDetails: { firstName: "Test", lastName: "User" },
    contactMethods: { emails: [{ value: `${id}@example.com` }] },
  } as ContactDTO;
}

describe("GetOrgContacts", () => {
  let findByOrgId: ReturnType<typeof createMockFindByOrgId>;
  let getContactsByExtKeys: Queries.IGetContactsByExtKeysHandler;
  let handler: GetOrgContacts;

  beforeEach(() => {
    findByOrgId = createMockFindByOrgId();
    getContactsByExtKeys = createMockGetContactsByExtKeys();
    handler = new GetOrgContacts(
      findByOrgId as unknown as IFindOrgContactsByOrgIdHandler,
      createTestContext(),
      getContactsByExtKeys,
    );
  });

  // -----------------------------------------------------------------------
  // Validation tests (Zod schema)
  // -----------------------------------------------------------------------

  it("should return validationFailed when organizationId is not a valid UUID", async () => {
    const result = await handler.handleAsync({ organizationId: "not-a-uuid" });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(Array.isArray(result.inputErrors)).toBe(true);
    expect(result.inputErrors.length).toBeGreaterThanOrEqual(1);
    expect(findByOrgId.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when limit is negative", async () => {
    const result = await handler.handleAsync({
      organizationId: VALID_ORG_ID,
      limit: -1,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(Array.isArray(result.inputErrors)).toBe(true);
    expect(findByOrgId.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when limit exceeds 100", async () => {
    const result = await handler.handleAsync({
      organizationId: VALID_ORG_ID,
      limit: 101,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(Array.isArray(result.inputErrors)).toBe(true);
    expect(findByOrgId.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when offset is negative", async () => {
    const result = await handler.handleAsync({
      organizationId: VALID_ORG_ID,
      offset: -1,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(Array.isArray(result.inputErrors)).toBe(true);
    expect(findByOrgId.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Hydration tests
  // -----------------------------------------------------------------------

  it("should return hydrated contacts with Geo data", async () => {
    const contacts = [createContact("oc-1"), createContact("oc-2"), createContact("oc-3")];
    findByOrgId.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { contacts } }));

    // Map keyed by "auth_org_contact:{junction.id}" → ContactDTO[]
    const geoMap = new Map<string, ContactDTO[]>([
      ["auth_org_contact:oc-1", [createContactDTO("geo-1")]],
      ["auth_org_contact:oc-2", [createContactDTO("geo-2")]],
      ["auth_org_contact:oc-3", [createContactDTO("geo-3")]],
    ]);
    getContactsByExtKeys.handleAsync = vi
      .fn()
      .mockResolvedValue(D2Result.ok({ data: { data: geoMap } }));

    const result = await handler.handleAsync({ organizationId: VALID_ORG_ID });

    expect(result.success).toBe(true);
    expect(result.data?.contacts).toHaveLength(3);

    // Verify hydration: each contact has its Geo data attached
    expect(result.data?.contacts[0].geoContact).toBeDefined();
    expect(result.data?.contacts[0].geoContact?.id).toBe("geo-1");
    expect(result.data?.contacts[1].geoContact?.id).toBe("geo-2");
    expect(result.data?.contacts[2].geoContact?.id).toBe("geo-3");

    // Junction metadata is preserved
    expect(result.data?.contacts[0].label).toBe("Office");
  });

  it("should batch fetch all contacts in a single getContactsByExtKeys call", async () => {
    const contacts = [createContact("oc-1"), createContact("oc-2")];
    findByOrgId.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { contacts } }));

    await handler.handleAsync({ organizationId: VALID_ORG_ID });

    expect(getContactsByExtKeys.handleAsync).toHaveBeenCalledOnce();
    expect(getContactsByExtKeys.handleAsync).toHaveBeenCalledWith({
      keys: [
        { contextKey: "auth_org_contact", relatedEntityId: "oc-1" },
        { contextKey: "auth_org_contact", relatedEntityId: "oc-2" },
      ],
    });
  });

  it("should return geoContact: undefined for orphaned junctions (Geo contact missing)", async () => {
    const contacts = [createContact("oc-1"), createContact("oc-2")];
    findByOrgId.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { contacts } }));

    // Only oc-1 is in the map — oc-2 is missing (orphaned)
    const geoMap = new Map<string, ContactDTO[]>([
      ["auth_org_contact:oc-1", [createContactDTO("geo-1")]],
    ]);
    getContactsByExtKeys.handleAsync = vi
      .fn()
      .mockResolvedValue(D2Result.ok({ data: { data: geoMap } }));

    const result = await handler.handleAsync({ organizationId: VALID_ORG_ID });

    expect(result.success).toBe(true);
    expect(result.data?.contacts[0].geoContact?.id).toBe("geo-1");
    expect(result.data?.contacts[1].geoContact).toBeUndefined();
  });

  it("should return all contacts with geoContact: undefined when Geo call fails", async () => {
    const contacts = [createContact("oc-1")];
    findByOrgId.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { contacts } }));
    getContactsByExtKeys.handleAsync = vi.fn().mockResolvedValue(
      D2Result.fail({
        messages: ["Geo service unavailable."],
        statusCode: HttpStatusCode.ServiceUnavailable,
      }),
    );

    const result = await handler.handleAsync({ organizationId: VALID_ORG_ID });

    expect(result.success).toBe(true);
    expect(result.data?.contacts).toHaveLength(1);
    expect(result.data?.contacts[0].geoContact).toBeUndefined();
  });

  // -----------------------------------------------------------------------
  // Empty results
  // -----------------------------------------------------------------------

  it("should return empty array when no contacts exist", async () => {
    const result = await handler.handleAsync({ organizationId: VALID_ORG_ID });

    expect(result.success).toBe(true);
    expect(result.data?.contacts).toHaveLength(0);
    // Should not call getContactsByExtKeys when there are no junctions
    expect(getContactsByExtKeys.handleAsync).not.toHaveBeenCalled();
  });

  it("should treat empty Geo contact array for a key as orphaned (geoContact: undefined)", async () => {
    const contacts = [createContact("oc-1")];
    findByOrgId.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { contacts } }));

    // Geo returns the key but with an empty array instead of a ContactDTO
    const geoMap = new Map<string, ContactDTO[]>([["auth_org_contact:oc-1", []]]);
    getContactsByExtKeys.handleAsync = vi
      .fn()
      .mockResolvedValue(D2Result.ok({ data: { data: geoMap } }));

    const result = await handler.handleAsync({ organizationId: VALID_ORG_ID });

    expect(result.success).toBe(true);
    expect(result.data?.contacts).toHaveLength(1);
    expect(result.data?.contacts[0].geoContact).toBeUndefined();
  });

  it("should propagate failure when findByOrgId returns failure", async () => {
    findByOrgId.handleAsync = vi.fn().mockResolvedValue(D2Result.fail({ messages: ["DB error"] }));

    const result = await handler.handleAsync({ organizationId: VALID_ORG_ID });

    expect(result.success).toBe(false);
    // Should not call Geo when repo failed
    expect(getContactsByExtKeys.handleAsync).not.toHaveBeenCalled();
  });

  it("should pass limit and offset to repository", async () => {
    await handler.handleAsync({ organizationId: VALID_ORG_ID, limit: 10, offset: 20 });

    expect(findByOrgId.handleAsync).toHaveBeenCalledWith({
      organizationId: VALID_ORG_ID,
      limit: 10,
      offset: 20,
    });
  });
});
