import { z } from "zod";
import { and, eq, lte } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import { FILE_STATUSES } from "@d2/files-domain";
import type {
  GetStaleFilesInput as I,
  GetStaleFilesOutput as O,
  IGetStaleFilesHandler,
} from "@d2/files-app";
import { file } from "../../schema/tables.js";
import { toFile } from "../../mappers/file-mapper.js";

const schema = z.object({
  status: z.enum(FILE_STATUSES),
  cutoffDate: z.date(),
  limit: z.number().int().positive().max(10_000),
}) as unknown as z.ZodType<I>;

export class GetStaleFiles extends BaseHandler<I, O> implements IGetStaleFilesHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  override get redaction(): RedactionSpec {
    return { suppressOutput: true };
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const rows = await this.db
      .select()
      .from(file)
      .where(and(eq(file.status, input.status), lte(file.createdAt, input.cutoffDate)))
      .limit(input.limit);

    return D2Result.ok({ data: { files: rows.map(toFile) } });
  }
}
