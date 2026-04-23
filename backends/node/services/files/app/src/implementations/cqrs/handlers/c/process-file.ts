import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import { z } from "zod";
import {
  transitionFileStatus,
  resolveContentCategory,
  createFileVariant,
  requiresResize,
} from "@d2/files-domain";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { FileRepoHandlers } from "../../../../interfaces/repository/handlers/index.js";
import type { FileStorageHandlers } from "../../../../interfaces/providers/storage/handlers/index.js";
import type { IScanFile } from "../../../../interfaces/providers/scanning/handlers/scan-file.js";
import type { IProcessVariants } from "../../../../interfaces/providers/image-processing/handlers/process-variants.js";
import type { INotifyFileProcessedHandler } from "../../../../interfaces/cqrs/handlers/c/notify-file-processed.js";
import type { IPushFileUpdate } from "../../../../interfaces/realtime/handlers/push-file-update.js";
import type { ContextKeyConfigMap } from "../../../../context-key-config.js";
import { buildRawStorageKey, buildVariantStorageKey } from "../../../utils/storage-keys.js";

type Input = Commands.ProcessFileInput;
type Output = Commands.ProcessFileOutput;

const schema = z.object({
  fileId: z.string().min(1).max(255),
});

/**
 * Processes an uploaded file: ClamAV scan → variant generation → store variants
 * → OnFileProcessed callback → mark ready.
 *
 * Uses fail-last pattern: DB status update only after callback succeeds.
 * Sharp is only invoked for image content with resize variants (maxDimension > 0).
 */
