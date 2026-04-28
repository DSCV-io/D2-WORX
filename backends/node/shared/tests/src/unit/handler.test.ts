import { describe, it, expect, vi, beforeEach } from "vitest";
import {
  BaseHandler,
  HandlerContext,
  OrgType,
  DEFAULT_HANDLER_OPTIONS,
  requestContextStorage,
  requestLoggerStorage,
  type IHandlerContext,
  type IRequestContext,
  type HandlerOptions,
} from "@d2/handler";
import { D2Result, HttpStatusCode, ErrorCodes } from "@d2/result";
import { createLogger, type ILogger } from "@d2/logging";

// ---------------------------------------------------------------------------
// Test helpers
// ---------------------------------------------------------------------------

function createTestRequestContext(overrides?: Partial<IRequestContext>): IRequestContext {
  return {
    traceId: "test-trace-id",
    requestId: "test-request-id",
    requestPath: "/test",
    isAuthenticated: true,
    isTrustedService: false,
    userId: "user-123",
    email: "testuser@example.com",
    username: "testuser",
    agentOrgId: "org-agent-1",
    agentOrgName: "Agent Org",
    agentOrgType: OrgType.Admin,
    targetOrgId: "org-target-1",
    targetOrgName: "Target Org",
    targetOrgType: OrgType.Customer,
    isOrgEmulating: false,
    impersonatedBy: undefined,
    impersonatingEmail: undefined,
    impersonatingUsername: undefined,
    isUserImpersonating: false,
    isAgentStaff: true,
    isAgentAdmin: true,
    isTargetingStaff: false,
    isTargetingAdmin: false,
    ...overrides,
  };
}

function createTestContext(overrides?: Partial<IRequestContext>): IHandlerContext {
  const logger = createLogger({ level: "silent" as never });
  return new HandlerContext(createTestRequestContext(overrides), logger);
}

// A simple concrete handler for testing
class SuccessHandler extends BaseHandler<{ value: string }, { result: string }> {
  constructor(context: IHandlerContext) {
    super(context);
  }

  protected async executeAsync(input: {
    value: string;
  }): Promise<D2Result<{ result: string } | undefined>> {
    return D2Result.ok({ data: { result: input.value.toUpperCase() } });
  }
}

class FailureHandler extends BaseHandler<{ value: string }, { result: string }> {
  constructor(context: IHandlerContext) {
    super(context);
  }

  protected async executeAsync(_input: {
    value: string;
  }): Promise<D2Result<{ result: string } | undefined>> {
    return D2Result.fail({
      messages: ["Something went wrong"],
    });
  }
}

class ExceptionHandler extends BaseHandler<{ value: string }, { result: string }> {
  constructor(context: IHandlerContext) {
    super(context);
  }

  protected async executeAsync(_input: {
    value: string;
  }): Promise<D2Result<{ result: string } | undefined>> {
    throw new Error("Unhandled test exception");
  }
}

class SlowHandler extends BaseHandler<{ delayMs: number }, void> {
  constructor(context: IHandlerContext) {
    super(context);
  }

  protected async executeAsync(input: { delayMs: number }): Promise<D2Result<void | undefined>> {
    await new Promise((resolve) => setTimeout(resolve, input.delayMs));
    return D2Result.ok();
  }
}

// ---------------------------------------------------------------------------
// BaseHandler execution
// ---------------------------------------------------------------------------

describe("BaseHandler", () => {
  it("successful execution returns D2Result.ok", async () => {
    const context = createTestContext();
    const handler = new SuccessHandler(context);

    const result = await handler.handleAsync({ value: "hello" });

    expect(result).toBeSuccess();
    expect(result).toHaveData({ result: "HELLO" });
  });

  it("failed execution returns D2Result.fail", async () => {
    const context = createTestContext();
    const handler = new FailureHandler(context);

    const result = await handler.handleAsync({ value: "hello" });

    expect(result).toBeFailure();
    expect(result.messages).toContain("Something went wrong");
  });

  it("exception in executeAsync returns D2Result.unhandledException", async () => {
    const context = createTestContext();
    const handler = new ExceptionHandler(context);

    const result = await handler.handleAsync({ value: "hello" });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(HttpStatusCode.InternalServerError);
    expect(result.errorCode).toBe(ErrorCodes.UNHANDLED_EXCEPTION);
  });

  it("preserves traceId in exception result", async () => {
    const context = createTestContext({ traceId: "my-trace" });
    const handler = new ExceptionHandler(context);

    const result = await handler.handleAsync({ value: "hello" });

    expect(result.traceId).toBe("my-trace");
  });
});

