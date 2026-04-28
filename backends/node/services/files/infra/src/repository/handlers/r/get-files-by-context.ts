import { z } from "zod";
import { and, eq, count, sql } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  GetFilesByContextInput as I,
  GetFilesByContextOutput as O,
  IGetFilesByContextHandler,
} from "@d2/files-app";
import { file } from "../../schema/tables.js";
import { toFile } from "../../mappers/file-mapper.js";

const DEFAULT_LIMIT = 50;
const MAX_LIMIT = 100;

// Schema validates basic shape; the handler clamps `limit` to MAX_LIMIT
// internally, so the schema accepts any positive integer for backwards
// compatibility with callers that pass over-cap values.
const schema = z.object({
  contextKey: z.string().min(1).max(100),
  relatedEntityId: z.string().min(1).max(255),
  limit: z.number().int().positive().optional(),
  offset: z.number().int().nonnegative().optional(),
}) as unknown as z.ZodType<I>;

export class GetFilesByContext extends BaseHandler<I, O> implements IGetFilesByContextHandler {
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

    const limit = Math.min(input.limit ?? DEFAULT_LIMIT, MAX_LIMIT);
    const offset = input.offset ?? 0;

    const where = and(
      eq(file.contextKey, input.contextKey),
      eq(file.relatedEntityId, input.relatedEntityId),
    );

    const [rows, countResult] = await Promise.all([
      this.db
        .select()
        .from(file)
        .where(where)
        .limit(limit)
        .offset(offset)
        // Tiebreaker on id: when two files are created in the same ms (UUIDv7
        // shares the 48-bit timestamp prefix), createdAt alone is ambiguous.
        // Sorting by id DESC as well gives a stable, deterministic order to
        // both callers and tests.
        .orderBy(sql`${file.createdAt} DESC, ${file.id} DESC`),
      this.db.select({ total: count() }).from(file).where(where),
    ]);

    return D2Result.ok({
      data: {
        files: rows.map(toFile),
        total: countResult[0]?.total ?? 0,
      },
    });
  }
}
