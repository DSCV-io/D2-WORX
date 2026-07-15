// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { TK } from "@d2/i18n-keys";
import { PROBLEM_DETAILS_CONTENT_TYPE } from "@d2/problem-details-abstractions";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  normalizeKeys,
  parseGatewayResponse,
  parseProblemDetailsResponse,
  networkErrorResult,
  executeFetch,
} from "./gateway-response";
import type { HttpStatusCode } from "@d2/result";

// ---------------------------------------------------------------------------
// normalizeKeys
// ---------------------------------------------------------------------------
describe("normalizeKeys", () => {
  it("converts PascalCase keys to camelCase", () => {
    const input = { Success: true, StatusCode: 200, TraceId: "abc" };
    const result = normalizeKeys(input);
    expect(result).toEqual({ success: true, statusCode: 200, traceId: "abc" });
  });

  it("leaves camelCase keys unchanged", () => {
    const input = { success: false, messages: ["error"] };
    const result = normalizeKeys(input);
    expect(result).toEqual({ success: false, messages: ["error"] });
  });

  it("handles multi-char uppercase prefixes (IPAddress → ipAddress)", () => {
    const input = { IPAddress: "1.2.3.4", HTTPStatus: 200 };
    const result = normalizeKeys<Record<string, unknown>>(input);
    expect(result).toHaveProperty("ipAddress", "1.2.3.4");
    expect(result).toHaveProperty("httpStatus", 200);
  });

  it("normalizes nested objects recursively", () => {
    const input = { Data: { CountryCode: "US", CityName: "Denver" } };
    const result = normalizeKeys<{ data: { countryCode: string; cityName: string } }>(input);
    expect(result.data.countryCode).toBe("US");
    expect(result.data.cityName).toBe("Denver");
  });

  it("normalizes arrays of objects", () => {
    const input = [{ FirstName: "Alice" }, { FirstName: "Bob" }];
    const result = normalizeKeys<{ firstName: string }[]>(input);
    expect(result).toEqual([{ firstName: "Alice" }, { firstName: "Bob" }]);
  });

  it("passes through primitives unchanged", () => {
    expect(normalizeKeys(42)).toBe(42);
    expect(normalizeKeys("hello")).toBe("hello");
    expect(normalizeKeys(true)).toBe(true);
    expect(normalizeKeys(null)).toBeNull();
    expect(normalizeKeys(undefined)).toBeUndefined();
  });

  it("handles empty objects and arrays", () => {
    expect(normalizeKeys({})).toEqual({});
    expect(normalizeKeys([])).toEqual([]);
  });

  it("handles single-char uppercase key (A → a)", () => {
    const input = { A: 1 };
    const result = normalizeKeys<Record<string, unknown>>(input);
    expect(result).toEqual({ a: 1 });
  });

  it("does not modify keys that are already lowercase", () => {
    const input = { already_lower: 1, another: 2 };
    const result = normalizeKeys(input);
    expect(result).toEqual({ already_lower: 1, another: 2 });
  });

  it("handles all-caps key (ID → id)", () => {
    const input = { ID: "abc" };
    const result = normalizeKeys<Record<string, unknown>>(input);
    expect(result).toEqual({ id: "abc" });
  });
});

