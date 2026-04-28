import { describe, it, expect, vi } from "vitest";
import { D2Result } from "@d2/result";
import { IntakeFile, type ContextKeyConfig, type ContextKeyConfigMap } from "@d2/files-app";
import { createFile } from "@d2/files-domain";
import {
  createMockRepo,
  createMockStorage,
  createMockContext,
} from "../../helpers/mock-handlers.js";

const _DEFAULT_CONFIG: ContextKeyConfig = {
  contextKey: "user_avatar",
  uploadResolution: "jwt_owner",
  readResolution: "authenticated",
  listResolution: "jwt_owner",
  callbackAddress: "auth:5101",
  allowedCategories: ["image"],
  maxSizeBytes: 5_242_880, // 5MB
  variants: [],
};

function createConfigs(overrides: Partial<ContextKeyConfig> = {}): ContextKeyConfigMap {
  const merged = { ..._DEFAULT_CONFIG, ...overrides };
  return new Map([[merged.contextKey, merged]]);
}

function createHandler(
  repo = createMockRepo(),
  storage = createMockStorage(),
  configs: ContextKeyConfigMap = createConfigs(),
) {
  const context = createMockContext();
  const storagePick = { head: storage.head, delete: storage.delete };
  return { handler: new IntakeFile(repo, storagePick, configs, context), repo, storage };
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

  it("should reject upload when actual size exceeds the contextKey's maxSizeBytes (declared was within cap)", async () => {
    // Regression for B3: a malicious client can declare a small size to pass
    // UploadFile's pre-signing validation, then PUT a much larger object.
    // Without the config-cap check IntakeFile only compares actual vs declared,
    // so an oversized upload could land if declared was below the cap.
    const repo = createMockRepo();
    const storage = createMockStorage();
    const file = { ...makePendingFile(), sizeBytes: 4_000_000 }; // declared 4MB (within 5MB cap)
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));
    vi.mocked(storage.head.handleAsync).mockResolvedValue(
      D2Result.ok({ data: { sizeBytes: 5_000_000_000, contentType: "image/jpeg" } }), // 5GB actual
    );
    vi.mocked(storage.delete.handleAsync).mockResolvedValue(
      D2Result.ok({ data: { deleted: true } }),
    );
    vi.mocked(repo.update.handleAsync).mockResolvedValue(
      D2Result.ok({ data: { file: { ...file, status: "rejected" } } }),
    );
    const { handler } = createHandler(repo, storage, createConfigs({ maxSizeBytes: 5_242_880 }));

    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(true);
    expect(result.data?.discarded).toBe(true);
    expect(result.data?.reason).toBe("size_mismatch");
    // The oversized object MUST be deleted from storage to prevent cost amplification
    expect(storage.delete.handleAsync).toHaveBeenCalledTimes(1);
    // Status MUST flip to "rejected" so the row doesn't get re-processed
    expect(repo.update.handleAsync).toHaveBeenCalledTimes(1);
  });

  it("should accept upload when actual size is within declared and within config cap", async () => {
    const repo = createMockRepo();
    const storage = createMockStorage();
    const file = { ...makePendingFile(), sizeBytes: 1_000_000 };
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));
    vi.mocked(storage.head.handleAsync).mockResolvedValue(
      D2Result.ok({ data: { sizeBytes: 950_000, contentType: "image/jpeg" } }),
    );
    vi.mocked(repo.update.handleAsync).mockResolvedValue(
      D2Result.ok({ data: { file: { ...file, status: "processing" } } }),
    );
    const { handler } = createHandler(repo, storage, createConfigs({ maxSizeBytes: 5_242_880 }));

    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(true);
    expect(result.data?.discarded).toBe(false);
    expect(storage.delete.handleAsync).not.toHaveBeenCalled();
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
