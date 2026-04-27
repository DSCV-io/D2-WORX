import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { z } from "zod";
import { Queries } from "../../../../interfaces/cqrs/handlers/index.js";
import { GET_FILE_VARIANT_URL_REDACTION } from "../../../../interfaces/cqrs/handlers/q/get-file-variant-url.js";
import type { IGetFileByIdHandler } from "../../../../interfaces/repository/handlers/index.js";
import type { ContextKeyConfigMap } from "../../../../context-key-config.js";
import type { IResolveFileAccessHandler } from "../../../../interfaces/cqrs/handlers/u/resolve-file-access.js";
import type { IPresignGetUrl } from "../../../../interfaces/providers/storage/handlers/presign-get-url.js";
import { buildVariantStorageKey } from "../../../utils/storage-keys.js";

type Input = Queries.GetFileVariantUrlInput;
type Output = Queries.GetFileVariantUrlOutput;

const schema = z.object({
  fileId: z.string().min(1).max(255),
  variantName: z.string().min(1).max(64),
});

/**
 * Resolves a presigned GET URL for a specific file variant.
 *
 * Orchestrates: file lookup → access check → variant validation → presign GET.
 * The presigned URL is time-limited (1 hour) and points at the browser-reachable
 * S3 endpoint. Browser HTTP cache stores the content immutably.
 */
export class GetFileVariantUrl
  extends BaseHandler<Input, Output>
  implements Queries.IGetFileVariantUrlHandler
{
  private readonly getById: IGetFileByIdHandler;
  private readonly configs: ContextKeyConfigMap;
  private readonly resolveAccess: IResolveFileAccessHandler;
  private readonly presignGet: IPresignGetUrl;

  constructor(
    getById: IGetFileByIdHandler,
    configs: ContextKeyConfigMap,
    context: IHandlerContext,
    resolveAccess: IResolveFileAccessHandler,
    presignGet: IPresignGetUrl,
  ) {
    super(context);
    this.getById = getById;
    this.configs = configs;
    this.resolveAccess = resolveAccess;
    this.presignGet = presignGet;
  }

  override get redaction() {
    return GET_FILE_VARIANT_URL_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    // Look up file
    const findResult = await this.getById.handleAsync({ id: input.fileId });
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

    // Build storage key and presign
    const storageKey = buildVariantStorageKey(
      {
        id: file.id,
        contextKey: file.contextKey,
        relatedEntityId: file.relatedEntityId,
      },
      input.variantName,
      variant.contentType,
    );

    const presignResult = await this.presignGet.handleAsync({ key: storageKey });
    if (!presignResult.success) return D2Result.bubbleFail(presignResult);

    return D2Result.ok({ data: { url: presignResult.data!.url } });
  }
}
