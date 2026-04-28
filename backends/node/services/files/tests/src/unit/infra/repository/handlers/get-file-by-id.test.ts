import { describe, it, expect, vi } from "vitest";
import { GetFileById } from "@d2/files-infra";
import { createTestContext, createSampleFileRow } from "../../helpers/test-context.js";

function createMockDb(rows: unknown[] = []) {
  const limit = vi.fn().mockResolvedValue(rows);
  const where = vi.fn().mockReturnValue({ limit });
  const from = vi.fn().mockReturnValue({ where });
  const select = vi.fn().mockReturnValue({ from });
  return { select, from, where, limit };
}

describe("GetFileById", () => {
  it("should return the mapped file when found", async () => {
    const row = createSampleFileRow();
    const { select } = createMockDb([row]);
    const db = { select } as never;
    const handler = new GetFileById(db, createTestContext());

    const result = await handler.handleAsync({ id: "file-001" });

    expect(result).toBeSuccess();
    expect(result.data?.file).toBeDefined();
    expect(result.data?.file.id).toBe("file-001");
    expect(result.data?.file.contextKey).toBe("user_avatar");
    expect(result.data?.file.displayName).toBe("avatar.jpg");
  });

  it("should return notFound when no rows are returned", async () => {
    const { select } = createMockDb([]);
    const db = { select } as never;
    const handler = new GetFileById(db, createTestContext());

    const result = await handler.handleAsync({ id: "nonexistent-id" });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(404);
  });

  it("should use the first row when multiple are returned", async () => {
    const row1 = createSampleFileRow({ id: "file-001" });
    const row2 = createSampleFileRow({ id: "file-002" });
    const { select } = createMockDb([row1, row2]);
    const db = { select } as never;
    const handler = new GetFileById(db, createTestContext());

    const result = await handler.handleAsync({ id: "file-001" });

    expect(result).toBeSuccess();
    expect(result.data?.file.id).toBe("file-001");
  });

  it("should correctly map variants from the row", async () => {
    const variants = [
      {
        size: "thumb",
        key: "user_avatar/user-123/file-001/thumb.jpg",
        width: 64,
        height: 64,
        sizeBytes: 512,
        contentType: "image/jpeg",
      },
    ];
    const row = createSampleFileRow({ status: "ready", variants });
    const { select } = createMockDb([row]);
    const db = { select } as never;
    const handler = new GetFileById(db, createTestContext());

    const result = await handler.handleAsync({ id: "file-001" });

    expect(result).toBeSuccess();
    expect(result.data?.file.variants).toHaveLength(1);
    expect(result.data?.file.variants![0].size).toBe("thumb");
  });

  it("should correctly map rejectionReason from the row", async () => {
    const row = createSampleFileRow({
      status: "rejected",
      rejectionReason: "corrupt_file",
    });
    const { select } = createMockDb([row]);
    const db = { select } as never;
    const handler = new GetFileById(db, createTestContext());

    const result = await handler.handleAsync({ id: "file-001" });

    expect(result).toBeSuccess();
    expect(result.data?.file.rejectionReason).toBe("corrupt_file");
    expect(result.data?.file.status).toBe("rejected");
  });

  it("should not include updatedAt in the mapped File", async () => {
    const row = createSampleFileRow();
    const { select } = createMockDb([row]);
    const db = { select } as never;
    const handler = new GetFileById(db, createTestContext());

    const result = await handler.handleAsync({ id: "file-001" });

    expect(result).toBeSuccess();
    expect("updatedAt" in result.data!.file).toBe(false);
  });

  it("should propagate DB errors as unhandled exceptions", async () => {
    const limit = vi.fn().mockRejectedValue(new Error("connection timeout"));
    const where = vi.fn().mockReturnValue({ limit });
    const from = vi.fn().mockReturnValue({ where });
    const select = vi.fn().mockReturnValue({ from });
    const db = { select } as never;
    const handler = new GetFileById(db, createTestContext());

    const result = await handler.handleAsync({ id: "file-001" });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(500);
  });

  it("should call db.select with correct where clause using input.id", async () => {
    const { select, where } = createMockDb([]);
    const db = { select } as never;
    const handler = new GetFileById(db, createTestContext());

    await handler.handleAsync({ id: "my-file-id" });

    expect(select).toHaveBeenCalledTimes(1);
    expect(where).toHaveBeenCalledTimes(1);
  });

  // ---------------------------------------------------------------------------
  // Input validation
  // ---------------------------------------------------------------------------

  it("should reject empty id with validationFailed", async () => {
    const { select } = createMockDb([]);
    const db = { select } as never;
    const handler = new GetFileById(db, createTestContext());

    const result = await handler.handleAsync({ id: "" });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(400);
    expect(select).not.toHaveBeenCalled();
  });

  it("should reject id over 36 chars with validationFailed", async () => {
    const { select } = createMockDb([]);
    const db = { select } as never;
    const handler = new GetFileById(db, createTestContext());

    const result = await handler.handleAsync({ id: "a".repeat(37) });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(400);
    expect(select).not.toHaveBeenCalled();
  });
});
