import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  ProcessUploadedFileInput as I,
  ProcessUploadedFileOutput as O,
  IProcessUploadedFileHandler,
} from "@d2/files-app";
import type { FilesCommands } from "@d2/files-app";

/**
 * Messaging subscriber handler — invokes ProcessFile for scanning
 * and variant generation on an uploaded file.
 */
export class ProcessUploadedFile extends BaseHandler<I, O> implements IProcessUploadedFileHandler {
  private readonly processFile: FilesCommands.IProcessFileHandler;

  constructor(processFile: FilesCommands.IProcessFileHandler, context: IHandlerContext) {
    super(context);
    this.processFile = processFile;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const result = await this.processFile.handleAsync({ fileId: input.fileId });
    if (!result.success) {
      return D2Result.bubble(result);
    }
    return D2Result.ok({ data: {} });
  }
}
