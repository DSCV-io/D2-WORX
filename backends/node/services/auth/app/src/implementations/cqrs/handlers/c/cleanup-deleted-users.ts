import { randomUUID } from "node:crypto";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { DistributedCache } from "@d2/interfaces";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { IGetDeletedUsersToPurgeHandler } from "../../../../interfaces/repository/handlers/r/get-deleted-users-to-purge.js";
import type { AuthJobOptions } from "../../../../auth-job-options.js";

type Input = Commands.CleanupDeletedUsersInput;
type Output = Commands.CleanupDeletedUsersOutput;

const LOCK_KEY = "lock:job:cleanup-deleted-users";

/**
 * Nightly job orchestrator for the post-grace-period user purge.
 *
 * Runs once per scheduler tick. Holds a distributed lock so multiple Auth
 * instances don't pile on; if the lock is held, returns `lockAcquired: false`
 * with zero counts (Dkron retries on its next tick).
 *
 * Workload shape:
 *   1. GetDeletedUsersToPurge cursor-pages internally → flat string[] of
 *      every eligible user id (status='pending_deletion' AND deleted_at <
 *      now - graceCutoffMs). Bounded memory via DEFAULT_BATCH_SIZE per chunk;
 *      the caller sees a single flat list.
 *   2. Promise.all over FinalizeDeletedUser per id. We rely on the pg pool's
 *      built-in queueing — no semaphore. Each finalize is its own short tx.
 *   3. processed = ids.length; anonymized = count where data.anonymized=true;
 *      skipped = processed - anonymized (covers users who signed back in
 *      between find + finalize, plus per-user failures).
 *
 * Failure isolation: a single FinalizeDeletedUser failure does NOT abort
 * the run — we count it under `skipped` and continue. The pending row stays
 * eligible and the next nightly run will retry.
 */
export class CleanupDeletedUsers
  extends BaseHandler<Input, Output>
  implements Commands.ICleanupDeletedUsersHandler
{
  constructor(
    private readonly acquireLock: DistributedCache.IAcquireLockHandler,
    private readonly releaseLock: DistributedCache.IReleaseLockHandler,
    private readonly getDeletedUsersToPurge: IGetDeletedUsersToPurgeHandler,
    private readonly finalizeDeletedUser: Commands.IFinalizeDeletedUserHandler,
    private readonly options: AuthJobOptions,
    context: IHandlerContext,
  ) {
    super(context);
  }

  protected async executeAsync(_input: Input): Promise<D2Result<Output | undefined>> {
    const start = performance.now();
    const lockId = randomUUID();

    const lockResult = await this.acquireLock.handleAsync({
      key: LOCK_KEY,
      lockId,
      expirationMs: this.options.jobLockTtlMs,
    });

    if (!lockResult.success || !lockResult.data?.acquired) {
      return D2Result.ok({
        data: {
          processed: 0,
          anonymized: 0,
          skipped: 0,
          lockAcquired: false,
          durationMs: Math.round(performance.now() - start),
          rowsAffected: 0,
        },
      });
    }

    try {
      const graceCutoff = new Date(Date.now() - this.options.userDeletionGracePeriodMs);
      const purgeListResult = await this.getDeletedUsersToPurge.handleAsync({ graceCutoff });
      if (!purgeListResult.success) return D2Result.bubbleFail(purgeListResult);
      const ids = purgeListResult.data?.userIds ?? [];

      if (ids.length === 0) {
        return D2Result.ok({
          data: {
            processed: 0,
            anonymized: 0,
            skipped: 0,
            lockAcquired: true,
            durationMs: Math.round(performance.now() - start),
            rowsAffected: 0,
          },
        });
      }

      // Failure isolation: convert per-user failures into "skipped" rather
      // than failing the whole run. Promise.all on the wrapped values.
      const results = await Promise.all(
        ids.map(async (userId) => {
          try {
            const r = await this.finalizeDeletedUser.handleAsync({ userId });
            return r.success && r.data?.anonymized === true;
          } catch (err: unknown) {
            this.context.logger.error("CleanupDeletedUsers: per-user finalize threw", {
              userId,
              error: err instanceof Error ? err.message : String(err),
            });
            return false;
          }
        }),
      );

      const anonymized = results.filter(Boolean).length;
      const processed = ids.length;
      return D2Result.ok({
        data: {
          processed,
          anonymized,
          skipped: processed - anonymized,
          lockAcquired: true,
          durationMs: Math.round(performance.now() - start),
          rowsAffected: anonymized,
        },
      });
    } finally {
      await this.releaseLock.handleAsync({ key: LOCK_KEY, lockId }).catch(() => {
        // Lock release is best-effort — TTL will reclaim it.
      });
    }
  }
}

export type {
  CleanupDeletedUsersInput,
  CleanupDeletedUsersOutput,
} from "../../../../interfaces/cqrs/handlers/c/cleanup-deleted-users.js";
