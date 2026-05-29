// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { ALL_AMQP_HEADERS, AmqpHeaders } from "../src/amqp-headers.g.js";

describe("AmqpHeaders — per-VALUE pin (mirrors .NET AmqpHeaders)", () => {
  it.each([
    ["CONTENT_TYPE", "content-type"],
    ["ENCRYPTION_KID", "x-d2-encryption-kid"],
    ["FAILURE_REASON", "x-d2-failure-reason"],
    ["MESSAGE_ID", "message-id"],
    ["PROPAGATED_CONTEXT", "x-d2-context"],
    ["PROTO_TYPE", "x-proto-type"],
    ["TIMESTAMP", "timestamp"],
    ["TRACEPARENT", "traceparent"],
    ["TRACESTATE", "tracestate"],
  ])("AmqpHeaders.%s = %s", (key, value) => {
    expect(AmqpHeaders[key as keyof typeof AmqpHeaders]).toBe(value);
  });

  it("ALL_AMQP_HEADERS contains every wire value sorted by constName", () => {
    expect([...ALL_AMQP_HEADERS]).toEqual([
      "content-type",
      "x-d2-encryption-kid",
      "x-d2-failure-reason",
      "message-id",
      "x-d2-context",
      "x-proto-type",
      "timestamp",
      "traceparent",
      "tracestate",
    ]);
  });

  it("AmqpHeaders has 9 entries (6 AMQP-only + 3 cross-transport)", () => {
    expect(Object.keys(AmqpHeaders)).toHaveLength(9);
  });
});
