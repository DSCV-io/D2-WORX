import { z } from "zod";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import { isPgUniqueViolation } from "@d2/errors-pg";
import type {
  CreateFileRecordInput as I,
  CreateFileRecordOutput as O,
  ICreateFileRecordHandler,
} from "@d2/files-app";
import { file } from "../../schema/tables.js";

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
}) as unknown as z.ZodType<I>;

export class CreateFileRecord extends BaseHandler<I, O> implements ICreateFileRecordHandler {
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
    try {
      await this.db.insert(file).values({
        id: f.id,
        contextKey: f.contextKey,
        relatedEntityId: f.relatedEntityId,
        uploaderUserId: f.uploaderUserId,
        status: f.status,
        contentType: f.contentType,
        displayName: f.displayName,
        sizeBytes: f.sizeBytes,
        variants: f.variants ?? undefined,
        rejectionReason: f.rejectionReason ?? undefined,
        createdAt: f.createdAt,
      });

      return D2Result.ok({ data: { file: f } });
    } catch (err) {
      if (isPgUniqueViolation(err)) {
        return D2Result.conflict();
      }
      throw err;
    }
  }
}
