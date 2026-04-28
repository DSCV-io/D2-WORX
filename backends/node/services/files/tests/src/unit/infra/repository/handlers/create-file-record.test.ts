import { describe, it, expect, vi } from "vitest";
import { CreateFileRecord } from "@d2/files-infra";
import type { File } from "@d2/files-domain";
import { createTestContext } from "../../helpers/test-context.js";

function createMockDb() {
  const returning = vi.fn().mockResolvedValue([]);
  const values = vi.fn().mockReturnValue({ returning });
  const insert = vi.fn().mockReturnValue({ values });
  return { insert, values, returning };
}

function createSampleFile(overrides: Partial<File> = {}): File {
  return {
    id: "file-001",
    contextKey: "user_avatar",
    relatedEntityId: "user-123",
    uploaderUserId: "user-123",
    status: "pending",
    contentType: "image/jpeg",
    displayName: "avatar.jpg",
    sizeBytes: 2048,
    variants: undefined,
    rejectionReason: undefined,
    createdAt: new Date("2026-01-15T10:00:00Z"),
    ...overrides,
  };
}

describe("CreateFileRecord", () => {
  it("should insert a file record and return ok with the file", async () => {
    const { insert, values } = createMockDb();
    const db = { insert } as never;
    const handler = new CreateFileRecord(db, createTestContext());
    const file = createSampleFile();

    const result = await handler.handleAsync({ file });

    expect(result).toBeSuccess();
    expect(result.data?.file).toBe(file);
    expect(insert).toHaveBeenCalledTimes(1);
    expect(values).toHaveBeenCalledWith({
      id: "file-001",
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      uploaderUserId: "user-123",
      status: "pending",
      contentType: "image/jpeg",
      displayName: "avatar.jpg",
      sizeBytes: 2048,
      variants: undefined,
      rejectionReason: undefined,
      createdAt: new Date("2026-01-15T10:00:00Z"),
    });
  });

  it("should map undefined variants to undefined for the DB insert", async () => {
    const { insert, values } = createMockDb();
    const db = { insert } as never;
    const handler = new CreateFileRecord(db, createTestContext());
    const file = createSampleFile({ variants: undefined });

    await handler.handleAsync({ file });

    const insertedValues = values.mock.calls[0][0];
    expect(insertedValues.variants).toBeUndefined();
  });

  it("should map undefined rejectionReason to undefined for the DB insert", async () => {
    const { insert, values } = createMockDb();
    const db = { insert } as never;
    const handler = new CreateFileRecord(db, createTestContext());
    const file = createSampleFile({ rejectionReason: undefined });

    await handler.handleAsync({ file });

    const insertedValues = values.mock.calls[0][0];
    expect(insertedValues.rejectionReason).toBeUndefined();
  });

  it("should pass variants through when present", async () => {
    const { insert, values } = createMockDb();
    const db = { insert } as never;
    const handler = new CreateFileRecord(db, createTestContext());
    const variants = [
      {
        size: "thumb" as const,
        key: "user_avatar/user-123/file-001/thumb.jpg",
        width: 64,
        height: 64,
        sizeBytes: 512,
        contentType: "image/jpeg",
      },
    ];
    const file = createSampleFile({ status: "ready", variants });

    await handler.handleAsync({ file });

    const insertedValues = values.mock.calls[0][0];
    expect(insertedValues.variants).toEqual(variants);
  });

  it("should pass rejectionReason through when present", async () => {
    const { insert, values } = createMockDb();
    const db = { insert } as never;
    const handler = new CreateFileRecord(db, createTestContext());
    const file = createSampleFile({
      status: "rejected",
      rejectionReason: "size_exceeded",
    });

    await handler.handleAsync({ file });

    const insertedValues = values.mock.calls[0][0];
    expect(insertedValues.rejectionReason).toBe("size_exceeded");
  });

  it("should propagate DB errors as unhandled exceptions", async () => {
    const values = vi.fn().mockRejectedValue(new Error("connection refused"));
    const insert = vi.fn().mockReturnValue({ values });
    const db = { insert } as never;
    const handler = new CreateFileRecord(db, createTestContext());
    const file = createSampleFile();

    const result = await handler.handleAsync({ file });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(500);
  });

  it("should return the exact input file object in the output", async () => {
    const { insert } = createMockDb();
    const db = { insert } as never;
    const handler = new CreateFileRecord(db, createTestContext());
    const file = createSampleFile();

    const result = await handler.handleAsync({ file });

    expect(result).toBeSuccess();
    // CreateFileRecord returns the input file directly, not a DB-mapped version
    expect(result.data?.file).toBe(file);
  });

  it("should return status 200 (ok) on success", async () => {
    const { insert } = createMockDb();
    const db = { insert } as never;
    const handler = new CreateFileRecord(db, createTestContext());

    const result = await handler.handleAsync({ file: createSampleFile() });

    expect(result.statusCode).toBe(200);
    expect(result.success).toBe(true);
  });
});
