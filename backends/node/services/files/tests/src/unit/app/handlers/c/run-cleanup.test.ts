import { describe, it, expect, vi } from "vitest";
import { D2Result } from "@d2/result";
import { RunCleanup, DEFAULT_FILES_JOB_OPTIONS } from "@d2/files-app";
import { createFile } from "@d2/files-domain";
import {
  createMockAcquireLock,
  createMockReleaseLock,
  createMockGetStaleFiles,
  createMockDeleteByIds,
  createMockStorage,
  createMockContext,
} from "../../helpers/mock-handlers.js";

function createHandler(
  overrides: {
    acquireLock?: ReturnType<typeof createMockAcquireLock>;
    releaseLock?: ReturnType<typeof createMockReleaseLock>;
    getStaleFiles?: ReturnType<typeof createMockGetStaleFiles>;
    deleteByIds?: ReturnType<typeof createMockDeleteByIds>;
    storage?: ReturnType<typeof createMockStorage>;
  } = {},
) {
  const acquireLock = overrides.acquireLock ?? createMockAcquireLock();
  const releaseLock = overrides.releaseLock ?? createMockReleaseLock();
  const getStaleFiles = overrides.getStaleFiles ?? createMockGetStaleFiles();
  const deleteByIds = overrides.deleteByIds ?? createMockDeleteByIds();
  const storage = overrides.storage ?? createMockStorage();
  const context = createMockContext();

  return {
    handler: new RunCleanup(
      acquireLock,
      releaseLock,
      getStaleFiles,
      deleteByIds,
      storage,
      DEFAULT_FILES_JOB_OPTIONS,
      context,
    ),
    acquireLock,
    releaseLock,
    getStaleFiles,
    deleteByIds,
    storage,
  };
}

describe("RunCleanup", () => {
  it("should return lockAcquired=false when lock is held", async () => {
    const acquireLock = createMockAcquireLock(false);
    const { handler } = createHandler({ acquireLock });

    const result = await handler.handleAsync({});

    expect(result.success).toBe(true);
    expect(result.data?.lockAcquired).toBe(false);
  });

  it("should always release lock in finally block", async () => {
    const releaseLock = createMockReleaseLock();
    const getStaleFiles = createMockGetStaleFiles();
    vi.mocked(getStaleFiles.handleAsync).mockRejectedValue(new Error("boom"));

    const { handler } = createHandler({ releaseLock, getStaleFiles });

    // Should not throw — BaseHandler catches exceptions
    const result = await handler.handleAsync({});
    // Lock should still be released
    expect(releaseLock.handleAsync).toHaveBeenCalled();
  });

  it("should clean stale files and return counts", async () => {
    const staleFile = createFile({
      contextKey: "user_avatar",
      relatedEntityId: "user-old",
      uploaderUserId: "user-123",
      contentType: "image/jpeg",
      displayName: "old.jpg",
      sizeBytes: 512,
      id: "stale-001",
    });
    const getStaleFiles = createMockGetStaleFiles([staleFile]);
    const storage = createMockStorage();
    const deleteByIds = createMockDeleteByIds();

    const { handler } = createHandler({ getStaleFiles, storage, deleteByIds });

    const result = await handler.handleAsync({});

    expect(result.success).toBe(true);
    expect(result.data?.lockAcquired).toBe(true);
    // getStaleFiles called 3 times (pending, processing, rejected)
    expect(getStaleFiles.handleAsync).toHaveBeenCalledTimes(3);
    // Storage deleteMany called for each status that has files
    expect(storage.deleteMany.handleAsync).toHaveBeenCalled();
    // DB delete called for each status that has files
    expect(deleteByIds.handleAsync).toHaveBeenCalled();
  });

  it("should report zero cleaned when no stale files found", async () => {
    const getStaleFiles = createMockGetStaleFiles([]);
    const { handler } = createHandler({ getStaleFiles });

    const result = await handler.handleAsync({});

    expect(result.success).toBe(true);
    expect(result.data?.pendingCleaned).toBe(0);
    expect(result.data?.processingCleaned).toBe(0);
    expect(result.data?.rejectedCleaned).toBe(0);
  });

  it("should include durationMs in output", async () => {
    const { handler } = createHandler();
    const result = await handler.handleAsync({});

    expect(result.success).toBe(true);
    expect(result.data?.durationMs).toBeGreaterThanOrEqual(0);
  });

  it("should search for each status with correct cutoff", async () => {
    const getStaleFiles = createMockGetStaleFiles([]);
    const { handler } = createHandler({ getStaleFiles });

    await handler.handleAsync({});

    const calls = vi.mocked(getStaleFiles.handleAsync).mock.calls;
    expect(calls).toHaveLength(3);

    // Verify statuses
    expect(calls[0]![0].status).toBe("pending");
    expect(calls[1]![0].status).toBe("processing");
    expect(calls[2]![0].status).toBe("rejected");

    // Verify cutoff dates are in the past
    for (const call of calls) {
      expect(call[0].cutoffDate.getTime()).toBeLessThan(Date.now());
    }
  });

  it("should pass batch limit to getStaleFiles", async () => {
    const getStaleFiles = createMockGetStaleFiles([]);
    const { handler } = createHandler({ getStaleFiles });

    await handler.handleAsync({});

    const calls = vi.mocked(getStaleFiles.handleAsync).mock.calls;
    for (const call of calls) {
      expect(call[0].limit).toBe(100);
    }
  });
});
