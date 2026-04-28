import { z } from "zod";
import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  GetFileByIdInput as I,
  GetFileByIdOutput as O,
  IGetFileByIdHandler,
} from "@d2/files-app";
import { file } from "../../schema/tables.js";
import { toFile } from "../../mappers/file-mapper.js";

const schema = z.object({
  id: z.string().min(1).max(36),
}) as unknown as z.ZodType<I>;

export class GetFileById extends BaseHandler<I, O> implements IGetFileByIdHandler {
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

    const rows = await this.db.select().from(file).where(eq(file.id, input.id)).limit(1);

    const row = rows[0];
    if (!row) {
      return D2Result.notFound();
    }

    return D2Result.ok({ data: { file: toFile(row) } });
  }
}
