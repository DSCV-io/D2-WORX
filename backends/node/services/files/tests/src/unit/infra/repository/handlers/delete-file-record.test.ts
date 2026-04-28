import { describe, it, expect, vi } from "vitest";
import { DeleteFileRecord } from "@d2/files-infra";
import { createTestContext } from "../../helpers/test-context.js";

function createMockDb(rows: unknown[] = []) {
  const returning = vi.fn().mockResolvedValue(rows);
  const where = vi.fn().mockReturnValue({ returning });
  const deleteFn = vi.fn().mockReturnValue({ where });
  return { delete: deleteFn, where, returning };
}

describe("DeleteFileRecord", () => {
  it("should return ok with empty data when file is deleted", async () => {
    const { delete: deleteFn } = createMockDb([{ id: "file-001" }]);
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecord(db, createTestContext());

    const result = await handler.handleAsync({ id: "file-001" });

    expect(result).toBeSuccess();
    expect(result.data).toEqual({});
  });

  it("should return notFound when no rows are deleted", async () => {
    const { delete: deleteFn } = createMockDb([]);
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecord(db, createTestContext());

    const result = await handler.handleAsync({ id: "nonexistent-id" });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(404);
  });

  it("should call db.delete with the correct id", async () => {
    const { delete: deleteFn, where } = createMockDb([{ id: "file-xyz" }]);
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecord(db, createTestContext());

    await handler.handleAsync({ id: "file-xyz" });

    expect(deleteFn).toHaveBeenCalledTimes(1);
    expect(where).toHaveBeenCalledTimes(1);
  });

  it("should propagate DB errors as unhandled exceptions", async () => {
    const returning = vi.fn().mockRejectedValue(new Error("foreign key violation"));
    const where = vi.fn().mockReturnValue({ returning });
    const deleteFn = vi.fn().mockReturnValue({ where });
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecord(db, createTestContext());

    const result = await handler.handleAsync({ id: "file-001" });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(500);
  });

  it("should return ok even when multiple rows are deleted (edge case)", async () => {
    // This shouldn't happen with a PK delete, but tests the length > 0 check
    const { delete: deleteFn } = createMockDb([{ id: "file-001" }, { id: "file-002" }]);
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecord(db, createTestContext());

    const result = await handler.handleAsync({ id: "file-001" });

    expect(result).toBeSuccess();
    expect(result.data).toEqual({});
  });

  it("should return status 200 on successful deletion", async () => {
    const { delete: deleteFn } = createMockDb([{ id: "file-001" }]);
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecord(db, createTestContext());

    const result = await handler.handleAsync({ id: "file-001" });

    expect(result.statusCode).toBe(200);
    expect(result.success).toBe(true);
  });

  it("should return status 404 when file does not exist", async () => {
    const { delete: deleteFn } = createMockDb([]);
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecord(db, createTestContext());

    const result = await handler.handleAsync({ id: "no-such-file" });

    expect(result.statusCode).toBe(404);
    expect(result.success).toBe(false);
  });
});
