import { describe, it, expect, vi } from "vitest";
import { D2Result } from "@d2/result";
import { IntakeFile } from "@d2/files-app";
import { createFile } from "@d2/files-domain";
import {
  createMockRepo,
  createMockStorage,
  createMockContext,
} from "../../helpers/mock-handlers.js";

function createHandler(repo = createMockRepo(), storage = createMockStorage()) {
  const context = createMockContext();
  const storagePick = { head: storage.head, delete: storage.delete };
  return { handler: new IntakeFile(repo, storagePick, context), repo, storage };
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

describe("IntakeFile", () => {
  it("should transition pending file to processing", async () => {
    const repo = createMockRepo();
    const file = makePendingFile();
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));
    // Mock update to return the file in "processing" status (optimistic concurrency check)
    vi.mocked(repo.update.handleAsync).mockResolvedValue(
      D2Result.ok({ data: { file: { ...file, status: "processing" } } }),
    );
    const { handler } = createHandler(repo);

    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(true);
    expect(result.data?.discarded).toBe(false);
    expect(result.data?.file?.status).toBe("processing");
    expect(repo.update.handleAsync).toHaveBeenCalledTimes(1);
  });

  it("should discard when file not found", async () => {
    const repo = createMockRepo();
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.notFound());
    const { handler } = createHandler(repo);

    const result = await handler.handleAsync({ fileId: "nonexistent" });

    expect(result.success).toBe(true);
    expect(result.data?.discarded).toBe(true);
    expect(result.data?.reason).toBe("not_found");
  });

  it("should discard when file is already processing", async () => {
    const repo = createMockRepo();
    const file = { ...makePendingFile(), status: "processing" as const };
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));
    const { handler } = createHandler(repo);

    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(true);
    expect(result.data?.discarded).toBe(true);
    expect(result.data?.reason).toBe("wrong_status");
  });

  it("should discard when file is ready", async () => {
    const repo = createMockRepo();
    const file = { ...makePendingFile(), status: "ready" as const };
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));
    const { handler } = createHandler(repo);

    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(true);
    expect(result.data?.discarded).toBe(true);
    expect(result.data?.reason).toBe("wrong_status");
  });

  it("should return validation error for empty fileId", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({ fileId: "" });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("should propagate update failure", async () => {
    const repo = createMockRepo();
    const file = makePendingFile();
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));
    vi.mocked(repo.update.handleAsync).mockResolvedValue(D2Result.serviceUnavailable());
    const { handler } = createHandler(repo);

    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(503);
  });
});
