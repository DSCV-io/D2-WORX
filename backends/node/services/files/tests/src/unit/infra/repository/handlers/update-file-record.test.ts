import { describe, it, expect, vi } from "vitest";
import { UpdateFileRecord } from "@d2/files-infra";
import type { File } from "@d2/files-domain";
import { createTestContext, createSampleFileRow } from "../../helpers/test-context.js";

function createMockDb(rows: unknown[] = []) {
  const returning = vi.fn().mockResolvedValue(rows);
  const where = vi.fn().mockReturnValue({ returning });
  const set = vi.fn().mockReturnValue({ where });
  const update = vi.fn().mockReturnValue({ set });
  return { update, set, where, returning };
}

function createSampleFile(overrides: Partial<File> = {}): File {
  return {
    id: "file-001",
    contextKey: "user_avatar",
    relatedEntityId: "user-123",
    uploaderUserId: "user-123",
    status: "processing",
    contentType: "image/jpeg",
    displayName: "avatar.jpg",
    sizeBytes: 2048,
    variants: undefined,
    rejectionReason: undefined,
    createdAt: new Date("2026-01-15T10:00:00Z"),
    ...overrides,
  };
}

describe("UpdateFileRecord", () => {
  it("should return the mapped updated file on success", async () => {
    const updatedRow = createSampleFileRow({
      id: "file-001",
      status: "ready",
      variants: [
        {
          size: "thumb",
          key: "k",
          width: 64,
          height: 64,
          sizeBytes: 100,
          contentType: "image/jpeg",
        },
      ],
    });
    const { update } = createMockDb([updatedRow]);
    const db = { update } as never;
    const handler = new UpdateFileRecord(db, createTestContext());

    const file = createSampleFile({ status: "ready" });
    const result = await handler.handleAsync({ file });

    expect(result).toBeSuccess();
    expect(result.data?.file).toBeDefined();
    expect(result.data?.file.id).toBe("file-001");
  });

  it("should return notFound when no rows are returned (file does not exist)", async () => {
    const { update } = createMockDb([]);
    const db = { update } as never;
    const handler = new UpdateFileRecord(db, createTestContext());

    const file = createSampleFile({ id: "nonexistent" });
    const result = await handler.handleAsync({ file });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(404);
  });

  it("should set the correct fields for update", async () => {
    const row = createSampleFileRow({ status: "rejected", rejectionReason: "corrupt_file" });
    const { update, set } = createMockDb([row]);
    const db = { update } as never;
    const handler = new UpdateFileRecord(db, createTestContext());

    const file = createSampleFile({
      status: "rejected",
      rejectionReason: "corrupt_file",
      displayName: "updated-avatar.jpg",
      contentType: "image/png",
      sizeBytes: 4096,
    });
    await handler.handleAsync({ file });

    const setArg = set.mock.calls[0][0];
    expect(setArg.status).toBe("rejected");
    expect(setArg.contentType).toBe("image/png");
    expect(setArg.displayName).toBe("updated-avatar.jpg");
    expect(setArg.sizeBytes).toBe(4096);
    expect(setArg.rejectionReason).toBe("corrupt_file");
    expect(setArg.updatedAt).toBeInstanceOf(Date);
  });

  it("should map undefined variants to undefined in set clause", async () => {
    const row = createSampleFileRow();
    const { update, set } = createMockDb([row]);
    const db = { update } as never;
    const handler = new UpdateFileRecord(db, createTestContext());

    const file = createSampleFile({ variants: undefined });
    await handler.handleAsync({ file });

    const setArg = set.mock.calls[0][0];
    expect(setArg.variants).toBeUndefined();
  });

  it("should map undefined rejectionReason to undefined in set clause", async () => {
    const row = createSampleFileRow();
    const { update, set } = createMockDb([row]);
    const db = { update } as never;
    const handler = new UpdateFileRecord(db, createTestContext());

    const file = createSampleFile({ rejectionReason: undefined });
    await handler.handleAsync({ file });

    const setArg = set.mock.calls[0][0];
    expect(setArg.rejectionReason).toBeUndefined();
  });

  it("should pass variants through when present", async () => {
    const variants = [
      {
        size: "thumb" as const,
        key: "k",
        width: 64,
        height: 64,
        sizeBytes: 100,
        contentType: "image/jpeg",
      },
    ];
    const row = createSampleFileRow({ variants });
    const { update, set } = createMockDb([row]);
    const db = { update } as never;
    const handler = new UpdateFileRecord(db, createTestContext());

    const file = createSampleFile({ status: "ready", variants });
    await handler.handleAsync({ file });

    const setArg = set.mock.calls[0][0];
    expect(setArg.variants).toEqual(variants);
  });

  it("should use toFile mapper on the returned row", async () => {
    const row = createSampleFileRow({
      id: "file-001",
      status: "ready",
      variants: [
        {
          size: "medium",
          key: "m",
          width: 256,
          height: 256,
          sizeBytes: 2000,
          contentType: "image/webp",
        },
      ],
    });
    const { update } = createMockDb([row]);
    const db = { update } as never;
    const handler = new UpdateFileRecord(db, createTestContext());

    const file = createSampleFile({ status: "ready" });
    const result = await handler.handleAsync({ file });

    expect(result).toBeSuccess();
    // Mapped file should not have updatedAt
    expect("updatedAt" in result.data!.file).toBe(false);
    expect(result.data?.file.variants).toHaveLength(1);
    expect(result.data?.file.variants![0].contentType).toBe("image/webp");
  });

  it("should propagate DB errors as unhandled exceptions", async () => {
    const returning = vi.fn().mockRejectedValue(new Error("deadlock"));
    const where = vi.fn().mockReturnValue({ returning });
    const set = vi.fn().mockReturnValue({ where });
    const update = vi.fn().mockReturnValue({ set });
    const db = { update } as never;
    const handler = new UpdateFileRecord(db, createTestContext());

    const file = createSampleFile();
    const result = await handler.handleAsync({ file });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(500);
  });

  it("should set updatedAt to a recent date", async () => {
    const row = createSampleFileRow();
    const { update, set } = createMockDb([row]);
    const db = { update } as never;
    const handler = new UpdateFileRecord(db, createTestContext());

    const before = new Date();
    await handler.handleAsync({ file: createSampleFile() });
    const after = new Date();

    const setArg = set.mock.calls[0][0];
    expect(setArg.updatedAt.getTime()).toBeGreaterThanOrEqual(before.getTime());
    expect(setArg.updatedAt.getTime()).toBeLessThanOrEqual(after.getTime());
  });
});
