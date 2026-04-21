import { and, eq, lt } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { USER_STATUS } from "@d2/auth-domain";
import type {
  FindDeletedUsersToPurgeInput as I,
  FindDeletedUsersToPurgeOutput as O,
  IFindDeletedUsersToPurgeHandler,
} from "@d2/auth-app";
import { user } from "../../schema/better-auth-tables.js";

/**
 * Returns every user id whose grace clock has expired and is still pending
 * deletion. Backed by the partial index `user_pending_deletion_idx ON
 * deleted_at WHERE status='pending_deletion'`, so the scan only touches
 * eligible rows regardless of total user count.
 *
 * Realistic data shape: nightly job, expected return size in the hundreds
 * (users who initiated deletion exactly 30+ days ago and never signed back
 * in to cancel). A single query is correct.
 *
 * The `MAX_PURGE_BATCH` cap is defense-in-depth — if it ever fires, the
 * downstream FinalizeDeletedUser is failing silently and rows are piling
 * up. The warn surfaces it; the next nightly tick absorbs whatever's left.
 */
const MAX_PURGE_BATCH = 50_000;

export class FindDeletedUsersToPurge
  extends BaseHandler<I, O>
  implements IFindDeletedUsersToPurgeHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const rows = await this.db
      .select({ id: user.id })
      .from(user)
      .where(
        and(
          eq(user.status, USER_STATUS.PENDING_DELETION),
          lt(user.deletedAt, input.graceCutoff),
        ),
      )
      .limit(MAX_PURGE_BATCH);

    if (rows.length === MAX_PURGE_BATCH) {
      this.context.logger.warn(
        "FindDeletedUsersToPurge: hit MAX_PURGE_BATCH cap — deletion is likely broken upstream and rows are accumulating",
        { cap: MAX_PURGE_BATCH },
      );
    }

    return D2Result.ok({ data: { userIds: rows.map((r) => r.id) } });
  }
}
