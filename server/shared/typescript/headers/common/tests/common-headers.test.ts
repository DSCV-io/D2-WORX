// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { ALL_COMMON_HEADERS, CommonHeaders } from "../src/common-headers.g.js";

describe("CommonHeaders — per-VALUE pin (mirrors .NET CommonHeaders)", () => {
  it.each([
    ["AUTHORIZATION", "Authorization"],
    ["PROPAGATED_CONTEXT", "x-d2-context"],
    ["TRACEPARENT", "traceparent"],
    ["TRACESTATE", "tracestate"],
  ])("CommonHeaders.%s = %s", (key, value) => {
    expect(CommonHeaders[key as keyof typeof CommonHeaders]).toBe(value);
  });

  it("ALL_COMMON_HEADERS contains every wire value sorted by constName", () => {
    expect([...ALL_COMMON_HEADERS]).toEqual([
      "Authorization",
      "x-d2-context",
      "traceparent",
      "tracestate",
    ]);
  });

  it("CommonHeaders contains exactly the cross-transport spec subset", () => {
    // Authorization is cross-transport (http + grpc); the other three are
    // applicable to all 3 transports. All four have applicability count >= 2
    // and therefore appear in CommonHeaders.
    expect(Object.keys(CommonHeaders)).toHaveLength(4);
  });
});
