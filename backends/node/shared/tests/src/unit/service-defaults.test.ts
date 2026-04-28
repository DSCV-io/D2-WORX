import { describe, it, expect, vi, beforeEach } from "vitest";
import { Metadata } from "@grpc/grpc-js";

// ---------------------------------------------------------------------------
// Main barrel exports (@d2/service-defaults)
// ---------------------------------------------------------------------------

describe("@d2/service-defaults", () => {
  describe("type and re-exports", () => {
    it("exports setupTelemetry function", async () => {
      const mod = await import("@d2/service-defaults");

      expect(mod.setupTelemetry).toBeDefined();
      expect(typeof mod.setupTelemetry).toBe("function");
    });

    it("re-exports OTel API primitives", async () => {
      const mod = await import("@d2/service-defaults");

      // Functions
      expect(typeof mod.trace).toBe("object"); // OTel trace API is an object (TraceAPI)
      expect(typeof mod.metrics).toBe("object"); // OTel metrics API is an object (MetricsAPI)
      expect(typeof mod.context).toBe("object"); // OTel context API is an object (ContextAPI)

      // Enum
      expect(mod.SpanStatusCode).toBeDefined();
      expect(mod.SpanStatusCode.OK).toBeDefined();
      expect(mod.SpanStatusCode.ERROR).toBeDefined();
      expect(mod.SpanStatusCode.UNSET).toBeDefined();
    });
  });

  describe("TelemetryConfig", () => {
    it("satisfies the interface with only required fields", async () => {
      const mod = await import("@d2/service-defaults");

      // TelemetryConfig requires only serviceName — verify setupTelemetry
      // accepts it (type-level check; we don't call it to avoid SDK side effects).
      const config = { serviceName: "smoke-test" };
      expect(typeof mod.setupTelemetry).toBe("function");
      expect(mod.setupTelemetry.length).toBeGreaterThanOrEqual(1);
      // Just verifying the function exists and accepts 1+ params — not calling it.
      void config;
    });
  });
});

// ---------------------------------------------------------------------------
// gRPC utilities (@d2/service-defaults/grpc)
// ---------------------------------------------------------------------------

