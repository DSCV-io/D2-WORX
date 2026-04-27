import { describe, it, expect, vi } from "vitest";
import { D2Result } from "@d2/result";
import { ListFiles } from "@d2/files-app";
import { createFile } from "@d2/files-domain";
import {
  createMockRepo,
  createMockContext,
  createMockResolveFileAccess,
} from "../../helpers/mock-handlers.js";
import { TEST_CONFIG_MAP, TEST_USER_AVATAR_CONFIG } from "../../helpers/test-config.js";

function createHandler(
  overrides: {
    repo?: ReturnType<typeof createMockRepo>;
    contextOverrides?: Parameters<typeof createMockContext>[0];
    resolveAccess?: ReturnType<typeof createMockResolveFileAccess>;
  } = {},
) {
  const repo = overrides.repo ?? createMockRepo();
  const context = createMockContext(overrides.contextOverrides);
  const resolveAccess = overrides.resolveAccess ?? createMockResolveFileAccess();
  return {
    handler: new ListFiles(repo, TEST_CONFIG_MAP, context, resolveAccess),
    repo,
    resolveAccess,
  };
}

describe("ListFiles", () => {
  it("should return files on success", async () => {
    const repo = createMockRepo();
    const file = createFile({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      uploaderUserId: "user-123",
      contentType: "image/jpeg",
      displayName: "avatar.jpg",
      sizeBytes: 1024,
    });
    vi.mocked(repo.getByContext.handleAsync).mockResolvedValue(
      D2Result.ok({ data: { files: [file], total: 1 } }),
    );
    const { handler } = createHandler({ repo });

    const result = await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
    });

    expect(result.success).toBe(true);
    expect(result.data?.files).toHaveLength(1);
    expect(result.data?.total).toBe(1);
  });

  it("should return forbidden for unknown context key", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({
      contextKey: "unknown_key",
      relatedEntityId: "user-123",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(403);
  });

  it("should return forbidden when access is denied", async () => {
    const { handler } = createHandler({
      resolveAccess: createMockResolveFileAccess(false),
    });
    const result = await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(403);
  });

  it("should return validation error for empty contextKey", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({
      contextKey: "",
      relatedEntityId: "user-123",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should return validation error for empty relatedEntityId", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should pass default limit and offset to repo", async () => {
    const repo = createMockRepo();
    const { handler } = createHandler({ repo });

    await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
    });

    expect(repo.getByContext.handleAsync).toHaveBeenCalledWith({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      limit: 50,
      offset: 0,
    });
  });

  it("should reject limit over 100", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      limit: 101,
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should delegate to resolveAccess with correct input", async () => {
    const resolveAccess = createMockResolveFileAccess();
    const { handler } = createHandler({ resolveAccess });

    await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
    });

    expect(resolveAccess.handleAsync).toHaveBeenCalledWith({
      config: TEST_USER_AVATAR_CONFIG,
      action: "read",
      relatedEntityId: "user-123",
    });
  });
});
