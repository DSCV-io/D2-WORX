import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { z } from "zod";
import { createFile, isContentTypeAllowed } from "@d2/files-domain";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import { UPLOAD_FILE_REDACTION } from "../../../../interfaces/cqrs/handlers/c/upload-file.js";
import type { FileRepoHandlers } from "../../../../interfaces/repository/handlers/index.js";
import type { FileStorageHandlers } from "../../../../interfaces/providers/storage/handlers/index.js";
import type { ContextKeyConfigMap } from "../../../../context-key-config.js";
import type { IResolveFileAccessHandler } from "../../../../interfaces/cqrs/handlers/u/resolve-file-access.js";
import { buildRawStorageKey } from "../../../utils/storage-keys.js";

type Input = Commands.UploadFileInput;
type Output = Commands.UploadFileOutput;

const schema = z.object({
  contextKey: z.string().min(1).max(100),
  relatedEntityId: z.string().min(1).max(255),
  contentType: z.string().min(1).max(255),
  displayName: z.string().min(1).max(255),
  sizeBytes: z.number().int().positive().finite(),
});

export class UploadFile extends BaseHandler<Input, Output> implements Commands.IUploadFileHandler {
  private readonly repo: FileRepoHandlers;
  private readonly storage: FileStorageHandlers;
  private readonly configs: ContextKeyConfigMap;
  private readonly resolveAccess: IResolveFileAccessHandler;

  constructor(
    repo: FileRepoHandlers,
    storage: FileStorageHandlers,
    configs: ContextKeyConfigMap,
    context: IHandlerContext,
    resolveAccess: IResolveFileAccessHandler,
  ) {
    super(context);
    this.repo = repo;
    this.storage = storage;
    this.configs = configs;
    this.resolveAccess = resolveAccess;
  }

  override get redaction() {
    return UPLOAD_FILE_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    // Look up context key config
    const config = this.configs.get(input.contextKey);
    if (!config) return D2Result.forbidden();

    // Upload permission check
    const accessResult = await this.resolveAccess.handleAsync({
      config,
      action: "upload",
      relatedEntityId: input.relatedEntityId,
    });
    if (!accessResult.success) return D2Result.bubbleFail(accessResult);

    // Validate content type against allowed categories
    if (!isContentTypeAllowed(input.contentType, config.allowedCategories)) {
      return D2Result.validationFailed({ errorCode: "FILES_CONTENT_TYPE_NOT_ALLOWED" });
    }

    // Validate size against config limit
    if (input.sizeBytes > config.maxSizeBytes) {
      return D2Result.payloadTooLarge();
    }

    // Authenticated user required — uploaderUserId tracks who initiated the upload
    const uploaderUserId = this.context.request.userId;
    if (!uploaderUserId) {
      return D2Result.unauthorized();
    }

    // Create file entity
    const file = createFile({
      contextKey: input.contextKey,
      relatedEntityId: input.relatedEntityId,
      uploaderUserId,
      contentType: input.contentType,
      displayName: input.displayName,
      sizeBytes: input.sizeBytes,
      maxSizeBytes: config.maxSizeBytes,
    });

    // Persist to DB
    const createResult = await this.repo.create.handleAsync({ file });
    if (!createResult.success) return D2Result.bubbleFail(createResult);

    // Generate presigned PUT URL
    const storageKey = buildRawStorageKey(file);
    const presignResult = await this.storage.presignPut.handleAsync({
      key: storageKey,
      contentType: input.contentType,
      maxSizeBytes: config.maxSizeBytes,
    });
    if (!presignResult.success) return D2Result.bubbleFail(presignResult);

    return D2Result.ok({
      data: {
        fileId: file.id,
        presignedUrl: presignResult.data!.url,
      },
    });
  }
}
