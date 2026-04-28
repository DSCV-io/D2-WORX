import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { z } from "zod";
import { Queries } from "../../../../interfaces/cqrs/handlers/index.js";
import { GET_FILE_METADATA_REDACTION } from "../../../../interfaces/cqrs/handlers/q/get-file-metadata.js";
import type { FileRepoHandlers } from "../../../../interfaces/repository/handlers/index.js";
import type { ContextKeyConfigMap } from "../../../../context-key-config.js";
import type { IResolveFileAccessHandler } from "../../../../interfaces/cqrs/handlers/u/resolve-file-access.js";

type Input = Queries.GetFileMetadataInput;
type Output = Queries.GetFileMetadataOutput;

const schema = z.object({
  fileId: z.string().min(1).max(255),
});

export class GetFileMetadata
  extends BaseHandler<Input, Output>
  implements Queries.IGetFileMetadataHandler
{
  private readonly repo: FileRepoHandlers;
  private readonly configs: ContextKeyConfigMap;
  private readonly resolveAccess: IResolveFileAccessHandler;

  constructor(
    repo: FileRepoHandlers,
    configs: ContextKeyConfigMap,
    context: IHandlerContext,
    resolveAccess: IResolveFileAccessHandler,
  ) {
    super(context);
    this.repo = repo;
    this.configs = configs;
    this.resolveAccess = resolveAccess;
  }

  override get redaction() {
    return GET_FILE_METADATA_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const findResult = await this.repo.getById.handleAsync({ id: input.fileId });
    if (!findResult.success) return D2Result.bubbleFail(findResult);
    if (!findResult.data?.file) return D2Result.notFound();

    const file = findResult.data.file;
    const config = this.configs.get(file.contextKey);
    if (!config) return D2Result.forbidden();

    const accessResult = await this.resolveAccess.handleAsync({
      config,
      action: "read",
      relatedEntityId: file.relatedEntityId,
    });
    if (!accessResult.success) return D2Result.bubbleFail(accessResult);

    return D2Result.ok({ data: { file } });
  }
}
