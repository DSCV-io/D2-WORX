// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { QueuePattern } from "./queue-pattern.js";

/**
 * Optional broker-level tiered-retry contract. Mirrors the .NET
 * `TieredRetryDescriptor` — the framework declares the tier topology; the
 * per-handler driver is responsible for routing a transient failure to a
 * tier exchange. `maxAttempts` caps the total retry cycles via the broker's
 * `x-death` header count.
 */
export interface TieredRetryDescriptor {
  /** Total retry cycles allowed before a message routes direct to the DLQ. */
  readonly maxAttempts: number;
  /** Per-tier message TTL in milliseconds (one tier per entry, in order). */
  readonly tiersMs: readonly number[];
}

/**
 * The transport-agnostic subscription contract a consumer registers. Mirrors
 * the fields of the .NET `ISubscriberRegistration` + its `MqSubscriptionDescriptor`
 * that the consumer runtime needs: which queue, bound to which exchange, with
 * what durability pattern, prefetch, idempotency opt-in, and optional retry
 * tiers. The producer/message-type coupling lives in the spec-driven
 * `MqMessages`/`MqMessagesRegistry` descriptor mirror, not here.
 */
export interface SubscriptionDescriptor {
  /**
   * Base primary-queue name. For a {@link QueuePattern.FanoutExclusiveAutoDelete}
   * subscription the runtime appends a per-process suffix (see
   * {@link resolveQueueName}) so multi-replica services don't race on the
   * broker's exclusive-queue lock.
   */
  readonly queueName: string;
  /** AMQP exchange the primary queue binds to. */
  readonly exchange: string;
  /** AMQP exchange type. */
  readonly exchangeType: "fanout" | "topic" | "direct";
  /** Queue durability / exclusivity pattern. */
  readonly pattern: QueuePattern;
  /** Routing key used for the primary queue↔exchange binding (empty for fanout). */
  readonly routingKeyBinding: string;
  /** Per-consumer prefetch window (whole messages). */
  readonly prefetch: number;
  /** Opt-in idempotency dedup on `message-id` (requires an idempotency store). */
  readonly idempotency: boolean;
  /** Optional broker-level tiered-retry topology + attempt cap. */
  readonly tieredRetry?: TieredRetryDescriptor;
  /** Max handler invocations running concurrently (defaults to prefetch). */
  readonly concurrency?: number;
  /** Optional service name stamped into `DlqFailureMetadata.nackedBy`. */
  readonly nackedBy?: string;
}

const _QUEUE_SUFFIX_LENGTH = 8;

/**
 * Resolves the on-broker queue name from a descriptor. For a
 * {@link QueuePattern.FanoutExclusiveAutoDelete} subscription the base name
 * gets a per-process 8-char suffix (mirrors the .NET
 * `SubscriberRegistrar.ResolveQueueName`); every other pattern uses the base
 * name unchanged.
 *
 * @param descriptor The subscription descriptor.
 * @param suffix Optional explicit suffix (deterministic tests supply one);
 *   defaults to a random 8-char hex token.
 */
export function resolveQueueName(
  descriptor: SubscriptionDescriptor,
  suffix?: string,
): string {
  if (descriptor.pattern !== QueuePattern.FanoutExclusiveAutoDelete)
    return descriptor.queueName;

  const token = suffix ?? randomSuffix();
  return `${descriptor.queueName}.${token}`;
}

function randomSuffix(): string {
  let out = "";
  while (out.length < _QUEUE_SUFFIX_LENGTH)
    out += Math.floor(Math.random() * 16).toString(16);

  return out.slice(0, _QUEUE_SUFFIX_LENGTH);
}
