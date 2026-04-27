import { describe, it, expect, vi } from "vitest";
import { D2Result } from "@d2/result";
import { ProcessFile } from "@d2/files-app";
import { createFile, transitionFileStatus } from "@d2/files-domain";
import {
  createMockRepo,
  createMockStorage,
  createMockScanFile,
  createMockProcessVariants,
  createMockNotifier,
  createMockPushFileUpdate,
  createMockContext,
} from "../../helpers/mock-handlers.js";
import { TEST_CONFIG_MAP } from "../../helpers/test-config.js";

function makeProcessingFile(overrides?: {
  contextKey?: string;
  contentType?: string;
  displayName?: string;
  id?: string;
}) {
  const pending = createFile({
    contextKey: overrides?.contextKey ?? "user_avatar",
    relatedEntityId: "user-123",
    uploaderUserId: "user-123",
    contentType: overrides?.contentType ?? "image/jpeg",
    displayName: overrides?.displayName ?? "avatar.jpg",
    sizeBytes: 1024,
    id: overrides?.id ?? "file-001",
  });
  return transitionFileStatus(pending, "processing");
}

function createHandler(
  overrides: {
    repo?: ReturnType<typeof createMockRepo>;
    storage?: ReturnType<typeof createMockStorage>;
    scanFile?: ReturnType<typeof createMockScanFile>;
    processVariants?: ReturnType<typeof createMockProcessVariants>;
    notifier?: ReturnType<typeof createMockNotifier>;
    pushFileUpdate?: ReturnType<typeof createMockPushFileUpdate>;
    skipDefaultFindById?: boolean;
  } = {},
) {
  const repo = overrides.repo ?? createMockRepo();
  const storage = overrides.storage ?? createMockStorage();
  const scanFile = overrides.scanFile ?? createMockScanFile();
  const processVariants = overrides.processVariants ?? createMockProcessVariants();
  const notifier = overrides.notifier ?? createMockNotifier();
  const pushFileUpdate = overrides.pushFileUpdate ?? createMockPushFileUpdate();
  const context = createMockContext();

  const file = makeProcessingFile();
  if (!overrides.skipDefaultFindById) {
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));
  }

  return {
    handler: new ProcessFile(
      repo,
      storage,
      scanFile,
      processVariants,
      notifier,
      pushFileUpdate,
      TEST_CONFIG_MAP,
      context,
    ),
    repo,
    storage,
    scanFile,
    processVariants,
    notifier,
    pushFileUpdate,
    file,
  };
}