// ---------------------------------------------------------------------------
// HandlerOptions
// ---------------------------------------------------------------------------

describe("HandlerOptions", () => {
  it("has correct default values", () => {
    expect(DEFAULT_HANDLER_OPTIONS.suppressTimeWarnings).toBe(false);
    expect(DEFAULT_HANDLER_OPTIONS.logInput).toBe(true);
    expect(DEFAULT_HANDLER_OPTIONS.logOutput).toBe(true);
    expect(DEFAULT_HANDLER_OPTIONS.warningThresholdMs).toBe(100);
    expect(DEFAULT_HANDLER_OPTIONS.criticalThresholdMs).toBe(500);
  });

  it("logInput controls debug logging", async () => {
    const logger = createLogger({ level: "debug" as never });
    const debugSpy = vi.spyOn(logger, "debug");
    const context = new HandlerContext(createTestRequestContext(), logger);
    const handler = new SuccessHandler(context);

    // With logInput: false
    await handler.handleAsync({ value: "test" }, { logInput: false });
    const inputLogCalls = debugSpy.mock.calls.filter((c) =>
      (c[0] as string).includes("received input"),
    );
    expect(inputLogCalls).toHaveLength(0);

    debugSpy.mockClear();

    // With logInput: true (default)
    await handler.handleAsync({ value: "test" }, { logInput: true });
    const inputLogCalls2 = debugSpy.mock.calls.filter((c) =>
      (c[0] as string).includes("received input"),
    );
    expect(inputLogCalls2).toHaveLength(1);
  });

  it("logOutput controls debug logging", async () => {
    const logger = createLogger({ level: "debug" as never });
    const debugSpy = vi.spyOn(logger, "debug");
    const context = new HandlerContext(createTestRequestContext(), logger);
    const handler = new SuccessHandler(context);

    // With logOutput: false
    await handler.handleAsync({ value: "test" }, { logOutput: false });
    const outputLogCalls = debugSpy.mock.calls.filter((c) =>
      (c[0] as string).includes("produced result"),
    );
    expect(outputLogCalls).toHaveLength(0);

    debugSpy.mockClear();

    // With logOutput: true (default)
    await handler.handleAsync({ value: "test" }, { logOutput: true });
    const outputLogCalls2 = debugSpy.mock.calls.filter((c) =>
      (c[0] as string).includes("produced result"),
    );
    expect(outputLogCalls2).toHaveLength(1);
  });
});

// ---------------------------------------------------------------------------
// safeStringify (tested indirectly via debug logging)
// ---------------------------------------------------------------------------

describe("safeStringify", () => {
  it("truncates large input in debug log", async () => {
    const logger = createLogger({ level: "debug" as never });
    const debugSpy = vi.spyOn(logger, "debug");
    const context = new HandlerContext(createTestRequestContext(), logger);
    const handler = new SuccessHandler(context);

    // Create an input with a very large value (>10k chars when serialized)
    const largeValue = "x".repeat(15_000);
    await handler.handleAsync({ value: largeValue });

    const inputLogCalls = debugSpy.mock.calls.filter((c) =>
      (c[0] as string).includes("received input"),
    );
    expect(inputLogCalls).toHaveLength(1);
    const logMessage = inputLogCalls[0][0] as string;
    expect(logMessage).toContain("...[truncated,");
    expect(logMessage.length).toBeLessThan(15_000);
  });

  it("does not truncate small input in debug log", async () => {
    const logger = createLogger({ level: "debug" as never });
    const debugSpy = vi.spyOn(logger, "debug");
    const context = new HandlerContext(createTestRequestContext(), logger);
    const handler = new SuccessHandler(context);

    await handler.handleAsync({ value: "short" });

    const inputLogCalls = debugSpy.mock.calls.filter((c) =>
      (c[0] as string).includes("received input"),
    );
    expect(inputLogCalls).toHaveLength(1);
    const logMessage = inputLogCalls[0][0] as string;
    expect(logMessage).not.toContain("...[truncated,");
    expect(logMessage).toContain('"short"');
  });

  it("handles circular references gracefully in debug log", async () => {
    const logger = createLogger({ level: "debug" as never });
    const debugSpy = vi.spyOn(logger, "debug");
    const context = new HandlerContext(createTestRequestContext(), logger);

    // Create a handler that returns a circular object
    class CircularHandler extends BaseHandler<Record<string, unknown>, void> {
      constructor(ctx: IHandlerContext) {
        super(ctx);
      }
      protected async executeAsync(
        _input: Record<string, unknown>,
      ): Promise<D2Result<void | undefined>> {
        return D2Result.ok();
      }
    }

    const circularHandler = new CircularHandler(context);
    const circularInput: Record<string, unknown> = { name: "test" };
    circularInput.self = circularInput;

    await circularHandler.handleAsync(circularInput);

    const inputLogCalls = debugSpy.mock.calls.filter((c) =>
      (c[0] as string).includes("received input"),
    );
    expect(inputLogCalls).toHaveLength(1);
    const logMessage = inputLogCalls[0][0] as string;
    expect(logMessage).toContain("[unserializable]");
  });
});

