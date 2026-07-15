// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { ALL_HTTP_HEADERS, HttpHeaders } from "../src/http-headers.g.js";

describe("HttpHeaders — per-VALUE pin (mirrors .NET HttpHeaders)", () => {
  it.each([
    ["ACCEPT_LANGUAGE", "Accept-Language"],
    ["AUTHORIZATION", "Authorization"],
    ["CLIENT_FINGERPRINT", "X-D2-Client-Fingerprint"],
    ["CORRELATION_ID", "X-Correlation-Id"],
    ["D2_CURRENCY", "X-D2-Currency"],
    ["D2_LOCALE", "X-D2-Locale"],
    ["D2_TIMEZONE", "X-D2-Timezone"],
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
      "Accept-Language",
      "Authorization",
      "X-D2-Client-Fingerprint",
      "X-Correlation-Id",
      "X-D2-Currency",
      "X-D2-Locale",
      "X-D2-Timezone",
      "Idempotency-Key",
      "X-D2-Internal-Token",
      "x-d2-context",
      "traceparent",
      "tracestate",
    ]);
  });

  it("HttpHeaders has 12 entries (9 HTTP-only + 3 cross-transport)", () => {
    expect(Object.keys(HttpHeaders)).toHaveLength(12);
  });
});
