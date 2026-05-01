import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

// Mock $env/dynamic/public
vi.mock("$env/dynamic/public", () => ({
  env: { PUBLIC_GATEWAY_URL: "http://localhost:5461" },
}));

// Mock Paraglide runtime — default locale "en-US", overridable per-test
const mockGetLocale = vi.fn(() => "en-US");

vi.mock("$lib/paraglide/runtime.js", () => ({
  getLocale: () => mockGetLocale(),
}));

describe("gateway-client", () => {
  let fetchSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    vi.resetModules();
    fetchSpy = vi.spyOn(globalThis, "fetch");
    mockGetLocale.mockReturnValue("en-US");
  });

  afterEach(() => {
    fetchSpy.mockRestore();
  });

  // -------------------------------------------------------------------------
  // setClientFingerprint
  // -------------------------------------------------------------------------
  describe("setClientFingerprint", () => {
    it("sets the fingerprint used in X-Client-Fingerprint header", async () => {
      fetchSpy.mockResolvedValue(new Response(JSON.stringify({ success: true }), { status: 200 }));

      const { setClientFingerprint, apiCallAnon } = await import("./gateway-client");
      setClientFingerprint("fp-abc-123");

      await apiCallAnon("/api/v1/test");

      const headers = fetchSpy.mock.calls[0][1]!.headers as Headers;
      expect(headers.get("X-Client-Fingerprint")).toBe("fp-abc-123");
    });
  });

  // -------------------------------------------------------------------------
  // apiCallAnon
  // -------------------------------------------------------------------------
  describe("apiCallAnon", () => {
    it("sends no auth headers for anonymous calls", async () => {
      fetchSpy.mockResolvedValue(
        new Response(JSON.stringify({ success: true, data: [] }), { status: 200 }),
      );

      const { apiCallAnon } = await import("./gateway-client");
      await apiCallAnon("/api/v1/public");

      const [url, init] = fetchSpy.mock.calls[0];
      expect(url).toBe("http://localhost:5461/api/v1/public");
      const headers = init!.headers as Headers;
      expect(headers.has("Authorization")).toBe(false);
    });

    it("parses PascalCase gateway response", async () => {
      fetchSpy.mockResolvedValue(
        new Response(JSON.stringify({ Success: true, Data: { count: 5 } }), { status: 200 }),
      );

      const { apiCallAnon } = await import("./gateway-client");
      const result = await apiCallAnon<{ count: number }>("/api/v1/test");

      expect(result.success).toBe(true);
      expect(result.data?.count).toBe(5);
    });

    it("sends body and Content-Type for POST", async () => {
      fetchSpy.mockResolvedValue(new Response(JSON.stringify({ success: true }), { status: 201 }));

      const { apiCallAnon } = await import("./gateway-client");
      await apiCallAnon("/api/v1/test", {
        method: "POST",
        body: { name: "test" },
      });

      const [, init] = fetchSpy.mock.calls[0];
      expect(init!.method).toBe("POST");
      expect(init!.body).toBe(JSON.stringify({ name: "test" }));
      const headers = init!.headers as Headers;
      expect(headers.get("Content-Type")).toBe("application/json");
    });

    it("sends Idempotency-Key header when provided", async () => {
      fetchSpy.mockResolvedValue(new Response(JSON.stringify({ success: true }), { status: 200 }));

      const { apiCallAnon } = await import("./gateway-client");
      await apiCallAnon("/api/v1/test", {
        method: "POST",
        idempotencyKey: "key-456",
      });

      const headers = fetchSpy.mock.calls[0][1]!.headers as Headers;
      expect(headers.get("Idempotency-Key")).toBe("key-456");
    });

    it("returns network error on fetch failure", async () => {
      fetchSpy.mockRejectedValue(new Error("Network failure"));

      const { apiCallAnon } = await import("./gateway-client");
      const result = await apiCallAnon("/api/v1/test");

      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(500);
      expect(result.messages[0]).toContain("Network failure");
    });
  });

  // -------------------------------------------------------------------------
  // apiCall (authenticated)
  // -------------------------------------------------------------------------
  describe("apiCall", () => {
    /** Mock the token endpoint to return a JWT. */
    function mockTokenEndpoint(token: string | null) {
      fetchSpy.mockImplementation(async (input: string | URL | Request) => {
        const url =
          typeof input === "string" ? input : input instanceof URL ? input.href : input.url;
        if (url === "/api/auth/token") {
          if (!token) return new Response("Unauthorized", { status: 401 });
          // Create a minimal JWT with exp claim (15 min from now)
          const exp = Math.floor(Date.now() / 1000) + 900;
          const payload = btoa(JSON.stringify({ exp }));
          const fakeJwt = `header.${payload}.signature`;
          return new Response(JSON.stringify({ token: fakeJwt }), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          });
        }
        // Gateway call
        return new Response(JSON.stringify({ success: true, data: { ok: true } }), {
          status: 200,
        });
      });
    }

    it("returns unauthorized when not authenticated", async () => {
      mockTokenEndpoint(null);

      const { apiCall, invalidateToken } = await import("./gateway-client");
      invalidateToken();
      const result = await apiCall("/api/v1/test");

      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(401);
    });

    it("sends Authorization header with JWT", async () => {
      mockTokenEndpoint("real-jwt");

      const { apiCall, invalidateToken } = await import("./gateway-client");
      invalidateToken();
      await apiCall("/api/v1/test");

      // First call is to /api/auth/token, second is to gateway
      expect(fetchSpy).toHaveBeenCalledTimes(2);
      const [url, init] = fetchSpy.mock.calls[1];
      expect(url).toBe("http://localhost:5461/api/v1/test");
      const headers = init!.headers as Headers;
      expect(headers.get("Authorization")).toMatch(/^Bearer /);
    });

    it("returns original 401 result when retry token fetch also fails", async () => {
      let callCount = 0;
      fetchSpy.mockImplementation(async (input: string | URL | Request) => {
        const url =
          typeof input === "string" ? input : input instanceof URL ? input.href : input.url;

        if (url === "/api/auth/token") {
          callCount++;
          if (callCount === 1) {
            // First token fetch succeeds
            const exp = Math.floor(Date.now() / 1000) + 900;
            const payload = btoa(JSON.stringify({ exp }));
            return new Response(JSON.stringify({ token: `header.${payload}.signature` }), {
              status: 200,
            });
          }
          // Second token fetch fails (user signed out)
          return new Response("Unauthorized", { status: 401 });
        }
        // Gateway always returns 401
        return new Response(JSON.stringify({ success: false, messages: ["Token invalid"] }), {
          status: 401,
        });
      });

      const { apiCall, invalidateToken } = await import("./gateway-client");
      invalidateToken();
      const result = await apiCall("/api/v1/test");

      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(401);
      expect(result.messages[0]).toContain("Token invalid");
    });

    it("retries once on 401 with a fresh token", async () => {
      let callCount = 0;
      fetchSpy.mockImplementation(async (input: string | URL | Request) => {
        const url =
          typeof input === "string" ? input : input instanceof URL ? input.href : input.url;

        if (url === "/api/auth/token") {
          const exp = Math.floor(Date.now() / 1000) + 900;
          const payload = btoa(JSON.stringify({ exp }));
          return new Response(JSON.stringify({ token: `header.${payload}.signature` }), {
            status: 200,
          });
        }

        callCount++;
        if (callCount === 1) {
          // First gateway call returns 401
          return new Response(JSON.stringify({ success: false, messages: ["Token expired"] }), {
            status: 401,
          });
        }
        // Second gateway call succeeds
        return new Response(JSON.stringify({ success: true, data: { retried: true } }), {
          status: 200,
        });
      });

      const { apiCall, invalidateToken } = await import("./gateway-client");
      invalidateToken();
      const result = await apiCall<{ retried: boolean }>("/api/v1/test");

      expect(result.success).toBe(true);
      expect(result.data?.retried).toBe(true);
      // 1: token fetch, 2: first gateway (401), 3: token re-fetch, 4: retry gateway
      expect(fetchSpy).toHaveBeenCalledTimes(4);
    });
  });

  // -------------------------------------------------------------------------
  // invalidateToken
  // -------------------------------------------------------------------------
  describe("invalidateToken", () => {
    it("forces a fresh token fetch on next apiCall", async () => {
      let tokenCallCount = 0;
      fetchSpy.mockImplementation(async (input: string | URL | Request) => {
        const url =
          typeof input === "string" ? input : input instanceof URL ? input.href : input.url;

        if (url === "/api/auth/token") {
          tokenCallCount++;
          const exp = Math.floor(Date.now() / 1000) + 900;
          const payload = btoa(JSON.stringify({ exp }));
          return new Response(JSON.stringify({ token: `header.${payload}.sig` }), { status: 200 });
        }
        return new Response(JSON.stringify({ success: true }), { status: 200 });
      });

      const { apiCall, invalidateToken } = await import("./gateway-client");

      invalidateToken();
      await apiCall("/api/v1/test1");
      expect(tokenCallCount).toBe(1);

      // Cached — should NOT re-fetch
      await apiCall("/api/v1/test2");
      expect(tokenCallCount).toBe(1);

      // Invalidate — next call should re-fetch
      invalidateToken();
      await apiCall("/api/v1/test3");
      expect(tokenCallCount).toBe(2);
    });
  });

  // -------------------------------------------------------------------------
  // Token edge cases
  // -------------------------------------------------------------------------
  describe("token edge cases", () => {
    it("returns unauthorized when token endpoint returns OK but no token field", async () => {
      fetchSpy.mockImplementation(async (input: string | URL | Request) => {
        const url =
          typeof input === "string" ? input : input instanceof URL ? input.href : input.url;
        if (url === "/api/auth/token") {
          return new Response(JSON.stringify({ noTokenHere: true }), { status: 200 });
        }
        return new Response(JSON.stringify({ success: true }), { status: 200 });
      });

      const { apiCall, invalidateToken } = await import("./gateway-client");
      invalidateToken();
      const result = await apiCall("/api/v1/test");

      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(401);
    });

    it("handles malformed JWT payload gracefully (uses fallback expiry)", async () => {
      fetchSpy.mockImplementation(async (input: string | URL | Request) => {
        const url =
          typeof input === "string" ? input : input instanceof URL ? input.href : input.url;
        if (url === "/api/auth/token") {
          // Return a JWT with invalid base64 in the payload segment
          return new Response(JSON.stringify({ token: "header.!!!invalid-base64!!!.signature" }), {
            status: 200,
          });
        }
        return new Response(JSON.stringify({ success: true }), { status: 200 });
      });

      const { apiCall, invalidateToken } = await import("./gateway-client");
      invalidateToken();
      const result = await apiCall("/api/v1/test");

      // Should still work — uses fallback 15min expiry
      expect(result.success).toBe(true);
    });

    it("re-fetches token when cached token is near expiry", async () => {
      let tokenCallCount = 0;
      fetchSpy.mockImplementation(async (input: string | URL | Request) => {
        const url =
          typeof input === "string" ? input : input instanceof URL ? input.href : input.url;
        if (url === "/api/auth/token") {
          tokenCallCount++;
          // Token that expires in 1 minute (within 2-minute REFRESH_BUFFER_MS)
          const exp = Math.floor(Date.now() / 1000) + 60;
          const payload = btoa(JSON.stringify({ exp }));
          return new Response(JSON.stringify({ token: `header.${payload}.sig` }), { status: 200 });
        }
        return new Response(JSON.stringify({ success: true }), { status: 200 });
      });

      const { apiCall, invalidateToken } = await import("./gateway-client");
      invalidateToken();

      await apiCall("/api/v1/test1");
      expect(tokenCallCount).toBe(1);

      // Token is near expiry (1min left < 2min buffer) — should re-fetch
      await apiCall("/api/v1/test2");
      expect(tokenCallCount).toBe(2);
    });

    it("returns null when token endpoint network error occurs", async () => {
      fetchSpy.mockImplementation(async (input: string | URL | Request) => {
        const url =
          typeof input === "string" ? input : input instanceof URL ? input.href : input.url;
        if (url === "/api/auth/token") {
          throw new Error("Network failure");
        }
        return new Response(JSON.stringify({ success: true }), { status: 200 });
      });

      const { apiCall, invalidateToken } = await import("./gateway-client");
      invalidateToken();
      const result = await apiCall("/api/v1/test");

      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(401);
    });
  });

  // -------------------------------------------------------------------------
  // Timeout / abort handling
  // -------------------------------------------------------------------------
  describe("timeout and abort handling", () => {
    it("returns 408 on AbortError", async () => {
      const abortError = new DOMException("The operation was aborted.", "AbortError");
      fetchSpy.mockRejectedValue(abortError);

      const { apiCallAnon } = await import("./gateway-client");
      const result = await apiCallAnon("/api/v1/test");

      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(408);
      expect(result.messages[0]).toContain("common_errors_CANCELLED");
    });

    it("returns 408 on TimeoutError", async () => {
      const timeoutError = new DOMException("The operation timed out.", "TimeoutError");
      fetchSpy.mockRejectedValue(timeoutError);

      const { apiCallAnon } = await import("./gateway-client");
      const result = await apiCallAnon("/api/v1/test");

      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(408);
      expect(result.messages[0]).toContain("common_errors_REQUEST_FAILED");
    });
  });

  // -------------------------------------------------------------------------
  // D2-Locale header
  // -------------------------------------------------------------------------
  describe("D2-Locale header", () => {
    it("sends D2-Locale header with default locale on anonymous calls", async () => {
      fetchSpy.mockResolvedValue(new Response(JSON.stringify({ success: true }), { status: 200 }));

      const { apiCallAnon } = await import("./gateway-client");
      await apiCallAnon("/api/v1/test");

      const headers = fetchSpy.mock.calls[0][1]!.headers as Headers;
      expect(headers.get("D2-Locale")).toBe("en-US");
    });

    it("sends D2-Locale header with non-default locale", async () => {
      mockGetLocale.mockReturnValue("ja-JP");
      fetchSpy.mockResolvedValue(new Response(JSON.stringify({ success: true }), { status: 200 }));

      const { apiCallAnon } = await import("./gateway-client");
      await apiCallAnon("/api/v1/test");

      const headers = fetchSpy.mock.calls[0][1]!.headers as Headers;
      expect(headers.get("D2-Locale")).toBe("ja-JP");
    });

    it("sends D2-Locale header on authenticated calls", async () => {
      mockGetLocale.mockReturnValue("es-ES");

      fetchSpy.mockImplementation(async (input: string | URL | Request) => {
        const url =
          typeof input === "string" ? input : input instanceof URL ? input.href : input.url;
        if (url === "/api/auth/token") {
          const exp = Math.floor(Date.now() / 1000) + 900;
          const payload = btoa(JSON.stringify({ exp }));
          return new Response(JSON.stringify({ token: `header.${payload}.signature` }), {
            status: 200,
          });
        }
        return new Response(JSON.stringify({ success: true }), { status: 200 });
      });

      const { apiCall, invalidateToken } = await import("./gateway-client");
      invalidateToken();
      await apiCall("/api/v1/test");

      // Second call is the gateway call (first is /api/auth/token)
      const headers = fetchSpy.mock.calls[1][1]!.headers as Headers;
      expect(headers.get("D2-Locale")).toBe("es-ES");
    });
  });

  // -------------------------------------------------------------------------
  // Missing env var
  // -------------------------------------------------------------------------
  describe("missing PUBLIC_GATEWAY_URL", () => {
    it("throws when PUBLIC_GATEWAY_URL is not set", async () => {
      // Temporarily remove the env var from the mock
      const dynamicEnv = await import("$env/dynamic/public");
      const original = dynamicEnv.env.PUBLIC_GATEWAY_URL;
      dynamicEnv.env.PUBLIC_GATEWAY_URL = "";

      fetchSpy.mockResolvedValue(new Response(JSON.stringify({ success: true }), { status: 200 }));

      try {
        const { apiCallAnon } = await import("./gateway-client");
        await expect(apiCallAnon("/api/v1/test")).rejects.toThrow("PUBLIC_GATEWAY_URL");
      } finally {
        // Restore for other tests
        dynamicEnv.env.PUBLIC_GATEWAY_URL = original;
      }
    });
  });
});
