// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { ALL_HTTP_HEADERS, HttpHeaders } from "../src/http-headers.g.js";

describe("HttpHeaders — per-VALUE pin (mirrors .NET HttpHeaders)", () => {
  it.each([
    ["AUTHORIZATION", "Authorization"],
    ["CLIENT_FINGERPRINT", "X-D2-Client-Fingerprint"],
    ["IDEMPOTENCY_KEY", "Idempotency-Key"],
    ["INTERNAL_TOKEN", "X-D2-Internal-Token"],
    ["PROPAGATED_CONTEXT", "x-d2-context"],
    ["TRACEPARENT", "traceparent"],
    ["TRACESTATE", "tracestate"],
  ])("HttpHeaders.%s = %s", (key, value) => {
    expect(HttpHeaders[key as keyof typeof HttpHeaders]).toBe(value);
  });

  it("ALL_HTTP_HEADERS contains every wire value sorted by constName", () => {
    expect([...ALL_HTTP_HEADERS]).toEqual([
      "Authorization",
      "X-D2-Client-Fingerprint",
      "Idempotency-Key",
      "X-D2-Internal-Token",
      "x-d2-context",
      "traceparent",
      "tracestate",
    ]);
  });

  it("HttpHeaders has 7 entries (4 HTTP-only + 3 cross-transport)", () => {
    expect(Object.keys(HttpHeaders)).toHaveLength(7);
  });
});
