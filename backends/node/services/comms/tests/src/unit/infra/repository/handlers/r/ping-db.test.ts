import { describe, it, expect, vi, beforeEach } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { PingDb } from "@d2/comms-infra";

function createTestContext(): HandlerContext {
  const request: IRequestContext = {
    traceId: "trace-ping-db-test",
    isAuthenticated: false,
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

function createMockDb(limitFn: ReturnType<typeof vi.fn> = vi.fn().mockResolvedValue([])) {
  return {
    select: vi.fn().mockReturnValue({
      from: vi.fn().mockReturnValue({
        limit: limitFn,
      }),
    }),
  } as never;
}

describe("PingDb", () => {
  let mockLimit: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    mockLimit = vi.fn().mockResolvedValue([]);
  });

  it("should return healthy:true with latencyMs when db query succeeds", async () => {
    const handler = new PingDb(createMockDb(mockLimit), createTestContext());

    const result = await handler.handleAsync({});

    expect(result).toBeSuccess();
    expect(result.data?.healthy).toBe(true);
    expect(result.data?.latencyMs).toBeTypeOf("number");
    expect(result.data?.error).toBeUndefined();
  });

  it("should return healthy:false with error message when db query throws Error", async () => {
    mockLimit.mockRejectedValue(new Error("Connection refused"));
    const handler = new PingDb(createMockDb(mockLimit), createTestContext());

    const result = await handler.handleAsync({});

    expect(result).toBeSuccess();
    expect(result.data?.healthy).toBe(false);
    expect(result.data?.error).toBe("Connection refused");
  });

  it('should return healthy:false with "Unknown error" when db query throws non-Error', async () => {
    mockLimit.mockRejectedValue("some string error");
    const handler = new PingDb(createMockDb(mockLimit), createTestContext());

    const result = await handler.handleAsync({});

    expect(result).toBeSuccess();
    expect(result.data?.healthy).toBe(false);
    expect(result.data?.error).toBe("Unknown error");
  });

  it("should return latencyMs as a non-negative integer", async () => {
    const handler = new PingDb(createMockDb(mockLimit), createTestContext());

    const result = await handler.handleAsync({});

    expect(result).toBeSuccess();
    const latencyMs = result.data!.latencyMs;
    expect(latencyMs).toBeGreaterThanOrEqual(0);
    expect(Number.isInteger(latencyMs)).toBe(true);
  });

  it("should return D2Result.ok even on DB failure (health check never fails)", async () => {
    mockLimit.mockRejectedValue(new Error("Database crashed"));
    const handler = new PingDb(createMockDb(mockLimit), createTestContext());

    const result = await handler.handleAsync({});

    expect(result.success).toBe(true);
    expect(result.statusCode).toBe(200);
    expect(result.data?.healthy).toBe(false);
    expect(result.data?.latencyMs).toBeGreaterThanOrEqual(0);
    expect(result.data?.error).toBe("Database crashed");
  });
});