// ---------------------------------------------------------------------------
// parseGatewayResponse — PascalCase endpoint responses
// ---------------------------------------------------------------------------
describe("parseGatewayResponse — PascalCase responses", () => {
  it("parses a successful PascalCase response", async () => {
    const body = {
      Success: true,
      Data: { countries: ["US", "CA"] },
      Messages: [],
      InputErrors: [],
      ErrorCode: null,
      TraceId: "trace-123",
      StatusCode: 200,
    };

    const response = new Response(JSON.stringify(body), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    });

    const result = await parseGatewayResponse<{ countries: string[] }>(response);

    expect(result.success).toBe(true);
    expect(result.data?.countries).toEqual(["US", "CA"]);
    expect(result.traceId).toBe("trace-123");
    expect(result.statusCode).toBe(200);
  });

  it("parses a PascalCase failure response", async () => {
    const body = {
      Success: false,
      // .NET emits TKMessage objects via TKMessageJsonConverter: {Key, Params?}
      // → camelCase-normalized to {key, params?} on the TS side. Wire-shape
      // parity is pinned by contracts/tk-message/tk-message.spec.json.
      Messages: [{ Key: "common_errors_NOT_FOUND" }],
      InputErrors: [],
      ErrorCode: "NOT_FOUND",
      TraceId: "trace-456",
      StatusCode: 404,
    };

    const response = new Response(JSON.stringify(body), {
      status: 404,
      headers: { "Content-Type": "application/json" },
    });

    const result = await parseGatewayResponse(response);

    expect(result.success).toBe(false);
    expect(result.messages).toEqual([{ key: TK.common.errors.NOT_FOUND }]);
    expect(result.errorCode).toBe("NOT_FOUND");
    expect(result.statusCode).toBe(404);
  });
});

// ---------------------------------------------------------------------------
// parseGatewayResponse — camelCase middleware responses
// ---------------------------------------------------------------------------
describe("parseGatewayResponse — camelCase responses", () => {
  it("parses a camelCase middleware error", async () => {
    const body = {
      success: false,
      messages: [{ key: "common_errors_TOO_MANY_REQUESTS" }],
      inputErrors: [],
      errorCode: "RATE_LIMITED",
      traceId: "trace-789",
      statusCode: 429,
    };

    const response = new Response(JSON.stringify(body), {
      status: 429,
      headers: { "Content-Type": "application/json" },
    });

    const result = await parseGatewayResponse(response);

    expect(result.success).toBe(false);
    expect(result.messages).toEqual([{ key: TK.common.errors.TOO_MANY_REQUESTS }]);
    expect(result.errorCode).toBe("RATE_LIMITED");
    expect(result.statusCode).toBe(429);
  });

  it("parses a camelCase unauthorized response", async () => {
    const body = {
      success: false,
      messages: [{ key: "auth_errors_INVALID_OR_EXPIRED_JWT" }],
      inputErrors: [],
      errorCode: "UNAUTHORIZED",
      traceId: "trace-auth",
    };

    const response = new Response(JSON.stringify(body), {
      status: 401,
      headers: { "Content-Type": "application/json" },
    });

    const result = await parseGatewayResponse(response);

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(401);
    expect(result.errorCode).toBe("UNAUTHORIZED");
  });
});

// ---------------------------------------------------------------------------
// parseGatewayResponse — uses HTTP status, not body statusCode
// ---------------------------------------------------------------------------
describe("parseGatewayResponse — HTTP status authority", () => {
  it("uses HTTP status code, not body statusCode", async () => {
    // Middleware sends statusCode as string in body, endpoint sends as int.
    // We ignore body.statusCode entirely and use response.status.
    const body = {
      Success: false,
      Messages: ["Something went wrong"],
      StatusCode: "BadRequest", // string in body — ignored
    };

    const response = new Response(JSON.stringify(body), {
      status: 400,
      headers: { "Content-Type": "application/json" },
    });

    const result = await parseGatewayResponse(response);
    expect(result.statusCode).toBe(400);
  });
});

