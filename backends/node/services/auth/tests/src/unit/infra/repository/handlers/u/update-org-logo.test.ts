import { describe, it, expect, vi } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { UpdateOrgLogo } from "@d2/auth-infra";

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

describe("UpdateOrgLogo", () => {
  it("should return success when organization is found and updated", async () => {
    const { update, set } = createMockDb([{ id: "org-789" }]);
    const db = { update } as never;
    const handler = new UpdateOrgLogo(db, createTestContext());

    const result = await handler.handleAsync({
      orgId: "org-789",
      logo: "file-002",
      clear: false,
    });

    expect(result).toBeSuccess();
    expect(set.mock.calls[0][0].logo).toBe("file-002");
    expect(set.mock.calls[0][0].updatedAt).toBeInstanceOf(Date);
  });

  it("should return notFound when organization does not exist (empty returning)", async () => {
    const { update } = createMockDb([]);
    const db = { update } as never;
    const handler = new UpdateOrgLogo(db, createTestContext());

    const result = await handler.handleAsync({
      orgId: "nonexistent-org",
      logo: "file-003",
      clear: false,
    });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(404);
  });

  it("should write NULL when clear:true regardless of logo value", async () => {
    const { update, set } = createMockDb([{ id: "org-789" }]);
    const db = { update } as never;
    const handler = new UpdateOrgLogo(db, createTestContext());

    const result = await handler.handleAsync({
      orgId: "org-789",
      clear: true,
    });

    expect(result).toBeSuccess();
    expect(set.mock.calls[0][0].logo).toBeNull();
  });

  it("should ignore logo and write NULL when clear:true even if logo is supplied", async () => {
    const { update, set } = createMockDb([{ id: "org-789" }]);
    const db = { update } as never;
    const handler = new UpdateOrgLogo(db, createTestContext());

    const result = await handler.handleAsync({
      orgId: "org-789",
      logo: "leftover-id-should-be-ignored",
      clear: true,
    });

    expect(result).toBeSuccess();
    expect(set.mock.calls[0][0].logo).toBeNull();
  });

  it("should set updatedAt to a recent date", async () => {
    const { update, set } = createMockDb([{ id: "org-789" }]);
    const db = { update } as never;
    const handler = new UpdateOrgLogo(db, createTestContext());

    const before = new Date();
    await handler.handleAsync({ orgId: "org-789", logo: "file-002", clear: false });
    const after = new Date();

    const setArg = set.mock.calls[0][0];
    expect(setArg.updatedAt.getTime()).toBeGreaterThanOrEqual(before.getTime());
    expect(setArg.updatedAt.getTime()).toBeLessThanOrEqual(after.getTime());
  });

  it("should propagate DB errors as unhandled exceptions", async () => {
    const returning = vi.fn().mockRejectedValue(new Error("connection refused"));
    const where = vi.fn().mockReturnValue({ returning });
    const set = vi.fn().mockReturnValue({ where });
    const update = vi.fn().mockReturnValue({ set });
    const db = { update } as never;
    const handler = new UpdateOrgLogo(db, createTestContext());

    const result = await handler.handleAsync({
      orgId: "org-789",
      logo: "file-002",
      clear: false,
    });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(500);
  });
});
