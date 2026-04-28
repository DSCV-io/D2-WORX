import { describe, it, expect, vi } from "vitest";
import { D2Result } from "@d2/result";
import { CheckHealth } from "@d2/files-app";
import {
  createMockPingDb,
  createMockStorage,
  createMockContext,
} from "../../helpers/mock-handlers.js";

describe("CheckHealth", () => {
  it("should return healthy when all components are healthy", async () => {
    const pingDb = createMockPingDb(true);
    const storage = createMockStorage();
    const context = createMockContext();
    const handler = new CheckHealth(pingDb as never, storage, context);

    const result = await handler.handleAsync({});

    expect(result.success).toBe(true);
    expect(result.data?.status).toBe("healthy");
    expect(result.data?.components["db"]?.status).toBe("healthy");
    expect(result.data?.components["storage"]?.status).toBe("healthy");
  });

  it("should return degraded when DB is unhealthy", async () => {
    const pingDb = {
      handleAsync: vi
        .fn()
        .mockResolvedValue(D2Result.serviceUnavailable({ messages: ["DB down"] })),
    };
    const storage = createMockStorage();
    const context = createMockContext();
    const handler = new CheckHealth(pingDb as never, storage, context);

    const result = await handler.handleAsync({});

    expect(result.success).toBe(true);
    expect(result.data?.status).toBe("degraded");
    expect(result.data?.components["db"]?.status).toBe("unhealthy");
    expect(result.data?.components["db"]?.error).toBe("Ping handler failed");
  });

  it("should return degraded when storage is unhealthy", async () => {
    const pingDb = createMockPingDb(true);
    const storage = createMockStorage();
    vi.mocked(storage.ping.handleAsync).mockResolvedValue(
      D2Result.serviceUnavailable({ messages: ["timeout"] }),
    );
    const context = createMockContext();
    const handler = new CheckHealth(pingDb as never, storage, context);

    const result = await handler.handleAsync({});

    expect(result.success).toBe(true);
    expect(result.data?.status).toBe("degraded");
    expect(result.data?.components["storage"]?.status).toBe("unhealthy");
    expect(result.data?.components["storage"]?.error).toBe("Ping handler failed");
  });

  it("should include latencyMs from components", async () => {
    const pingDb = createMockPingDb(true);
    const storage = createMockStorage();
    vi.mocked(storage.ping.handleAsync).mockResolvedValue(
      D2Result.ok({ data: { healthy: true, latencyMs: 42 } }),
    );
    const context = createMockContext();
    const handler = new CheckHealth(pingDb as never, storage, context);

    const result = await handler.handleAsync({});

    expect(result.data?.components["storage"]?.latencyMs).toBe(42);
  });

  it("should return degraded when both DB and storage are unhealthy", async () => {
    const pingDb = {
      handleAsync: vi
        .fn()
        .mockResolvedValue(D2Result.serviceUnavailable({ messages: ["DB connection refused"] })),
    };
    const storage = createMockStorage();
    vi.mocked(storage.ping.handleAsync).mockResolvedValue(
      D2Result.serviceUnavailable({ messages: ["Storage unreachable"] }),
    );
    const context = createMockContext();
    const handler = new CheckHealth(pingDb as never, storage, context);

    const result = await handler.handleAsync({});

    expect(result.success).toBe(true);
    expect(result.data?.status).toBe("degraded");
    expect(result.data?.components["db"]?.status).toBe("unhealthy");
    expect(result.data?.components["storage"]?.status).toBe("unhealthy");
  });

  it("should handle ping handler throwing an exception gracefully", async () => {
    const pingDb = {
      handleAsync: vi.fn().mockRejectedValue(new Error("Unexpected DB crash")),
    };
    const storage = createMockStorage();
    const context = createMockContext();
    const handler = new CheckHealth(pingDb as never, storage, context);

    // BaseHandler.handleAsync wraps executeAsync in a try/catch and returns
    // unhandledException on throw, so the handler should not propagate.
    const result = await handler.handleAsync({});

    // The handler returns either a gracefully degraded result or a caught
    // unhandledException from BaseHandler — it must not throw.
    expect(result).toBeDefined();
    expect(result.success === true || result.success === false).toBe(true);
  });
});
