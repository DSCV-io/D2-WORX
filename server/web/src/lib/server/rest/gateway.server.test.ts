import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import type { D2Result } from "@d2/result";

// Mock auth.server module
const mockGetToken = vi.fn<(cookies: string) => Promise<string | null>>();

vi.mock("../auth.server", () => ({
  getAuthContext: () => ({
    jwtManager: { getToken: mockGetToken },
  }),
}));

// Mock Paraglide runtime — default locale "en-US", overridable per-test
const mockGetLocale = vi.fn(() => "en-US");

vi.mock("$lib/paraglide/runtime.js", () => ({
  getLocale: () => mockGetLocale(),
}));

describe("gateway.server", () => {
  const ENV_BACKUP: Record<string, string | undefined> = {};

  function setEnv(key: string, value: string) {
    ENV_BACKUP[key] = process.env[key];
    process.env[key] = value;
  }

  beforeEach(() => {
    vi.resetModules();
    mockGetToken.mockReset();
    mockGetLocale.mockReturnValue("en-US");
  });

  afterEach(() => {
    for (const [key, value] of Object.entries(ENV_BACKUP)) {
      if (value === undefined) {
        delete process.env[key];
      } else {
        process.env[key] = value;
      }
    }
    Object.keys(ENV_BACKUP).forEach((k) => delete ENV_BACKUP[k]);
  });

  // -------------------------------------------------------------------------
  // getGatewayContext
  // -------------------------------------------------------------------------
  describe("getGatewayContext", () => {
    it("throws when SVELTEKIT_GATEWAY__URL is missing", async () => {
      const { getGatewayContext } = await import("./gateway.server");
      expect(() => getGatewayContext()).toThrow("SVELTEKIT_GATEWAY__URL");
    });

    it("returns context when SVELTEKIT_GATEWAY__URL is set", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");

      const { getGatewayContext } = await import("./gateway.server");
      const ctx = getGatewayContext();

      expect(ctx.baseUrl).toBe("http://localhost:5461");
    });

    it("strips trailing slashes from base URL", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461///");

      const { getGatewayContext } = await import("./gateway.server");
      const ctx = getGatewayContext();

      expect(ctx.baseUrl).toBe("http://localhost:5461");
    });

    it("includes service key when configured", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");
      setEnv("SVELTEKIT_GATEWAY__SERVICE_KEY", "test-key");

      const { getGatewayContext } = await import("./gateway.server");
      const ctx = getGatewayContext();

      expect(ctx.serviceKey).toBe("test-key");
    });

    it("returns undefined serviceKey when not configured", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");

      const { getGatewayContext } = await import("./gateway.server");
      const ctx = getGatewayContext();

      expect(ctx.serviceKey).toBeUndefined();
    });

    it("returns cached singleton on subsequent calls", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");

      const { getGatewayContext } = await import("./gateway.server");
      const ctx1 = getGatewayContext();
      const ctx2 = getGatewayContext();

      expect(ctx1).toBe(ctx2);
    });
  });

  // -------------------------------------------------------------------------
  // gatewayFetch (authenticated)
  // -------------------------------------------------------------------------
  describe("gatewayFetch", () => {
    it("returns unauthorized when JWT is null", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");
      mockGetToken.mockResolvedValue(null);

      const { gatewayFetch } = await import("./gateway.server");
      const result = await gatewayFetch("/api/v1/test", "session_token=abc");

      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(401);
      expect(result.messages[0]).toContain("common_errors_UNAUTHORIZED");
    });

    it("sends Authorization and X-Api-Key headers", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");
      setEnv("SVELTEKIT_GATEWAY__SERVICE_KEY", "sk-123");
      mockGetToken.mockResolvedValue("jwt-token-abc");

      const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(
        new Response(JSON.stringify({ success: true, data: { id: 1 } }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      );

      const { gatewayFetch } = await import("./gateway.server");
      const result = await gatewayFetch<{ id: number }>("/api/v1/test", "cookies=here");

      expect(fetchSpy).toHaveBeenCalledTimes(1);
      const [url, init] = fetchSpy.mock.calls[0];
      expect(url).toBe("http://localhost:5461/api/v1/test");

      const headers = init!.headers as Headers;
      expect(headers.get("Authorization")).toBe("Bearer jwt-token-abc");
      expect(headers.get("X-Api-Key")).toBe("sk-123");

      expect(result.success).toBe(true);
      expect(result.data?.id).toBe(1);

      fetchSpy.mockRestore();
    });

    it("does not send X-Api-Key when service key is not configured", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");
      mockGetToken.mockResolvedValue("jwt-token-abc");

      const fetchSpy = vi
        .spyOn(globalThis, "fetch")
        .mockResolvedValue(new Response(JSON.stringify({ success: true }), { status: 200 }));

      const { gatewayFetch } = await import("./gateway.server");
      await gatewayFetch("/api/v1/test", "cookies=here");

      const headers = fetchSpy.mock.calls[0][1]!.headers as Headers;
      expect(headers.has("X-Api-Key")).toBe(false);

      fetchSpy.mockRestore();
    });

    it("sends Content-Type and body for POST requests", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");
      mockGetToken.mockResolvedValue("jwt-token");

      const fetchSpy = vi
        .spyOn(globalThis, "fetch")
        .mockResolvedValue(new Response(JSON.stringify({ success: true }), { status: 200 }));

      const { gatewayFetch } = await import("./gateway.server");
      await gatewayFetch("/api/v1/test", "cookies", {
        method: "POST",
        body: { name: "test" },
      });

      const [, init] = fetchSpy.mock.calls[0];
      expect(init!.method).toBe("POST");
      expect(init!.body).toBe(JSON.stringify({ name: "test" }));
      const headers = init!.headers as Headers;
      expect(headers.get("Content-Type")).toBe("application/json");

      fetchSpy.mockRestore();
    });

    it("sends Idempotency-Key header when provided", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");
      mockGetToken.mockResolvedValue("jwt-token");

      const fetchSpy = vi
        .spyOn(globalThis, "fetch")
        .mockResolvedValue(new Response(JSON.stringify({ success: true }), { status: 200 }));

      const { gatewayFetch } = await import("./gateway.server");
      await gatewayFetch("/api/v1/test", "cookies", {
        method: "POST",
        idempotencyKey: "idem-key-123",
      });

      const headers = fetchSpy.mock.calls[0][1]!.headers as Headers;
      expect(headers.get("Idempotency-Key")).toBe("idem-key-123");

      fetchSpy.mockRestore();
    });

    it("returns network error result on fetch failure", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");
      mockGetToken.mockResolvedValue("jwt-token");

      const fetchSpy = vi.spyOn(globalThis, "fetch").mockRejectedValue(new Error("ECONNREFUSED"));

      const { gatewayFetch } = await import("./gateway.server");
      const result = await gatewayFetch("/api/v1/test", "cookies");

      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(500);
      expect(result.messages[0]).toContain("ECONNREFUSED");

      fetchSpy.mockRestore();
    });

    it("returns 408 on AbortError", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");
      mockGetToken.mockResolvedValue("jwt-token");

      const abortError = new DOMException("The operation was aborted.", "AbortError");
      const fetchSpy = vi.spyOn(globalThis, "fetch").mockRejectedValue(abortError);

      const { gatewayFetch } = await import("./gateway.server");
      const result = await gatewayFetch("/api/v1/test", "cookies");

      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(408);
      expect(result.messages[0]).toContain("common_errors_CANCELLED");

      fetchSpy.mockRestore();
    });

    it("returns 408 on TimeoutError", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");
      mockGetToken.mockResolvedValue("jwt-token");

      const timeoutError = new DOMException("The operation timed out.", "TimeoutError");
      const fetchSpy = vi.spyOn(globalThis, "fetch").mockRejectedValue(timeoutError);

      const { gatewayFetch } = await import("./gateway.server");
      const result = await gatewayFetch("/api/v1/test", "cookies");

      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(408);
      expect(result.messages[0]).toContain("common_errors_REQUEST_FAILED");

      fetchSpy.mockRestore();
    });

    it("defaults to GET method when not specified", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");
      mockGetToken.mockResolvedValue("jwt-token");

      const fetchSpy = vi
        .spyOn(globalThis, "fetch")
        .mockResolvedValue(new Response(JSON.stringify({ success: true }), { status: 200 }));

      const { gatewayFetch } = await import("./gateway.server");
      await gatewayFetch("/api/v1/test", "cookies");

      expect(fetchSpy.mock.calls[0][1]!.method).toBe("GET");

      fetchSpy.mockRestore();
    });
  });

  // -------------------------------------------------------------------------
  // gatewayFetchAnon (anonymous)
  // -------------------------------------------------------------------------
  describe("gatewayFetchAnon", () => {
    it("sends no auth headers", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");

      const fetchSpy = vi
        .spyOn(globalThis, "fetch")
        .mockResolvedValue(
          new Response(JSON.stringify({ success: true, data: [] }), { status: 200 }),
        );

      const { gatewayFetchAnon } = await import("./gateway.server");
      await gatewayFetchAnon("/api/v1/geo/countries");

      const headers = fetchSpy.mock.calls[0][1]!.headers as Headers;
      expect(headers.has("Authorization")).toBe(false);
      expect(headers.has("X-Api-Key")).toBe(false);

      fetchSpy.mockRestore();
    });

    it("parses successful response", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");

      const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(
        new Response(JSON.stringify({ Success: true, Data: { items: [1, 2, 3] } }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      );

      const { gatewayFetchAnon } = await import("./gateway.server");
      const result = await gatewayFetchAnon<{ items: number[] }>("/api/v1/test");

      expect(result.success).toBe(true);
      expect(result.data?.items).toEqual([1, 2, 3]);

      fetchSpy.mockRestore();
    });
  });

  // -------------------------------------------------------------------------
  // D2-Locale header
  // -------------------------------------------------------------------------
  describe("D2-Locale header", () => {
    it("sends D2-Locale header with default locale on authenticated calls", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");
      mockGetToken.mockResolvedValue("jwt-token");

      const fetchSpy = vi
        .spyOn(globalThis, "fetch")
        .mockResolvedValue(new Response(JSON.stringify({ success: true }), { status: 200 }));

      const { gatewayFetch } = await import("./gateway.server");
      await gatewayFetch("/api/v1/test", "cookies");

      const headers = fetchSpy.mock.calls[0][1]!.headers as Headers;
      expect(headers.get("D2-Locale")).toBe("en-US");

      fetchSpy.mockRestore();
    });

    it("sends D2-Locale header with non-default locale", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");
      mockGetToken.mockResolvedValue("jwt-token");
      mockGetLocale.mockReturnValue("de-DE");

      const fetchSpy = vi
        .spyOn(globalThis, "fetch")
        .mockResolvedValue(new Response(JSON.stringify({ success: true }), { status: 200 }));

      const { gatewayFetch } = await import("./gateway.server");
      await gatewayFetch("/api/v1/test", "cookies");

      const headers = fetchSpy.mock.calls[0][1]!.headers as Headers;
      expect(headers.get("D2-Locale")).toBe("de-DE");

      fetchSpy.mockRestore();
    });

    it("sends D2-Locale header on anonymous calls", async () => {
      setEnv("SVELTEKIT_GATEWAY__URL", "http://localhost:5461");
      mockGetLocale.mockReturnValue("fr-FR");

      const fetchSpy = vi
        .spyOn(globalThis, "fetch")
        .mockResolvedValue(new Response(JSON.stringify({ success: true }), { status: 200 }));

      const { gatewayFetchAnon } = await import("./gateway.server");
      await gatewayFetchAnon("/api/v1/geo/countries");

      const headers = fetchSpy.mock.calls[0][1]!.headers as Headers;
      expect(headers.get("D2-Locale")).toBe("fr-FR");

      fetchSpy.mockRestore();
    });
  });
});
