import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  DeleteAllUserSessionsInput as I,
  DeleteAllUserSessionsOutput as O,
  IDeleteAllUserSessionsHandler,
} from "@d2/auth-app";
import { session } from "../../schema/better-auth-tables.js";

/**
 * Hard-deletes every session row for a user.
 *
 * Used by `RequestUserDeletion` at initiate time to immediately log the user
 * out everywhere — they need to actively re-sign-in (which triggers the
 * cancel flow if they change their mind during grace).
 *
 * Note: the BetterAuth Redis secondary storage cache must be invalidated
 * separately via `InvalidateUserSessionCache` so cookie-cached sessions on
 * other devices stop resolving immediately. The DB DELETE alone leaves a
 * window of up to `cookieCacheMaxAge` (5 min) where stale cookies still pass.
 */
export class DeleteAllUserSessions
  extends BaseHandler<I, O>
  implements IDeleteAllUserSessionsHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const rows = await this.db
      .delete(session)
      .where(eq(session.userId, input.userId))
      .returning({ id: session.id });

    return D2Result.ok({ data: { rowsAffected: rows.length } });
  }
}
