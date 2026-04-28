import { describe, it, expect, vi, beforeEach } from "vitest";
import type { IHandlerContext, IRequestContext } from "@d2/handler";

// --- Hoisted mocks ---

const { mockCanAccess, mockOnFileProcessed, mockCredentials, mockMetadata, mockCtor } = vi.hoisted(
  () => ({
    mockCanAccess: vi.fn(),
    mockOnFileProcessed: vi.fn(),
    mockCredentials: { createInsecure: vi.fn() },
    mockMetadata: vi.fn().mockImplementation(() => ({})),
    mockCtor: vi.fn(),
  }),
);

vi.mock("@grpc/grpc-js", () => ({
  credentials: mockCredentials,
  Metadata: mockMetadata,
}));

vi.mock("@d2/protos", () => ({
  FileCallbackClientCtor: mockCtor,
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

import {
  CallCanAccess,
  CallOnFileProcessed,
  DEFAULT_FILES_OUTBOUND_OPTIONS,
} from "@d2/files-infra";

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

/**
 * Creates a pre-seeded client map with a mock client for the given address.
 * This avoids relying on the FileCallbackClientCtor mock surviving clearMocks/restoreMocks.
 */
function createPreSeededClients(address: string) {
  const clients = new Map<string, unknown>();
  const client = {
    canAccess: mockCanAccess,
    onFileProcessed: mockOnFileProcessed,
  };
  clients.set(address, client);
  return clients;
}

// --- CallCanAccess Tests ---

describe("CallCanAccess", () => {
  beforeEach(() => {
    // Re-set the ctor mock for tests that need new client creation.
    // Must use `function` (not arrow) so it works as a constructor with `new`.
    mockCtor.mockImplementation(function (this: Record<string, unknown>) {
      this.canAccess = mockCanAccess;
      this.onFileProcessed = mockOnFileProcessed;
    });
  });

  it("should return allowed=true on successful gRPC call", async () => {
    const clients = createPreSeededClients("auth:5101");
    const handler = new CallCanAccess(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockCanAccess.mockImplementation(
      (_req: unknown, _meta: unknown, _opts: unknown, cb: (err: unknown, res: unknown) => void) => {
        cb(null, { result: okResultProto, allowed: true });
      },
    );

    const result = await handler.handleAsync({
      address: "auth:5101",
      contextKey: "user_avatar",
      relatedEntityId: "entity-1",
      requestingUserId: "user-123",
      requestingOrgId: "org-456",
      action: "upload",
    });

    expect(result).toBeSuccess();
    expect(result.data?.allowed).toBe(true);
  });

  it("should return allowed=false when service denies access", async () => {
    const clients = createPreSeededClients("auth:5101");
    const handler = new CallCanAccess(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockCanAccess.mockImplementation(
      (_req: unknown, _meta: unknown, _opts: unknown, cb: (err: unknown, res: unknown) => void) => {
        cb(null, { result: okResultProto, allowed: false });
      },
    );

    const result = await handler.handleAsync({
      address: "auth:5101",
      contextKey: "user_avatar",
      relatedEntityId: "entity-1",
      requestingUserId: "user-999",
      requestingOrgId: "org-456",
      action: "read",
    });

    expect(result).toBeSuccess();
    expect(result.data?.allowed).toBe(false);
  });

  it("should return serviceUnavailable on gRPC error", async () => {
    const clients = createPreSeededClients("auth:5101");
    const handler = new CallCanAccess(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockCanAccess.mockImplementation(
      (_req: unknown, _meta: unknown, _opts: unknown, cb: (err: unknown, res: unknown) => void) => {
        const err = Object.assign(new Error("UNAVAILABLE: connection refused"), { code: 14 });
        cb(err, null);
      },
    );

    const result = await handler.handleAsync({
      address: "auth:5101",
      contextKey: "user_avatar",
      relatedEntityId: "entity-1",
      requestingUserId: "user-123",
      requestingOrgId: "org-456",
      action: "upload",
    });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(503);
  });

  it("should pass correct request fields to gRPC", async () => {
    const clients = createPreSeededClients("auth:5101");
    const handler = new CallCanAccess(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockCanAccess.mockImplementation(
      (
        req: Record<string, unknown>,
        _meta: unknown,
        _opts: unknown,
        cb: (err: unknown, res: unknown) => void,
      ) => {
        expect(req).toEqual({
          contextKey: "org_logo",
          relatedEntityId: "org-789",
          requestingUserId: "user-aaa",
          requestingOrgId: "org-bbb",
          action: "read",
        });
        cb(null, { result: okResultProto, allowed: true });
      },
    );

    await handler.handleAsync({
      address: "auth:5101",
      contextKey: "org_logo",
      relatedEntityId: "org-789",
      requestingUserId: "user-aaa",
      requestingOrgId: "org-bbb",
      action: "read",
    });
  });

  it("should set deadline on gRPC call options", async () => {
    const clients = createPreSeededClients("auth:5101");
    const handler = new CallCanAccess(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockCanAccess.mockImplementation(
      (
        _req: unknown,
        _meta: unknown,
        opts: { deadline: number },
        cb: (err: unknown, res: unknown) => void,
      ) => {
        // Deadline should be Date.now() + 10_000
        expect(opts.deadline).toBeGreaterThan(Date.now() - 1000);
        expect(opts.deadline).toBeLessThanOrEqual(Date.now() + 11_000);
        cb(null, { result: okResultProto, allowed: true });
      },
    );

    await handler.handleAsync({
      address: "auth:5101",
      contextKey: "user_avatar",
      relatedEntityId: "entity-1",
      requestingUserId: "user-123",
      requestingOrgId: "org-456",
      action: "upload",
    });
  });

  it("should create and cache client for new address", async () => {
    const clients = new Map<string, unknown>();
    const handler = new CallCanAccess(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockCanAccess.mockImplementation(
      (_req: unknown, _meta: unknown, _opts: unknown, cb: (err: unknown, res: unknown) => void) => {
        cb(null, { result: okResultProto, allowed: true });
      },
    );

    await handler.handleAsync({
      address: "auth:5101",
      contextKey: "user_avatar",
      relatedEntityId: "entity-1",
      requestingUserId: "user-123",
      requestingOrgId: "org-456",
      action: "upload",
    });

    expect(clients.size).toBe(1);
    expect(clients.has("auth:5101")).toBe(true);
  });

  it("should reuse cached client for same address", async () => {
    const clients = createPreSeededClients("auth:5101");
    const handler = new CallCanAccess(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockCanAccess.mockImplementation(
      (_req: unknown, _meta: unknown, _opts: unknown, cb: (err: unknown, res: unknown) => void) => {
        cb(null, { result: okResultProto, allowed: true });
      },
    );

    await handler.handleAsync({
      address: "auth:5101",
      contextKey: "user_avatar",
      relatedEntityId: "e1",
      requestingUserId: "u1",
      requestingOrgId: "o1",
      action: "upload",
    });

    await handler.handleAsync({
      address: "auth:5101",
      contextKey: "org_logo",
      relatedEntityId: "e2",
      requestingUserId: "u2",
      requestingOrgId: "o2",
      action: "read",
    });

    // Should still have only 1 client entry for auth:5101
    expect(clients.size).toBe(1);
    // Constructor should NOT have been called (client was pre-seeded)
    expect(mockCtor).not.toHaveBeenCalled();
  });

  it("should create separate clients for different addresses", async () => {
    const clients = new Map<string, unknown>();
    const handler = new CallCanAccess(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockCanAccess.mockImplementation(
      (_req: unknown, _meta: unknown, _opts: unknown, cb: (err: unknown, res: unknown) => void) => {
        cb(null, { result: okResultProto, allowed: true });
      },
    );

    await handler.handleAsync({
      address: "auth:5101",
      contextKey: "user_avatar",
      relatedEntityId: "e1",
      requestingUserId: "u1",
      requestingOrgId: "o1",
      action: "upload",
    });

    await handler.handleAsync({
      address: "comms:3200",
      contextKey: "thread_attachment",
      relatedEntityId: "e2",
      requestingUserId: "u2",
      requestingOrgId: "o2",
      action: "read",
    });

    expect(clients.size).toBe(2);
    expect(clients.has("auth:5101")).toBe(true);
    expect(clients.has("comms:3200")).toBe(true);
  });
});

// --- CallOnFileProcessed Tests ---

describe("CallOnFileProcessed", () => {
  beforeEach(() => {
    mockCtor.mockImplementation(function (this: Record<string, unknown>) {
      this.canAccess = mockCanAccess;
      this.onFileProcessed = mockOnFileProcessed;
    });
  });

  it("should return success=true on successful gRPC call", async () => {
    const clients = createPreSeededClients("auth:5101");
    const handler = new CallOnFileProcessed(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockOnFileProcessed.mockImplementation(
      (_req: unknown, _meta: unknown, _opts: unknown, cb: (err: unknown, res: unknown) => void) => {
        cb(null, { result: okResultProto, success: true });
      },
    );

    const result = await handler.handleAsync({
      address: "auth:5101",
      fileId: "file-001",
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      status: "ready",
      variants: ["thumb", "small", "original"],
    });

    expect(result).toBeSuccess();
    expect(result.data?.success).toBe(true);
  });

  it("should return success=false when service responds with failure", async () => {
    const clients = createPreSeededClients("auth:5101");
    const handler = new CallOnFileProcessed(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockOnFileProcessed.mockImplementation(
      (_req: unknown, _meta: unknown, _opts: unknown, cb: (err: unknown, res: unknown) => void) => {
        cb(null, { result: okResultProto, success: false });
      },
    );

    const result = await handler.handleAsync({
      address: "auth:5101",
      fileId: "file-001",
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      status: "rejected",
    });

    expect(result).toBeSuccess();
    expect(result.data?.success).toBe(false);
  });

  it("should return serviceUnavailable on gRPC error", async () => {
    const clients = createPreSeededClients("auth:5101");
    const handler = new CallOnFileProcessed(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockOnFileProcessed.mockImplementation(
      (_req: unknown, _meta: unknown, _opts: unknown, cb: (err: unknown, res: unknown) => void) => {
        const err = Object.assign(new Error("DEADLINE_EXCEEDED"), { code: 4 });
        cb(err, null);
      },
    );

    const result = await handler.handleAsync({
      address: "auth:5101",
      fileId: "file-001",
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      status: "ready",
    });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(503);
  });

  it("should pass correct request fields including status and variants", async () => {
    const clients = createPreSeededClients("auth:5101");
    const handler = new CallOnFileProcessed(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockOnFileProcessed.mockImplementation(
      (
        req: Record<string, unknown>,
        _meta: unknown,
        _opts: unknown,
        cb: (err: unknown, res: unknown) => void,
      ) => {
        expect(req).toEqual({
          fileId: "file-002",
          contextKey: "org_logo",
          relatedEntityId: "org-789",
          status: "rejected",
          variants: [],
        });
        cb(null, { result: okResultProto, success: true });
      },
    );

    await handler.handleAsync({
      address: "auth:5101",
      fileId: "file-002",
      contextKey: "org_logo",
      relatedEntityId: "org-789",
      status: "rejected",
    });
  });

  it("should spread variants array to new array", async () => {
    const clients = createPreSeededClients("auth:5101");
    const handler = new CallOnFileProcessed(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );
    const inputVariants = ["thumb", "medium", "original"];

    mockOnFileProcessed.mockImplementation(
      (
        req: { variants: string[] },
        _meta: unknown,
        _opts: unknown,
        cb: (err: unknown, res: unknown) => void,
      ) => {
        expect(req.variants).toEqual(inputVariants);
        // Should be a new array, not the same reference
        expect(req.variants).not.toBe(inputVariants);
        cb(null, { result: okResultProto, success: true });
      },
    );

    await handler.handleAsync({
      address: "auth:5101",
      fileId: "file-001",
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      status: "ready",
      variants: inputVariants,
    });
  });

  it("should pass empty array when variants is undefined", async () => {
    const clients = createPreSeededClients("comms:3200");
    const handler = new CallOnFileProcessed(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockOnFileProcessed.mockImplementation(
      (
        req: { variants: string[] },
        _meta: unknown,
        _opts: unknown,
        cb: (err: unknown, res: unknown) => void,
      ) => {
        expect(req.variants).toEqual([]);
        cb(null, { result: okResultProto, success: true });
      },
    );

    await handler.handleAsync({
      address: "comms:3200",
      fileId: "file-003",
      contextKey: "thread_attachment",
      relatedEntityId: "thread-1",
      status: "ready",
    });
  });

  it("should set deadline on gRPC call options", async () => {
    const clients = createPreSeededClients("auth:5101");
    const handler = new CallOnFileProcessed(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockOnFileProcessed.mockImplementation(
      (
        _req: unknown,
        _meta: unknown,
        opts: { deadline: number },
        cb: (err: unknown, res: unknown) => void,
      ) => {
        expect(opts.deadline).toBeGreaterThan(Date.now() - 1000);
        expect(opts.deadline).toBeLessThanOrEqual(Date.now() + 11_000);
        cb(null, { result: okResultProto, success: true });
      },
    );

    await handler.handleAsync({
      address: "auth:5101",
      fileId: "file-001",
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      status: "ready",
    });
  });

  it("should create and cache client for new address", async () => {
    const clients = new Map<string, unknown>();
    const handler = new CallOnFileProcessed(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockOnFileProcessed.mockImplementation(
      (_req: unknown, _meta: unknown, _opts: unknown, cb: (err: unknown, res: unknown) => void) => {
        cb(null, { result: okResultProto, success: true });
      },
    );

    await handler.handleAsync({
      address: "auth:5101",
      fileId: "file-001",
      contextKey: "user_avatar",
      relatedEntityId: "user-123",
      status: "ready",
    });

    expect(clients.size).toBe(1);
    expect(clients.has("auth:5101")).toBe(true);
  });

  it("should reuse cached client for same address", async () => {
    const clients = createPreSeededClients("auth:5101");
    const handler = new CallOnFileProcessed(
      clients as never,
      "test-callback-key",
      DEFAULT_FILES_OUTBOUND_OPTIONS,
      createTestContext(),
    );

    mockOnFileProcessed.mockImplementation(
      (_req: unknown, _meta: unknown, _opts: unknown, cb: (err: unknown, res: unknown) => void) => {
        cb(null, { result: okResultProto, success: true });
      },
    );

    await handler.handleAsync({
      address: "auth:5101",
      fileId: "file-001",
      contextKey: "a",
      relatedEntityId: "e1",
      status: "ready",
    });

    await handler.handleAsync({
      address: "auth:5101",
      fileId: "file-002",
      contextKey: "b",
      relatedEntityId: "e2",
      status: "rejected",
    });

    expect(clients.size).toBe(1);
    expect(mockCtor).not.toHaveBeenCalled();
  });
});
