import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import {
  UPDATE_USER_IMAGE_REDACTION,
  type UpdateUserImageInput as I,
  type UpdateUserImageOutput as O,
  type IUpdateUserImageHandler,
} from "@d2/auth-app";
import { user } from "../../schema/better-auth-tables.js";

export class UpdateUserImage extends BaseHandler<I, O> implements IUpdateUserImageHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  override get redaction(): RedactionSpec {
    return UPDATE_USER_IMAGE_REDACTION;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    // `clear: true`  → write NULL (remove avatar)
    // `clear: false` → write the supplied `image` string
    const nextImage = input.clear ? null : (input.image ?? null);
    const rows = await this.db
      .update(user)
      .set({ image: nextImage, updatedAt: new Date() })
      .where(eq(user.id, input.userId))
      .returning({ id: user.id });

    if (rows.length === 0) return D2Result.notFound();

    return D2Result.ok({ data: {} });
  }
}
