// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, it, expect } from "vitest";
import { normalizeKeys, parseGatewayResponse, networkErrorResult } from "./gateway-response";

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
    expect(result.messages).toEqual([{ key: "common_errors_NOT_FOUND" }]);
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
    expect(result.messages).toEqual([{ key: "common_errors_TOO_MANY_REQUESTS" }]);
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
    expect(result.messages).toEqual([{ key: "common_errors_REQUEST_FAILED" }]);
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
          { key: "common_validation_EMAIL_INVALID" },
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
    expect(result.messages).toEqual([{ key: "common_errors_REQUEST_FAILED" }]);
  });

  it("creates a REQUEST_FAILED TKMessage for non-Error string values", () => {
    const result = networkErrorResult("something went wrong");

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(500);
    expect(result.messages).toEqual([{ key: "common_errors_REQUEST_FAILED" }]);
  });

  it("creates a REQUEST_FAILED TKMessage for null", () => {
    const result = networkErrorResult(null);

    expect(result.success).toBe(false);
    expect(result.messages).toEqual([{ key: "common_errors_REQUEST_FAILED" }]);
  });
});
