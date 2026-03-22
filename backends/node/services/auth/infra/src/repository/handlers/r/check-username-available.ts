import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  CheckUsernameAvailableInput as I,
  CheckUsernameAvailableOutput as O,
  ICheckUsernameAvailableHandler,
} from "@d2/auth-app";
import { user } from "../../schema/better-auth-tables.js";

export class CheckUsernameAvailable
  extends BaseHandler<I, O>
  implements ICheckUsernameAvailableHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    // Username column stores lowercased values — app layer normalizes before saving.
    const rows = await this.db
      .select({ id: user.id })
      .from(user)
      .where(eq(user.username, input.username.toLowerCase()))
      .limit(1);

    return D2Result.ok({ data: { available: rows.length === 0 } });
  }
}
