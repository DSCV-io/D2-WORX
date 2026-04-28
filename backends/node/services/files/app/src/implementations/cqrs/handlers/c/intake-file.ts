import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import { z } from "zod";
import { transitionFileStatus } from "@d2/files-domain";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { FileRepoHandlers } from "../../../../interfaces/repository/handlers/index.js";
import type { FileStorageHandlers } from "../../../../interfaces/storage/handlers/index.js";
import { buildRawStorageKey } from "@d2/files-domain";

type Input = Commands.IntakeFileInput;
type Output = Commands.IntakeFileOutput;

const schema = z.object({
  fileId: z.string().min(1).max(255),
});

/**
 * Intake handler — called by the MinIO bucket notification consumer.
 *
 * Validates that the file exists and is in "pending" status, then
 * transitions it to "processing". If the file is not found or in the
 * wrong status, the event is silently discarded (not an error).
 *
 * Also verifies actual upload size matches declared size — S3 presigned
 * PUT URLs cannot enforce Content-Length, so post-upload verification
 * is required to prevent size bypass.
 */
export class IntakeFile extends BaseHandler<Input, Output> implements Commands.IIntakeFileHandler {
  override get redaction(): RedactionSpec {
    return { suppressOutput: true };
  }

  private readonly repo: FileRepoHandlers;
  private readonly storage: Pick<FileStorageHandlers, "head" | "delete">;

  constructor(
    repo: FileRepoHandlers,
    storage: Pick<FileStorageHandlers, "head" | "delete">,
    context: IHandlerContext,
  ) {
    super(context);
    this.repo = repo;
    this.storage = storage;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const findResult = await this.repo.getById.handleAsync({ id: input.fileId });
    if (!findResult.success || !findResult.data?.file) {
      return D2Result.ok({ data: { discarded: true, reason: "not_found" } });
    }

    const file = findResult.data.file;

    // Verify actual upload size matches declared size (S3 presigned URLs can't enforce Content-Length)
    const headResult = await this.storage.head.handleAsync({
      key: buildRawStorageKey(file),
    });
    if (headResult.success && headResult.data) {
      const actualSize = headResult.data.sizeBytes;
      if (actualSize !== undefined && actualSize > file.sizeBytes) {
        // Actual upload exceeds declared size — reject and clean up
        this.context.logger.warn("Upload size mismatch", {
          fileId: input.fileId,
          declaredSize: file.sizeBytes,
          actualSize,
        });
        const deleteResult = await this.storage.delete.handleAsync({
          key: buildRawStorageKey(file),
        });
        if (!deleteResult.success) {
          this.context.logger.warn("IntakeFile: failed to delete oversized object from storage", {
            fileId: input.fileId,
          });
        }
        const rejectedFile = transitionFileStatus(file, "rejected", {
          rejectionReason: "size_mismatch",
        });
        const rejectUpdateResult = await this.repo.update.handleAsync({ file: rejectedFile });
        if (!rejectUpdateResult.success) return D2Result.bubbleFail(rejectUpdateResult);
        return D2Result.ok({ data: { discarded: true, reason: "size_mismatch" } });
      }
    }

    if (file.status !== "pending") {
      return D2Result.ok({ data: { discarded: true, reason: "wrong_status" } });
    }

    const processingFile = transitionFileStatus(file, "processing");

    const updateResult = await this.repo.update.handleAsync({
      file: processingFile,
      expectedStatus: "pending",
    });
    if (!updateResult.success) {
      if (updateResult.statusCode === 404) {
        return D2Result.ok({ data: { discarded: true, reason: "concurrent_transition" } });
      }
      return D2Result.bubbleFail(updateResult);
    }

    return D2Result.ok({ data: { discarded: false, file: processingFile } });
  }
}
