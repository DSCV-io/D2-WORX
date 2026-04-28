import { z } from "zod";
import type { MessageBus, IncomingMessage } from "@d2/messaging";
import { ConsumerResult } from "@d2/messaging";
import type { ServiceProvider, ServiceScope } from "@d2/di";
import type { ILogger } from "@d2/logging";
import { IProcessUploadedFileKey } from "@d2/files-app";
import { FILES_MESSAGING } from "@d2/files-domain";

export interface FileProcessingConsumerDeps {
  readonly messageBus: MessageBus;
  readonly provider: ServiceProvider;
  readonly createScope: (provider: ServiceProvider) => ServiceScope;
  readonly logger: ILogger;
  readonly prefetchCount?: number;
}

const messageSchema = z.object({
  fileId: z.string().min(1).max(36),
});

/**
 * Creates a RabbitMQ consumer for file processing messages.
 *
 * Receives file IDs from the processing queue, resolves the
 * ProcessUploadedFile handler from DI, and dispatches processing.
 *
 * Always ACKs — failed files stay in their current status and
 * are caught by the cleanup job.
 */
export function createFileProcessingConsumer(deps: FileProcessingConsumerDeps) {
  const { messageBus, provider, createScope, logger, prefetchCount = 1 } = deps;

  return messageBus.subscribeEnriched<unknown>(
    {
      queue: FILES_MESSAGING.PROCESSING_QUEUE,
      queueOptions: { durable: true },
      prefetchCount,
      exchanges: [
        {
          exchange: FILES_MESSAGING.EVENTS_EXCHANGE,
          type: FILES_MESSAGING.EVENTS_EXCHANGE_TYPE,
          durable: true,
        },
      ],
      queueBindings: [
        {
          exchange: FILES_MESSAGING.EVENTS_EXCHANGE,
          routingKey: FILES_MESSAGING.PROCESSING_ROUTING_KEY,
        },
      ],
    },
    async (msg: IncomingMessage<unknown>) => {
      const parseResult = messageSchema.safeParse(msg.body);
      if (!parseResult.success) {
        logger.warn("Invalid file processing message — dropping", {
          errors: parseResult.error.issues.map((i) => `${i.path.join(".")}: ${i.message}`),
        });
        return ConsumerResult.ACK;
      }

      const { fileId } = parseResult.data;
      const scope = createScope(provider);
      try {
        const handler = scope.resolve(IProcessUploadedFileKey);
        await handler.handleAsync({ fileId });
      } catch (error: unknown) {
        logger.error("File processing failed", {
          fileId,
          error: error instanceof Error ? error.message : String(error),
        });
      } finally {
        scope.dispose();
      }

      return ConsumerResult.ACK;
    },
  );
}
