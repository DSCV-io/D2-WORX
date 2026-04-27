import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import { z } from "zod";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { FileRepoHandlers } from "../../../../interfaces/repository/handlers/index.js";
import type { FileStorageHandlers } from "../../../../interfaces/providers/storage/handlers/index.js";
import type { ContextKeyConfigMap } from "../../../../context-key-config.js";
import type { IResolveFileAccessHandler } from "../../../../interfaces/cqrs/handlers/u/resolve-file-access.js";
import { buildRawStorageKey, buildVariantStorageKey } from "../../../utils/storage-keys.js";

type Input = Commands.DeleteFileInput;
type Output = Commands.DeleteFileOutput;

const schema = z.object({
  fileId: z.string().min(1).max(255),
});

/**
 * Deletes a file: verifies access, removes all MinIO objects (raw + variants),
 * then deletes the DB record.
 */
export class DeleteFile extends BaseHandler<Input, Output> implements Commands.IDeleteFileHandler {
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

  override get redaction(): RedactionSpec {
    return { suppressOutput: true };
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const findResult = await this.repo.getById.handleAsync({ id: input.fileId });
    if (!findResult.success) return D2Result.bubbleFail(findResult);
    if (!findResult.data?.file) return D2Result.notFound();

    const file = findResult.data.file;

    // Access control — verify the requesting user can delete this file
    const config = this.configs.get(file.contextKey);
    if (!config) return D2Result.forbidden();

    const accessResult = await this.resolveAccess.handleAsync({
      config,
      action: "upload", // delete requires same access level as upload (owner/org)
      relatedEntityId: file.relatedEntityId,
    });
    if (!accessResult.success) return D2Result.bubbleFail(accessResult);

    // Collect all storage keys to delete
    const keysToDelete: string[] = [buildRawStorageKey(file)];
    if (file.variants) {
      for (const variant of file.variants) {
        keysToDelete.push(buildVariantStorageKey(file, variant.size, variant.contentType));
      }
    }

    // Delete from storage (silently ignores missing keys)
    const deleteResult = await this.storage.deleteMany.handleAsync({ keys: keysToDelete });
    if (!deleteResult.success) return D2Result.bubbleFail(deleteResult);

    // Delete DB record
    const repoDeleteResult = await this.repo.delete.handleAsync({ id: file.id });
    if (!repoDeleteResult.success) return D2Result.bubbleFail(repoDeleteResult);

    return D2Result.ok({ data: {} });
  }
}
