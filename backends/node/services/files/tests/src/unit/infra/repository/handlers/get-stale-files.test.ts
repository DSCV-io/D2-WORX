import { describe, it, expect, vi } from "vitest";
import { GetStaleFiles } from "@d2/files-infra";
import { createTestContext, createSampleFileRow } from "../../helpers/test-context.js";

function createMockDb(rows: unknown[] = []) {
  const limit = vi.fn().mockResolvedValue(rows);
  const where = vi.fn().mockReturnValue({ limit });
  const from = vi.fn().mockReturnValue({ where });
  const select = vi.fn().mockReturnValue({ from });
  return { select, from, where, limit };
}

describe("GetStaleFiles", () => {
  const cutoffDate = new Date("2026-01-01T00:00:00Z");

  it("should return mapped files when stale files exist", async () => {
    const row1 = createSampleFileRow({ id: "file-001", status: "pending" });
    const row2 = createSampleFileRow({ id: "file-002", status: "pending" });
    const { select } = createMockDb([row1, row2]);
    const db = { select } as never;
    const handler = new GetStaleFiles(db, createTestContext());

    const result = await handler.handleAsync({
      status: "pending",
      cutoffDate,
      limit: 100,
    });

    expect(result).toBeSuccess();
    expect(result.data?.files).toHaveLength(2);
    expect(result.data?.files[0].id).toBe("file-001");
    expect(result.data?.files[1].id).toBe("file-002");
  });

  it("should return empty array when no stale files found", async () => {
    const { select } = createMockDb([]);
    const db = { select } as never;
    const handler = new GetStaleFiles(db, createTestContext());

    const result = await handler.handleAsync({
      status: "pending",
      cutoffDate,
      limit: 100,
    });

    expect(result).toBeSuccess();
    expect(result.data?.files).toHaveLength(0);
  });

  it("should always return ok (not notFound) even when empty", async () => {
    const { select } = createMockDb([]);
    const db = { select } as never;
    const handler = new GetStaleFiles(db, createTestContext());

    const result = await handler.handleAsync({
      status: "processing",
      cutoffDate,
      limit: 50,
    });

    expect(result).toBeSuccess();
    expect(result.statusCode).toBe(200);
  });

  it("should pass the limit to the DB query", async () => {
    const { select, limit } = createMockDb([]);
    const db = { select } as never;
    const handler = new GetStaleFiles(db, createTestContext());

    await handler.handleAsync({
      status: "pending",
      cutoffDate,
      limit: 42,
    });

    expect(limit).toHaveBeenCalledWith(42);
  });

  it("should map rows through toFile (excludes updatedAt)", async () => {
    const row = createSampleFileRow({
      id: "file-stale",
      status: "pending",
      updatedAt: new Date("2025-12-01"),
    });
    const { select } = createMockDb([row]);
    const db = { select } as never;
    const handler = new GetStaleFiles(db, createTestContext());

    const result = await handler.handleAsync({
      status: "pending",
      cutoffDate,
      limit: 10,
    });

    expect(result).toBeSuccess();
    expect(result.data?.files[0].id).toBe("file-stale");
    expect("updatedAt" in result.data!.files[0]).toBe(false);
  });

  it("should propagate DB errors as unhandled exceptions", async () => {
    const limit = vi.fn().mockRejectedValue(new Error("connection lost"));
    const where = vi.fn().mockReturnValue({ limit });
    const from = vi.fn().mockReturnValue({ where });
    const select = vi.fn().mockReturnValue({ from });
    const db = { select } as never;
    const handler = new GetStaleFiles(db, createTestContext());

    const result = await handler.handleAsync({
      status: "pending",
      cutoffDate,
      limit: 100,
    });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(500);
  });

  it("should handle processing status filter", async () => {
    const row = createSampleFileRow({ id: "file-proc", status: "processing" });
    const { select } = createMockDb([row]);
    const db = { select } as never;
    const handler = new GetStaleFiles(db, createTestContext());

    const result = await handler.handleAsync({
      status: "processing",
      cutoffDate,
      limit: 50,
    });

    expect(result).toBeSuccess();
    expect(result.data?.files[0].status).toBe("processing");
  });

  it("should correctly handle files with variants in the result", async () => {
    const variants = [
      {
        size: "thumb",
        key: "k",
        width: 64,
        height: 64,
        sizeBytes: 100,
        contentType: "image/jpeg",
      },
    ];
    const row = createSampleFileRow({ status: "ready", variants });
    const { select } = createMockDb([row]);
    const db = { select } as never;
    const handler = new GetStaleFiles(db, createTestContext());

    const result = await handler.handleAsync({
      status: "ready",
      cutoffDate,
      limit: 10,
    });

    expect(result).toBeSuccess();
    expect(result.data?.files[0].variants).toHaveLength(1);
  });

  // ---------------------------------------------------------------------------
  // Input validation
  // ---------------------------------------------------------------------------

  it("should reject invalid status with validationFailed", async () => {
    const { select } = createMockDb([]);
    const db = { select } as never;
    const handler = new GetStaleFiles(db, createTestContext());

    const result = await handler.handleAsync({
      // Cast — runtime garbage from a misbehaving caller.
      status: "garbage" as never,
      cutoffDate,
      limit: 10,
    });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(400);
    expect(select).not.toHaveBeenCalled();
  });

  it("should reject negative limit with validationFailed", async () => {
    const { select } = createMockDb([]);
    const db = { select } as never;
    const handler = new GetStaleFiles(db, createTestContext());

    const result = await handler.handleAsync({
      status: "pending",
      cutoffDate,
      limit: -1,
    });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(400);
    expect(select).not.toHaveBeenCalled();
  });

  it("should reject zero limit with validationFailed", async () => {
    const { select } = createMockDb([]);
    const db = { select } as never;
    const handler = new GetStaleFiles(db, createTestContext());

    const result = await handler.handleAsync({
      status: "pending",
      cutoffDate,
      limit: 0,
    });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(400);
    expect(select).not.toHaveBeenCalled();
  });
});
