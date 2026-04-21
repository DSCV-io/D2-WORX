/**
 * Lifecycle status for an auth user account.
 *
 * Drives the self-service deletion flow:
 *   - `active`           : normal usable account.
 *   - `pending_deletion` : user clicked Delete; in 30-day grace. Sessions are
 *                          revoked but signing back in flips this to `active`
 *                          (via the session.create.before hook).
 *   - `deleted`          : grace expired and the row has been anonymized.
 *                          Tombstone row — sign-in is blocked.
 *
 * Stored as plain text in the DB (not a PG enum) — matches how OrgType / Role
 * are stored, keeps cross-platform parity (.NET reads the same column without
 * needing PG enum bindings), and avoids enum-altering migrations.
 *
 * Banned (BetterAuth admin plugin's `user.banned` boolean) is orthogonal.
 * Sign-in is blocked if `banned === true` OR `status !== 'active'`.
 */
export const USER_STATUS = {
  ACTIVE: "active",
  PENDING_DELETION: "pending_deletion",
  DELETED: "deleted",
} as const;

export type UserStatus = (typeof USER_STATUS)[keyof typeof USER_STATUS];
