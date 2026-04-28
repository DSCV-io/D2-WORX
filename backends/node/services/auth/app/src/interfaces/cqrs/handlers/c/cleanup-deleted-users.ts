import type { IHandler } from "@d2/handler";

export interface CleanupDeletedUsersInput {}

export interface CleanupDeletedUsersOutput {
  /** Number of eligible user ids returned by the find query. */
  readonly processed: number;
  /** Number successfully anonymized this run. */
  readonly anonymized: number;
  /** processed - anonymized — covers concurrent cancellations + per-user failures. */
  readonly skipped: number;
  /** False when the distributed lock was already held by another instance. */
  readonly lockAcquired: boolean;
  readonly durationMs: number;
  /**
   * Alias for `anonymized` — required by the shared `JobRpcOutput` contract
   * so this handler plugs into the standard `handleJobRpc` proto envelope.
   */
  readonly rowsAffected: number;
}

/** Job orchestrator for the nightly user-deletion purge. */
export type ICleanupDeletedUsersHandler = IHandler<
  CleanupDeletedUsersInput,
  CleanupDeletedUsersOutput
>;
