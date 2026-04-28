import { describe, it, expect, vi, beforeEach } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { D2Result, HttpStatusCode } from "@d2/result";
import { HandleFileProcessed } from "@d2/auth-app";
import type { IUpdateUserImageHandler, IUpdateOrgLogoHandler } from "@d2/auth-app";

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

function createMockUpdateUserImage() {
  return { handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })) };
}

function createMockUpdateOrgLogo() {
  return { handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })) };
}

describe("HandleFileProcessed", () => {
  let updateUserImage: ReturnType<typeof createMockUpdateUserImage>;
  let updateOrgLogo: ReturnType<typeof createMockUpdateOrgLogo>;
  let handler: HandleFileProcessed;

  beforeEach(() => {
    updateUserImage = createMockUpdateUserImage();
    updateOrgLogo = createMockUpdateOrgLogo();
    handler = new HandleFileProcessed(
      updateUserImage as unknown as IUpdateUserImageHandler,
      updateOrgLogo as unknown as IUpdateOrgLogoHandler,
      createTestContext(),
    );
  });

  // -----------------------------------------------------------------------
  // user_avatar + ready
  // -----------------------------------------------------------------------

  it("should call updateUserImage with correct userId/fileId for user_avatar + ready", async () => {
    const result = await handler.handleAsync({
      fileId: "file-001",
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      status: "ready",
      variants: ["thumb", "medium"],
    });

    expect(result.success).toBe(true);
    expect(result.data?.success).toBe(true);
    expect(updateUserImage.handleAsync).toHaveBeenCalledOnce();
    expect(updateUserImage.handleAsync).toHaveBeenCalledWith({
      userId: "user-123",
      image: "file-001",
      clear: false,
    });
    expect(updateOrgLogo.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // org_logo + ready
  // -----------------------------------------------------------------------

  it("should call updateOrgLogo with correct orgId/fileId for org_logo + ready", async () => {
    const result = await handler.handleAsync({
      fileId: "file-002",
      contextKey: "org_logo",
      relatedEntityId: "org-789",
      status: "ready",
    });

    expect(result.success).toBe(true);
    expect(result.data?.success).toBe(true);
    expect(updateOrgLogo.handleAsync).toHaveBeenCalledOnce();
    expect(updateOrgLogo.handleAsync).toHaveBeenCalledWith({
      orgId: "org-789",
      logo: "file-002",
      clear: false,
    });
    expect(updateUserImage.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // org_document + ready
  // -----------------------------------------------------------------------

  it("should return success without calling any repo handler for org_document + ready", async () => {
    const result = await handler.handleAsync({
      fileId: "file-003",
      contextKey: "org_document",
      relatedEntityId: "org-456",
      status: "ready",
    });

    expect(result.success).toBe(true);
    expect(result.data?.success).toBe(true);
    expect(updateUserImage.handleAsync).not.toHaveBeenCalled();
    expect(updateOrgLogo.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // rejected status
  // -----------------------------------------------------------------------

  it("should return success and NOT call any repo handler for rejected status", async () => {
    const result = await handler.handleAsync({
      fileId: "file-004",
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      status: "rejected",
    });

    expect(result.success).toBe(true);
    expect(result.data?.success).toBe(true);
    expect(updateUserImage.handleAsync).not.toHaveBeenCalled();
    expect(updateOrgLogo.handleAsync).not.toHaveBeenCalled();
  });

  it("should return success for rejected org_logo without calling updateOrgLogo", async () => {
    const result = await handler.handleAsync({
      fileId: "file-005",
      contextKey: "org_logo",
      relatedEntityId: "org-789",
      status: "rejected",
    });

    expect(result.success).toBe(true);
    expect(updateOrgLogo.handleAsync).not.toHaveBeenCalled();
    expect(updateUserImage.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // unknown context key
  // -----------------------------------------------------------------------

  it("should return success for unknown context key (logs warning)", async () => {
    const result = await handler.handleAsync({
      fileId: "file-006",
      contextKey: "unknown_key",
      relatedEntityId: "entity-999",
      status: "ready",
    });

    expect(result.success).toBe(true);
    expect(result.data?.success).toBe(true);
    expect(updateUserImage.handleAsync).not.toHaveBeenCalled();
    expect(updateOrgLogo.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Validation failures
  // -----------------------------------------------------------------------

  it("should return validationFailed when fileId is empty", async () => {
    const result = await handler.handleAsync({
      fileId: "",
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      status: "ready",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(updateUserImage.handleAsync).not.toHaveBeenCalled();
    expect(updateOrgLogo.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when contextKey is empty", async () => {
    const result = await handler.handleAsync({
      fileId: "file-001",
      contextKey: "",
      relatedEntityId: "user-123",
      status: "ready",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(updateUserImage.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when relatedEntityId is empty", async () => {
    const result = await handler.handleAsync({
      fileId: "file-001",
      contextKey: "user_avatar",
      relatedEntityId: "",
      status: "ready",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(updateUserImage.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when status is invalid", async () => {
    const result = await handler.handleAsync({
      fileId: "file-001",
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      status: "processing" as "ready",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(updateUserImage.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when fileId exceeds 36 characters", async () => {
    const result = await handler.handleAsync({
      fileId: "x".repeat(37),
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      status: "ready",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(updateUserImage.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when contextKey exceeds 100 characters", async () => {
    const result = await handler.handleAsync({
      fileId: "file-001",
      contextKey: "x".repeat(101),
      relatedEntityId: "user-123",
      status: "ready",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(updateUserImage.handleAsync).not.toHaveBeenCalled();
  });

  it("should accept valid input at max field lengths", async () => {
    const result = await handler.handleAsync({
      fileId: "x".repeat(36),
      contextKey: "x".repeat(100),
      relatedEntityId: "x".repeat(255),
      status: "ready",
    });

    expect(result.success).toBe(true);
  });

  // -----------------------------------------------------------------------
  // Error propagation
  // -----------------------------------------------------------------------

  it("should bubble failure when updateUserImage returns error", async () => {
    updateUserImage.handleAsync = vi.fn().mockResolvedValue(
      D2Result.fail({
        messages: ["user not found"],
        statusCode: HttpStatusCode.NotFound,
      }),
    );

    const result = await handler.handleAsync({
      fileId: "file-001",
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      status: "ready",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.NotFound);
    expect(updateUserImage.handleAsync).toHaveBeenCalledOnce();
  });

  it("should bubble failure when updateOrgLogo returns error", async () => {
    updateOrgLogo.handleAsync = vi.fn().mockResolvedValue(
      D2Result.fail({
        messages: ["org not found"],
        statusCode: HttpStatusCode.NotFound,
      }),
    );

    const result = await handler.handleAsync({
      fileId: "file-002",
      contextKey: "org_logo",
      relatedEntityId: "org-789",
      status: "ready",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.NotFound);
    expect(updateOrgLogo.handleAsync).toHaveBeenCalledOnce();
  });

  it("should bubble 500 when updateUserImage returns internal error", async () => {
    updateUserImage.handleAsync = vi.fn().mockResolvedValue(
      D2Result.fail({
        messages: ["connection lost"],
        statusCode: HttpStatusCode.InternalServerError,
      }),
    );

    const result = await handler.handleAsync({
      fileId: "file-001",
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      status: "ready",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.InternalServerError);
    expect(updateUserImage.handleAsync).toHaveBeenCalledOnce();
  });
});
