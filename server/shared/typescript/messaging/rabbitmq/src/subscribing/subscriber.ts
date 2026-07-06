// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  type AsyncMessage,
  type Connection,
  ConsumerStatus,
} from "rabbitmq-client";
import type { ILogger } from "@d2/logging";
import { sanitizedErrorRender } from "@d2/logging";

import { type BodyOpener, PlaintextBodyOpener } from "./body-opener.js";
import { type ConsumedMessage } from "./consumed-message.js";
import {
  DeliveryAction,
  DeliveryProcessor,
  type MessageHandler,
  type RepublishFn,
} from "./delivery-processor.js";
import { type IMessageIdempotencyStore } from "../idempotency/message-idempotency-store.js";
import { readValidatedMessageId } from "./message-id.js";
import {
  type ExchangeDeclareParams,
  planTopology,
  type QueueBindParams,
  type QueueDeclareParams,
} from "../topology/topology-plan.js";
import {
  resolveQueueName,
  type SubscriptionDescriptor,
} from "../topology/subscription-descriptor.js";

/** Everything needed to register one subscriber against a live connection. */
export interface SubscribeOptions {
  /** A live (auto-reconnecting) connection. */
  readonly connection: Connection;
  /** The subscription contract (queue / exchange / pattern / prefetch / ...). */
  readonly descriptor: SubscriptionDescriptor;
  /** The message handler. */
  readonly handler: MessageHandler;
  /** Structured logger (never receives an `Error` directly — PII safety). */
  readonly logger: ILogger;
  /** Body-decompose seam (defaults to {@link PlaintextBodyOpener}). */
  readonly opener?: BodyOpener;
  /** Idempotency store — required only when `descriptor.idempotency` is true. */
  readonly store?: IMessageIdempotencyStore;
  /** Explicit fanout-queue suffix (deterministic tests supply one). */
  readonly queueSuffix?: string;
}

/** A running subscription — its resolved queue, a readiness promise, and close. */
export interface Subscription {
  /** The on-broker queue name (suffix-resolved for fanout-exclusive patterns). */
  readonly queueName: string;
  /** Resolves once topology is declared and the consumer has started. */
  readonly ready: Promise<void>;
  /** Stops consuming and closes the consumer + republish channels. */
  close(): Promise<void>;
}

/**
 * Registers a manual-ack subscriber against a live connection — the TS twin of
 * the .NET `SubscriberChannel` + `ConsumerHostedService`. Declares the exact
 * .NET topology (primary + `{q}.dlx` + `{q}.dlq` + optional retry tiers), starts
 * a competing/fanout consumer, and routes every delivery through the
 * {@link DeliveryProcessor}. Auto-reconnect is inherited from the connection;
 * the consumer re-declares its primary queue + exchanges + bindings on
 * reconnect, and this function re-declares the DLQ / retry-tier queues on each
 * `connection` event.
 *
 * @param options The subscription registration.
 */
