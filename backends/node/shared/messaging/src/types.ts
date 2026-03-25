/** Minimal logger interface for MessageBus (avoids dependency on @d2/logging). */
export interface MessageBusLogger {
  warn(msg: string, ...args: unknown[]): void;
  error(msg: string, ...args: unknown[]): void;
}

export interface MessageBusOptions {
  url: string;
  connectionName?: string;
  /** Optional structured logger. Falls back to console if not provided. */
  logger?: MessageBusLogger;
  /** Pass-through options for the underlying rabbitmq-client Connection (reconnection tuning, etc.). */
  connectionOptions?: {
    retryLow?: number;
    retryHigh?: number;
    maxRetries?: number;
  };
}

export interface ConsumerConfig<T = unknown> {
  queue: string;
  queueOptions?: { durable?: boolean; arguments?: Record<string, unknown> };
  prefetchCount?: number;
  exchanges?: Array<{ exchange: string; type: "topic" | "direct" | "fanout"; durable?: boolean }>;
  queueBindings?: Array<{ exchange: string; routingKey: string }>;
  /** Optional deserializer for the raw message body. When provided, the raw JSON is passed through this function before reaching the handler. */
  deserialize?: (raw: unknown) => T;
}

export interface PublisherConfig {
  confirm?: boolean;
  maxAttempts?: number;
  exchanges?: Array<{ exchange: string; type: "topic" | "direct" | "fanout"; durable?: boolean }>;
}

export interface PublishTarget {
  exchange: string;
  routingKey: string;
  headers?: Record<string, unknown>;
  expiration?: string;
  messageId?: string;
}

export interface IMessageConsumer {
  /** Resolves when the consumer is fully set up (queue declared, bindings created, consuming). */
  ready: Promise<void>;
  close(): Promise<void>;
}

export interface IMessagePublisher {
  send<T>(target: PublishTarget, message: T): Promise<void>;
  close(): Promise<void>;
}

/**
 * Incoming message wrapper that preserves AMQP metadata alongside the typed body.
 * Used by `subscribeEnriched()` to give handlers access to headers, message IDs, etc.
 */
export interface IncomingMessage<T> {
  body: T;
  headers: Record<string, unknown>;
  messageId?: string;
  correlationId?: string;
  redelivered: boolean;
}

/**
 * Consumer result codes controlling ACK/NACK behavior.
 * Mirrors rabbitmq-client's ConsumerStatus enum values.
 */
export enum ConsumerResult {
  /** BasicAck — message processed successfully. */
  ACK = 0,
  /** BasicNack(requeue=true) — message returned to the queue. */
  REQUEUE = 1,
  /** BasicNack(requeue=false) — message sent to dead-letter exchange or discarded. */
  DROP = 2,
}

/**
 * Handler for `subscribeEnriched()` that receives full AMQP metadata.
 * Return a `ConsumerResult` to control ACK/NACK, or void/undefined for ACK.
 */
export type EnrichedConsumerHandler<T> = (
  msg: IncomingMessage<T>,
) => Promise<ConsumerResult | void>;

/**
 * Topology declaration config for `declareTopology()`.
 * Declares exchanges, queues, and bindings in a single call.
 */
export interface TopologyConfig {
  exchanges?: Array<{
    exchange: string;
    type: "topic" | "direct" | "fanout";
    durable?: boolean;
  }>;
  queues?: Array<{
    queue: string;
    durable?: boolean;
    arguments?: Record<string, unknown>;
  }>;
  bindings?: Array<{
    exchange: string;
    queue: string;
    routingKey: string;
  }>;
}
