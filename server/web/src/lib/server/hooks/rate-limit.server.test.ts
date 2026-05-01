import { describe, it, expect, vi, beforeEach } from "vitest";
import type { RequestEvent } from "@sveltejs/kit";
import type { IRequestContext } from "@d2/handler";
import { D2Result } from "@d2/result";

const mockGetMiddlewareContext = vi.fn();

vi.mock("../middleware.server", () => ({
  getMiddlewareContext: (...args: unknown[]) => mockGetMiddlewareContext(...args),
}));

import { createRateLimitHandle } from "./rate-limit.server";

describe("createRateLimitHandle", () => {
  const handle = createRateLimitHandle();
  const mockResolve = vi.fn().mockResolvedValue(new Response("OK"));

  const fakeRequestContext: Partial<IRequestContext> = {
    clientIp: "1.2.3.4",
    serverFingerprint: "abc",
    isAuthenticated: false,
    isTrustedService: false,
  };

  function makeEvent(withRequestContext = true): RequestEvent {
    const locals: Record<string, unknown> = {};
    if (withRequestContext) locals.requestContext = fakeRequestContext;
    return {
      request: { method: "GET", headers: new Headers() } as unknown as Request,
      locals,
      url: new URL("http://localhost/test"),
    } as unknown as RequestEvent;
  }

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("propagates error when middleware context throws", async () => {
    mockGetMiddlewareContext.mockImplementation(() => {
      throw new Error("FATAL: Missing required env vars");
    });
    const event = makeEvent();

    await expect(handle({ event, resolve: mockResolve })).rejects.toThrow("FATAL");
  });

  it("skips when requestContext is not on locals", async () => {
    mockGetMiddlewareContext.mockReturnValue({ rateLimitCheck: { handleAsync: vi.fn() } });
    const event = makeEvent(false);

    await handle({ event, resolve: mockResolve });

    expect(mockResolve).toHaveBeenCalledWith(event);
  });

  it("allows request when rate limit check passes", async () => {
    const mockCheck = vi.fn().mockResolvedValue(D2Result.ok({ data: { isBlocked: false } }));
    mockGetMiddlewareContext.mockReturnValue({
      rateLimitCheck: { handleAsync: mockCheck },
    });

    const event = makeEvent();
    await handle({ event, resolve: mockResolve });

    expect(mockCheck).toHaveBeenCalledWith({ requestContext: fakeRequestContext });
    expect(mockResolve).toHaveBeenCalledWith(event);
  });

  it("returns 429 when rate limit is exceeded", async () => {
    const mockCheck = vi
      .fn()
      .mockResolvedValue(D2Result.ok({ data: { isBlocked: true, retryAfterMs: 60_000 } }));
    mockGetMiddlewareContext.mockReturnValue({
      rateLimitCheck: { handleAsync: mockCheck },
    });

    const event = makeEvent();
    const response = await handle({ event, resolve: mockResolve });

    expect(response.status).toBe(429);
    expect(response.headers.get("Retry-After")).toBe("60");
    expect(response.headers.get("Content-Type")).toBe("application/json");

    const body = await response.json();
    expect(body.success).toBe(false);
    expect(body.errorCode).toBe("RATE_LIMITED");
  });

  it("defaults Retry-After to 300 when retryAfterMs is absent", async () => {
    const mockCheck = vi.fn().mockResolvedValue(D2Result.ok({ data: { isBlocked: true } }));
    mockGetMiddlewareContext.mockReturnValue({
      rateLimitCheck: { handleAsync: mockCheck },
    });

    const event = makeEvent();
    const response = await handle({ event, resolve: mockResolve });

    expect(response.status).toBe(429);
    expect(response.headers.get("Retry-After")).toBe("300");
  });

  it("proceeds when rate limit check fails (fail-open)", async () => {
    const mockCheck = vi
      .fn()
      .mockResolvedValue(
        D2Result.fail({ messages: ["Redis error"] }) as D2Result<{ isBlocked: boolean }>,
      );
    mockGetMiddlewareContext.mockReturnValue({
      rateLimitCheck: { handleAsync: mockCheck },
    });

    const event = makeEvent();
    await handle({ event, resolve: mockResolve });

    expect(mockResolve).toHaveBeenCalledWith(event);
  });
});