export function subscribe(options: SubscribeOptions): Subscription {
  const { connection, descriptor, handler, logger } = options;
  const resolvedQueueName = resolveQueueName(descriptor, options.queueSuffix);
  const plan = planTopology(descriptor, resolvedQueueName);
  const opener = options.opener ?? new PlaintextBodyOpener();

  const publisher = connection.createPublisher({ confirm: true });
  const republish: RepublishFn = async (dlxName, body, headers) => {
    await publisher.send(
      { exchange: dlxName, routingKey: "", durable: true, headers },
      body,
    );
  };

  const processor = new DeliveryProcessor({
    descriptor,
    resolvedQueueName,
    handler,
    opener,
    republish,
    logger,
    store: options.store,
  });

  // The DLQ + retry-tier queues (and their bindings) are not expressible via
  // ConsumerProps — declare them explicitly, up front and on every reconnect.
  const auxQueues = plan.queues.filter((q) => q.queue !== resolvedQueueName);
  const auxBindings = plan.bindings.filter(
    (b) => b.queue !== resolvedQueueName,
  );
  const declareAux = (): Promise<void> =>
    applyAuxTopology(connection, plan.exchanges, auxQueues, auxBindings);

  // Re-declare the aux topology on every reconnect. Captured so `close()` can
  // deregister it — the `connection` is long-lived and shared across
  // subscribers, so a leaked listener would keep firing (redeclaring a closed
  // subscriber's topology) for the life of the process.
  const onReconnect = (): void => {
    declareAux().catch((err: unknown) =>
      logger.error("aux topology declaration failed", {
        queue: resolvedQueueName,
        error: sanitizedErrorRender(err),
      }),
    );
  };

  connection.on("connection", onReconnect);

  const consumer = connection.createConsumer(
    {
      queue: resolvedQueueName,
      lazy: true,
      requeue: false,
      concurrency: descriptor.concurrency ?? descriptor.prefetch,
      qos: { prefetchCount: descriptor.prefetch },
      queueOptions: toQueueDeclare(plan.primaryQueue),
      exchanges: plan.exchanges.map(toExchangeDeclare),
      queueBindings: plan.bindings
        .filter((b) => b.queue === resolvedQueueName)
        .map(toQueueBind),
    },
    async (msg: AsyncMessage) => {
      const action = await processor.process(toConsumedMessage(msg));
      return action === DeliveryAction.Drop
        ? ConsumerStatus.DROP
        : ConsumerStatus.ACK;
    },
  );

  consumer.on("error", (err: unknown) =>
    logger.error("consumer error", {
      queue: resolvedQueueName,
      error: sanitizedErrorRender(err),
    }),
  );

  logger.info("subscriber starting", {
    queue: resolvedQueueName,
    pattern: descriptor.pattern,
    prefetch: descriptor.prefetch,
  });

  const ready = (async (): Promise<void> => {
    await declareAux();
    consumer.start();
    await new Promise<void>((resolve) => consumer.on("ready", resolve));
  })();

  return {
    queueName: resolvedQueueName,
    ready,
    close: async () => {
      connection.off("connection", onReconnect);
      await consumer.close();
      await publisher.close();
    },
  };
}

/** Maps a rabbitmq-client `AsyncMessage` to the pipeline's normalized shape. */
export function toConsumedMessage(msg: AsyncMessage): ConsumedMessage {
  const headers = (msg.headers ?? {}) as Readonly<Record<string, unknown>>;
  return {
    body: toBodyBuffer(msg.body),
    messageId: readValidatedMessageId(msg.messageId, headers),
    headers,
    deliveryTag: msg.deliveryTag,
    redelivered: msg.redelivered,
    exchange: msg.exchange,
    routingKey: msg.routingKey,
    contentType: msg.contentType,
    correlationId: msg.correlationId,
  };
}

function toBodyBuffer(body: unknown): Buffer {
  if (Buffer.isBuffer(body)) return body;
  if (body instanceof Uint8Array) return Buffer.from(body);
  if (typeof body === "string") return Buffer.from(body, "utf8");

  return Buffer.from(JSON.stringify(body), "utf8");
}

async function applyAuxTopology(
  connection: Connection,
  exchanges: readonly ExchangeDeclareParams[],
  queues: readonly QueueDeclareParams[],
  bindings: readonly QueueBindParams[],
): Promise<void> {
  for (const e of exchanges)
    await connection.exchangeDeclare(toExchangeDeclare(e));
  for (const q of queues) await connection.queueDeclare(toQueueDeclare(q));
  for (const b of bindings) await connection.queueBind(toQueueBind(b));
}

function toExchangeDeclare(e: ExchangeDeclareParams): {
  exchange: string;
  type: string;
  durable: boolean;
  autoDelete: boolean;
} {
  return {
    exchange: e.exchange,
    type: e.type,
    durable: e.durable,
    autoDelete: e.autoDelete,
  };
}

function toQueueDeclare(q: QueueDeclareParams): {
  queue: string;
  durable: boolean;
  exclusive: boolean;
  autoDelete: boolean;
  arguments?: Record<string, unknown>;
} {
  return {
    queue: q.queue,
    durable: q.durable,
    exclusive: q.exclusive,
    autoDelete: q.autoDelete,
    arguments: q.arguments === undefined ? undefined : { ...q.arguments },
  };
}

function toQueueBind(b: QueueBindParams): {
  queue: string;
  exchange: string;
  routingKey: string;
} {
  return { queue: b.queue, exchange: b.exchange, routingKey: b.routingKey };
}
