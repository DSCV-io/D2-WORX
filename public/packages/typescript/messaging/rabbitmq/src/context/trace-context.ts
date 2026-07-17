// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  type Context,
  ROOT_CONTEXT,
  type SpanContext,
  type TraceFlags,
  trace,
} from "@opentelemetry/api";

// W3C traceparent: `{version}-{trace-id}-{parent-id}-{trace-flags}`, version 00.
const TRACEPARENT_RE = /^00-([0-9a-f]{32})-([0-9a-f]{16})-([0-9a-f]{2})$/;
const INVALID_TRACE_ID = "0".repeat(32);
const INVALID_SPAN_ID = "0".repeat(16);

/**
 * Parses a W3C `traceparent` header into a remote {@link SpanContext}. A
 * missing / malformed / all-zero value returns `undefined` (the consume span
 * then starts a fresh root trace) — the exact mirror of the .NET
 * `ActivityContext.TryParse`-fails-then-default posture, so a forged header can
 * never crash a delivery.
 *
 * @param traceparent The raw `traceparent` header value (or undefined).
 */
export function parseTraceparent(
  traceparent: string | undefined,
): SpanContext | undefined {
  if (traceparent === undefined) return undefined;

  const match = TRACEPARENT_RE.exec(traceparent);
  if (match === null) return undefined;

  const traceId = match[1];
  const spanId = match[2];
  const flags = match[3];
  if (
    traceId === undefined ||
    spanId === undefined ||
    flags === undefined ||
    traceId === INVALID_TRACE_ID ||
    spanId === INVALID_SPAN_ID
  ) {
    return undefined;
  }

  return {
    traceId,
    spanId,
    traceFlags: Number.parseInt(flags, 16) as TraceFlags,
    isRemote: true,
  };
}

/**
 * Builds the parent {@link Context} for a consume span from a parsed remote
 * span context — or `undefined` when there is no valid parent (root span).
 *
 * @param spanContext The parsed remote span context (or undefined).
 */
export function parentContextFrom(
  spanContext: SpanContext | undefined,
): Context | undefined {
  return spanContext === undefined
    ? undefined
    : trace.setSpanContext(ROOT_CONTEXT, spanContext);
}
