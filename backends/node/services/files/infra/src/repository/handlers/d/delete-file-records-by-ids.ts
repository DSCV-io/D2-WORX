import { z } from "zod";
import { inArray } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  DeleteFileRecordsByIdsInput as I,
  DeleteFileRecordsByIdsOutput as O,
  IDeleteFileRecordsByIdsHandler,
} from "@d2/files-app";
import { file } from "../../schema/tables.js";

const schema = z.object({
  ids: z.array(z.string().min(1).max(36)).max(1000),
}) as unknown as z.ZodType<I>;

export class DeleteFileRecordsByIds
  extends BaseHandler<I, O>
  implements IDeleteFileRecordsByIdsHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    if (input.ids.length === 0) {
      return D2Result.ok({ data: { rowsAffected: 0 } });
    }

    const rows = await this.db
      .delete(file)
      .where(inArray(file.id, [...input.ids]))
      .returning({ id: file.id });

    return D2Result.ok({ data: { rowsAffected: rows.length } });
  }
}
