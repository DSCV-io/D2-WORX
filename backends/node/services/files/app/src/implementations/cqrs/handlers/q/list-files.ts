import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { z } from "zod";
import { Queries } from "../../../../interfaces/cqrs/handlers/index.js";
import { LIST_FILES_REDACTION } from "../../../../interfaces/cqrs/handlers/q/list-files.js";
import type { FileRepoHandlers } from "../../../../interfaces/repository/handlers/index.js";
import type { ContextKeyConfigMap } from "../../../../context-key-config.js";
import type { IResolveFileAccessHandler } from "../../../../interfaces/cqrs/handlers/u/resolve-file-access.js";

type Input = Queries.ListFilesInput;
type Output = Queries.ListFilesOutput;

const DEFAULT_LIMIT = 50;
const MAX_LIMIT = 100;

const schema = z.object({
  contextKey: z.string().min(1).max(100),
  relatedEntityId: z.string().min(1).max(255),
  limit: z.number().int().min(1).max(MAX_LIMIT).optional(),
  offset: z.number().int().min(0).optional(),
});

export class ListFiles extends BaseHandler<Input, Output> implements Queries.IListFilesHandler {
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
    return LIST_FILES_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const config = this.configs.get(input.contextKey);
    if (!config) return D2Result.forbidden();

    const accessResult = await this.resolveAccess.handleAsync({
      config,
      action: "read",
      relatedEntityId: input.relatedEntityId,
    });
    if (!accessResult.success) return D2Result.bubbleFail(accessResult);

    const result = await this.repo.getByContext.handleAsync({
      contextKey: input.contextKey,
      relatedEntityId: input.relatedEntityId,
      limit: input.limit ?? DEFAULT_LIMIT,
      offset: input.offset ?? 0,
    });
    if (!result.success) return D2Result.bubbleFail(result);

    return D2Result.ok({
      data: {
        files: result.data?.files ?? [],
        total: result.data?.total ?? 0,
      },
    });
  }
}
