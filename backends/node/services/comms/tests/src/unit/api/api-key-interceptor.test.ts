import { describe, it, expect, vi } from "vitest";
import * as grpc from "@grpc/grpc-js";
import type { ILogger } from "@d2/logging";
import { withApiKeyAuth } from "@d2/comms-api";

const VALID_KEY = "test-secret-key-123";

function createMockLogger(): ILogger {
  return {
    trace: vi.fn(),
    debug: vi.fn(),
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
    fatal: vi.fn(),
    child: vi.fn().mockReturnThis(),
  } as unknown as ILogger;
}

function createMockCall(apiKey?: string) {
  const metadata = new grpc.Metadata();
  if (apiKey !== undefined) {
    metadata.set("x-api-key", apiKey);
  }
  return { metadata } as unknown as grpc.ServerUnaryCall<unknown, unknown>;
}

function createMockService() {
  return {
    getPreferences: vi.fn() as unknown as grpc.UntypedHandleCall,
    setPreferences: vi.fn() as unknown as grpc.UntypedHandleCall,
    deliver: vi.fn() as unknown as grpc.UntypedHandleCall,
  };
}

describe("withApiKeyAuth interceptor", () => {
  it("should reject with UNAUTHENTICATED when x-api-key header is missing", () => {
    const logger = createMockLogger();
    const service = createMockService();
    const wrapped = withApiKeyAuth(service, { validKeys: new Set([VALID_KEY]), logger });

    const call = createMockCall(); // no key
    const callback = vi.fn();

    (wrapped.getPreferences as grpc.handleUnaryCall<unknown, unknown>)(call, callback);

    expect(callback).toHaveBeenCalledWith(
      expect.objectContaining({
        code: grpc.status.UNAUTHENTICATED,
        message: "Missing x-api-key header.",
      }),
    );
  });

  it("should reject with UNAUTHENTICATED when x-api-key is invalid", () => {
    const logger = createMockLogger();
    const service = createMockService();
    const wrapped = withApiKeyAuth(service, { validKeys: new Set([VALID_KEY]), logger });

    const call = createMockCall("wrong-key");
    const callback = vi.fn();

    (wrapped.getPreferences as grpc.handleUnaryCall<unknown, unknown>)(call, callback);

    expect(callback).toHaveBeenCalledWith(
      expect.objectContaining({
        code: grpc.status.UNAUTHENTICATED,
        message: "Invalid API key.",
      }),
    );
  });

  it("should delegate to original handler when x-api-key is valid", () => {
    const logger = createMockLogger();
    const originalHandler = vi.fn();
    const service = {
      getPreferences: originalHandler as unknown as grpc.UntypedHandleCall,
    };
    const wrapped = withApiKeyAuth(service, { validKeys: new Set([VALID_KEY]), logger });

    const call = createMockCall(VALID_KEY);
    const callback = vi.fn();

    (wrapped.getPreferences as grpc.handleUnaryCall<unknown, unknown>)(call, callback);

    expect(originalHandler).toHaveBeenCalledWith(call, callback);
    // callback should NOT have been called with an error
    expect(callback).not.toHaveBeenCalled();
  });

  it("should wrap all methods in the service object", () => {
    const logger = createMockLogger();
    const service = createMockService();
    const wrapped = withApiKeyAuth(service, { validKeys: new Set([VALID_KEY]), logger });

    expect(Object.keys(wrapped).sort()).toEqual(Object.keys(service).sort());
    for (const key of Object.keys(service)) {
      expect(typeof wrapped[key as keyof typeof wrapped]).toBe("function");
    }
  });

  it("should log warning when x-api-key is missing", () => {
    const logger = createMockLogger();
    const service = createMockService();
    const wrapped = withApiKeyAuth(service, { validKeys: new Set([VALID_KEY]), logger });

    const call = createMockCall(); // no key
    const callback = vi.fn();

    (wrapped.deliver as grpc.handleUnaryCall<unknown, unknown>)(call, callback);

    expect(logger.warn).toHaveBeenCalledWith(
      expect.stringContaining("Missing x-api-key header on RPC deliver"),
    );
  });

  it("should accept the first key in a multi-key set", () => {
    const logger = createMockLogger();
    const originalHandler = vi.fn();
    const service = {
      getPreferences: originalHandler as unknown as grpc.UntypedHandleCall,
    };
    const wrapped = withApiKeyAuth(service, { validKeys: new Set(["key-a", "key-b"]), logger });

    const call = createMockCall("key-a");
    const callback = vi.fn();

    (wrapped.getPreferences as grpc.handleUnaryCall<unknown, unknown>)(call, callback);

    expect(originalHandler).toHaveBeenCalledWith(call, callback);
    expect(callback).not.toHaveBeenCalled();
  });

  it("should accept the second key in a multi-key set", () => {
    const logger = createMockLogger();
    const originalHandler = vi.fn();
    const service = {
      getPreferences: originalHandler as unknown as grpc.UntypedHandleCall,
    };
    const wrapped = withApiKeyAuth(service, { validKeys: new Set(["key-a", "key-b"]), logger });

    const call = createMockCall("key-b");
    const callback = vi.fn();

    (wrapped.getPreferences as grpc.handleUnaryCall<unknown, unknown>)(call, callback);

    expect(originalHandler).toHaveBeenCalledWith(call, callback);
    expect(callback).not.toHaveBeenCalled();
  });

  it("should reject an unknown key when multiple valid keys exist", () => {
    const logger = createMockLogger();
    const service = createMockService();
    const wrapped = withApiKeyAuth(service, { validKeys: new Set(["key-a", "key-b"]), logger });

    const call = createMockCall("key-c");
    const callback = vi.fn();

    (wrapped.getPreferences as grpc.handleUnaryCall<unknown, unknown>)(call, callback);

    expect(callback).toHaveBeenCalledWith(
      expect.objectContaining({
        code: grpc.status.UNAUTHENTICATED,
        message: "Invalid API key.",
      }),
    );
  });

  it("throws at construction when validKeys is empty (fail-closed posture)", () => {
    // Behavior changed in commit that closed review.md m24: instead of
    // wrapping the service and rejecting every inbound call (silently signaling
    // "auth is configured" while accepting nothing), the wrapper now throws
    // at construction so misconfiguration surfaces at startup. Mirrors the
    // pattern landed in commit 95ca94c7 for the auth/files Hono service-key
    // middleware. Cross-tested in @d2/shared-tests' with-api-key-auth.test.ts.
    const logger = createMockLogger();
    const service = createMockService();

    expect(() => withApiKeyAuth(service, { validKeys: new Set<string>(), logger })).toThrow(
      /validKeys is empty/,
    );
  });
});