// ---------------------------------------------------------------------------
// parseGatewayResponse — edge cases
// ---------------------------------------------------------------------------
describe("parseGatewayResponse — edge cases", () => {
  it("handles empty body with 204 No Content", async () => {
    const response = new Response(null, { status: 204 });
    const result = await parseGatewayResponse(response);

    expect(result.success).toBe(true);
    expect(result.statusCode).toBe(204);
  });

  it("handles non-JSON response body", async () => {
    const response = new Response("Internal Server Error", { status: 500 });
    const result = await parseGatewayResponse(response);

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(500);
    // Non-JSON bodies wrap in a synthetic REQUEST_FAILED TKMessage rather
    // than smuggling the raw text into the i18n envelope — see §11.30 wire
    // shape spec at contracts/tk-message.
    expect(result.messages).toEqual([{ key: TK.common.errors.REQUEST_FAILED }]);
  });

  it("handles whitespace-only body as empty", async () => {
    const response = new Response("   \n  ", { status: 200 });
    const result = await parseGatewayResponse(response);

    expect(result.success).toBe(true);
    expect(result.statusCode).toBe(200);
  });

  it("handles response with success=true based on body when present", async () => {
    // Even with 200 status, if body says success=false, body wins
    const body = { success: false, messages: ["Something failed"] };
    const response = new Response(JSON.stringify(body), { status: 200 });
    const result = await parseGatewayResponse(response);

    expect(result.success).toBe(false);
  });

  it("falls back to response.ok when body has no success field", async () => {
    const body = { data: { value: 42 } };
    const response = new Response(JSON.stringify(body), { status: 200 });
    const result = await parseGatewayResponse(response);

    expect(result.success).toBe(true);
  });

  it("parses inputErrors with field name and TKMessage[] errors (wire-shape object)", async () => {
    const body = {
      Success: false,
      // .NET emits InputError as {Field, Errors: TKMessage[]} per the
      // contracts/input-error spec — NOT a tuple. The TS-side parser
      // camelCase-normalizes Field → field and Errors → errors.
      InputErrors: [
        {
          Field: "email",
          Errors: [
            { Key: "common_validation_EMAIL_REQUIRED" },
            { Key: "common_validation_EMAIL_INVALID" },
          ],
        },
      ],
      Messages: [{ Key: "common_errors_VALIDATION_FAILED" }],
    };

    const response = new Response(JSON.stringify(body), { status: 400 });
    const result = await parseGatewayResponse(response);

    expect(result.inputErrors).toEqual([
      {
        field: "email",
        errors: [
          { key: "common_validation_EMAIL_REQUIRED" },
          { key: TK.common.validation.EMAIL_INVALID },
        ],
      },
    ]);
  });

  // -------------------------------------------------------------------
  // Regression test pinning the `messages` field as TKMessage[]. The
  // .NET gateway emits TKMessage objects via TKMessageJsonConverter, so
  // the TS-side type must declare `messages?: TKMessage[]` — consumers
  // handling the field as `string[]` (e.g. passing to toast
  // notifications) would crash on the runtime TKMessage objects, which
  // are `{key, args?}` records. This test pins the TKMessage[]
  // declaration against any future drift: the parsed value is always an
  // object with a `key` property, never a bare string.
  // -------------------------------------------------------------------
  it("regression: messages field is TKMessage[], not string[]", async () => {
    const body = {
      success: false,
      messages: [{ key: "auth_errors_BEARER_MISSING" }],
      errorCode: "AUTH_BEARER_MISSING",
    };
    const response = new Response(JSON.stringify(body), { status: 401 });
    const result = await parseGatewayResponse(response);

    expect(result.messages).toHaveLength(1);
    // The wire-shape pin — value at index 0 is an object with a `key`
    // property, NOT a bare string.
    const first = result.messages[0]!;
    expect(typeof first).toBe("object");
    expect(first).toHaveProperty("key", "auth_errors_BEARER_MISSING");
  });
});

// ---------------------------------------------------------------------------
// networkErrorResult
// ---------------------------------------------------------------------------
describe("networkErrorResult", () => {
  // Per §11.30 wire-shape spec rollout: the `messages` field is reserved
  // for translation-key envelopes (TKMessage[]). Raw exception text gets
  // surfaced via the trace id / log pipeline rather than smuggled into
  // the i18n field — the runtime never sees ad-hoc English strings on
  // the wire path. networkErrorResult always emits REQUEST_FAILED
  // regardless of the underlying error.
  it("creates an unhandled exception result with REQUEST_FAILED TKMessage from Error", () => {
    const error = new Error("fetch failed: ECONNREFUSED");
    const result = networkErrorResult(error);

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(500);
    expect(result.messages).toEqual([{ key: TK.common.errors.REQUEST_FAILED }]);
  });

  it("creates a REQUEST_FAILED TKMessage for non-Error string values", () => {
    const result = networkErrorResult("something went wrong");

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(500);
    expect(result.messages).toEqual([{ key: TK.common.errors.REQUEST_FAILED }]);
  });

  it("creates a REQUEST_FAILED TKMessage for null", () => {
    const result = networkErrorResult(null);

    expect(result.success).toBe(false);
    expect(result.messages).toEqual([{ key: TK.common.errors.REQUEST_FAILED }]);
  });
});

