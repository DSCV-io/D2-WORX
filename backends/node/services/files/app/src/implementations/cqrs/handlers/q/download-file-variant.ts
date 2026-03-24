import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { z } from "zod";
import { cleanDisplayStr } from "@d2/utilities";
import { Queries } from "../../../../interfaces/cqrs/handlers/index.js";
import { DOWNLOAD_FILE_VARIANT_REDACTION } from "../../../../interfaces/cqrs/handlers/q/download-file-variant.js";
import type { IFindFileByIdHandler } from "../../../../interfaces/repository/handlers/index.js";
import type { ContextKeyConfigMap } from "../../../../context-key-config.js";
import type { IResolveFileAccessHandler } from "../../../../interfaces/cqrs/handlers/u/resolve-file-access.js";
import type { IGetStorageObject } from "../../../../interfaces/providers/storage/handlers/get-storage-object.js";
import { buildVariantStorageKey } from "../../../utils/storage-keys.js";

type Input = Queries.DownloadFileVariantInput;
type Output = Queries.DownloadFileVariantOutput;

const schema = z.object({
  fileId: z.string().min(1).max(255),
  variantName: z.string().min(1).max(64),
});

/**
 * Downloads a file variant's content after verifying access.
 *
 * Orchestrates: file lookup → access check → variant validation → stream from storage.
 * Returns the raw buffer, content type, and sanitized display name for HTTP response
 * formatting by the API layer.
 */
export class DownloadFileVariant
  extends BaseHandler<Input, Output>
  implements Queries.IDownloadFileVariantHandler
{
  private readonly findById: IFindFileByIdHandler;
  private readonly configs: ContextKeyConfigMap;
  private readonly resolveAccess: IResolveFileAccessHandler;
  private readonly getStorage: IGetStorageObject;

  constructor(
    findById: IFindFileByIdHandler,
    configs: ContextKeyConfigMap,
    context: IHandlerContext,
    resolveAccess: IResolveFileAccessHandler,
    getStorage: IGetStorageObject,
  ) {
    super(context);
    this.findById = findById;
    this.configs = configs;
    this.resolveAccess = resolveAccess;
    this.getStorage = getStorage;
  }

  override get redaction() {
    return DOWNLOAD_FILE_VARIANT_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    // Look up file
    const findResult = await this.findById.handleAsync({ id: input.fileId });
    if (!findResult.success) return D2Result.bubbleFail(findResult);
    if (!findResult.data?.file) return D2Result.notFound();

    const file = findResult.data.file;

    // Only serve ready files
    if (file.status !== "ready") return D2Result.notFound();

    // Check read access
    const config = this.configs.get(file.contextKey);
    if (!config) return D2Result.forbidden();

    const accessResult = await this.resolveAccess.handleAsync({
      config,
      action: "read",
      relatedEntityId: file.relatedEntityId,
    });
    if (!accessResult.success) return D2Result.bubbleFail(accessResult);

    // Verify variant exists
    const variant = file.variants?.find((v) => v.size === input.variantName);
    if (!variant) return D2Result.notFound();

    // Build storage key and fetch content
    const storageKey = buildVariantStorageKey(
      {
        id: file.id,
        contextKey: file.contextKey,
        relatedEntityId: file.relatedEntityId,
      },
      input.variantName,
      variant.contentType,
    );

    const storageResult = await this.getStorage.handleAsync({ key: storageKey });
    if (!storageResult.success || !storageResult.data) return D2Result.notFound();

    const displayName = cleanDisplayStr(file.displayName)?.slice(0, 255) ?? "download";

    return D2Result.ok({
      data: {
        buffer: storageResult.data.buffer,
        contentType: variant.contentType,
        displayName,
      },
    });
  }
}
