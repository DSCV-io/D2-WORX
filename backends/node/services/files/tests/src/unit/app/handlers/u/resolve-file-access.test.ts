import { describe, it, expect, vi } from "vitest";
import { D2Result } from "@d2/result";
import { ResolveFileAccess } from "@d2/files-app";
import type { ContextKeyConfig } from "@d2/files-app";
import { createMockContext, createMockAccessChecker } from "../../helpers/mock-handlers.js";

function makeConfig(overrides: Partial<ContextKeyConfig> = {}): ContextKeyConfig {
  return {
    contextKey: "user_avatar",
    uploadResolution: "jwt_owner",
    readResolution: "jwt_owner",
    listResolution: "jwt_owner",
    callbackAddress: "auth:5101",
    allowedCategories: ["image"],
    maxSizeBytes: 5 * 1024 * 1024,
    variants: [{ name: "original" }],
    ...overrides,
  };
}

describe("ResolveFileAccess", () => {
  // --- jwt_owner resolution ---

  it("should allow when userId matches relatedEntityId for jwt_owner", async () => {
    const context = createMockContext({ userId: "user-123" });
    const handler = new ResolveFileAccess(context);

    const result = await handler.handleAsync({
      config: makeConfig({ uploadResolution: "jwt_owner" }),
      action: "upload",
      relatedEntityId: "user-123",
    });

    expect(result.success).toBe(true);
  });

  it("should deny when userId does not match for jwt_owner", async () => {
    const context = createMockContext({ userId: "other-user" });
    const handler = new ResolveFileAccess(context);

    const result = await handler.handleAsync({
      config: makeConfig({ uploadResolution: "jwt_owner" }),
      action: "upload",
      relatedEntityId: "user-123",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(403);
  });

  it("should use readResolution when action is read", async () => {
    const context = createMockContext({ userId: "user-123" });
    const handler = new ResolveFileAccess(context);

    const result = await handler.handleAsync({
      config: makeConfig({ readResolution: "jwt_owner" }),
      action: "read",
      relatedEntityId: "user-123",
    });

    expect(result.success).toBe(true);
  });

  it("should use listResolution when action is list (NOT readResolution)", async () => {
    // Regression for B6: prior to the split, listing fell back to
    // readResolution. A contextKey shipping with `read=authenticated` would
    // accidentally allow any logged-in user to enumerate `relatedEntityId=*`
    // — cross-tenant leak. The split makes list semantics explicit.
    const context = createMockContext({ userId: "user-123", isAuthenticated: true });
    const handler = new ResolveFileAccess(context);

    // Read is `authenticated` (public-by-id), but list is `jwt_owner`
    // (only your own collection). Listing for someone else's id should fail.
    const result = await handler.handleAsync({
      config: makeConfig({ readResolution: "authenticated", listResolution: "jwt_owner" }),
      action: "list",
      relatedEntityId: "another-user-id",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(403);

    // And listing your own id with the same config should succeed.
    const ownResult = await handler.handleAsync({
      config: makeConfig({ readResolution: "authenticated", listResolution: "jwt_owner" }),
      action: "list",
      relatedEntityId: "user-123",
    });
    expect(ownResult.success).toBe(true);
  });

  // --- jwt_org resolution ---

  it("should allow when targetOrgId matches relatedEntityId for jwt_org", async () => {
    const context = createMockContext({ targetOrgId: "org-456" });
    const handler = new ResolveFileAccess(context);

    const result = await handler.handleAsync({
      config: makeConfig({ uploadResolution: "jwt_org" }),
      action: "upload",
      relatedEntityId: "org-456",
    });

    expect(result.success).toBe(true);
  });

  it("should deny when targetOrgId does not match for jwt_org", async () => {
    const context = createMockContext({ targetOrgId: "org-456" });
    const handler = new ResolveFileAccess(context);

    const result = await handler.handleAsync({
      config: makeConfig({ uploadResolution: "jwt_org" }),
      action: "upload",
      relatedEntityId: "org-999",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(403);
  });

  // --- authenticated resolution ---

  it("should allow any authenticated user for authenticated resolution", async () => {
    const context = createMockContext({ isAuthenticated: true });
    const handler = new ResolveFileAccess(context);

    const result = await handler.handleAsync({
      config: makeConfig({ readResolution: "authenticated" }),
      action: "read",
      relatedEntityId: "thread-1",
    });

    expect(result.success).toBe(true);
  });

  it("should return unauthorized when not authenticated", async () => {
    const context = createMockContext({ isAuthenticated: false });
    const handler = new ResolveFileAccess(context);

    const result = await handler.handleAsync({
      config: makeConfig({ readResolution: "authenticated" }),
      action: "read",
      relatedEntityId: "thread-1",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(401);
  });

  it("should return unauthorized when isAuthenticated is null (pre-auth)", async () => {
    const context = createMockContext({ isAuthenticated: null });
    const handler = new ResolveFileAccess(context);

    const result = await handler.handleAsync({
      config: makeConfig({ readResolution: "authenticated" }),
      action: "read",
      relatedEntityId: "thread-1",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(401);
  });

  // --- callback resolution ---

  it("should delegate to accessChecker for callback resolution", async () => {
    const context = createMockContext();
    const accessChecker = createMockAccessChecker(true);
    const handler = new ResolveFileAccess(context, accessChecker);

    const config = makeConfig({
      uploadResolution: "callback",
      callbackAddress: "comms:3200",
      contextKey: "thread_attachment",
    });

    const result = await handler.handleAsync({
      config,
      action: "upload",
      relatedEntityId: "thread-1",
    });

    expect(result.success).toBe(true);
    expect(accessChecker.handleAsync).toHaveBeenCalledWith({
      address: "comms:3200",
      contextKey: "thread_attachment",
      relatedEntityId: "thread-1",
      requestingUserId: "user-123",
      requestingOrgId: "org-456",
      action: "upload",
    });
  });

  it("should return forbidden when accessChecker denies", async () => {
    const context = createMockContext();
    const accessChecker = createMockAccessChecker(false);
    const handler = new ResolveFileAccess(context, accessChecker);

    const result = await handler.handleAsync({
      config: makeConfig({
        uploadResolution: "callback",
        callbackAddress: "comms:3200",
      }),
      action: "upload",
      relatedEntityId: "thread-1",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(403);
  });

  it("should return forbidden when no accessChecker is registered for callback", async () => {
    const context = createMockContext();
    const handler = new ResolveFileAccess(context);

    const result = await handler.handleAsync({
      config: makeConfig({
        uploadResolution: "callback",
        callbackAddress: "comms:3200",
      }),
      action: "upload",
      relatedEntityId: "thread-1",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(403);
  });

  it("should propagate accessChecker failure via bubbleFail", async () => {
    const context = createMockContext();
    const accessChecker = createMockAccessChecker();
    vi.mocked(accessChecker.handleAsync).mockResolvedValue(D2Result.serviceUnavailable());
    const handler = new ResolveFileAccess(context, accessChecker);

    const result = await handler.handleAsync({
      config: makeConfig({
        uploadResolution: "callback",
        callbackAddress: "comms:3200",
      }),
      action: "upload",
      relatedEntityId: "thread-1",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(403);
  });

  // --- Unknown resolution ---

  it("should return forbidden for unknown resolution type", async () => {
    const context = createMockContext();
    const handler = new ResolveFileAccess(context);

    const result = await handler.handleAsync({
      config: makeConfig({ uploadResolution: "unknown" as "jwt_owner" }),
      action: "upload",
      relatedEntityId: "user-123",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(403);
  });
});
