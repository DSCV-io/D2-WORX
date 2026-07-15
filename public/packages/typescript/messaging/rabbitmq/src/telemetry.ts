// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { type Counter, metrics, type Tracer, trace } from "@opentelemetry/api";

/**
 * OTel source / meter name for the messaging-rabbitmq consumer runtime.
 * Byte-identical to the .NET `MessagingTelemetry.SOURCE_NAME` so consume
 * spans + metrics from both runtimes aggregate under one instrumentation
 * scope.
 */
export const MESSAGING_SOURCE_NAME = "D2.Shared.Messaging.RabbitMq";

const meter = metrics.getMeter(MESSAGING_SOURCE_NAME);

/**
 * Consumer-side republish-to-DLX failures (failure-header lost; fell back
 * to a plain NACK-no-requeue). Mirrors the .NET
 * `d2.messaging.rabbitmq.dlq_republish_failures` counter.
 */
export const dlqRepublishFailuresCounter: Counter = meter.createCounter(
  "d2.messaging.rabbitmq.dlq_republish_failures",
  {
    unit: "{republish}",
    description:
      "Consumer-side republish-to-DLX failures (failure-header lost; " +
      "fell back to NACK-no-requeue).",
  },
);

/**
 * Post-handler-success ack failures + idempotency-mark write failures.
 * Mirrors the .NET `d2.messaging.rabbitmq.ack_failures` counter — the
 * idempotency mark prevents duplicate work on broker redelivery.
 */
export const ackFailuresCounter: Counter = meter.createCounter(
  "d2.messaging.rabbitmq.ack_failures",
  {
    unit: "{ack}",
    description:
      "Consumer-side ack / idempotency-mark failures (handler succeeded but " +
      "the mark or ack could not be committed — broker will redeliver).",
  },
);

/**
 * Successful publisher-confirmed publishes. Mirrors the .NET
 * `d2.messaging.rabbitmq.publishes` counter — one increment per message the
 * broker confirmed.
 */
export const publishesCounter: Counter = meter.createCounter(
  "d2.messaging.rabbitmq.publishes",
  {
    unit: "{publish}",
    description: "Publisher-confirmed message publishes.",
  },
);

/**
 * Publisher-side failures — a compose (encrypt/seal) failure, an unknown
 * message constant, or a broker send/confirm failure. Mirrors the .NET
 * `d2.messaging.rabbitmq.publish_failures` counter.
 */
export const publishFailuresCounter: Counter = meter.createCounter(
  "d2.messaging.rabbitmq.publish_failures",
  {
    unit: "{publish}",
    description:
      "Publisher-side failures (compose failure, unknown constant, or " +
      "broker send/confirm failure).",
  },
);

/** Returns the shared Consumer-kind tracer for the messaging runtime. */
export function consumerTracer(): Tracer {
  return trace.getTracer(MESSAGING_SOURCE_NAME);
}

/** Returns the shared Producer-kind tracer for the messaging runtime. */
export function producerTracer(): Tracer {
  return trace.getTracer(MESSAGING_SOURCE_NAME);
}