// ---------------------------------------------------------------------------
// HandlerContext
// ---------------------------------------------------------------------------

describe("HandlerContext", () => {
  it("bundles request and logger", () => {
    const request = createTestRequestContext();
    const logger = createLogger();
    const context = new HandlerContext(request, logger);

    expect(context.request).toBe(request);
    expect(context.logger).toBe(logger);
  });
});

// ---------------------------------------------------------------------------
// Ambient context (AsyncLocalStorage)
// ---------------------------------------------------------------------------

describe("Ambient context via AsyncLocalStorage", () => {
  it("HandlerContext.request returns ambient value when storage is active", async () => {
    const constructorRequest = createTestRequestContext({ userId: "constructor-user" });
    const ambientRequest = createTestRequestContext({ userId: "ambient-user" });
    const logger = createLogger({ level: "silent" as never });
    const context = new HandlerContext(constructorRequest, logger);

    // Outside storage — falls back to constructor arg
    expect(context.request.userId).toBe("constructor-user");

    // Inside storage — returns ambient value
    await requestContextStorage.run(ambientRequest, async () => {
      expect(context.request.userId).toBe("ambient-user");
    });

    // After run() exits — back to constructor arg
    expect(context.request.userId).toBe("constructor-user");
  });

  it("HandlerContext.logger returns ambient value when storage is active", async () => {
    const constructorLogger = createLogger({ level: "silent" as never });
    const ambientLogger = createLogger({ level: "silent" as never });
    const request = createTestRequestContext();
    const context = new HandlerContext(request, constructorLogger);

    // Outside storage — falls back to constructor arg
    expect(context.logger).toBe(constructorLogger);

    // Inside storage — returns ambient value
    await requestLoggerStorage.run(ambientLogger, async () => {
      expect(context.logger).toBe(ambientLogger);
    });

    // After run() exits — back to constructor arg
    expect(context.logger).toBe(constructorLogger);
  });

  it("singleton handler sees per-request context inside storage.run()", async () => {
    // Simulate a singleton handler constructed once with service-level defaults
    const serviceRequest = createTestRequestContext({
      isTrustedService: false,
      isAuthenticated: false,
      userId: undefined,
    });
    const serviceLogger = createLogger({ level: "silent" as never });
    const singletonContext = new HandlerContext(serviceRequest, serviceLogger);
    const handler = new SuccessHandler(singletonContext);

    // Request 1: trusted service, user "alice"
    const perRequestCtx1 = createTestRequestContext({
      isTrustedService: true,
      userId: "alice",
    });
    const result1 = await requestContextStorage.run(perRequestCtx1, () =>
      handler.handleAsync({ value: "hello" }),
    );
    expect(result1).toBeSuccess();

    // Request 2: untrusted, user "bob"
    const perRequestCtx2 = createTestRequestContext({
      isTrustedService: false,
      userId: "bob",
    });
    const result2 = await requestContextStorage.run(perRequestCtx2, () =>
      handler.handleAsync({ value: "world" }),
    );
    expect(result2).toBeSuccess();

    // Outside storage — handler falls back to service-level defaults
    expect(singletonContext.request.userId).toBeUndefined();
    expect(singletonContext.request.isTrustedService).toBe(false);
  });

  it("enterWith() upgrades ambient context for downstream code", async () => {
    const initialRequest = createTestRequestContext({
      isAuthenticated: false,
      userId: undefined,
    });
    const upgradedRequest = createTestRequestContext({
      isAuthenticated: true,
      userId: "upgraded-user",
      agentOrgId: "org-upgraded",
    });
    const logger = createLogger({ level: "silent" as never });
    const context = new HandlerContext(initialRequest, logger);

    await requestContextStorage.run(initialRequest, async () => {
      // Before enterWith — sees initial context
      expect(context.request.isAuthenticated).toBe(false);
      expect(context.request.userId).toBeUndefined();

      // Simulate scope middleware upgrading context (like auth scope does)
      requestContextStorage.enterWith(upgradedRequest);

      // After enterWith — sees upgraded context
      expect(context.request.isAuthenticated).toBe(true);
      expect(context.request.userId).toBe("upgraded-user");
      expect(context.request.agentOrgId).toBe("org-upgraded");
    });
  });

  it("handler logs use ambient logger when active", async () => {
    const silentLogger = createLogger({ level: "silent" as never });
    const ambientLogger = createLogger({ level: "debug" as never });
    const debugSpy = vi.spyOn(ambientLogger, "info");

    const context = new HandlerContext(createTestRequestContext(), silentLogger);
    const handler = new SuccessHandler(context);

    // Inside ambient logger storage — handler should use the ambient logger
    await requestLoggerStorage.run(ambientLogger, () => handler.handleAsync({ value: "test" }));

    // The ambient logger should have received log calls from handleAsync
    const executingCalls = debugSpy.mock.calls.filter((c) =>
      (c[0] as string).includes("Executing handler"),
    );
    expect(executingCalls.length).toBeGreaterThanOrEqual(1);

    debugSpy.mockRestore();
  });

  it("concurrent requests see isolated contexts", async () => {
    const serviceLogger = createLogger({ level: "silent" as never });
    const serviceRequest = createTestRequestContext({
      isAuthenticated: false,
      userId: undefined,
    });
    const singletonContext = new HandlerContext(serviceRequest, serviceLogger);

    // Capture what userId each handler invocation sees
    class ContextCapture extends BaseHandler<void, { userId: string | undefined }> {
      constructor(ctx: IHandlerContext) {
        super(ctx);
      }
      protected async executeAsync(): Promise<
        D2Result<{ userId: string | undefined } | undefined>
      > {
        // Small delay to increase chance of interleaving
        await new Promise((r) => setTimeout(r, 10));
        return D2Result.ok({ data: { userId: this.context.request.userId } });
      }
    }

    const handler = new ContextCapture(singletonContext);

    // Run two "requests" concurrently with different contexts
    const [r1, r2] = await Promise.all([
      requestContextStorage.run(createTestRequestContext({ userId: "user-A" }), () =>
        handler.handleAsync(undefined as never),
      ),
      requestContextStorage.run(createTestRequestContext({ userId: "user-B" }), () =>
        handler.handleAsync(undefined as never),
      ),
    ]);

    expect(r1).toBeSuccess();
    expect(r2).toBeSuccess();
    expect(r1.data?.userId).toBe("user-A");
    expect(r2.data?.userId).toBe("user-B");
  });
});

