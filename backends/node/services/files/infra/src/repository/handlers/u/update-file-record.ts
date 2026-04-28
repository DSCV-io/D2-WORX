import { z } from "zod";
import { and, eq } from "drizzle-orm";
import type { SQL } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  UpdateFileRecordInput as I,
  UpdateFileRecordOutput as O,
  IUpdateFileRecordHandler,
} from "@d2/files-app";
import { file } from "../../schema/tables.js";
import { toFile } from "../../mappers/file-mapper.js";

const schema = z.object({
  file: z.object({
    id: z.string().min(1).max(36),
    contextKey: z.string().min(1).max(100),
    relatedEntityId: z.string().min(1).max(255),
    uploaderUserId: z.string().min(1).max(36),
    status: z.string().min(1).max(20),
    contentType: z.string().min(1).max(255),
    displayName: z.string().min(1).max(255),
    sizeBytes: z.number().int().nonnegative(),
    variants: z.array(z.unknown()).optional(),
    rejectionReason: z.string().max(50).optional(),
    createdAt: z.date(),
  }),
  expectedStatus: z.string().min(1).max(20).optional(),
}) as unknown as z.ZodType<I>;

export class UpdateFileRecord extends BaseHandler<I, O> implements IUpdateFileRecordHandler {
  override get redaction(): RedactionSpec {
    return { suppressInput: true, suppressOutput: true };
  }

  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const f = input.file;

    const conditions: SQL[] = [eq(file.id, f.id)];
    if (input.expectedStatus) {
      conditions.push(eq(file.status, input.expectedStatus));
    }

    const rows = await this.db
      .update(file)
      .set({
        status: f.status,
        contentType: f.contentType,
        displayName: f.displayName,
        sizeBytes: f.sizeBytes,
        variants: f.variants ?? undefined,
        rejectionReason: f.rejectionReason ?? undefined,
        updatedAt: new Date(),
      })
      .where(and(...conditions))
      .returning();

    const row = rows[0];
    if (!row) {
      return D2Result.notFound();
    }

    return D2Result.ok({ data: { file: toFile(row) } });
  }
}