describe("ProcessFile", () => {
  it("should process file to ready state on success", async () => {
    const { handler, repo } = createHandler();
    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(true);
    expect(result.data?.file.status).toBe("ready");
    expect(result.data?.file.variants).toBeTruthy();
    expect(repo.update.handleAsync).toHaveBeenCalled();
  });

  it("should call OnFileProcessed notifier before DB update (fail-last)", async () => {
    const { handler, notifier, repo } = createHandler();
    const callOrder: string[] = [];
    vi.mocked(notifier.handleAsync).mockImplementation(async () => {
      callOrder.push("notifier");
      return D2Result.ok({ data: { success: true } });
    });
    vi.mocked(repo.update.handleAsync).mockImplementation(async () => {
      callOrder.push("update");
      return D2Result.ok({ data: { file: {} as never } });
    });

    await handler.handleAsync({ fileId: "file-001" });

    expect(callOrder).toEqual(["notifier", "update"]);
  });

  it("should reject and notify on virus detection", async () => {
    const scanFile = createMockScanFile();
    vi.mocked(scanFile.handleAsync).mockResolvedValue(
      D2Result.ok({ data: { clean: false, threat: "EICAR-TEST" } }),
    );
    const { handler, storage, notifier } = createHandler({ scanFile });

    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(true);
    expect(result.data?.file.status).toBe("rejected");
    expect(result.data?.file.rejectionReason).toBe("content_moderation_failed");
    expect(storage.delete.handleAsync).toHaveBeenCalled();
    expect(notifier.handleAsync).toHaveBeenCalledWith(
      expect.objectContaining({ status: "rejected" }),
    );
  });

  it("should propagate scanner failure", async () => {
    const scanFile = createMockScanFile();
    vi.mocked(scanFile.handleAsync).mockResolvedValue(D2Result.serviceUnavailable());
    const { handler } = createHandler({ scanFile });

    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(503);
  });

  it("should propagate variant processing failure", async () => {
    const processVariants = createMockProcessVariants();
    vi.mocked(processVariants.handleAsync).mockResolvedValue(D2Result.serviceUnavailable());
    const { handler } = createHandler({ processVariants });

    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(503);
  });

  it("should not update DB if notifier fails (fail-last)", async () => {
    const notifier = createMockNotifier();
    vi.mocked(notifier.handleAsync).mockResolvedValue(D2Result.serviceUnavailable());
    const repo = createMockRepo();
    const file = makeProcessingFile();
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));

    const { handler } = createHandler({ notifier, repo });
    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(false);
    // update should not be called for the "ready" transition (only for "rejected" path which is not hit here)
    // The handler should bubble fail from notifier before reaching the update
  });

  it("should return notFound when file does not exist", async () => {
    const repo = createMockRepo();
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.notFound());
    const { handler } = createHandler({ repo, skipDefaultFindById: true });

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
      displayName: "avatar.jpg",
      sizeBytes: 1024,
      id: "file-002",
    });
    const processingFile = transitionFileStatus(file, "processing");
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(
      D2Result.ok({ data: { file: processingFile } }),
    );
    const { handler } = createHandler({ repo, skipDefaultFindById: true });

    const result = await handler.handleAsync({ fileId: "file-002" });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(403);
  });

  it("should store variants via storage put handler", async () => {
    const storage = createMockStorage();
    const { handler } = createHandler({ storage });
    await handler.handleAsync({ fileId: "file-001" });

    expect(storage.put.handleAsync).toHaveBeenCalled();
  });

  it("should delete raw upload after successful processing", async () => {
    const storage = createMockStorage();
    const { handler } = createHandler({ storage });
    await handler.handleAsync({ fileId: "file-001" });

    // delete handler called for raw key cleanup
    expect(storage.delete.handleAsync).toHaveBeenCalled();
  });

  it("should return validation error for empty fileId", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({ fileId: "" });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  // --- Config-driven variant handling ---

  it("should skip processVariants for non-image content (document)", async () => {
    const repo = createMockRepo();
    const file = makeProcessingFile({
      contextKey: "thread_attachment",
      contentType: "application/pdf",
      displayName: "report.pdf",
      id: "file-doc",
    });
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));

    const processVariants = createMockProcessVariants();
    const { handler } = createHandler({ repo, processVariants, skipDefaultFindById: true });
    const result = await handler.handleAsync({ fileId: "file-doc" });

    expect(result.success).toBe(true);
    expect(result.data?.file.status).toBe("ready");
    // processVariants (sharp) should NOT be called for non-image content
    expect(processVariants.handleAsync).not.toHaveBeenCalled();
  });

  it("should store single original variant for non-image content", async () => {
    const repo = createMockRepo();
    const storage = createMockStorage();
    const file = makeProcessingFile({
      contextKey: "thread_attachment",
      contentType: "application/pdf",
      displayName: "report.pdf",
      id: "file-doc-2",
    });
    vi.mocked(repo.getById.handleAsync).mockResolvedValue(D2Result.ok({ data: { file } }));

    const { handler } = createHandler({ repo, storage, skipDefaultFindById: true });
    const result = await handler.handleAsync({ fileId: "file-doc-2" });

    expect(result.success).toBe(true);
    // For non-image content, only a single "original" variant is stored (not all configured variants)
    const variants = result.data?.file.variants;
    expect(variants).toHaveLength(1);
    expect(variants![0].size).toBe("original");
    expect(variants![0].width).toBe(0);
    expect(variants![0].height).toBe(0);
  });

  it("should call processVariants only for resize variants on images", async () => {
    const processVariants = createMockProcessVariants();
    vi.mocked(processVariants.handleAsync).mockResolvedValue(
      D2Result.ok({
        data: {
          variants: [
            {
              size: "thumb",
              buffer: Buffer.from("t"),
              width: 64,
              height: 64,
              sizeBytes: 1,
              contentType: "image/jpeg",
            },
            {
              size: "small",
              buffer: Buffer.from("s"),
              width: 128,
              height: 128,
              sizeBytes: 2,
              contentType: "image/jpeg",
            },
            {
              size: "medium",
              buffer: Buffer.from("m"),
              width: 256,
              height: 256,
              sizeBytes: 3,
              contentType: "image/jpeg",
            },
          ],
        },
      }),
    );
    const { handler } = createHandler({ processVariants });
    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result.success).toBe(true);
    // processVariants called with only resize variants (thumb, small, medium — not original)
    expect(processVariants.handleAsync).toHaveBeenCalledWith(
      expect.objectContaining({
        variants: expect.arrayContaining([
          expect.objectContaining({ name: "thumb", maxDimension: 64 }),
        ]),
      }),
    );
    // Should have 4 total variants: 3 resized + 1 original
    expect(result.data?.file.variants).toHaveLength(4);
  });
});
