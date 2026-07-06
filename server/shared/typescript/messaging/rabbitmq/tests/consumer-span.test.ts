// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  startConsumerSpan,
  validTraceId,
} from "../src/subscribing/consumer-span.js";
import {
  makeMessage,
  SAMPLE_PRODUCER_TRACE_ID,
  SAMPLE_TRACEPARENT,
} from "./helpers.js";

describe("startConsumerSpan + validTraceId", () => {
  it("parents the span to the producer traceparent (cross-runtime linkage)", () => {
    const msg = makeMessage({ headers: { traceparent: SAMPLE_TRACEPARENT } });
    const span = startConsumerSpan("audit.key-rotated", msg);
    try {
      expect(validTraceId(span)).toBe(SAMPLE_PRODUCER_TRACE_ID);
    } finally {
      span.end();
    }
  });

  it("starts a root span (no valid trace id) when traceparent is absent + no message id", () => {
    const msg = makeMessage({ headers: {}, messageId: undefined });
    const span = startConsumerSpan("audit.key-rotated", msg);
    try {
      // Without a registered tracer provider a root consume span has no
      // recorded trace id — mirrors the .NET null-traceId-when-no-listener.
      expect(validTraceId(span)).toBeUndefined();
      expect(typeof span.end).toBe("function");
    } finally {
      span.end();
    }
  });
});
