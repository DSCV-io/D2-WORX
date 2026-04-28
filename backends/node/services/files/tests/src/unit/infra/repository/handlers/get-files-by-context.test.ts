import { describe, it, expect, vi } from "vitest";
import { GetFilesByContext } from "@d2/files-infra";
import { createTestContext, createSampleFileRow } from "../../helpers/test-context.js";

function createMockDb(options: { rows?: unknown[]; total?: number } = {}) {
  const { rows = [], total = 0 } = options;

  // Count query chain
  const countWhere = vi.fn().mockResolvedValue([{ total }]);
  const countFrom = vi.fn().mockReturnValue({ where: countWhere });

  // Data query chain
  const orderBy = vi.fn().mockResolvedValue(rows);
  const offset = vi.fn().mockReturnValue({ orderBy });
  const limit = vi.fn().mockReturnValue({ offset });
  const dataWhere = vi.fn().mockReturnValue({ limit });
  const dataFrom = vi.fn().mockReturnValue({ where: dataWhere });

  // select() alternates between data query and count query
  let selectCallCount = 0;
  const select = vi.fn().mockImplementation((...args: unknown[]) => {
    selectCallCount++;
    // First select = data query (no args or columns), second = count query (with { total: count() })
    if (args.length > 0 && typeof args[0] === "object") {
      return { from: countFrom };
    }
    return { from: dataFrom };
  });

  return { select, dataWhere, limit, offset, orderBy };
}

describe("GetFilesByContext", () => {
  it("should return files and total count on success", async () => {
    const row1 = createSampleFileRow({ id: "file-001" });
    const row2 = createSampleFileRow({ id: "file-002" });
    const { select } = createMockDb({ rows: [row1, row2], total: 2 });
    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    const result = await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
    });

    expect(result).toBeSuccess();
    expect(result.data?.files).toHaveLength(2);
    expect(result.data?.total).toBe(2);
  });

  it("should return empty files and zero total when no matches", async () => {
    const { select } = createMockDb({ rows: [], total: 0 });
    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    const result = await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "nonexistent-user",
    });

    expect(result).toBeSuccess();
    expect(result.data?.files).toHaveLength(0);
    expect(result.data?.total).toBe(0);
  });

  it("should default limit to 50 when not provided", async () => {
    const { select, limit } = createMockDb();
    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
    });

    expect(limit).toHaveBeenCalledWith(50);
  });

  it("should cap limit at 100 when larger value provided", async () => {
    const { select, limit } = createMockDb();
    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      limit: 500,
    });

    expect(limit).toHaveBeenCalledWith(100);
  });

  it("should use provided limit when within max", async () => {
    const { select, limit } = createMockDb();
    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      limit: 25,
    });

    expect(limit).toHaveBeenCalledWith(25);
  });

  it("should default offset to 0 when not provided", async () => {
    const { select, offset } = createMockDb();
    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
    });

    expect(offset).toHaveBeenCalledWith(0);
  });

  it("should use provided offset", async () => {
    const { select, offset } = createMockDb();
    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      offset: 10,
    });

    expect(offset).toHaveBeenCalledWith(10);
  });

  it("should map rows through toFile mapper", async () => {
    const row = createSampleFileRow({
      id: "file-abc",
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
    const { select } = createMockDb({ rows: [row], total: 1 });
    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    const result = await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
    });

    expect(result).toBeSuccess();
    expect(result.data?.files[0].id).toBe("file-abc");
    expect(result.data?.files[0].variants).toHaveLength(1);
    // updatedAt should not be in the mapped File
    expect("updatedAt" in result.data!.files[0]).toBe(false);
  });

  it("should default total to 0 when count result is empty", async () => {
    // Edge case: countResult[0] is undefined
    const countWhere = vi.fn().mockResolvedValue([]);
    const countFrom = vi.fn().mockReturnValue({ where: countWhere });

    const orderBy = vi.fn().mockResolvedValue([]);
    const offset = vi.fn().mockReturnValue({ orderBy });
    const limit = vi.fn().mockReturnValue({ offset });
    const dataWhere = vi.fn().mockReturnValue({ limit });
    const dataFrom = vi.fn().mockReturnValue({ where: dataWhere });

    const select = vi.fn().mockImplementation((...args: unknown[]) => {
      if (args.length > 0 && typeof args[0] === "object") {
        return { from: countFrom };
      }
      return { from: dataFrom };
    });

    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    const result = await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
    });

    expect(result).toBeSuccess();
    expect(result.data?.total).toBe(0);
  });

  it("should propagate DB errors as unhandled exceptions", async () => {
    const orderBy = vi.fn().mockRejectedValue(new Error("query failed"));
    const offset = vi.fn().mockReturnValue({ orderBy });
    const limit = vi.fn().mockReturnValue({ offset });
    const dataWhere = vi.fn().mockReturnValue({ limit });
    const dataFrom = vi.fn().mockReturnValue({ where: dataWhere });

    const countWhere = vi.fn().mockResolvedValue([{ total: 0 }]);
    const countFrom = vi.fn().mockReturnValue({ where: countWhere });

    const select = vi.fn().mockImplementation((...args: unknown[]) => {
      if (args.length > 0 && typeof args[0] === "object") {
        return { from: countFrom };
      }
      return { from: dataFrom };
    });

    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    const result = await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
    });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(500);
  });

  it("should always return ok (not notFound) even when no files exist", async () => {
    const { select } = createMockDb({ rows: [], total: 0 });
    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    const result = await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
    });

    // GetFilesByContext returns ok with empty array, not notFound
    expect(result).toBeSuccess();
    expect(result.statusCode).toBe(200);
    expect(result.data?.files).toEqual([]);
  });

  it("should handle limit of exactly 100 (boundary)", async () => {
    const { select, limit } = createMockDb();
    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      limit: 100,
    });

    expect(limit).toHaveBeenCalledWith(100);
  });

  it("should handle limit of 101 (capped to 100)", async () => {
    const { select, limit } = createMockDb();
    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      limit: 101,
    });

    expect(limit).toHaveBeenCalledWith(100);
  });

  // ---------------------------------------------------------------------------
  // Input validation
  // ---------------------------------------------------------------------------

  it("should reject empty contextKey with validationFailed", async () => {
    const { select } = createMockDb();
    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    const result = await handler.handleAsync({
      contextKey: "",
      relatedEntityId: "user-123",
    });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(400);
    expect(select).not.toHaveBeenCalled();
  });

  it("should reject contextKey over 100 chars with validationFailed", async () => {
    const { select } = createMockDb();
    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    const result = await handler.handleAsync({
      contextKey: "a".repeat(101),
      relatedEntityId: "user-123",
    });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(400);
    expect(select).not.toHaveBeenCalled();
  });

  it("should reject negative offset with validationFailed", async () => {
    const { select } = createMockDb();
    const db = { select } as never;
    const handler = new GetFilesByContext(db, createTestContext());

    const result = await handler.handleAsync({
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      offset: -1,
    });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(400);
    expect(select).not.toHaveBeenCalled();
  });
});
