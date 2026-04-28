import { describe, it, expect, vi, beforeEach } from "vitest";
import type { IHandlerContext, IRequestContext } from "@d2/handler";

// --- Hoisted mocks ---

const { mockSocketCtor } = vi.hoisted(() => ({
  mockSocketCtor: vi.fn(),
}));

vi.mock("node:net", () => ({
  Socket: mockSocketCtor,
}));

// --- Imports (after mocks) ---

import { ScanFile, type ClamdConfig, DEFAULT_FILES_SCANNING_OPTIONS } from "@d2/files-infra";

// --- Helpers ---

const TEST_CLAMD_CONFIG: ClamdConfig = {
  host: "127.0.0.1",
  port: 3310,
};

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

interface MockSocketInstance {
  setTimeout: ReturnType<typeof vi.fn>;
  on: ReturnType<typeof vi.fn>;
  connect: ReturnType<typeof vi.fn>;
  write: ReturnType<typeof vi.fn>;
  destroy: ReturnType<typeof vi.fn>;
  end: ReturnType<typeof vi.fn>;
}

/**
 * Creates a fresh mock socket instance and sets up the Socket constructor
 * to return it. Must be called in beforeEach since clearMocks/restoreMocks
 * clears constructor implementations.
 */
function createMockSocket(): MockSocketInstance {
  const socket: MockSocketInstance = {
    setTimeout: vi.fn(),
    on: vi.fn().mockReturnThis(),
    connect: vi.fn().mockReturnThis(),
    write: vi.fn(),
    destroy: vi.fn(),
    end: vi.fn(),
  };

  // Must use `function` (not arrow) so it works as a constructor with `new`
  mockSocketCtor.mockImplementation(function () {
    return socket;
  });

  return socket;
}

/**
 * Configures the mock socket to simulate a clamd response.
 * The connect callback triggers sending the response data then end.
 */
function simulateClamdResponse(socket: MockSocketInstance, response: string): void {
  const onHandlers = new Map<string, (...args: unknown[]) => void>();
  socket.on.mockImplementation((event: string, handler: (...args: unknown[]) => void) => {
    onHandlers.set(event, handler);
    return socket;
  });

  socket.connect.mockImplementation((_port: number, _host: string, callback: () => void) => {
    callback();
    const dataHandler = onHandlers.get("data");
    if (dataHandler) {
      dataHandler(Buffer.from(response));
    }
    const endHandler = onHandlers.get("end");
    if (endHandler) {
      endHandler();
    }
    return socket;
  });
}

function simulateClamdError(socket: MockSocketInstance, error: Error): void {
  const onHandlers = new Map<string, (...args: unknown[]) => void>();
  socket.on.mockImplementation((event: string, handler: (...args: unknown[]) => void) => {
    onHandlers.set(event, handler);
    return socket;
  });

  socket.connect.mockImplementation(() => {
    const errorHandler = onHandlers.get("error");
    if (errorHandler) {
      errorHandler(error);
    }
    return socket;
  });
}

function simulateClamdTimeout(socket: MockSocketInstance): void {
  const onHandlers = new Map<string, (...args: unknown[]) => void>();
  socket.on.mockImplementation((event: string, handler: (...args: unknown[]) => void) => {
    onHandlers.set(event, handler);
    return socket;
  });

  socket.connect.mockImplementation(() => {
    const timeoutHandler = onHandlers.get("timeout");
    if (timeoutHandler) {
      timeoutHandler();
    }
    return socket;
  });

  socket.destroy.mockImplementation((err: Error) => {
    const errorHandler = onHandlers.get("error");
    if (errorHandler) {
      errorHandler(err);
    }
  });
}

// --- Tests ---

