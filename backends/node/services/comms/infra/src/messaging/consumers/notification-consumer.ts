import { z } from "zod";
import { zodGuid } from "@d2/handler";
import type { MessageBus, IMessagePublisher, IncomingMessage } from "@d2/messaging";
import { ConsumerResult } from "@d2/messaging";
import type { ILogger } from "@d2/logging";
import type { ServiceProvider, ServiceScope } from "@d2/di";
import { COMMS_MESSAGING, COMMS_RETRY } from "@d2/comms-domain";
import { COMMS_EVENTS } from "@d2/comms-client";
import { IDeliverKey } from "@d2/comms-app";
import { getRetryTierQueue } from "../retry-topology.js";

export interface NotificationConsumerDeps {
  messageBus: MessageBus;
  provider: ServiceProvider;
  createScope: (provider: ServiceProvider) => ServiceScope;
  retryPublisher: IMessagePublisher;
  logger: ILogger;
  /** Number of unacknowledged messages the broker delivers. Matches .NET ConsumerConfig default. */
  prefetchCount?: number;
}

/**
 * Zod schema for validating incoming notification messages.
 * Matches the NotifyInput shape published by @d2/comms-client.
 */
const notificationMessageSchema = z.object({
  recipientContactId: zodGuid,
  title: z.string().min(1).max(255),
  content: z.string().min(1).max(50_000),
  plaintext: z.string().min(1).max(50_000),
  channels: z.array(z.enum(["email", "sms"])).optional(),
  urgency: z.enum(["normal", "urgent"]).optional(),
  correlationId: z.string().min(1).max(36),
  senderService: z.string().min(1).max(50),
  metadata: z.record(z.string(), z.unknown()).optional(),
});

type NotificationMessage = z.infer<typeof notificationMessageSchema>;

/**
 * Creates a RabbitMQ consumer for notification requests with DLX-based retry.
 *
 * Receives universal notification messages from @d2/comms-client publishers,
 * resolves the Deliver handler from DI, and dispatches the delivery.
 *
 * On success: ACK the message.
 * On retryable delivery failure (D2Result with DELIVERY_FAILED): schedule retry via tier queue.
 * On unexpected error (DI failure, etc.): schedule retry via tier queue.
 * At max attempts: log and drop.
 *
 * Always returns ACK — retry is via re-publish to tier queues, not NACK/requeue.
 */
export function createNotificationConsumer(deps: NotificationConsumerDeps) {
  const { messageBus, provider, createScope, retryPublisher, logger, prefetchCount = 1 } = deps;

  return messageBus.subscribeEnriched<unknown>(
    {
      queue: COMMS_MESSAGING.NOTIFICATIONS_QUEUE,
      queueOptions: { durable: true },
      prefetchCount,
      exchanges: [
        {
          exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE,
          type: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE_TYPE,
        },
      ],
      queueBindings: [{ exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, routingKey: "" }],
    },
    async (msg: IncomingMessage<unknown>) => {
      const parseResult = notificationMessageSchema.safeParse(msg.body);
      if (!parseResult.success) {
        logger.warn("Invalid notification message — validation failed, dropping", {
          errors: parseResult.error.issues.map((i) => `${i.path.join(".")}: ${i.message}`),
          correlationId: (msg.body as Record<string, unknown>)?.correlationId,
          senderService: (msg.body as Record<string, unknown>)?.senderService,
        });
        return ConsumerResult.ACK;
      }
      const body = parseResult.data;

      const scope = createScope(provider);
      try {
        const deliver = scope.resolve(IDeliverKey);
        const result = await deliver.handleAsync({
          senderService: body.senderService,
          title: body.title,
          content: body.content,
          plainTextContent: body.plaintext,
          channels: body.channels,
          urgency: body.urgency,
          recipientContactId: body.recipientContactId,
          correlationId: body.correlationId,
          metadata: body.metadata,
        });

        // Check for retryable delivery failure (provider down, SMTP timeout, etc.)
        if (!result.success && result.errorCode === COMMS_RETRY.DELIVERY_FAILED) {
          await scheduleRetry(msg, body, logger, retryPublisher);
        }
        // Non-DELIVERY_FAILED failures (NOT_FOUND, validation, etc.) are terminal — ACK and move on.
      } catch (error) {
        // Truly unexpected errors (DI resolution failure, etc.)
        await scheduleRetry(msg, body, logger, retryPublisher, error);
      } finally {
        scope.dispose();
      }

      return ConsumerResult.ACK;
    },
  );
}

/**
 * Schedules a retry by re-publishing the message to the appropriate tier queue.
 * At max attempts, logs an error and drops the message.
 */
async function scheduleRetry(
  msg: IncomingMessage<unknown>,
  body: Record<string, unknown> | NotificationMessage,
  logger: ILogger,
  retryPublisher: IMessagePublisher,
  error?: unknown,
): Promise<void> {
  const retryCount = Number(msg.headers[COMMS_RETRY.RETRY_COUNT_HEADER] ?? 0);
  const tierQueue = getRetryTierQueue(retryCount);

  if (tierQueue == null) {
    logger.error(
      "Max retry attempts reached, dropping message",
      {
        retryCount,
        correlationId: (body as Record<string, unknown>)?.correlationId,
        senderService: (body as Record<string, unknown>)?.senderService,
      },
      error,
    );
    return;
  }

  logger.warn("Handler failed, scheduling retry", {
    retryCount: retryCount + 1,
    tierQueue,
    error: error instanceof Error ? error.message : String(error ?? "delivery failed"),
  });

  await retryPublisher.send(
    {
      exchange: "",
      routingKey: tierQueue,
      headers: {
        ...msg.headers,
        [COMMS_RETRY.RETRY_COUNT_HEADER]: retryCount + 1,
      },
    },
    body,
  );
}
