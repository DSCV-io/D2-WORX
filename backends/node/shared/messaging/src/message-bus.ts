import { Connection, ConsumerStatus } from "rabbitmq-client";
import type {
  MessageBusOptions,
  MessageBusLogger,
  ConsumerConfig,
  PublisherConfig,
  PublishTarget,
  IMessageConsumer,
  IMessagePublisher,
  EnrichedConsumerHandler,
  IncomingMessage,
  TopologyConfig,
} from "./types.js";
import { ConsumerResult } from "./types.js";

const CONSOLE_FALLBACK: MessageBusLogger = {
  warn: (msg, ...args) => console.warn(`[MessageBus] ${msg}`, ...args),
  error: (msg, ...args) => console.error(`[MessageBus] ${msg}`, ...args),
};

/**
 * Thin wrapper around rabbitmq-client providing consumer and publisher abstractions.
 * Replaces MassTransit for both inbound (consume) and outbound (publish) messaging.
 *
 * Features: auto-reconnect, broker confirms, JSON auto-serialization.
 */
export class MessageBus {
  private readonly connection: Connection;
  private readonly logger: MessageBusLogger;

  constructor(options: MessageBusOptions) {
    this.logger = options.logger ?? CONSOLE_FALLBACK;
    this.connection = new Connection({
      url: options.url,
      connectionName: options.connectionName,
      ...options.connectionOptions,
    });
  }

  /**
   * Subscribe to messages on a queue.
   * Normal return = ACK. Throw = NACK (requeue/dead-letter per RabbitMQ policy).
   */
  subscribe<T>(
    config: ConsumerConfig<T>,
    handler: (message: T) => Promise<void>,
  ): IMessageConsumer {
    const consumer = this.connection.createConsumer(
      {
        queue: config.queue,
        queueOptions: config.queueOptions,
        qos:
          config.prefetchCount !== undefined ? { prefetchCount: config.prefetchCount } : undefined,
        exchanges: config.exchanges,
        queueBindings: config.queueBindings,
      },
      async (msg) => {
        const body = config.deserialize ? config.deserialize(msg.body) : (msg.body as T);
        await handler(body);
      },
    );

    // Prevent unhandled rejection — handler throws are expected (NACK path).
    consumer.on("error", (err) => {
      this.logger.error(`Consumer error on queue "${config.queue}"`, err);
    });

    return {
      ready: new Promise<void>((resolve) => consumer.on("ready", resolve)),
      close: () => consumer.close(),
    };
  }

  /**
   * Subscribe to messages with full AMQP metadata (headers, messageId, etc.).
   * The handler receives an `IncomingMessage<T>` and returns a `ConsumerResult`
   * to control ACK/NACK behavior. Returning void or undefined = ACK.
   *
   * Uses `requeue: false` so the handler has full control over retry behavior.
   */
  subscribeEnriched<T>(
    config: ConsumerConfig<T>,
    handler: EnrichedConsumerHandler<T>,
  ): IMessageConsumer {
    const consumer = this.connection.createConsumer(
      {
        queue: config.queue,
        queueOptions: config.queueOptions,
        qos:
          config.prefetchCount !== undefined ? { prefetchCount: config.prefetchCount } : undefined,
        exchanges: config.exchanges,
        queueBindings: config.queueBindings,
        requeue: false,
      },
      async (msg) => {
        const body = config.deserialize ? config.deserialize(msg.body) : (msg.body as T);

        const incoming: IncomingMessage<T> = {
          body,
          headers: (msg.headers as Record<string, unknown>) ?? {},
          messageId: msg.messageId,
          correlationId: msg.correlationId,
          redelivered: msg.redelivered,
        };

        const result = await handler(incoming);
        return (result ?? ConsumerResult.ACK) as unknown as ConsumerStatus;
      },
    );

    // Prevent unhandled rejection — handler throws are expected (DROP path with requeue=false).
    consumer.on("error", (err) => {
      this.logger.error(`Enriched consumer error on queue "${config.queue}"`, err);
    });

    return {
      ready: new Promise<void>((resolve) => consumer.on("ready", resolve)),
      close: () => consumer.close(),
    };
  }

  /** Create a publisher for outbound messages. */
  createPublisher(config?: PublisherConfig): IMessagePublisher {
    const publisher = this.connection.createPublisher({
      confirm: config?.confirm ?? true,
      maxAttempts: config?.maxAttempts ?? 2,
      exchanges: config?.exchanges,
    });

    return {
      send: async <T>(target: PublishTarget, message: T): Promise<void> => {
        await publisher.send(
          {
            exchange: target.exchange,
            routingKey: target.routingKey,
            headers: target.headers,
            expiration: target.expiration,
            messageId: target.messageId,
          },
          message,
        );
      },
      close: () => publisher.close(),
    };
  }

  /**
   * Declare exchanges, queues, and bindings on the broker.
   * Used to set up infrastructure (e.g., retry topology) without subscribing.
   */
  async declareTopology(config: TopologyConfig): Promise<void> {
    for (const ex of config.exchanges ?? []) {
      await this.connection.exchangeDeclare({
        exchange: ex.exchange,
        type: ex.type,
        durable: ex.durable ?? true,
      });
    }

    for (const q of config.queues ?? []) {
      await this.connection.queueDeclare({
        queue: q.queue,
        durable: q.durable ?? true,
        arguments: q.arguments,
      });
    }

    for (const b of config.bindings ?? []) {
      await this.connection.queueBind({
        exchange: b.exchange,
        queue: b.queue,
        routingKey: b.routingKey,
      });
    }
  }

  /**
   * Check if the underlying connection is alive.
   * Returns true if connected, false on failure/timeout.
   */
  async ping(timeout = 5_000): Promise<boolean> {
    try {
      // Attempt to wait for the connection within the timeout.
      // If already connected, this resolves immediately.
      await this.connection.onConnect(timeout, true);
      return this.connection.ready;
    } catch {
      return false;
    }
  }

  /** Wait for the underlying connection to be established. */
  async waitForConnection(timeout = 10_000): Promise<void> {
    await this.connection.onConnect(timeout, true);
  }

  async close(): Promise<void> {
    await this.connection.close();
  }
}