describe("ScanFile", () => {
  let handler: ScanFile;

  beforeEach(() => {
    handler = new ScanFile(TEST_CLAMD_CONFIG, DEFAULT_FILES_SCANNING_OPTIONS, createTestContext());
  });

  it("should return clean=true for OK response", async () => {
    const socket = createMockSocket();
    simulateClamdResponse(socket, "stream: OK");

    const result = await handler.handleAsync({
      buffer: Buffer.from("safe-content"),
      contentType: "image/jpeg",
    });

    expect(result).toBeSuccess();
    expect(result.data?.clean).toBe(true);
    expect(result.data?.threat).toBeUndefined();
  });

  it("should return clean=false with threat name for FOUND response", async () => {
    const socket = createMockSocket();
    simulateClamdResponse(socket, "stream: Eicar-Signature FOUND");

    const result = await handler.handleAsync({
      buffer: Buffer.from("malicious-content"),
      contentType: "application/octet-stream",
    });

    expect(result).toBeSuccess();
    expect(result.data?.clean).toBe(false);
    expect(result.data?.threat).toBe("Eicar-Signature");
  });

  it("should return clean=false with multi-word threat name", async () => {
    const socket = createMockSocket();
    simulateClamdResponse(socket, "stream: Win.Trojan.Agent-12345 FOUND");

    const result = await handler.handleAsync({
      buffer: Buffer.from("trojan"),
      contentType: "application/octet-stream",
    });

    expect(result).toBeSuccess();
    expect(result.data?.clean).toBe(false);
    expect(result.data?.threat).toBe("Win.Trojan.Agent-12345");
  });

  it("should return serviceUnavailable for unexpected response format", async () => {
    const socket = createMockSocket();
    simulateClamdResponse(socket, "UNKNOWN RESPONSE FORMAT");

    const result = await handler.handleAsync({
      buffer: Buffer.from("data"),
      contentType: "text/plain",
    });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(503);
  });

  it("should return serviceUnavailable on connection error", async () => {
    const socket = createMockSocket();
    simulateClamdError(socket, new Error("ECONNREFUSED"));

    const result = await handler.handleAsync({
      buffer: Buffer.from("data"),
      contentType: "image/png",
    });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(503);
  });

  it("should return serviceUnavailable on timeout", async () => {
    const socket = createMockSocket();
    simulateClamdTimeout(socket);

    const result = await handler.handleAsync({
      buffer: Buffer.from("data"),
      contentType: "image/jpeg",
    });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(503);
  });

  it("should set 30s socket timeout", async () => {
    const socket = createMockSocket();
    simulateClamdResponse(socket, "stream: OK");

    await handler.handleAsync({
      buffer: Buffer.from("data"),
      contentType: "image/jpeg",
    });

    expect(socket.setTimeout).toHaveBeenCalledWith(30_000);
  });

  it("should send INSTREAM command on connect", async () => {
    const socket = createMockSocket();
    simulateClamdResponse(socket, "stream: OK");

    await handler.handleAsync({
      buffer: Buffer.from("test"),
      contentType: "text/plain",
    });

    // First write should be the INSTREAM command
    expect(socket.write).toHaveBeenCalledWith("zINSTREAM\0");
  });

  it("should send zero terminator after data chunks", async () => {
    const socket = createMockSocket();
    simulateClamdResponse(socket, "stream: OK");

    await handler.handleAsync({
      buffer: Buffer.from("test"),
      contentType: "text/plain",
    });

    // Last write call should be the 4-byte zero terminator
    const writeCalls = socket.write.mock.calls;
    const lastWrite = writeCalls[writeCalls.length - 1]![0] as Buffer;
    expect(Buffer.isBuffer(lastWrite)).toBe(true);
    expect(lastWrite.length).toBe(4);
    expect(lastWrite.readUInt32BE(0)).toBe(0);
  });

  it("should send chunk length prefix as 4-byte big-endian", async () => {
    const socket = createMockSocket();
    simulateClamdResponse(socket, "stream: OK");
    const smallBuffer = Buffer.from("hello");

    await handler.handleAsync({
      buffer: smallBuffer,
      contentType: "text/plain",
    });

    // Writes: zINSTREAM\0, header (4 bytes), data chunk, terminator (4 bytes)
    const writeCalls = socket.write.mock.calls;
    // Second write should be the header with length
    const header = writeCalls[1]![0] as Buffer;
    expect(Buffer.isBuffer(header)).toBe(true);
    expect(header.length).toBe(4);
    expect(header.readUInt32BE(0)).toBe(smallBuffer.length);
  });

  it("should connect to configured host and port", async () => {
    const socket = createMockSocket();
    simulateClamdResponse(socket, "stream: OK");

    await handler.handleAsync({
      buffer: Buffer.from("x"),
      contentType: "text/plain",
    });

    expect(socket.connect).toHaveBeenCalledWith(
      TEST_CLAMD_CONFIG.port,
      TEST_CLAMD_CONFIG.host,
      expect.any(Function),
    );
  });

  it("should handle empty buffer (zero-length file)", async () => {
    const socket = createMockSocket();
    simulateClamdResponse(socket, "stream: OK");

    const result = await handler.handleAsync({
      buffer: Buffer.alloc(0),
      contentType: "application/octet-stream",
    });

    expect(result).toBeSuccess();
    expect(result.data?.clean).toBe(true);
  });

  it("should handle buffer larger than chunk size (8192 bytes)", async () => {
    const socket = createMockSocket();
    simulateClamdResponse(socket, "stream: OK");
    // Create buffer larger than chunkSize=8192 to test multi-chunk sending
    const largeBuffer = Buffer.alloc(20_000, 0x41);

    const result = await handler.handleAsync({
      buffer: largeBuffer,
      contentType: "image/jpeg",
    });

    expect(result).toBeSuccess();

    // Should have written: zINSTREAM\0 + (header + data) * 3 chunks + terminator
    // 20000 bytes = ceil(20000/8192) = 3 chunks
    const writeCalls = socket.write.mock.calls;
    // 1 (INSTREAM) + 3*2 (header+data per chunk) + 1 (terminator) = 8
    expect(writeCalls.length).toBe(8);
  });
});
