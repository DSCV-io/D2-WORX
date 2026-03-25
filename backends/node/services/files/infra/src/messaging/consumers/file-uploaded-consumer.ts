import { z } from "zod";
import type { MessageBus, IncomingMessage } from "@d2/messaging";
import { ConsumerResult } from "@d2/messaging";
import type { ServiceProvider, ServiceScope } from "@d2/di";
import type { ILogger } from "@d2/logging";
import { IIntakeFileUploadedKey } from "@d2/files-app";
import { FILES_MESSAGING } from "@d2/files-domain";

export interface FileUploadedConsumerDeps {
  readonly messageBus: MessageBus;
  readonly provider: ServiceProvider;
  readonly createScope: (provider: ServiceProvider) => ServiceScope;
  readonly logger: ILogger;
  readonly prefetchCount?: number;
}

/**
 * MinIO S3 event notification payload (subset).
 * MinIO publishes S3-compatible event JSON; we extract the object key
 * which is the fileId (files are stored as `{fileId}` in the bucket).
 */
const minioEventSchema = z.object({
  Records: z
    .array(
      z.object({
        s3: z.object({
          object: z.object({
            key: z.string().min(1),
          }),
        }),
      }),
    )
    .min(1),
});

/**
 * Simple `{ fileId }` shape — used when the intake message is published
 * by an internal publisher rather than MinIO bucket notifications.
 */
const simpleMessageSchema = z.object({
  fileId: z.string().min(1).max(36),
});

/**
 * Creates a RabbitMQ consumer for MinIO file upload notifications.
 *
 * Receives S3-compatible event notifications from MinIO (or simple
 * `{ fileId }` messages from internal publishers), resolves the
 * IntakeFileUploaded handler from DI, and dispatches intake processing.
 *
 * Always ACKs — failed files stay in their current status and
 * are caught by the cleanup job.
 */
export function createFileUploadedConsumer(deps: FileUploadedConsumerDeps) {
  const { messageBus, provider, createScope, logger, prefetchCount = 1 } = deps;

  return messageBus.subscribeEnriched<unknown>(
    {
      queue: FILES_MESSAGING.INTAKE_QUEUE,
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
          routingKey: FILES_MESSAGING.UPLOAD_ROUTING_KEY,
        },
      ],
    },
    async (msg: IncomingMessage<unknown>) => {
      const fileId = extractFileId(msg.body);
      if (!fileId) {
        logger.warn("Invalid file upload message — dropping", {
          body: msg.body,
        });
        return ConsumerResult.ACK;
      }

      const scope = createScope(provider);
      try {
        const handler = scope.resolve(IIntakeFileUploadedKey);
        await handler.handleAsync({ fileId });
      } catch (error: unknown) {
        logger.error("File intake failed", {
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

/**
 * Extracts the fileId from either a MinIO S3 event notification
 * or a simple `{ fileId }` message body.
 *
 * MinIO notifications contain the S3 object key in `Records[0].s3.object.key`.
 * The object key follows the pattern `{contextKey}/{relatedEntityId}/{fileId}/raw.{ext}`.
 * The fileId is the third path segment (index 2).
 */
function extractFileId(body: unknown): string | undefined {
  // Try MinIO S3 event format first
  const minioResult = minioEventSchema.safeParse(body);
  if (minioResult.success) {
    const objectKey = decodeURIComponent(minioResult.data.Records[0]!.s3.object.key);
    // Storage key format: {contextKey}/{relatedEntityId}/{fileId}/{filename}
    const segments = objectKey.split("/");
    return segments.length >= 3 ? segments[2] : objectKey;
  }

  // Fall back to simple { fileId } format
  const simpleResult = simpleMessageSchema.safeParse(body);
  if (simpleResult.success) {
    return simpleResult.data.fileId;
  }

  return undefined;
}
