import type { IHandler, RedactionSpec } from "@d2/handler";
import type { UserStatus } from "@d2/auth-domain";

export interface UpdateUserStatusInput {
  readonly userId: string;
  readonly status: UserStatus;
  /** Set the grace clock when transitioning to `pending_deletion`; clear (null) when restoring. */
  readonly deletedAt?: Date | null;
  /** Optional flexible blob recorded with the deletion request. Pass through unchanged on cancel. */
  readonly deletionFeedback?: Record<string, unknown> | null;
  /**
   * Optional CAS guard — when set, the UPDATE only fires if the row's current
   * `status` equals this value. Used by `CancelUserDeletion` to avoid racing
   * the nightly anonymize job (otherwise a fire-and-forget cancel triggered
   * after the row was already DELETED-and-anonymized would resurrect a
   * tombstone). When the guard misses, `updated: false` is returned and the
   * caller no-ops.
   */
  readonly expectedStatus?: UserStatus;
}

export interface UpdateUserStatusOutput {
  /** True if a row was updated; false if no user matched the id. */
  readonly updated: boolean;
}

/**
 * `deletionFeedback` is user-supplied free text (`reason` ≤ 200, `comment` ≤
 * 2000). Treat it as PII — must NOT appear in handler I/O logs. `userId` and
 * `status` / `deletedAt` / `expectedStatus` are opaque/enum, not redacted.
 */
export const UPDATE_USER_STATUS_REDACTION: RedactionSpec = {
  inputFields: ["deletionFeedback"],
};

export interface IUpdateUserStatusHandler
  extends IHandler<UpdateUserStatusInput, UpdateUserStatusOutput> {
  readonly redaction: RedactionSpec;
}