export class ProcessFile
  extends BaseHandler<Input, Output>
  implements Commands.IProcessFileHandler
{
  override get redaction(): RedactionSpec {
    return { suppressOutput: true };
  }

  private readonly repo: FileRepoHandlers;
  private readonly storage: FileStorageHandlers;
  private readonly scanFile: IScanFile;
  private readonly processVariants: IProcessVariants;
  private readonly notifier: INotifyFileProcessedHandler;
  private readonly pushFileUpdate: IPushFileUpdate;
  private readonly configs: ContextKeyConfigMap;

  constructor(
    repo: FileRepoHandlers,
    storage: FileStorageHandlers,
    scanFile: IScanFile,
    processVariants: IProcessVariants,
    notifier: INotifyFileProcessedHandler,
    pushFileUpdate: IPushFileUpdate,
    configs: ContextKeyConfigMap,
    context: IHandlerContext,
  ) {
    super(context);
    this.repo = repo;
    this.storage = storage;
    this.scanFile = scanFile;
    this.processVariants = processVariants;
    this.notifier = notifier;
    this.pushFileUpdate = pushFileUpdate;
    this.configs = configs;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    // Fetch file record
    const findResult = await this.repo.findById.handleAsync({ id: input.fileId });
    if (!findResult.success) return D2Result.bubbleFail(findResult);
    if (!findResult.data?.file) return D2Result.notFound();

    const file = findResult.data.file;

    // Idempotency guard: if already past "processing", return as-is (duplicate message)
    if (file.status !== "processing") return D2Result.ok({ data: { file } });

    const config = this.configs.get(file.contextKey);
    if (!config) return D2Result.forbidden();

    // Download raw object from storage
    const rawKey = buildRawStorageKey(file);
    const getResult = await this.storage.get.handleAsync({ key: rawKey });
    if (!getResult.success) return D2Result.bubbleFail(getResult);

    if (!getResult.data) return D2Result.serviceUnavailable();
    const buffer = getResult.data.buffer;

    // ClamAV scan — ALL files, regardless of content type
    const scanResult = await this.scanFile.handleAsync({
      buffer,
      contentType: file.contentType,
    });
    if (!scanResult.success) return D2Result.bubbleFail(scanResult);

    if (!scanResult.data?.clean) {
      // Virus detected — reject and notify
      const deleteResult = await this.storage.delete.handleAsync({ key: rawKey });
      if (!deleteResult.success) {
        this.context.logger.warn("Failed to delete infected file from storage", {
          fileId: file.id,
          key: rawKey,
        });
      }

      const rejectedFile = transitionFileStatus(file, "rejected", {
        rejectionReason: "content_moderation_failed",
      });
      const rejectUpdateResult = await this.repo.update.handleAsync({ file: rejectedFile });
      if (!rejectUpdateResult.success) return D2Result.bubbleFail(rejectUpdateResult);

      const rejectNotifyResult = await this.notifier.handleAsync({
        address: config.callbackAddress,
        fileId: file.id,
        contextKey: file.contextKey,
        relatedEntityId: file.relatedEntityId,
        status: "rejected",
      });
      if (!rejectNotifyResult.success) return D2Result.bubbleFail(rejectNotifyResult);

      // Fire-and-forget: push rejection to uploader's browser
      this.pushFileUpdate
        .handleAsync({
          uploaderUserId: file.uploaderUserId,
          fileId: file.id,
          contextKey: file.contextKey,
          status: "rejected",
          rejectionReason: "content_moderation_failed",
        })
        .catch((err) =>
          this.context.logger.warn("Realtime push failed (rejected)", { fileId: file.id, err }),
        );

      return D2Result.ok({ data: { file: rejectedFile } });
    }

    // Resolve content category
    const category = resolveContentCategory(file.contentType);
    if (!category) {
      return D2Result.validationFailed({ errorCode: "FILES_INVALID_CONTENT_TYPE" });
    }

    const isImage = category === "image";

    // Partition variants: resize (sharp) vs original (pass-through)
    const resizeVariants = isImage ? config.variants.filter(requiresResize) : [];
    // For non-image files, only store a single original variant
    const originalVariant =
      config.variants.find((v) => v.name === "original") ?? config.variants[0];
    const originalVariants = isImage
      ? config.variants.filter((v) => !requiresResize(v))
      : originalVariant
        ? [originalVariant]
        : [];

    const fileVariants = [];

    // Process resize variants through sharp (images only)
    if (resizeVariants.length > 0) {
      const processResult = await this.processVariants.handleAsync({
        buffer,
        contentType: file.contentType,
        variants: resizeVariants,
      });
      if (!processResult.success) return D2Result.bubbleFail(processResult);

      const processedVariants = processResult.data?.variants ?? [];

      for (const pv of processedVariants) {
        const variantKey = buildVariantStorageKey(file, pv.size, pv.contentType);
        const putResult = await this.storage.put.handleAsync({
          key: variantKey,
          buffer: pv.buffer,
          contentType: pv.contentType,
        });
        if (!putResult.success) return D2Result.bubbleFail(putResult);

        fileVariants.push(
          createFileVariant({
            size: pv.size,
            key: variantKey,
            width: pv.width,
            height: pv.height,
            sizeBytes: pv.sizeBytes,
            contentType: pv.contentType,
          }),
        );
      }
    }

    // Store original variants (pass-through — no sharp processing)
    for (const ov of originalVariants) {
      const variantKey = buildVariantStorageKey(file, ov.name, file.contentType);
      const putResult = await this.storage.put.handleAsync({
        key: variantKey,
        buffer,
        contentType: file.contentType,
      });
      if (!putResult.success) return D2Result.bubbleFail(putResult);

      fileVariants.push(
        createFileVariant({
          size: ov.name,
          key: variantKey,
          width: 0,
          height: 0,
          sizeBytes: buffer.length,
          contentType: file.contentType,
        }),
      );
    }

    // Fail-last: notify callback BEFORE updating DB status
    const notifyResult = await this.notifier.handleAsync({
      address: config.callbackAddress,
      fileId: file.id,
      contextKey: file.contextKey,
      relatedEntityId: file.relatedEntityId,
      status: "ready",
      variants: fileVariants.map((v) => v.size),
    });
    if (!notifyResult.success) return D2Result.bubbleFail(notifyResult);

    // Only on callback success: transition to ready
    const readyFile = transitionFileStatus(file, "ready", { variants: fileVariants });
    const updateResult = await this.repo.update.handleAsync({ file: readyFile });
    if (!updateResult.success) return D2Result.bubbleFail(updateResult);

    // Clean up raw upload (fire-and-forget — orphaned objects cleaned by RunCleanup)
    this.storage.delete
      .handleAsync({ key: rawKey })
      .catch((err) =>
        this.context.logger.warn("Raw upload cleanup failed", { fileId: file.id, err }),
      );

    // Fire-and-forget: push ready status to uploader's browser
    this.pushFileUpdate
      .handleAsync({
        uploaderUserId: file.uploaderUserId,
        fileId: file.id,
        contextKey: file.contextKey,
        status: "ready",
        variants: fileVariants.map((v) => v.size),
      })
      .catch((err) =>
        this.context.logger.warn("Realtime push failed (ready)", { fileId: file.id, err }),
      );

    return D2Result.ok({ data: { file: readyFile } });
  }
}
