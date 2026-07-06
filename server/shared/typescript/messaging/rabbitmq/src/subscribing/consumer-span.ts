// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { type Span, SpanKind } from "@opentelemetry/api";
import { MessagingActivityTags } from "@d2/telemetry";

import { consumerTracer } from "../telemetry.js";
import { type ConsumedMessage, readHeaderString } from "./consumed-message.js";
import {
  parentContextFrom,
  parseTraceparent,
} from "../context/trace-context.js";

const _INVALID_TRACE_ID = "0".repeat(32);

/**
 * Starts a `Consumer`-kind span `receive {queue}` parented to the producer's
 * publish span via the delivery's `traceparent` header — the mirror of the .NET
 * consume-span establishment. Tags come from the spec-emitted
 * `MessagingActivityTags` closed set (same values the .NET consumer emits). A
 * missing / malformed `traceparent` yields a root span (never a reject).
 *
 * @param queue The resolved queue name (span destination).
 * @param msg The normalized delivery.
 */
export function startConsumerSpan(queue: string, msg: ConsumedMessage): Span {
  const traceparent = readHeaderString(msg.headers, "traceparent");
  const parentContext = parentContextFrom(parseTraceparent(traceparent));

  const span = consumerTracer().startSpan(
    `receive ${queue}`,
    { kind: SpanKind.CONSUMER },
    parentContext,
  );

  span.setAttribute(MessagingActivityTags.MESSAGING_SYSTEM, "rabbitmq");
  span.setAttribute(MessagingActivityTags.MESSAGING_DESTINATION_NAME, queue);
  span.setAttribute(MessagingActivityTags.MESSAGING_OPERATION_TYPE, "receive");
  if (msg.messageId !== undefined)
    span.setAttribute(
      MessagingActivityTags.MESSAGING_MESSAGE_ID,
      msg.messageId,
    );

  span.setAttribute(
    MessagingActivityTags.MESSAGING_RABBITMQ_DELIVERY_TAG,
    msg.deliveryTag,
  );
  span.setAttribute(
    MessagingActivityTags.MESSAGING_RABBITMQ_REDELIVERED,
    msg.redelivered,
  );

  return span;
}

/**
 * Returns the span's trace id if it is valid (a real parent trace or an active
 * recording provider), else `undefined` — mirrors the .NET
 * `Activity.Current?.TraceId` being null when no listener is attached, so a
 * DLQ entry's `traceId` stays absent rather than all-zero.
 *
 * @param span The consume span.
 */
export function validTraceId(span: Span): string | undefined {
  const traceId = span.spanContext().traceId;
  return traceId === _INVALID_TRACE_ID ? undefined : traceId;
}
