import { describe, it, expect, vi, beforeEach } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { D2Result, HttpStatusCode } from "@d2/result";
import { GetActiveConsents } from "@d2/auth-app";
import type { IFindActiveConsentsByUserIdHandler } from "@d2/auth-app";
import type { EmulationConsent } from "@d2/auth-domain";

const VALID_USER_ID = "01234567-89ab-cdef-0123-456789abcdef";

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

function createMockFindActiveByUserId() {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { consents: [] } })),
  };
}

function createConsent(id: string): EmulationConsent {
  return {
    id,
    userId: VALID_USER_ID,
    grantedToOrgId: "abcdef01-2345-6789-abcd-ef0123456789",
    expiresAt: new Date(Date.now() + 86_400_000),
    revokedAt: undefined,
    createdAt: new Date("2026-02-01"),
  };
}

describe("GetActiveConsents", () => {
  let findActiveByUserId: ReturnType<typeof createMockFindActiveByUserId>;
  let handler: GetActiveConsents;

  beforeEach(() => {
    findActiveByUserId = createMockFindActiveByUserId();
    handler = new GetActiveConsents(
      findActiveByUserId as unknown as IFindActiveConsentsByUserIdHandler,
      createTestContext(),
    );
  });

  // -----------------------------------------------------------------------
  // Validation tests (Zod schema)
  // -----------------------------------------------------------------------

  it("should return validationFailed when userId is not a valid UUID", async () => {
    const result = await handler.handleAsync({ userId: "not-a-uuid" });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(Array.isArray(result.inputErrors)).toBe(true);
    expect(result.inputErrors.length).toBeGreaterThanOrEqual(1);
    expect(findActiveByUserId.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when limit is negative", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      limit: -1,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(Array.isArray(result.inputErrors)).toBe(true);
    expect(findActiveByUserId.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when limit exceeds 100", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      limit: 101,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(Array.isArray(result.inputErrors)).toBe(true);
    expect(findActiveByUserId.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when offset is negative", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      offset: -1,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toBeDefined();
    expect(Array.isArray(result.inputErrors)).toBe(true);
    expect(findActiveByUserId.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Existing business-logic tests
  // -----------------------------------------------------------------------

  it("should return active consents for a user", async () => {
    const consents = [createConsent("c-1"), createConsent("c-2")];
    findActiveByUserId.handleAsync = vi.fn().mockResolvedValue(D2Result.ok({ data: { consents } }));

    const result = await handler.handleAsync({ userId: VALID_USER_ID });

    expect(result.success).toBe(true);
    expect(result.data?.consents).toHaveLength(2);
    expect(findActiveByUserId.handleAsync).toHaveBeenCalledWith({
      userId: VALID_USER_ID,
      limit: 50,
      offset: 0,
    });
  });

  it("should apply default limit of 50 and offset of 0 when not provided", async () => {
    await handler.handleAsync({ userId: VALID_USER_ID });

    expect(findActiveByUserId.handleAsync).toHaveBeenCalledWith({
      userId: VALID_USER_ID,
      limit: 50,
      offset: 0,
    });
  });

  it("should use provided limit and offset when specified", async () => {
    await handler.handleAsync({ userId: VALID_USER_ID, limit: 10, offset: 20 });

    expect(findActiveByUserId.handleAsync).toHaveBeenCalledWith({
      userId: VALID_USER_ID,
      limit: 10,
      offset: 20,
    });
  });

  it("should return empty array when no active consents exist", async () => {
    const result = await handler.handleAsync({ userId: VALID_USER_ID });

    expect(result.success).toBe(true);
    expect(result.data?.consents).toHaveLength(0);
  });
});
