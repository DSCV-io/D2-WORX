import { describe, it, expect, vi, beforeEach } from "vitest";
import * as grpc from "@grpc/grpc-js";
import { ServiceCollection, type ServiceProvider, createServiceKey } from "@d2/di";
import { D2Result } from "@d2/result";
import { handleRpc } from "@d2/service-defaults/grpc";
import { ILoggerKey, type ILogger } from "@d2/logging";

// --------------- Helpers ---------------

interface TestInput {
  readonly id: string;
}

interface TestOutput {
  readonly name: string;
  readonly age: number;
}

interface TestRequest {
  readonly userId: string;
}

interface TestResponse {
  readonly data: { name: string; age: number } | undefined;
  readonly ok: boolean;
}

const ITestHandlerKey = createServiceKey<{
  handleAsync(input: TestInput): Promise<D2Result<TestOutput | undefined>>;
}>("ITestHandler");

function createMockCall(request: TestRequest): grpc.ServerUnaryCall<TestRequest, TestResponse> {
  return {
    request,
    metadata: new grpc.Metadata(),
    getPeer: () => "127.0.0.1",
  } as unknown as grpc.ServerUnaryCall<TestRequest, TestResponse>;
}

function createProvider(handler: {
  handleAsync(input: TestInput): Promise<D2Result<TestOutput | undefined>>;
}): ServiceProvider {
  const services = new ServiceCollection();

  // Logger (singleton)
  const stubLogger: ILogger = {
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
    debug: vi.fn(),
    child: vi.fn().mockReturnThis(),
  } as unknown as ILogger;
  services.addSingleton(ILoggerKey, () => stubLogger);

  // Handler (scoped — resolved in per-RPC scope)
  services.addScoped(ITestHandlerKey, () => handler);

  // IRequestContextKey + IHandlerContextKey are set by createRpcScope, not manually registered.

  return services.build();
}

// --------------- Tests ---------------

describe("handleRpc", () => {
  let handlerMock: {
    handleAsync: ReturnType<typeof vi.fn>;
  };
  let provider: ServiceProvider;

  beforeEach(() => {
    handlerMock = {
      handleAsync: vi
        .fn()
        .mockResolvedValue(D2Result.ok<TestOutput>({ data: { name: "Alice", age: 30 } })),
    };
    provider = createProvider(handlerMock);
  });

  it("should resolve handler, map input, call handleAsync, and map response", async () => {
    const call = createMockCall({ userId: "u-123" });
    const callback = vi.fn();

    handleRpc(provider, call, callback, {
      handlerKey: ITestHandlerKey,
      mapInput: (req: TestRequest) => ({ id: req.userId }),
      mapResponse: (result) => ({
        data: result.data ? { name: result.data.name, age: result.data.age } : undefined,
        ok: result.success,
      }),
    });

    // handleRpc is async internally — give the microtask queue time to flush
    await vi.waitFor(() => expect(callback).toHaveBeenCalled());

    expect(handlerMock.handleAsync).toHaveBeenCalledWith({ id: "u-123" });
    expect(callback).toHaveBeenCalledWith(null, {
      data: { name: "Alice", age: 30 },
      ok: true,
    });
  });

  it("should forward failed D2Result through mapResponse", async () => {
    handlerMock.handleAsync.mockResolvedValue(
      D2Result.fail<TestOutput>({ messages: ["not found"] }),
    );
    const call = createMockCall({ userId: "u-404" });
    const callback = vi.fn();

    handleRpc(provider, call, callback, {
      handlerKey: ITestHandlerKey,
      mapInput: (req: TestRequest) => ({ id: req.userId }),
      mapResponse: (result) => ({
        data: undefined,
        ok: result.success,
      }),
    });

    await vi.waitFor(() => expect(callback).toHaveBeenCalled());

    expect(callback).toHaveBeenCalledWith(null, {
      data: undefined,
      ok: false,
    });
  });

  it("should return INTERNAL error when handler throws", async () => {
    handlerMock.handleAsync.mockRejectedValue(new Error("DB connection lost"));
    const call = createMockCall({ userId: "u-err" });
    const callback = vi.fn();

    handleRpc(provider, call, callback, {
      handlerKey: ITestHandlerKey,
      mapInput: (req: TestRequest) => ({ id: req.userId }),
      mapResponse: (result) => ({ data: undefined, ok: result.success }),
    });

    await vi.waitFor(() => expect(callback).toHaveBeenCalled());

    expect(callback).toHaveBeenCalledWith(
      expect.objectContaining({
        code: grpc.status.INTERNAL,
        message: "DB connection lost",
      }),
    );
  });

  it("should return INTERNAL with generic message for non-Error throws", async () => {
    handlerMock.handleAsync.mockRejectedValue("string-error");
    const call = createMockCall({ userId: "u-str" });
    const callback = vi.fn();

    handleRpc(provider, call, callback, {
      handlerKey: ITestHandlerKey,
      mapInput: (req: TestRequest) => ({ id: req.userId }),
      mapResponse: (result) => ({ data: undefined, ok: result.success }),
    });

    await vi.waitFor(() => expect(callback).toHaveBeenCalled());

    expect(callback).toHaveBeenCalledWith(
      expect.objectContaining({
        code: grpc.status.INTERNAL,
        message: "Unknown error",
      }),
    );
  });

  it("should call mapInput with the exact call.request", async () => {
    const mapInput = vi.fn().mockReturnValue({ id: "mapped" });
    const call = createMockCall({ userId: "u-map" });
    const callback = vi.fn();

    handleRpc(provider, call, callback, {
      handlerKey: ITestHandlerKey,
      mapInput,
      mapResponse: (result) => ({ data: undefined, ok: result.success }),
    });

    await vi.waitFor(() => expect(callback).toHaveBeenCalled());

    expect(mapInput).toHaveBeenCalledWith({ userId: "u-map" });
    expect(handlerMock.handleAsync).toHaveBeenCalledWith({ id: "mapped" });
  });
});
