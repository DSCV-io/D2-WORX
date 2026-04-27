import { describe, it, expect, vi } from "vitest";
import { D2Result } from "@d2/result";
import { GetFileMetadata } from "@d2/files-app";
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
    handler: new GetFileMetadata(repo, TEST_CONFIG_MAP, context, resolveAccess),
    repo,
    resolveAccess,
  };
}

function makeFile() {
  return createFile({
    contextKey: "user_avatar",
    relatedEntityId: "user-123",
    uploaderUserId: "user-123",
    contentType: "image/jpeg",
    displayName: "avatar.jpg",
    sizeBytes: 1024,
    id: "file-001",
  });
}

describe("GetFileMetadata", () => {
  it("should return file metadata on success", async () => {
    const repo = createMockRepo();
    const file = makeFile();
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));
    const { handler } = createHandler({ repo });

    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(true);
    expect(result.data?.file.id).toBe("file-001");
    expect(result.data?.file.displayName).toBe("avatar.jpg");
  });

  it("should return notFound when file does not exist", async () => {
    const repo = createMockRepo();
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.notFound());
    const { handler } = createHandler({ repo });

    const result = await handler.handleAsync({ fileId: "nonexistent" });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(404);
  });

  it("should return forbidden when context key config not found", async () => {
    const repo = createMockRepo();
    const file = createFile({
      contextKey: "unknown_key",
      relatedEntityId: "user-123",
      uploaderUserId: "user-123",
      contentType: "image/jpeg",
      displayName: "test.jpg",
      sizeBytes: 512,
      id: "file-002",
    });
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));
    const { handler } = createHandler({ repo });

    const result = await handler.handleAsync({ fileId: "file-002" });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(403);
  });

  it("should return forbidden when access is denied", async () => {
    const repo = createMockRepo();
    const file = makeFile();
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));
    const { handler } = createHandler({
      repo,
      resolveAccess: createMockResolveFileAccess(false),
    });

    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(403);
  });

  it("should return validation error for empty fileId", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({ fileId: "" });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should delegate to resolveAccess with correct input", async () => {
    const repo = createMockRepo();
    const file = makeFile();
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));
    const resolveAccess = createMockResolveFileAccess();
    const { handler } = createHandler({ repo, resolveAccess });

    await handler.handleAsync({ fileId: "file-001" });

    expect(resolveAccess.handleAsync).toHaveBeenCalledWith({
      config: TEST_USER_AVATAR_CONFIG,
      action: "read",
      relatedEntityId: "user-123",
    });
  });

  it("should propagate repo failure", async () => {
    const repo = createMockRepo();
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.serviceUnavailable());
    const { handler } = createHandler({ repo });

    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(503);
  });
});