// ---------------------------------------------------------------------------
// parseProblemDetailsResponse — RFC 7807 problem+json parse-back
// ---------------------------------------------------------------------------
//
// Mirrors the .NET producer Shape-A body (auth-middleware path A
// `D2ProblemDetailsExtensions.ToProblemDetails` + ASP.NET Core path B
// `D2ProblemDetailsCustomizer`): the D2Result fields ride the d2_-namespaced
// extension keys + `traceId`, alongside the RFC-7807 `type` / `title` /
// `status` / `instance`. The .NET TKMessageJsonConverter emits lowercase
// `key` / `params`, so `d2_messages` arrives as `{key, params?}` objects.
// ---------------------------------------------------------------------------
describe("parseProblemDetailsResponse", () => {
  const status401 = 401 as HttpStatusCode;

  it("re-materializes a faithful D2Result from a full problem+json body", () => {
    const body = JSON.stringify({
      type: "https://problems.d2.dcsv.io/auth-bearer-missing",
      title: "Unauthorized",
      status: 401,
      instance: "GET /api/files/abc",
      d2_error_code: "AUTH_BEARER_MISSING",
      d2_messages: [{ key: "auth_errors_UNAUTHORIZED" }],
      d2_category: "validation_failure",
      traceId: "trace-pd-1",
    });

    const result = parseProblemDetailsResponse(body, status401);

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(401);
    expect(result.errorCode).toBe("AUTH_BEARER_MISSING");
    expect(result.messages).toEqual([{ key: TK.auth.errors.UNAUTHORIZED }]);
    expect(result.category).toBe("validation_failure");
    expect(result.traceId).toBe("trace-pd-1");
  });

  it("re-materializes d2_messages with params verbatim", () => {
    const body = JSON.stringify({
      status: 429,
      d2_error_code: "RATE_LIMITED",
      d2_messages: [{ key: "common_errors_TOO_MANY_REQUESTS", params: { retryAfter: "30" } }],
      d2_category: "rate_limited",
    });

    const result = parseProblemDetailsResponse(body, 429 as HttpStatusCode);

    expect(result.messages).toEqual([
      { key: TK.common.errors.TOO_MANY_REQUESTS, params: { retryAfter: "30" } },
    ]);
    expect(result.category).toBe("rate_limited");
  });

  it("re-materializes d2_input_errors into inputErrors", () => {
    const body = JSON.stringify({
      status: 400,
      d2_error_code: "VALIDATION_FAILED",
      d2_messages: [{ key: "common_errors_VALIDATION_FAILED" }],
      d2_input_errors: [
        {
          field: "email",
          errors: [{ key: "common_validation_EMAIL_INVALID" }],
        },
      ],
      d2_category: "validation_failure",
    });

    const result = parseProblemDetailsResponse(body, 400 as HttpStatusCode);

    expect(result.inputErrors).toEqual([
      { field: "email", errors: [{ key: TK.common.validation.EMAIL_INVALID }] },
    ]);
  });

  it("safe-parses an unknown d2_category to undefined (no throw)", () => {
    const body = JSON.stringify({
      status: 401,
      d2_error_code: "AUTH_BEARER_MISSING",
      d2_messages: [{ key: "auth_errors_UNAUTHORIZED" }],
      d2_category: "totally_made_up_category",
    });

    const result = parseProblemDetailsResponse(body, status401);

    expect(result.category).toBeUndefined();
    expect(result.errorCode).toBe("AUTH_BEARER_MISSING");
  });

  it("yields undefined category when d2_category is absent", () => {
    const body = JSON.stringify({
      status: 401,
      d2_error_code: "AUTH_BEARER_MISSING",
      d2_messages: [{ key: "auth_errors_UNAUTHORIZED" }],
    });

    const result = parseProblemDetailsResponse(body, status401);

    expect(result.category).toBeUndefined();
  });

  it("handles a body with NO d2_* extensions gracefully (raw-exception path B)", () => {
    // The path-B raw-exception emit writes only type/title/status (+ traceId/
    // correlationId) — no d2_* extensions. Parse-back must not throw and must
    // not invent fields.
    const body = JSON.stringify({
      type: "about:blank",
      title: "Service Unavailable",
      status: 503,
      traceId: "trace-pd-bare",
    });

    const result = parseProblemDetailsResponse(body, 503 as HttpStatusCode);

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(503);
    expect(result.errorCode).toBeUndefined();
    expect(result.messages).toEqual([]);
    expect(result.inputErrors).toEqual([]);
    expect(result.category).toBeUndefined();
    expect(result.traceId).toBe("trace-pd-bare");
  });

  it("NEVER smuggles raw title / detail into messages", () => {
    // A populated title + detail but no d2_messages → messages stays []. The
    // operator-English strings must never reach the user-facing render path.
    const body = JSON.stringify({
      type: "https://problems.d2.dcsv.io/auth-jwt-expired",
      title: "Unauthorized",
      detail: "The JWT 'exp' claim is in the past (expired 2026-01-01T00:00:00Z).",
      status: 401,
      d2_error_code: "AUTH_JWT_EXPIRED",
    });

    const result = parseProblemDetailsResponse(body, status401);

    expect(result.messages).toEqual([]);
    const serialized = JSON.stringify(result.messages);
    expect(serialized).not.toContain("Unauthorized");
    expect(serialized).not.toContain("exp");
  });

  it("returns a defined REQUEST_FAILED fail for a malformed (non-JSON) body", () => {
    const result = parseProblemDetailsResponse("{not json", status401);

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(401);
    expect(result.messages).toEqual([{ key: TK.common.errors.REQUEST_FAILED }]);
  });

  it("returns REQUEST_FAILED for a valid-JSON-but-non-object body", () => {
    const result = parseProblemDetailsResponse('"just a string"', status401);

    expect(result.success).toBe(false);
    expect(result.messages).toEqual([{ key: TK.common.errors.REQUEST_FAILED }]);
  });

  it("returns REQUEST_FAILED for a JSON array body", () => {
    const result = parseProblemDetailsResponse("[1,2,3]", status401);

    expect(result.success).toBe(false);
    expect(result.messages).toEqual([{ key: TK.common.errors.REQUEST_FAILED }]);
  });
});

