import { describe, it, expect, vi } from "vitest";
import { D2Result } from "@d2/result";
import { DeleteFile } from "@d2/files-app";
import { createFile, transitionFileStatus, createFileVariant } from "@d2/files-domain";
import {
  createMockRepo,
  createMockStorage,
  createMockContext,
  createMockResolveFileAccess,
} from "../../helpers/mock-handlers.js";
import { TEST_CONFIG_MAP } from "../../helpers/test-config.js";

function createHandler(
  overrides: {
    repo?: ReturnType<typeof createMockRepo>;
    storage?: ReturnType<typeof createMockStorage>;
    accessAllowed?: boolean;
  } = {},
) {
  const repo = overrides.repo ?? createMockRepo();
  const storage = overrides.storage ?? createMockStorage();
  const resolveAccess = createMockResolveFileAccess(overrides.accessAllowed ?? true);
  const context = createMockContext();
  return {
    handler: new DeleteFile(repo, storage, TEST_CONFIG_MAP, context, resolveAccess),
    repo,
    storage,
    resolveAccess,
  };
}

function makePendingFile() {
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

function makeReadyFile() {
  const pending = makePendingFile();
  const processing = transitionFileStatus(pending, "processing");
  const variant = createFileVariant({
    size: "original",
    key: "user_avatar/user-123/file-001/original.jpg",
    width: 100,
    height: 100,
    sizeBytes: 1024,
    contentType: "image/jpeg",
  });
  return transitionFileStatus(processing, "ready", { variants: [variant] });
}

describe("DeleteFile", () => {
  it("should delete storage objects and DB record", async () => {
    const repo = createMockRepo();
    const storage = createMockStorage();
    const file = makePendingFile();
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));
    const { handler } = createHandler({ repo, storage });

    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(true);
    expect(storage.deleteMany.handleAsync).toHaveBeenCalled();
    expect(repo.delete.handleAsync).toHaveBeenCalledWith({ id: "file-001" });
  });

  it("should include variant keys when deleting ready file", async () => {
    const repo = createMockRepo();
    const storage = createMockStorage();
    const file = makeReadyFile();
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));
    const { handler } = createHandler({ repo, storage });

    await handler.handleAsync({ fileId: "file-001" });

    const deleteCall = vi.mocked(storage.deleteMany.handleAsync).mock.calls[0]![0];
    expect(deleteCall.keys.length).toBeGreaterThan(1); // raw + at least one variant
  });

  it("should return notFound when file does not exist", async () => {
    const repo = createMockRepo();
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.notFound());
    const { handler } = createHandler({ repo });

    const result = await handler.handleAsync({ fileId: "nonexistent" });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(404);
  });

  it("should return validation error for empty fileId", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({ fileId: "" });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should propagate repo delete failure", async () => {
    const repo = createMockRepo();
    const file = makePendingFile();
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));
    vi.mocked(repo.delete.handleAsync).mockResolvedValue(D2Result.serviceUnavailable());
    const { handler } = createHandler({ repo });

    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(503);
  });

  it("should return forbidden when access is denied", async () => {
    const repo = createMockRepo();
    const file = makePendingFile();
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));
    const { handler } = createHandler({ repo, accessAllowed: false });

    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(403);
  });
});
