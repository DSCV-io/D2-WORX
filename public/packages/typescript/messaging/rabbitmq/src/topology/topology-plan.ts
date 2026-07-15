// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { DlqNaming } from "./dlq-naming.js";
import { type SubscriptionDescriptor } from "./subscription-descriptor.js";
import { queueFlagsFor } from "./queue-pattern.js";

/** Exchange-declare parameters (structurally the rabbitmq-client shape). */
export interface ExchangeDeclareParams {
  readonly exchange: string;
  readonly type: string;
  readonly durable: boolean;
  readonly autoDelete: boolean;
}

/** Queue-declare parameters (structurally the rabbitmq-client shape). */
export interface QueueDeclareParams {
  readonly queue: string;
  readonly durable: boolean;
  readonly exclusive: boolean;
  readonly autoDelete: boolean;
  readonly arguments?: Readonly<Record<string, unknown>>;
}

/** Queue↔exchange binding parameters (structurally the rabbitmq-client shape). */
export interface QueueBindParams {
  readonly queue: string;
  readonly exchange: string;
  readonly routingKey: string;
}

/**
 * A fully-resolved topology declaration set for one subscription. The three
 * ordered lists reproduce the exact declaration set the .NET
 * `DefaultTopologyDeclarer` emits for the same descriptor — every exchange,
 * queue (incl. `{q}.dlx` DLX + `{q}.dlq` DLQ + optional retry tiers), and
 * binding with the same arguments. `primaryQueue` is broken out so the
 * consumer's own auto-redeclare (on reconnect) uses byte-identical args.
 */
export interface TopologyPlan {
  readonly exchanges: readonly ExchangeDeclareParams[];
  readonly queues: readonly QueueDeclareParams[];
  readonly bindings: readonly QueueBindParams[];
  readonly primaryQueue: QueueDeclareParams;
}

/**
 * Plans the complete broker topology for one subscription — the pure mirror
 * of `DefaultTopologyDeclarer.DeclareForSubscriberAsync` +
 * `DeclareTieredRetryAsync`. Declaration order matches .NET exactly:
 * primary exchange → DLX → DLQ → DLQ↔DLX bind → primary queue (with DLX args)
 * → primary↔exchange bind → (optional) retry-return exchange + tier
 * exchanges/queues/bindings.
 *
 * @param descriptor The subscription contract.
 * @param resolvedQueueName The on-broker primary-queue name (already suffixed
 *   for a fanout-exclusive pattern).
 */
export function planTopology(
  descriptor: SubscriptionDescriptor,
  resolvedQueueName: string,
): TopologyPlan {
  const exchanges: ExchangeDeclareParams[] = [];
  const queues: QueueDeclareParams[] = [];
  const bindings: QueueBindParams[] = [];

  const dlxName = DlqNaming.dlxFor(resolvedQueueName);
  const dlqName = DlqNaming.dlqFor(resolvedQueueName);

  // Primary exchange.
  exchanges.push({
    exchange: descriptor.exchange,
    type: descriptor.exchangeType,
    durable: true,
    autoDelete: false,
  });

  // DLX (fanout — routing key irrelevant) → DLQ.
  exchanges.push({
    exchange: dlxName,
    type: "fanout",
    durable: true,
    autoDelete: false,
  });

  queues.push({
    queue: dlqName,
    durable: true,
    exclusive: false,
    autoDelete: false,
  });

  bindings.push({ queue: dlqName, exchange: dlxName, routingKey: "" });

  // Primary queue with the DLX argument, declared per its durability pattern.
  const flags = queueFlagsFor(descriptor.pattern);
  const primaryQueue: QueueDeclareParams = {
    queue: resolvedQueueName,
    durable: flags.durable,
    exclusive: flags.exclusive,
    autoDelete: flags.autoDelete,
    arguments: {
      "x-dead-letter-exchange": dlxName,
      "x-dead-letter-routing-key": "",
    },
  };
  queues.push(primaryQueue);

  bindings.push({
    queue: resolvedQueueName,
    exchange: descriptor.exchange,
    routingKey: descriptor.routingKeyBinding,
  });

  if (descriptor.tieredRetry !== undefined) {
    const returnExchange = DlqNaming.retryReturnExchangeFor(resolvedQueueName);
    exchanges.push({
      exchange: returnExchange,
      type: "fanout",
      durable: true,
      autoDelete: false,
    });

    // Retry queues TTL-expire onto the return exchange, which routes back to
    // the primary queue for another attempt.
    bindings.push({
      queue: resolvedQueueName,
      exchange: returnExchange,
      routingKey: "",
    });

    const tiers = descriptor.tieredRetry.tiersMs;
    for (let i = 0; i < tiers.length; i++) {
      const tierExchange = DlqNaming.retryTierExchangeFor(resolvedQueueName, i);
      const tierQueue = DlqNaming.retryTierQueueFor(resolvedQueueName, i);

      exchanges.push({
        exchange: tierExchange,
        type: "fanout",
        durable: true,
        autoDelete: false,
      });

      queues.push({
        queue: tierQueue,
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: {
          "x-message-ttl": tiers[i],
          "x-dead-letter-exchange": returnExchange,
          "x-dead-letter-routing-key": "",
        },
      });

      bindings.push({
        queue: tierQueue,
        exchange: tierExchange,
        routingKey: "",
      });
    }
  }

  return { exchanges, queues, bindings, primaryQueue };
}