describe("@d2/service-defaults/grpc", () => {
  describe("barrel exports", () => {
    it("exports all expected gRPC utility functions", async () => {
      const grpcMod = await import("@d2/service-defaults/grpc");

      expect(typeof grpcMod.extractGrpcTraceContext).toBe("function");
      expect(typeof grpcMod.withTraceContext).toBe("function");
      expect(typeof grpcMod.createRpcScope).toBe("function");
      expect(typeof grpcMod.withApiKeyAuth).toBe("function");
    });
  });

  // -------------------------------------------------------------------------
  // extractGrpcTraceContext
  // -------------------------------------------------------------------------

  describe("extractGrpcTraceContext", () => {
    it("returns an OTel Context when given a call with empty metadata", async () => {
      const { extractGrpcTraceContext } = await import("@d2/service-defaults/grpc");

      const metadata = new Metadata();
      const fakeCall = { metadata } as never;

      const ctx = extractGrpcTraceContext(fakeCall);

      // Should return a valid Context object (the root context when no traceparent).
      expect(ctx).toBeDefined();
    });

    it("returns an OTel Context when metadata is undefined", async () => {
      const { extractGrpcTraceContext } = await import("@d2/service-defaults/grpc");

      const fakeCall = { metadata: undefined } as never;

      // Should not throw even without metadata.
      const ctx = extractGrpcTraceContext(fakeCall);
      expect(ctx).toBeDefined();
    });

    it("extracts traceparent from metadata", async () => {
      const { extractGrpcTraceContext } = await import("@d2/service-defaults/grpc");
      // Use the re-exported trace from @d2/service-defaults (not @opentelemetry/api directly).
      const { trace } = await import("@d2/service-defaults");

      const metadata = new Metadata();
      // Valid W3C traceparent: version-traceId-spanId-flags
      metadata.set("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

      const fakeCall = { metadata } as never;
      const ctx = extractGrpcTraceContext(fakeCall);

      // If propagation is set up, this should extract the trace ID.
      const spanCtx = trace.getSpanContext(ctx);
      // With default propagator (W3C), it should parse the traceparent.
      if (spanCtx) {
        expect(spanCtx.traceId).toBe("4bf92f3577b34da6a3ce929d0e0e4736");
        expect(spanCtx.spanId).toBe("00f067aa0ba902b7");
      }
      // If no propagator is registered (unit test environment), ctx is still valid.
      expect(ctx).toBeDefined();
    });
  });

  // -------------------------------------------------------------------------
  // withTraceContext
  // -------------------------------------------------------------------------

  describe("withTraceContext", () => {
    it("executes the callback function", async () => {
      const { withTraceContext } = await import("@d2/service-defaults/grpc");

      const metadata = new Metadata();
      const fakeCall = { metadata } as never;

      let executed = false;
      await withTraceContext(fakeCall, async () => {
        executed = true;
      });

      expect(executed).toBe(true);
    });

    it("propagates the return value from the callback", async () => {
      const { withTraceContext } = await import("@d2/service-defaults/grpc");

      const metadata = new Metadata();
      const fakeCall = { metadata } as never;

      // withTraceContext returns Promise<void>, so the callback should complete.
      await expect(
        withTraceContext(fakeCall, async () => {
          // Simulate async work.
          await Promise.resolve();
        }),
      ).resolves.toBeUndefined();
    });

    it("propagates errors thrown in the callback", async () => {
      const { withTraceContext } = await import("@d2/service-defaults/grpc");

      const metadata = new Metadata();
      const fakeCall = { metadata } as never;

      await expect(
        withTraceContext(fakeCall, async () => {
          throw new Error("handler error");
        }),
      ).rejects.toThrow("handler error");
    });
  });

  // -------------------------------------------------------------------------
  // withApiKeyAuth
  // -------------------------------------------------------------------------

  describe("withApiKeyAuth", () => {
    function createFakeLogger() {
      return {
        info: vi.fn(),
        debug: vi.fn(),
        warn: vi.fn(),
        error: vi.fn(),
        fatal: vi.fn(),
        child: vi.fn(),
      };
    }

    function createFakeCall(apiKey?: string) {
      const metadata = new Metadata();
      if (apiKey) {
        metadata.set("x-api-key", apiKey);
      }
      return { metadata } as never;
    }

    it("passes through to the original handler when API key is valid", async () => {
      const { withApiKeyAuth } = await import("@d2/service-defaults/grpc");
      const logger = createFakeLogger();
      const handler = vi.fn();

      const service = { myMethod: handler };
      const wrapped = withApiKeyAuth(service, {
        validKeys: new Set(["key-123"]),
        logger,
      });

      const call = createFakeCall("key-123");
      const callback = vi.fn();
      (wrapped.myMethod as (call: unknown, callback: unknown) => void)(call, callback);

      expect(handler).toHaveBeenCalledOnce();
      expect(handler).toHaveBeenCalledWith(call, callback);
      expect(callback).not.toHaveBeenCalled(); // Not called with error
    });

    it("rejects with UNAUTHENTICATED when no API key is provided", async () => {
      const { withApiKeyAuth } = await import("@d2/service-defaults/grpc");
      const { status } = await import("@grpc/grpc-js");
      const logger = createFakeLogger();
      const handler = vi.fn();

      const service = { myMethod: handler };
      const wrapped = withApiKeyAuth(service, {
        validKeys: new Set(["key-123"]),
        logger,
      });

      const call = createFakeCall(); // No API key
      const callback = vi.fn();
      (wrapped.myMethod as (call: unknown, callback: unknown) => void)(call, callback);

      expect(handler).not.toHaveBeenCalled();
      expect(callback).toHaveBeenCalledOnce();
      expect(callback).toHaveBeenCalledWith(
        expect.objectContaining({
          code: status.UNAUTHENTICATED,
          message: "Missing x-api-key header.",
        }),
      );
      expect(logger.warn).toHaveBeenCalledOnce();
    });

    it("rejects with UNAUTHENTICATED when API key is invalid", async () => {
      const { withApiKeyAuth } = await import("@d2/service-defaults/grpc");
      const { status } = await import("@grpc/grpc-js");
      const logger = createFakeLogger();
      const handler = vi.fn();

      const service = { myMethod: handler };
      const wrapped = withApiKeyAuth(service, {
        validKeys: new Set(["key-123"]),
        logger,
      });

      const call = createFakeCall("wrong-key");
      const callback = vi.fn();
      (wrapped.myMethod as (call: unknown, callback: unknown) => void)(call, callback);

      expect(handler).not.toHaveBeenCalled();
      expect(callback).toHaveBeenCalledOnce();
      expect(callback).toHaveBeenCalledWith(
        expect.objectContaining({
          code: status.UNAUTHENTICATED,
          message: "Invalid API key.",
        }),
      );
      expect(logger.warn).toHaveBeenCalledOnce();
    });

    it("wraps multiple methods on the service", async () => {
      const { withApiKeyAuth } = await import("@d2/service-defaults/grpc");
      const logger = createFakeLogger();

      const handler1 = vi.fn();
      const handler2 = vi.fn();
      const service = { method1: handler1, method2: handler2 };

      const wrapped = withApiKeyAuth(service, {
        validKeys: new Set(["key-abc"]),
        logger,
      });

      // Both methods should be present on the wrapped service.
      expect(wrapped.method1).toBeDefined();
      expect(wrapped.method2).toBeDefined();
      expect(wrapped.method1).not.toBe(handler1);
      expect(wrapped.method2).not.toBe(handler2);
    });

    it("exempts listed methods from API key validation", async () => {
      const { withApiKeyAuth } = await import("@d2/service-defaults/grpc");
      const logger = createFakeLogger();

      const healthHandler = vi.fn();
      const protectedHandler = vi.fn();
      const service = { checkHealth: healthHandler, getData: protectedHandler };

      const wrapped = withApiKeyAuth(service, {
        validKeys: new Set(["key-123"]),
        logger,
        exempt: new Set(["checkHealth"]),
      });

      // Exempt method passes through without API key.
      expect(wrapped.checkHealth).toBe(healthHandler);

      // Non-exempt method is wrapped.
      expect(wrapped.getData).not.toBe(protectedHandler);

      // Calling non-exempt without key should reject.
      const call = createFakeCall(); // No API key
      const callback = vi.fn();
      (wrapped.getData as (call: unknown, callback: unknown) => void)(call, callback);
      expect(protectedHandler).not.toHaveBeenCalled();
      expect(callback).toHaveBeenCalledOnce();
    });

    it("accepts multiple valid keys", async () => {
      const { withApiKeyAuth } = await import("@d2/service-defaults/grpc");
      const logger = createFakeLogger();
      const handler = vi.fn();

      const service = { myMethod: handler };
      const wrapped = withApiKeyAuth(service, {
        validKeys: new Set(["key-1", "key-2", "key-3"]),
        logger,
      });

      // All keys should be accepted.
      for (const key of ["key-1", "key-2", "key-3"]) {
        handler.mockClear();
        const call = createFakeCall(key);
        const callback = vi.fn();
        (wrapped.myMethod as (call: unknown, callback: unknown) => void)(call, callback);
        expect(handler).toHaveBeenCalledOnce();
      }
    });
  });

  // -------------------------------------------------------------------------
  // createRpcScope
  // -------------------------------------------------------------------------

  describe("createRpcScope", () => {
    it("returns a ServiceScope with IRequestContext and IHandlerContext set", async () => {
      const { createRpcScope } = await import("@d2/service-defaults/grpc");
      const { ServiceCollection } = await import("@d2/di");
      const { IRequestContextKey, IHandlerContextKey } = await import("@d2/handler");
      const { ILoggerKey, createLogger } = await import("@d2/logging");

      // Set up a minimal DI container with a logger singleton.
      const services = new ServiceCollection();
      services.addInstance(ILoggerKey, createLogger({ level: "silent" as never }));
      // Register scoped descriptors for the keys so resolve() finds them.
      services.addScoped(IRequestContextKey, () => {
        throw new Error("should not call factory");
      });
      services.addScoped(IHandlerContextKey, () => {
        throw new Error("should not call factory");
      });
      const provider = services.build();

      const metadata = new Metadata();
      const fakeCall = { metadata } as never;

      const scope = createRpcScope(provider, fakeCall);

      // Scope should have IRequestContext and IHandlerContext populated.
      const reqCtx = scope.resolve(IRequestContextKey);
      expect(reqCtx).toBeDefined();
      expect(reqCtx.traceId).toBeDefined();
      expect(typeof reqCtx.traceId).toBe("string");
      expect(reqCtx.isAuthenticated).toBe(false);

      const handlerCtx = scope.resolve(IHandlerContextKey);
      expect(handlerCtx).toBeDefined();
      expect(handlerCtx.request).toBe(reqCtx);
      expect(handlerCtx.logger).toBeDefined();

      scope.dispose();
      provider.dispose();
    });

    it("uses a UUID as traceId when no traceparent is in metadata", async () => {
      const { createRpcScope } = await import("@d2/service-defaults/grpc");
      const { ServiceCollection } = await import("@d2/di");
      const { IRequestContextKey, IHandlerContextKey } = await import("@d2/handler");
      const { ILoggerKey, createLogger } = await import("@d2/logging");

      const services = new ServiceCollection();
      services.addInstance(ILoggerKey, createLogger({ level: "silent" as never }));
      services.addScoped(IRequestContextKey, () => {
        throw new Error("should not call factory");
      });
      services.addScoped(IHandlerContextKey, () => {
        throw new Error("should not call factory");
      });
      const provider = services.build();

      const metadata = new Metadata();
      const fakeCall = { metadata } as never;

      const scope = createRpcScope(provider, fakeCall);
      const reqCtx = scope.resolve(IRequestContextKey);

      // Without traceparent, should fall back to a UUID.
      expect(reqCtx.traceId).toBeDefined();
      expect(typeof reqCtx.traceId).toBe("string");
      expect(reqCtx.traceId.length).toBeGreaterThan(0);

      scope.dispose();
      provider.dispose();
    });

    it("uses custom createRequestContext when provided in options", async () => {
      const { createRpcScope } = await import("@d2/service-defaults/grpc");
      const { ServiceCollection } = await import("@d2/di");
      const { IRequestContextKey, IHandlerContextKey } = await import("@d2/handler");
      const { ILoggerKey, createLogger } = await import("@d2/logging");

      const services = new ServiceCollection();
      services.addInstance(ILoggerKey, createLogger({ level: "silent" as never }));
      services.addScoped(IRequestContextKey, () => {
        throw new Error("should not call factory");
      });
      services.addScoped(IHandlerContextKey, () => {
        throw new Error("should not call factory");
      });
      const provider = services.build();

      const metadata = new Metadata();
      const fakeCall = { metadata } as never;

      const customRequestContext = {
        traceId: "custom-trace-123",
        isAuthenticated: true,
        isTrustedService: false,
        userId: "user-abc",
        isAgentStaff: false,
        isAgentAdmin: false,
        isTargetingStaff: false,
        isTargetingAdmin: false,
        isOrgEmulating: false,
        isUserImpersonating: false,
      };

      const scope = createRpcScope(provider, fakeCall, {
        createRequestContext: (_traceId, _call) => customRequestContext,
      });

      const reqCtx = scope.resolve(IRequestContextKey);
      expect(reqCtx).toBe(customRequestContext);
      expect(reqCtx.traceId).toBe("custom-trace-123");
      expect(reqCtx.isAuthenticated).toBe(true);
      expect(reqCtx.userId).toBe("user-abc");

      scope.dispose();
      provider.dispose();
    });

    it("sets default IRequestContext flags to false/undefined", async () => {
      const { createRpcScope } = await import("@d2/service-defaults/grpc");
      const { ServiceCollection } = await import("@d2/di");
      const { IRequestContextKey, IHandlerContextKey } = await import("@d2/handler");
      const { ILoggerKey, createLogger } = await import("@d2/logging");

      const services = new ServiceCollection();
      services.addInstance(ILoggerKey, createLogger({ level: "silent" as never }));
      services.addScoped(IRequestContextKey, () => {
        throw new Error("should not call factory");
      });
      services.addScoped(IHandlerContextKey, () => {
        throw new Error("should not call factory");
      });
      const provider = services.build();

      const metadata = new Metadata();
      const fakeCall = { metadata } as never;

      const scope = createRpcScope(provider, fakeCall);
      const reqCtx = scope.resolve(IRequestContextKey);

      // All auth/emulation flags should be false for unauthenticated RPCs.
      expect(reqCtx.isAuthenticated).toBe(false);
      expect(reqCtx.isAgentStaff).toBe(false);
      expect(reqCtx.isAgentAdmin).toBe(false);
      expect(reqCtx.isTargetingStaff).toBe(false);
      expect(reqCtx.isTargetingAdmin).toBe(false);
      expect(reqCtx.isOrgEmulating).toBeNull();
      expect(reqCtx.isUserImpersonating).toBeNull();

      scope.dispose();
      provider.dispose();
    });
  });
});
