import { z } from "zod";
import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  DeleteFileRecordInput as I,
  DeleteFileRecordOutput as O,
  IDeleteFileRecordHandler,
} from "@d2/files-app";
import { file } from "../../schema/tables.js";

const schema = z.object({
  id: z.string().min(1).max(36),
}) as unknown as z.ZodType<I>;

export class DeleteFileRecord extends BaseHandler<I, O> implements IDeleteFileRecordHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const rows = await this.db.delete(file).where(eq(file.id, input.id)).returning({ id: file.id });

    if (rows.length === 0) {
      return D2Result.notFound();
    }

    return D2Result.ok({ data: {} });
  }
}
