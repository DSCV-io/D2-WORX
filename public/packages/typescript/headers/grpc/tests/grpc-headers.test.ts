// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { ALL_GRPC_HEADERS, GrpcHeaders } from "../src/grpc-headers.g.js";

describe("GrpcHeaders — per-VALUE pin (mirrors .NET GrpcHeaders)", () => {
  it.each([
    ["AUTHORIZATION", "Authorization"],
    ["PROPAGATED_CONTEXT", "x-d2-context"],
    ["TRACEPARENT", "traceparent"],
    ["TRACESTATE", "tracestate"],
  ])("GrpcHeaders.%s = %s", (key, value) => {
    expect(GrpcHeaders[key as keyof typeof GrpcHeaders]).toBe(value);
  });

  it("ALL_GRPC_HEADERS contains every wire value sorted by constName", () => {
    expect([...ALL_GRPC_HEADERS]).toEqual([
      "Authorization",
      "x-d2-context",
      "traceparent",
      "tracestate",
    ]);
  });

  it("GrpcHeaders has 4 entries (1 HTTP+gRPC + 3 cross-transport)", () => {
    expect(Object.keys(GrpcHeaders)).toHaveLength(4);
  });
});
