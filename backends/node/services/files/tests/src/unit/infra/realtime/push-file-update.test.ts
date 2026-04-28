import { describe, it, expect, vi, beforeEach } from "vitest";
import type { IHandlerContext, IRequestContext } from "@d2/handler";

// --- Hoisted mocks ---

const { mockPushToChannel, mockCredentials, mockMetadata, mockCtor } = vi.hoisted(() => ({
  mockPushToChannel: vi.fn(),
  mockCredentials: { createInsecure: vi.fn() },
  mockMetadata: vi.fn().mockImplementation(() => ({})),
  mockCtor: vi.fn(),
}));

vi.mock("@grpc/grpc-js", () => ({
  credentials: mockCredentials,
  Metadata: mockMetadata,
}));

vi.mock("@d2/protos", () => ({
  RealtimeGatewayClientCtor: mockCtor,
}));

vi.mock("@d2/service-defaults/grpc", () => ({
  createApiKeyInterceptor: vi.fn().mockReturnValue(() => {}),
  createTraceContextInterceptor: vi.fn().mockReturnValue(() => {}),
}));

/** Helper to create a successful D2ResultProto shape for mock responses. */
const okResultProto = {
  success: true,
  statusCode: 200,
  messages: [],
  inputErrors: [],
  errorCode: "",
  traceId: "",
};

// --- Imports (after mocks) ---

import { PushFileUpdate, DEFAULT_FILES_OUTBOUND_OPTIONS } from "@d2/files-infra";

// --- Helpers ---

function createTestContext(): IHandlerContext {
  const request: IRequestContext = {
    isAuthenticated: true,
    isOrgEmulating: null,
    isUserImpersonating: null,
    isTrustedService: null,
    userId: "user-123",
    targetOrgId: "org-456",
  };
  return {
    request,
    logger: {
      info: vi.fn(),
      warn: vi.fn(),
      error: vi.fn(),
      debug: vi.fn(),
      trace: vi.fn(),
      fatal: vi.fn(),
      child: vi.fn().mockReturnThis(),
    },
  } as unknown as IHandlerContext;
}

// --- Tests ---

