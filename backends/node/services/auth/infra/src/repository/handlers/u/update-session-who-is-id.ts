import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  UpdateSessionWhoIsIdInput as I,
  UpdateSessionWhoIsIdOutput as O,
  IUpdateSessionWhoIsIdHandler,
} from "@d2/auth-app";
import { session } from "../../schema/better-auth-tables.js";

export class UpdateSessionWhoIsId
  extends BaseHandler<I, O>
  implements IUpdateSessionWhoIsIdHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const rows = await this.db
      .update(session)
      .set({ whoIsId: input.whoIsId })
      .where(eq(session.id, input.id))
      .returning({ id: session.id });

    if (rows.length === 0) return D2Result.notFound();

    return D2Result.ok({ data: {} });
  }
}
