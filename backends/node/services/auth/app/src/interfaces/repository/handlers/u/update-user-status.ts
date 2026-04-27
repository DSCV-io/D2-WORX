import type { IHandler, RedactionSpec } from "@d2/handler";
import type { UserStatus } from "@d2/auth-domain";

/**
 * Updates the lifecycle status of a user (active → pending_deletion → deleted)
 * with optional grace-clock and feedback mutations.
 *
 * Three-state semantics for the optional columns (`deletedAt`,
 * `deletionFeedback`):
 *   - field omitted (undefined)  → don't touch the column
 *   - field defined + clear flag false → write the supplied value
 *   - clear flag true → write NULL to the column (the value field is ignored)
 *
 * Mirrors the M22 `clear: boolean` pattern — explicit clear avoids the
 * `null`-as-data ambiguity. Each clearable column gets its own boolean
 * because their lifecycles differ (cancel sets `clearDeletedAt: true` but
 * leaves `deletionFeedback` untouched for analytics).
 */
export interface UpdateUserStatusInput {
  readonly userId: string;
  readonly status: UserStatus;
  /**
   * Set the grace clock when transitioning to `pending_deletion`. Omit to
   * leave the column unchanged. Use `clearDeletedAt: true` to clear the
   * column (e.g., on cancel/restore) — when `clearDeletedAt` is true this
   * field is ignored.
   */
  readonly deletedAt?: Date;
  /** When `true`, sets `deletedAt` to NULL regardless of the `deletedAt` value. */
  readonly clearDeletedAt?: boolean;
  /**
   * Optional flexible blob recorded with the deletion request. Omit to leave
   * the column unchanged (pass-through on cancel). Use
   * `clearDeletionFeedback: true` to scrub the column.
   */
  readonly deletionFeedback?: Record<string, unknown>;
  /** When `true`, sets `deletionFeedback` to NULL regardless of the value field. */
  readonly clearDeletionFeedback?: boolean;
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

export interface IUpdateUserStatusHandler extends IHandler<
  UpdateUserStatusInput,
  UpdateUserStatusOutput
> {
  readonly redaction: RedactionSpec;
}
