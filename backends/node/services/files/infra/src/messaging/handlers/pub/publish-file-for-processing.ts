import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { handlePublish, type IMessagePublisher } from "@d2/messaging";
import { FILES_MESSAGING } from "@d2/files-domain";
import type {
  PublishFileForProcessingInput as I,
  PublishFileForProcessingOutput as O,
  IPublishFileForProcessingHandler,
} from "@d2/files-app";

/**
 * Publishes a file ID to the processing queue after intake
 * transitions the file to "processing" status.
 *
 * Uses `handlePublish` for structured error handling — returns
 * `serviceUnavailable()` on RabbitMQ failure instead of an
 * unstructured exception.
 */
export class PublishFileForProcessing
  extends BaseHandler<I, O>
  implements IPublishFileForProcessingHandler
{
  private readonly publisher: IMessagePublisher;

  constructor(publisher: IMessagePublisher, context: IHandlerContext) {
    super(context);
    this.publisher = publisher;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const publishResult = await handlePublish(
      this.publisher,
      {
        exchange: FILES_MESSAGING.EVENTS_EXCHANGE,
        routingKey: FILES_MESSAGING.PROCESSING_ROUTING_KEY,
      },
      { fileId: input.fileId },
    );

    if (!publishResult.success) return D2Result.bubbleFail(publishResult);

    return D2Result.ok({ data: {} });
  }
}