describe("PushFileUpdate", () => {
  beforeEach(() => {
    mockCtor.mockImplementation(function (this: Record<string, unknown>) {
      this.pushToChannel = mockPushToChannel;
    });
  });

  it("should return delivered=true on successful push", async () => {
    const handler = new PushFileUpdate(
      "gateway:5200",
      "test-signalr-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockPushToChannel.mockImplementation(
      (_req: unknown, _meta: unknown, _opts: unknown, cb: (err: unknown, res: unknown) => void) => {
        cb(null, { result: okResultProto, delivered: true });
      },
    );

    const result = await handler.handleAsync({
      uploaderUserId: "user-123",
      fileId: "file-001",
      contextKey: "user_avatar",
      status: "ready",
      variants: ["thumb", "medium", "original"],
    });

    expect(result).toBeSuccess();
    expect(result.data?.delivered).toBe(true);
  });

  it("should return delivered=false when user is not connected", async () => {
    const handler = new PushFileUpdate(
      "gateway:5200",
      "test-signalr-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockPushToChannel.mockImplementation(
      (_req: unknown, _meta: unknown, _opts: unknown, cb: (err: unknown, res: unknown) => void) => {
        cb(null, { result: okResultProto, delivered: false });
      },
    );

    const result = await handler.handleAsync({
      uploaderUserId: "user-999",
      fileId: "file-001",
      contextKey: "user_avatar",
      status: "ready",
    });

    expect(result).toBeSuccess();
    expect(result.data?.delivered).toBe(false);
  });

  it("should return serviceUnavailable on gRPC error", async () => {
    const handler = new PushFileUpdate(
      "gateway:5200",
      "test-signalr-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockPushToChannel.mockImplementation(
      (_req: unknown, _meta: unknown, _opts: unknown, cb: (err: unknown, res: unknown) => void) => {
        const err = Object.assign(new Error("UNAVAILABLE: connection refused"), { code: 14 });
        cb(err, null);
      },
    );

    const result = await handler.handleAsync({
      uploaderUserId: "user-123",
      fileId: "file-001",
      contextKey: "user_avatar",
      status: "ready",
    });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(503);
  });

  it("should construct channel as user:{uploaderUserId}", async () => {
    const handler = new PushFileUpdate(
      "gateway:5200",
      "test-signalr-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockPushToChannel.mockImplementation(
      (
        req: { channel: string },
        _meta: unknown,
        _opts: unknown,
        cb: (err: unknown, res: unknown) => void,
      ) => {
        expect(req.channel).toBe("user:uploader-abc");
        cb(null, { result: okResultProto, delivered: true });
      },
    );

    await handler.handleAsync({
      uploaderUserId: "uploader-abc",
      fileId: "file-001",
      contextKey: "user_avatar",
      status: "ready",
    });

    expect(mockPushToChannel).toHaveBeenCalledOnce();
  });

  it("should map status 'ready' to event 'file:ready'", async () => {
    const handler = new PushFileUpdate(
      "gateway:5200",
      "test-signalr-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockPushToChannel.mockImplementation(
      (
        req: { event: string },
        _meta: unknown,
        _opts: unknown,
        cb: (err: unknown, res: unknown) => void,
      ) => {
        expect(req.event).toBe("file:ready");
        cb(null, { result: okResultProto, delivered: true });
      },
    );

    await handler.handleAsync({
      uploaderUserId: "user-123",
      fileId: "file-001",
      contextKey: "user_avatar",
      status: "ready",
    });

    expect(mockPushToChannel).toHaveBeenCalledOnce();
  });

  it("should map status 'rejected' to event 'file:rejected'", async () => {
    const handler = new PushFileUpdate(
      "gateway:5200",
      "test-signalr-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockPushToChannel.mockImplementation(
      (
        req: { event: string },
        _meta: unknown,
        _opts: unknown,
        cb: (err: unknown, res: unknown) => void,
      ) => {
        expect(req.event).toBe("file:rejected");
        cb(null, { result: okResultProto, delivered: true });
      },
    );

    await handler.handleAsync({
      uploaderUserId: "user-123",
      fileId: "file-001",
      contextKey: "user_avatar",
      status: "rejected",
      rejectionReason: "virus_detected",
    });

    expect(mockPushToChannel).toHaveBeenCalledOnce();
  });

  it("should include fileId, contextKey, status, variants, and rejectionReason in payload", async () => {
    const handler = new PushFileUpdate(
      "gateway:5200",
      "test-signalr-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockPushToChannel.mockImplementation(
      (
        req: { payloadJson: string },
        _meta: unknown,
        _opts: unknown,
        cb: (err: unknown, res: unknown) => void,
      ) => {
        const payload = JSON.parse(req.payloadJson);
        expect(payload.fileId).toBe("file-001");
        expect(payload.contextKey).toBe("user_avatar");
        expect(payload.status).toBe("rejected");
        expect(payload.rejectionReason).toBe("corrupt_file");
        expect(payload.variants).toEqual(["thumb", "medium"]);
        cb(null, { result: okResultProto, delivered: true });
      },
    );

    await handler.handleAsync({
      uploaderUserId: "user-123",
      fileId: "file-001",
      contextKey: "user_avatar",
      status: "rejected",
      rejectionReason: "corrupt_file",
      variants: ["thumb", "medium"],
    });

    expect(mockPushToChannel).toHaveBeenCalledOnce();
  });

  it("should include undefined rejectionReason and variants in payload when not provided", async () => {
    const handler = new PushFileUpdate(
      "gateway:5200",
      "test-signalr-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockPushToChannel.mockImplementation(
      (
        req: { payloadJson: string },
        _meta: unknown,
        _opts: unknown,
        cb: (err: unknown, res: unknown) => void,
      ) => {
        const payload = JSON.parse(req.payloadJson);
        expect(payload.fileId).toBe("file-002");
        expect(payload.contextKey).toBe("org_logo");
        expect(payload.status).toBe("ready");
        // When not provided, these keys still exist in JSON but are undefined
        expect(payload.rejectionReason).toBeUndefined();
        expect(payload.variants).toBeUndefined();
        cb(null, { result: okResultProto, delivered: true });
      },
    );

    await handler.handleAsync({
      uploaderUserId: "user-123",
      fileId: "file-002",
      contextKey: "org_logo",
      status: "ready",
    });

    expect(mockPushToChannel).toHaveBeenCalledOnce();
  });

  it("should set deadline on gRPC call options", async () => {
    const handler = new PushFileUpdate(
      "gateway:5200",
      "test-signalr-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockPushToChannel.mockImplementation(
      (
        _req: unknown,
        _meta: unknown,
        opts: { deadline: number },
        cb: (err: unknown, res: unknown) => void,
      ) => {
        expect(opts.deadline).toBeGreaterThan(Date.now() - 1000);
        expect(opts.deadline).toBeLessThanOrEqual(Date.now() + 11_000);
        cb(null, { result: okResultProto, delivered: true });
      },
    );

    await handler.handleAsync({
      uploaderUserId: "user-123",
      fileId: "file-001",
      contextKey: "user_avatar",
      status: "ready",
    });
  });

  it("should reuse client on subsequent calls", async () => {
    const handler = new PushFileUpdate(
      "gateway:5200",
      "test-signalr-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockPushToChannel.mockImplementation(
      (_req: unknown, _meta: unknown, _opts: unknown, cb: (err: unknown, res: unknown) => void) => {
        cb(null, { result: okResultProto, delivered: true });
      },
    );

    await handler.handleAsync({
      uploaderUserId: "user-1",
      fileId: "file-001",
      contextKey: "user_avatar",
      status: "ready",
    });

    await handler.handleAsync({
      uploaderUserId: "user-2",
      fileId: "file-002",
      contextKey: "org_logo",
      status: "rejected",
    });

    // Constructor should only be called once (lazy init + reuse)
    expect(mockCtor).toHaveBeenCalledOnce();
  });
});