// ---------------------------------------------------------------------------
// parseGatewayResponse — content-type discrimination (problem+json routing)
// ---------------------------------------------------------------------------
describe("parseGatewayResponse — content-type discrimination", () => {
  it("routes an application/problem+json response to the parse-back", async () => {
    const body = {
      type: "https://problems.d2.dcsv.io/auth-bearer-missing",
      title: "Unauthorized",
      status: 401,
      d2_error_code: "AUTH_BEARER_MISSING",
      d2_messages: [{ key: "auth_errors_UNAUTHORIZED" }],
      d2_category: "validation_failure",
      traceId: "trace-route-1",
    };
    const response = new Response(JSON.stringify(body), {
      status: 401,
      headers: { "Content-Type": PROBLEM_DETAILS_CONTENT_TYPE },
    });

    const result = await parseGatewayResponse(response);

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe("AUTH_BEARER_MISSING");
    // The envelope path would have dropped d2_error_code entirely — proves
    // the parse-back branch ran.
    expect(result.messages).toEqual([{ key: TK.auth.errors.UNAUTHORIZED }]);
    expect(result.category).toBe("validation_failure");
    expect(result.statusCode).toBe(401);
  });

  it("tolerates a charset suffix on the problem+json content-type", async () => {
    const body = {
      status: 403,
      d2_error_code: "AUTH_SCOPE_INSUFFICIENT",
      d2_messages: [{ key: "common_errors_FORBIDDEN" }],
      d2_category: "policy_denied",
    };
    const response = new Response(JSON.stringify(body), {
      status: 403,
      headers: { "Content-Type": `${PROBLEM_DETAILS_CONTENT_TYPE}; charset=utf-8` },
    });

    const result = await parseGatewayResponse(response);

    expect(result.errorCode).toBe("AUTH_SCOPE_INSUFFICIENT");
    expect(result.messages).toEqual([{ key: TK.common.errors.FORBIDDEN }]);
    expect(result.category).toBe("policy_denied");
  });

  it("still routes a plain application/json envelope through the envelope path", async () => {
    // A normal Shape-B envelope (camelCase field names, NOT d2_-namespaced)
    // must keep parsing via the existing envelope path.
    const body = {
      success: false,
      messages: [{ key: "common_errors_NOT_FOUND" }],
      errorCode: "NOT_FOUND",
      traceId: "trace-env-1",
    };
    const response = new Response(JSON.stringify(body), {
      status: 404,
      headers: { "Content-Type": "application/json" },
    });

    const result = await parseGatewayResponse(response);

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe("NOT_FOUND");
    expect(result.messages).toEqual([{ key: TK.common.errors.NOT_FOUND }]);
    expect(result.statusCode).toBe(404);
  });

  it("does not corrupt d2_-namespaced keys via normalizeKeys on the parse-back", async () => {
    // Regression: if a problem+json body fell into the envelope path,
    // normalizeKeys() would mangle d2_error_code → d2ErrorCode and the
    // extension would be lost. Prove the parse-back reads it verbatim.
    const body = {
      status: 401,
      d2_error_code: "AUTH_JWT_EXPIRED",
      d2_messages: [{ key: "auth_errors_UNAUTHORIZED" }],
    };
    const response = new Response(JSON.stringify(body), {
      status: 401,
      headers: { "Content-Type": PROBLEM_DETAILS_CONTENT_TYPE },
    });

    const result = await parseGatewayResponse(response);

    expect(result.errorCode).toBe("AUTH_JWT_EXPIRED");
  });
});

