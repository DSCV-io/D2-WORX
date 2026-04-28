import { USER_DELETION } from "@d2/auth-domain";

export interface AuthJobOptions {
  /** Retention period for sign-in events in days. Default: 90. */
  readonly signInEventRetentionDays: number;
  /** Retention period for expired invitations in days past expiry. Default: 7. */
  readonly invitationRetentionDays: number;
  /** Distributed lock TTL in milliseconds. Default: 300000 (5 min). */
  readonly jobLockTtlMs: number;
  /**
   * Grace period for self-service user deletion. Users in `pending_deletion`
   * older than this are anonymized by `CleanupDeletedUsers`. Default: 30 days.
   */
  readonly userDeletionGracePeriodMs: number;
  /**
   * Defense-in-depth cap on the number of users `GetDeletedUsersToPurge`
   * returns per nightly tick. Hitting this cap means the downstream finalize
   * is failing and rows are accumulating — the handler logs a warning and the
   * next tick absorbs whatever's left. Default: 50000.
   */
  readonly userPurgeBatchSize: number;
}

export const DEFAULT_AUTH_JOB_OPTIONS: AuthJobOptions = {
  signInEventRetentionDays: 90,
  invitationRetentionDays: 7,
  jobLockTtlMs: 300_000,
  userDeletionGracePeriodMs: USER_DELETION.GRACE_PERIOD_MS,
  userPurgeBatchSize: 50_000,
};
