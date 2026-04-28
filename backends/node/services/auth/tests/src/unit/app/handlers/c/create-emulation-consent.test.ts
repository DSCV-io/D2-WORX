import { describe, it, expect, vi, beforeEach } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { D2Result, HttpStatusCode, ErrorCodes } from "@d2/result";
import { CreateEmulationConsent } from "@d2/auth-app";
import type {
  ICreateEmulationConsentRecordHandler,
  IFindActiveConsentByUserIdAndOrgHandler,
  ICheckOrgExistsHandler,
} from "@d2/auth-app";
import type { EmulationConsent } from "@d2/auth-domain";

const VALID_USER_ID = "01234567-89ab-cdef-0123-456789abcdef";
const VALID_ORG_ID = "abcdef01-2345-6789-abcd-ef0123456789";

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

function createMockFindActiveByUserIdAndOrg() {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { consent: undefined } })),
  };
}

function createMockCheckOrgExists() {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { exists: true } })),
  };
}

describe("CreateEmulationConsent", () => {
  let createRecord: ReturnType<typeof createMockCreateRecord>;
  let findActiveByUserIdAndOrg: ReturnType<typeof createMockFindActiveByUserIdAndOrg>;
  let checkOrgExists: ReturnType<typeof createMockCheckOrgExists>;
  let handler: CreateEmulationConsent;

  beforeEach(() => {
    createRecord = createMockCreateRecord();
    findActiveByUserIdAndOrg = createMockFindActiveByUserIdAndOrg();
    checkOrgExists = createMockCheckOrgExists();
    handler = new CreateEmulationConsent(
      createRecord as unknown as ICreateEmulationConsentRecordHandler,
      findActiveByUserIdAndOrg as unknown as IFindActiveConsentByUserIdAndOrgHandler,
      createTestContext(),
      checkOrgExists as unknown as ICheckOrgExistsHandler,
    );
  });

  // -----------------------------------------------------------------------
  // Existing business-logic tests
  // -----------------------------------------------------------------------

  it("should create consent when org type is support", async () => {
    const futureDate = new Date(Date.now() + 86_400_000);
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      grantedToOrgId: VALID_ORG_ID,
      activeOrgType: "support",
      expiresAt: futureDate,
    });

    expect(result.success).toBe(true);
    expect(result.data?.consent).toBeDefined();
    expect(result.data?.consent.userId).toBe(VALID_USER_ID);
    expect(result.data?.consent.grantedToOrgId).toBe(VALID_ORG_ID);
    expect(result.data?.consent.revokedAt).toBeUndefined();
    expect(createRecord.handleAsync).toHaveBeenCalledOnce();
  });

  it("should create consent when org type is admin", async () => {
    const futureDate = new Date(Date.now() + 86_400_000);
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      grantedToOrgId: VALID_ORG_ID,
      activeOrgType: "admin",
      expiresAt: futureDate,
    });

    expect(result.success).toBe(true);
    expect(result.data?.consent).toBeDefined();
    expect(createRecord.handleAsync).toHaveBeenCalledOnce();
  });

  it("should return Forbidden when org type is customer", async () => {
    const futureDate = new Date(Date.now() + 86_400_000);
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      grantedToOrgId: VALID_ORG_ID,
      activeOrgType: "customer",
      expiresAt: futureDate,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.Forbidden);
    expect(result.errorCode).toBe(ErrorCodes.FORBIDDEN);
    expect(createRecord.handleAsync).not.toHaveBeenCalled();
  });

  it("should return Forbidden when org type is third_party", async () => {
    const futureDate = new Date(Date.now() + 86_400_000);
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      grantedToOrgId: VALID_ORG_ID,
      activeOrgType: "third_party",
      expiresAt: futureDate,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.Forbidden);
    expect(createRecord.handleAsync).not.toHaveBeenCalled();
  });

  it("should return Forbidden when org type is affiliate", async () => {
    const futureDate = new Date(Date.now() + 86_400_000);
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      grantedToOrgId: VALID_ORG_ID,
      activeOrgType: "affiliate",
      expiresAt: futureDate,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.Forbidden);
    expect(createRecord.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Validation tests (Zod schema)
  // -----------------------------------------------------------------------

  it("should return validationFailed when userId is not a valid UUID", async () => {
    const futureDate = new Date(Date.now() + 86_400_000);
    const result = await handler.handleAsync({
      userId: "not-a-uuid",
      grantedToOrgId: VALID_ORG_ID,
      activeOrgType: "support",
      expiresAt: futureDate,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(Array.isArray(result.inputErrors)).toBe(true);
    expect(result.inputErrors.length).toBeGreaterThanOrEqual(1);
    expect(createRecord.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when grantedToOrgId is not a valid UUID", async () => {
    const futureDate = new Date(Date.now() + 86_400_000);
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      grantedToOrgId: "bad-org-id",
      activeOrgType: "support",
      expiresAt: futureDate,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(Array.isArray(result.inputErrors)).toBe(true);
    expect(createRecord.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when expiresAt is in the past", async () => {
    const pastDate = new Date(Date.now() - 86_400_000);
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      grantedToOrgId: VALID_ORG_ID,
      activeOrgType: "support",
      expiresAt: pastDate,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(Array.isArray(result.inputErrors)).toBe(true);
    expect(createRecord.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when expiresAt exceeds 30 days", async () => {
    const tooFarDate = new Date(Date.now() + 31 * 24 * 60 * 60 * 1000);
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      grantedToOrgId: VALID_ORG_ID,
      activeOrgType: "support",
      expiresAt: tooFarDate,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(Array.isArray(result.inputErrors)).toBe(true);
    expect(createRecord.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Org existence check
  // -----------------------------------------------------------------------

  it("should return NotFound when target organization does not exist", async () => {
    checkOrgExists.handleAsync.mockResolvedValue(D2Result.ok({ data: { exists: false } }));

    const futureDate = new Date(Date.now() + 86_400_000);
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      grantedToOrgId: VALID_ORG_ID,
      activeOrgType: "support",
      expiresAt: futureDate,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.NotFound);
    expect(result.errorCode).toBe(ErrorCodes.NOT_FOUND);
    expect(checkOrgExists.handleAsync).toHaveBeenCalledWith({ orgId: VALID_ORG_ID });
    expect(createRecord.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Duplicate prevention
  // -----------------------------------------------------------------------

  it("should return Conflict when an active consent already exists for the same user and org", async () => {
    const existingConsent: EmulationConsent = {
      id: "existing-consent-id",
      userId: VALID_USER_ID,
      grantedToOrgId: VALID_ORG_ID,
      expiresAt: new Date(Date.now() + 86_400_000),
      revokedAt: undefined,
      createdAt: new Date("2026-02-01"),
    };
    findActiveByUserIdAndOrg.handleAsync = vi
      .fn()
      .mockResolvedValue(D2Result.ok({ data: { consent: existingConsent } }));

    const futureDate = new Date(Date.now() + 86_400_000);
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      grantedToOrgId: VALID_ORG_ID,
      activeOrgType: "support",
      expiresAt: futureDate,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.Conflict);
    expect(result.errorCode).toBe(ErrorCodes.CONFLICT);
    expect(findActiveByUserIdAndOrg.handleAsync).toHaveBeenCalledWith({
      userId: VALID_USER_ID,
      grantedToOrgId: VALID_ORG_ID,
    });
    expect(createRecord.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // DB unique constraint (via repo handler returning 409)
  // -----------------------------------------------------------------------

  it("should return 409 Conflict when repo.create returns conflict result", async () => {
    createRecord.handleAsync = vi.fn().mockResolvedValue(
      D2Result.fail({
        messages: ["Record already exists."],
        statusCode: HttpStatusCode.Conflict,
        errorCode: ErrorCodes.CONFLICT,
      }),
    );

    const futureDate = new Date(Date.now() + 86_400_000);
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      grantedToOrgId: VALID_ORG_ID,
      activeOrgType: "support",
      expiresAt: futureDate,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.Conflict);
    expect(result.errorCode).toBe(ErrorCodes.CONFLICT);
  });

  it("should return 500 when repo.create returns internal server error", async () => {
    createRecord.handleAsync = vi.fn().mockResolvedValue(
      D2Result.fail({
        messages: ["connection lost"],
        statusCode: HttpStatusCode.InternalServerError,
      }),
    );

    const futureDate = new Date(Date.now() + 86_400_000);
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      grantedToOrgId: VALID_ORG_ID,
      activeOrgType: "support",
      expiresAt: futureDate,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.InternalServerError);
  });
});
