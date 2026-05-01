import { vi, describe, it, expect, beforeEach } from "vitest";
import { D2Result } from "@d2/result";

vi.mock("$lib/shared/rest/gateway-response.js", () => ({
  executeFetch: vi.fn(),
}));

import { authApiCall } from "./auth-gateway-client.js";
import { executeFetch } from "$lib/shared/rest/gateway-response.js";

const mockExecuteFetch = vi.mocked(executeFetch);

describe("authApiCall", () => {
  beforeEach(() => {
    mockExecuteFetch.mockReset();
    mockExecuteFetch.mockResolvedValue(new D2Result({ success: true, statusCode: 200 }));
  });

  // ---------------------------------------------------------------------------
  // GET request defaults
  // ---------------------------------------------------------------------------
  describe("GET request defaults", () => {
    it("passes the path as the first argument", async () => {
      await authApiCall("/api/auth/check-email?email=test@example.com");

      expect(mockExecuteFetch).toHaveBeenCalledOnce();
      expect(mockExecuteFetch.mock.calls[0][0]).toBe(
        "/api/auth/check-email?email=test@example.com",
      );
    });

    it("passes undefined method when no method is specified", async () => {
      await authApiCall("/api/auth/test");

      const options = mockExecuteFetch.mock.calls[0][1];
      expect(options.method).toBeUndefined();
    });

    it("passes empty headers when no body is provided", async () => {
      await authApiCall("/api/auth/test");

      const options = mockExecuteFetch.mock.calls[0][1];
      const headers = options.headers as Headers;
      expect(headers).toBeInstanceOf(Headers);
      expect([...headers.entries()]).toHaveLength(0);
    });

    it("passes undefined body when no body is provided", async () => {
      await authApiCall("/api/auth/test");

      const options = mockExecuteFetch.mock.calls[0][1];
      expect(options.body).toBeUndefined();
    });

    it('sets credentials to "include"', async () => {
      await authApiCall("/api/auth/test");

      const options = mockExecuteFetch.mock.calls[0][1];
      expect(options.credentials).toBe("include");
    });

    it("uses the default timeout of 10000ms when none is specified", async () => {
      await authApiCall("/api/auth/test");

      const options = mockExecuteFetch.mock.calls[0][1];
      expect(options.timeout).toBe(10_000);
    });
  });

  // ---------------------------------------------------------------------------
  // POST with body
  // ---------------------------------------------------------------------------
  describe("POST with body", () => {
    it("sets Content-Type header to application/json when body is provided", async () => {
      await authApiCall("/api/auth/test", {
        method: "POST",
        body: { email: "user@example.com" },
      });

      const options = mockExecuteFetch.mock.calls[0][1];
      const headers = options.headers as Headers;
      expect(headers.get("Content-Type")).toBe("application/json");
    });

    it("JSON-stringifies the body", async () => {
      const payload = { email: "user@example.com", name: "Test" };

      await authApiCall("/api/auth/test", {
        method: "POST",
        body: payload,
      });

      const options = mockExecuteFetch.mock.calls[0][1];
      expect(options.body).toBe(JSON.stringify(payload));
    });

    it("passes the method through to executeFetch", async () => {
      await authApiCall("/api/auth/test", { method: "POST" });

      const options = mockExecuteFetch.mock.calls[0][1];
      expect(options.method).toBe("POST");
    });

    it("passes GET method through when explicitly set", async () => {
      await authApiCall("/api/auth/test", { method: "GET" });

      const options = mockExecuteFetch.mock.calls[0][1];
      expect(options.method).toBe("GET");
    });

    it("handles falsy body values correctly (null is still a body)", async () => {
      await authApiCall("/api/auth/test", { method: "POST", body: null });

      const options = mockExecuteFetch.mock.calls[0][1];
      // null is not undefined, so Content-Type should be set and body should be stringified
      const headers = options.headers as Headers;
      expect(headers.get("Content-Type")).toBe("application/json");
      expect(options.body).toBe("null");
    });

    it("handles empty string body correctly", async () => {
      await authApiCall("/api/auth/test", { method: "POST", body: "" });

      const options = mockExecuteFetch.mock.calls[0][1];
      // empty string is not undefined, so Content-Type should be set
      const headers = options.headers as Headers;
      expect(headers.get("Content-Type")).toBe("application/json");
      expect(options.body).toBe('""');
    });

    it("handles boolean false body correctly", async () => {
      await authApiCall("/api/auth/test", { method: "POST", body: false });

      const options = mockExecuteFetch.mock.calls[0][1];
      const headers = options.headers as Headers;
      expect(headers.get("Content-Type")).toBe("application/json");
      expect(options.body).toBe("false");
    });

    it("does not set Content-Type when body is explicitly undefined", async () => {
      await authApiCall("/api/auth/test", { method: "POST", body: undefined });

      const options = mockExecuteFetch.mock.calls[0][1];
      const headers = options.headers as Headers;
      expect(headers.has("Content-Type")).toBe(false);
      expect(options.body).toBeUndefined();
    });
  });

  // ---------------------------------------------------------------------------
  // Custom timeout
  // ---------------------------------------------------------------------------
  describe("custom timeout", () => {
    it("passes custom timeout through to executeFetch", async () => {
      await authApiCall("/api/auth/test", { timeout: 5_000 });

      const options = mockExecuteFetch.mock.calls[0][1];
      expect(options.timeout).toBe(5_000);
    });

    it("passes zero timeout through to executeFetch", async () => {
      await authApiCall("/api/auth/test", { timeout: 0 });

      const options = mockExecuteFetch.mock.calls[0][1];
      // 0 is not nullish, so ?? does NOT trigger — 0 passes through
      expect(options.timeout).toBe(0);
    });

    it("passes large timeout through to executeFetch", async () => {
      await authApiCall("/api/auth/test", { timeout: 60_000 });

      const options = mockExecuteFetch.mock.calls[0][1];
      expect(options.timeout).toBe(60_000);
    });
  });

  // ---------------------------------------------------------------------------
  // AbortSignal pass-through
  // ---------------------------------------------------------------------------
  describe("AbortSignal", () => {
    it("passes AbortSignal through to executeFetch", async () => {
      const controller = new AbortController();

      await authApiCall("/api/auth/test", { signal: controller.signal });

      const options = mockExecuteFetch.mock.calls[0][1];
      expect(options.signal).toBe(controller.signal);
    });

    it("passes undefined signal when none is provided", async () => {
      await authApiCall("/api/auth/test");

      const options = mockExecuteFetch.mock.calls[0][1];
      expect(options.signal).toBeUndefined();
    });
  });

  // ---------------------------------------------------------------------------
  // Return value
  // ---------------------------------------------------------------------------
  describe("return value", () => {
    it("returns the D2Result from executeFetch on success", async () => {
      const expected = D2Result.ok({ data: { id: "abc-123" } });
      mockExecuteFetch.mockResolvedValue(expected);

      const result = await authApiCall<{ id: string }>("/api/auth/test");

      expect(result).toBe(expected);
      expect(result.success).toBe(true);
      expect(result.data?.id).toBe("abc-123");
    });

    it("returns the D2Result from executeFetch on failure", async () => {
      const expected = D2Result.fail({ messages: ["Not found"], statusCode: 404 });
      mockExecuteFetch.mockResolvedValue(expected);

      const result = await authApiCall("/api/auth/test");

      expect(result).toBe(expected);
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(404);
      expect(result.messages[0]).toBe("Not found");
    });

    it("propagates rejections from executeFetch", async () => {
      mockExecuteFetch.mockRejectedValue(new Error("Unexpected error"));

      await expect(authApiCall("/api/auth/test")).rejects.toThrow("Unexpected error");
    });
  });

  // ---------------------------------------------------------------------------
  // Combined options
  // ---------------------------------------------------------------------------
  describe("combined options", () => {
    it("passes all options together correctly", async () => {
      const controller = new AbortController();
      const body = { email: "user@test.com" };

      await authApiCall("/api/auth/check-email", {
        method: "POST",
        body,
        signal: controller.signal,
        timeout: 3_000,
      });

      expect(mockExecuteFetch).toHaveBeenCalledOnce();
      const [url, options] = mockExecuteFetch.mock.calls[0];

      expect(url).toBe("/api/auth/check-email");
      expect(options.method).toBe("POST");
      expect(options.body).toBe(JSON.stringify(body));
      expect(options.signal).toBe(controller.signal);
      expect(options.timeout).toBe(3_000);
      expect(options.credentials).toBe("include");

      const headers = options.headers as Headers;
      expect(headers.get("Content-Type")).toBe("application/json");
    });

    it("calls executeFetch with no options argument provided", async () => {
      await authApiCall("/api/auth/status");

      expect(mockExecuteFetch).toHaveBeenCalledOnce();
      const [url, options] = mockExecuteFetch.mock.calls[0];

      expect(url).toBe("/api/auth/status");
      expect(options.method).toBeUndefined();
      expect(options.body).toBeUndefined();
      expect(options.signal).toBeUndefined();
      expect(options.timeout).toBe(10_000);
      expect(options.credentials).toBe("include");

      const headers = options.headers as Headers;
      expect([...headers.entries()]).toHaveLength(0);
    });
  });
});