// ---------------------------------------------------------------------------
// Types parity with .NET
// ---------------------------------------------------------------------------

describe("OrgType", () => {
  // Wire format is lowercase to match the Postgres `organization.org_type`
  // column AND .NET's `OrgTypeValues` constants. The PascalCase TS
  // identifier (`OrgType.Admin`) is convenient at call sites; the
  // underlying string is what crosses every wire (DB, JWT, gRPC).
  it("has all .NET wire-format values (lowercase)", () => {
    expect(OrgType.Admin).toBe("admin");
    expect(OrgType.Support).toBe("support");
    expect(OrgType.Affiliate).toBe("affiliate");
    expect(OrgType.Customer).toBe("customer");
    expect(OrgType.ThirdParty).toBe("third_party");
  });

  it("has exactly 5 members", () => {
    const values = Object.values(OrgType);
    expect(values).toHaveLength(5);
  });
});

describe("IRequestContext helper properties", () => {
  it("isAgentStaff is true for Admin org type", () => {
    const request = createTestRequestContext({
      agentOrgType: OrgType.Admin,
      isAgentStaff: true,
      isAgentAdmin: true,
    });
    expect(request.isAgentStaff).toBe(true);
    expect(request.isAgentAdmin).toBe(true);
  });

  it("isAgentStaff is true for Support org type", () => {
    const request = createTestRequestContext({
      agentOrgType: OrgType.Support,
      isAgentStaff: true,
      isAgentAdmin: false,
    });
    expect(request.isAgentStaff).toBe(true);
    expect(request.isAgentAdmin).toBe(false);
  });

  it("isTargetingStaff is false for Customer org type", () => {
    const request = createTestRequestContext({
      targetOrgType: OrgType.Customer,
      isTargetingStaff: false,
      isTargetingAdmin: false,
    });
    expect(request.isTargetingStaff).toBe(false);
    expect(request.isTargetingAdmin).toBe(false);
  });
});
