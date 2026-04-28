import { describe, it, expect, vi } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { UpdateUserImage } from "@d2/auth-infra";

function createTestContext() {
  const request: IRequestContext = {
    traceId: "trace-test",
    isAuthenticated: true,
    isTrustedService: false,
    isOrgEmulating: false,
    isUserImpersonating: false,
    isAgentStaff: false,
    isAgentAdmin: false,
    isTargetingStaff: false,
    isTargetingAdmin: false,
  };
  return new HandlerContext(request, createLogger({ level: "silent" as never }));
}

function createMockDb(rows: unknown[] = []) {
  const returning = vi.fn().mockResolvedValue(rows);
  const where = vi.fn().mockReturnValue({ returning });
  const set = vi.fn().mockReturnValue({ where });
  const update = vi.fn().mockReturnValue({ set });
  return { update, set, where, returning };
}

describe("UpdateUserImage", () => {
  it("should return success when user is found and updated", async () => {
    const { update, set } = createMockDb([{ id: "user-123" }]);
    const db = { update } as never;
    const handler = new UpdateUserImage(db, createTestContext());

    const result = await handler.handleAsync({
      userId: "user-123",
      image: "file-001",
      clear: false,
    });

    expect(result).toBeSuccess();
    expect(set.mock.calls[0][0].image).toBe("file-001");
    expect(set.mock.calls[0][0].updatedAt).toBeInstanceOf(Date);
  });

  it("should return notFound when user does not exist (empty returning)", async () => {
    const { update } = createMockDb([]);
    const db = { update } as never;
    const handler = new UpdateUserImage(db, createTestContext());

    const result = await handler.handleAsync({
      userId: "nonexistent-user",
      image: "file-002",
      clear: false,
    });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(404);
  });

  it("should write NULL when clear:true regardless of image value", async () => {
    const { update, set } = createMockDb([{ id: "user-123" }]);
    const db = { update } as never;
    const handler = new UpdateUserImage(db, createTestContext());

    const result = await handler.handleAsync({
      userId: "user-123",
      clear: true,
    });

    expect(result).toBeSuccess();
    expect(set.mock.calls[0][0].image).toBeNull();
  });

  it("should ignore image and write NULL when clear:true even if image is supplied", async () => {
    const { update, set } = createMockDb([{ id: "user-123" }]);
    const db = { update } as never;
    const handler = new UpdateUserImage(db, createTestContext());

    const result = await handler.handleAsync({
      userId: "user-123",
      image: "leftover-id-should-be-ignored",
      clear: true,
    });

    expect(result).toBeSuccess();
    expect(set.mock.calls[0][0].image).toBeNull();
  });

  it("should set updatedAt to a recent date", async () => {
    const { update, set } = createMockDb([{ id: "user-123" }]);
    const db = { update } as never;
    const handler = new UpdateUserImage(db, createTestContext());

    const before = new Date();
    await handler.handleAsync({ userId: "user-123", image: "file-001", clear: false });
    const after = new Date();

    const setArg = set.mock.calls[0][0];
    expect(setArg.updatedAt.getTime()).toBeGreaterThanOrEqual(before.getTime());
    expect(setArg.updatedAt.getTime()).toBeLessThanOrEqual(after.getTime());
  });

  it("should propagate DB errors as unhandled exceptions", async () => {
    const returning = vi.fn().mockRejectedValue(new Error("deadlock"));
    const where = vi.fn().mockReturnValue({ returning });
    const set = vi.fn().mockReturnValue({ where });
    const update = vi.fn().mockReturnValue({ set });
    const db = { update } as never;
    const handler = new UpdateUserImage(db, createTestContext());

    const result = await handler.handleAsync({
      userId: "user-123",
      image: "file-001",
      clear: false,
    });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(500);
  });
});
