import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  UpdateUserNameInput as I,
  UpdateUserNameOutput as O,
  IUpdateUserNameHandler,
} from "@d2/auth-app";
import { user } from "../../schema/better-auth-tables.js";

export class UpdateUserName extends BaseHandler<I, O> implements IUpdateUserNameHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const rows = await this.db
      .update(user)
      .set({ name: input.name, updatedAt: new Date() })
      .where(eq(user.id, input.userId))
      .returning({ id: user.id });

    if (rows.length === 0) return D2Result.notFound();

    return D2Result.ok({ data: {} });
  }
}
