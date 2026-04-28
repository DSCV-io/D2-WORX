import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  IntakeFileUploadedInput as I,
  IntakeFileUploadedOutput as O,
  IIntakeFileUploadedHandler,
} from "@d2/files-app";
import type { FilesCommands } from "@d2/files-app";
import type { IPublishFileForProcessingHandler } from "@d2/files-app";

/**
 * Messaging subscriber handler — invokes IntakeFile to transition
 * a pending file to processing status, then publishes to the
 * processing queue for scanning + variant generation.
 */
export class IntakeFileUploaded extends BaseHandler<I, O> implements IIntakeFileUploadedHandler {
  private readonly intakeFile: FilesCommands.IIntakeFileHandler;
  private readonly publishForProcessing: IPublishFileForProcessingHandler;

  constructor(
    intakeFile: FilesCommands.IIntakeFileHandler,
    publishForProcessing: IPublishFileForProcessingHandler,
    context: IHandlerContext,
  ) {
    super(context);
    this.intakeFile = intakeFile;
    this.publishForProcessing = publishForProcessing;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const result = await this.intakeFile.handleAsync({ fileId: input.fileId });
    if (!result.success) {
      return D2Result.bubble(result);
    }

    // Only publish if the file was actually transitioned (not discarded)
    if (!result.data?.discarded) {
      const publishResult = await this.publishForProcessing.handleAsync({
        fileId: input.fileId,
      });
      if (!publishResult.success) {
        this.context.logger.error("Failed to publish file for processing", {
          fileId: input.fileId,
        });
        return D2Result.bubble(publishResult);
      }
    }

    return D2Result.ok({ data: {} });
  }
}