// ---------------------------------------------------------------------------
// executeFetch — error catch-branches
// ---------------------------------------------------------------------------
describe("executeFetch — error catch-branches", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("AbortError → 408 result with CANCELED TKMessage", async () => {
    vi.mocked(fetch).mockRejectedValueOnce(new DOMException("", "AbortError"));

    const result = await executeFetch("https://example.test/api", {
      headers: new Headers(),
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(408);
    expect(result.messages).toEqual([{ key: TK.common.errors.CANCELED }]);
  });

  it("TimeoutError → 408 result with REQUEST_FAILED TKMessage", async () => {
    vi.mocked(fetch).mockRejectedValueOnce(new DOMException("", "TimeoutError"));

    const result = await executeFetch("https://example.test/api", {
      headers: new Headers(),
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(408);
    expect(result.messages).toEqual([{ key: TK.common.errors.REQUEST_FAILED }]);
  });

  it("generic network error → unhandled-exception result (500) with REQUEST_FAILED", async () => {
    vi.mocked(fetch).mockRejectedValueOnce(new TypeError("Failed to fetch"));

    const result = await executeFetch("https://example.test/api", {
      headers: new Headers(),
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(500);
    expect(result.messages).toEqual([{ key: TK.common.errors.REQUEST_FAILED }]);
  });
});

// ---------------------------------------------------------------------------
// parseProblemDetailsResponse — non-string d2_category guard
// ---------------------------------------------------------------------------
describe("parseProblemDetailsResponse — d2_category type guard", () => {
  it("yields undefined category when d2_category is a number (42)", () => {
    const body = JSON.stringify({
      status: 401,
      d2_error_code: "AUTH_BEARER_MISSING",
      d2_category: 42,
    });

    const result = parseProblemDetailsResponse(body, 401 as HttpStatusCode);

    expect(result.category).toBeUndefined();
  });

  it("yields undefined category when d2_category is null", () => {
    const body = JSON.stringify({
      status: 401,
      d2_error_code: "AUTH_BEARER_MISSING",
      d2_category: null,
    });

    const result = parseProblemDetailsResponse(body, 401 as HttpStatusCode);

    expect(result.category).toBeUndefined();
  });
});
