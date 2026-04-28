import { describe, it, expect, vi } from "vitest";
import { DeleteFileRecordsByIds } from "@d2/files-infra";
import { createTestContext } from "../../helpers/test-context.js";

function createMockDb(rows: unknown[] = []) {
  const returning = vi.fn().mockResolvedValue(rows);
  const where = vi.fn().mockReturnValue({ returning });
  const deleteFn = vi.fn().mockReturnValue({ where });
  return { delete: deleteFn, where, returning };
}

describe("DeleteFileRecordsByIds", () => {
  it("should return rowsAffected matching deleted count", async () => {
    const { delete: deleteFn } = createMockDb([
      { id: "file-001" },
      { id: "file-002" },
      { id: "file-003" },
    ]);
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecordsByIds(db, createTestContext());

    const result = await handler.handleAsync({
      ids: ["file-001", "file-002", "file-003"],
    });

    expect(result).toBeSuccess();
    expect(result.data?.rowsAffected).toBe(3);
  });

  it("should return rowsAffected:0 when empty ids array is passed", async () => {
    const { delete: deleteFn } = createMockDb();
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecordsByIds(db, createTestContext());

    const result = await handler.handleAsync({ ids: [] });

    expect(result).toBeSuccess();
    expect(result.data?.rowsAffected).toBe(0);
    // Should NOT call db.delete when ids is empty
    expect(deleteFn).not.toHaveBeenCalled();
  });

  it("should skip db.delete call for empty ids (short-circuit)", async () => {
    const { delete: deleteFn } = createMockDb();
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecordsByIds(db, createTestContext());

    await handler.handleAsync({ ids: [] });

    expect(deleteFn).not.toHaveBeenCalled();
  });

  it("should return rowsAffected:0 when no IDs match", async () => {
    const { delete: deleteFn } = createMockDb([]);
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecordsByIds(db, createTestContext());

    const result = await handler.handleAsync({
      ids: ["nonexistent-1", "nonexistent-2"],
    });

    expect(result).toBeSuccess();
    expect(result.data?.rowsAffected).toBe(0);
  });

  it("should handle partial matches (some ids exist, some don't)", async () => {
    const { delete: deleteFn } = createMockDb([{ id: "file-001" }]);
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecordsByIds(db, createTestContext());

    const result = await handler.handleAsync({
      ids: ["file-001", "nonexistent"],
    });

    expect(result).toBeSuccess();
    expect(result.data?.rowsAffected).toBe(1);
  });

  it("should return ok (never notFound) even when no rows are deleted", async () => {
    const { delete: deleteFn } = createMockDb([]);
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecordsByIds(db, createTestContext());

    const result = await handler.handleAsync({ ids: ["no-match"] });

    // Unlike single delete, batch delete always returns ok
    expect(result).toBeSuccess();
    expect(result.statusCode).toBe(200);
  });

  it("should propagate DB errors as unhandled exceptions", async () => {
    const returning = vi.fn().mockRejectedValue(new Error("disk full"));
    const where = vi.fn().mockReturnValue({ returning });
    const deleteFn = vi.fn().mockReturnValue({ where });
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecordsByIds(db, createTestContext());

    const result = await handler.handleAsync({
      ids: ["file-001"],
    });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(500);
  });

  it("should handle single id in array", async () => {
    const { delete: deleteFn } = createMockDb([{ id: "only-one" }]);
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecordsByIds(db, createTestContext());

    const result = await handler.handleAsync({ ids: ["only-one"] });

    expect(result).toBeSuccess();
    expect(result.data?.rowsAffected).toBe(1);
  });

  it("should call db.delete when ids array has elements", async () => {
    const { delete: deleteFn } = createMockDb([{ id: "file-001" }]);
    const db = { delete: deleteFn } as never;
    const handler = new DeleteFileRecordsByIds(db, createTestContext());

    await handler.handleAsync({ ids: ["file-001"] });

    expect(deleteFn).toHaveBeenCalledTimes(1);
  });
});
