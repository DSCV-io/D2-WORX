import { describe, it, expect, vi, beforeEach } from "vitest";
import type { RequestEvent } from "@sveltejs/kit";
import type { IdempotencyResult } from "../middleware.server";

const mockGetMiddlewareContext = vi.fn();
const mockCheckIdempotency = vi.fn();

vi.mock("../middleware.server", () => ({
  getMiddlewareContext: (...args: unknown[]) => mockGetMiddlewareContext(...args),
  checkIdempotency: (...args: unknown[]) => mockCheckIdempotency(...args),
}));

import { createIdempotencyHandle } from "./idempotency.server";

describe("createIdempotencyHandle", () => {
  const handle = createIdempotencyHandle();
  const mockResolve = vi.fn().mockResolvedValue(new Response("OK", { status: 200 }));

  const mockCtx = {
    idempotencyCheck: { handleAsync: vi.fn() },
    redisSet: { handleAsync: vi.fn() },
    redisRemove: { handleAsync: vi.fn() },
    logger: { info: vi.fn(), warn: vi.fn(), debug: vi.fn(), error: vi.fn() },
  };

  function makeEvent(method: string, headers?: Record<string, string>): RequestEvent {
    return {
      request: {
        method,
        headers: new Headers(headers),
      } as unknown as Request,
      locals: {} as Record<string, unknown>,
      url: new URL("http://localhost/api/test"),
    } as unknown as RequestEvent;
  }

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetMiddlewareContext.mockReturnValue(mockCtx);
    mockResolve.mockResolvedValue(new Response("OK", { status: 200 }));
  });

  it("propagates error when middleware context throws", async () => {
    mockGetMiddlewareContext.mockImplementation(() => {
      throw new Error("FATAL: Missing required env vars");
    });
    const event = makeEvent("POST", { "idempotency-key": "key-1" });

    await expect(handle({ event, resolve: mockResolve })).rejects.toThrow("FATAL");
    expect(mockCheckIdempotency).not.toHaveBeenCalled();
  });

  it("skips for GET requests", async () => {
    const event = makeEvent("GET", { "idempotency-key": "key-1" });

    await handle({ event, resolve: mockResolve });

    expect(mockResolve).toHaveBeenCalledWith(event);
    expect(mockCheckIdempotency).not.toHaveBeenCalled();
  });

  it("skips for HEAD requests", async () => {
    const event = makeEvent("HEAD", { "idempotency-key": "key-1" });

    await handle({ event, resolve: mockResolve });

    expect(mockResolve).toHaveBeenCalledWith(event);
    expect(mockCheckIdempotency).not.toHaveBeenCalled();
  });

  it("skips when Idempotency-Key header is absent", async () => {
    const event = makeEvent("POST");

    await handle({ event, resolve: mockResolve });

    expect(mockResolve).toHaveBeenCalledWith(event);
    expect(mockCheckIdempotency).not.toHaveBeenCalled();
  });

  it("returns cached response on replay (state=cached)", async () => {
    const cachedResponse = {
      statusCode: 201,
      body: '{"id":"123"}',
      contentType: "application/json",
    };
    mockCheckIdempotency.mockResolvedValue({
      state: "cached",
      cachedResponse,
      storeResponse: vi.fn(),
      removeLock: vi.fn(),
    } satisfies IdempotencyResult);

    const event = makeEvent("POST", { "idempotency-key": "key-1" });
    const response = await handle({ event, resolve: mockResolve });

    expect(response.status).toBe(201);
    expect(await response.json()).toEqual({ id: "123" });
    expect(mockResolve).not.toHaveBeenCalled();
  });

  it("returns 409 when request is already in flight", async () => {
    mockCheckIdempotency.mockResolvedValue({
      state: "in_flight",
      cachedResponse: undefined,
      storeResponse: vi.fn(),
      removeLock: vi.fn(),
    } satisfies IdempotencyResult);

    const event = makeEvent("POST", { "idempotency-key": "key-2" });
    const response = await handle({ event, resolve: mockResolve });

    expect(response.status).toBe(409);
    const body = await response.json();
    expect(body.errorCode).toBe("IDEMPOTENCY_IN_FLIGHT");
    expect(mockResolve).not.toHaveBeenCalled();
  });

  it("proceeds and stores response when lock is acquired", async () => {
    const mockStoreResponse = vi.fn();
    mockCheckIdempotency.mockResolvedValue({
      state: "acquired",
      cachedResponse: undefined,
      storeResponse: mockStoreResponse,
      removeLock: vi.fn(),
    } satisfies IdempotencyResult);

    const responseBody = '{"created":true}';
    mockResolve.mockResolvedValue(
      new Response(responseBody, {
        status: 201,
        headers: { "Content-Type": "application/json" },
      }),
    );

    const event = makeEvent("POST", { "idempotency-key": "key-3" });
    const response = await handle({ event, resolve: mockResolve });

    expect(mockResolve).toHaveBeenCalledWith(event);
    expect(response.status).toBe(201);
    expect(mockStoreResponse).toHaveBeenCalledWith(
      expect.objectContaining({
        statusCode: 201,
        body: responseBody,
        contentType: "application/json",
      }),
    );
  });

  it("applies to PUT method", async () => {
    mockCheckIdempotency.mockResolvedValue({
      state: "acquired",
      cachedResponse: undefined,
      storeResponse: vi.fn(),
      removeLock: vi.fn(),
    });

    const event = makeEvent("PUT", { "idempotency-key": "key-4" });
    await handle({ event, resolve: mockResolve });

    expect(mockCheckIdempotency).toHaveBeenCalled();
  });

  it("applies to PATCH method", async () => {
    mockCheckIdempotency.mockResolvedValue({
      state: "acquired",
      cachedResponse: undefined,
      storeResponse: vi.fn(),
      removeLock: vi.fn(),
    });

    const event = makeEvent("PATCH", { "idempotency-key": "key-5" });
    await handle({ event, resolve: mockResolve });

    expect(mockCheckIdempotency).toHaveBeenCalled();
  });

  it("applies to DELETE method", async () => {
    mockCheckIdempotency.mockResolvedValue({
      state: "acquired",
      cachedResponse: undefined,
      storeResponse: vi.fn(),
      removeLock: vi.fn(),
    });

    const event = makeEvent("DELETE", { "idempotency-key": "key-6" });
    await handle({ event, resolve: mockResolve });

    expect(mockCheckIdempotency).toHaveBeenCalled();
  });
});
