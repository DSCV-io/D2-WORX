/**
 * Repository interface for BetterAuth's verification table.
 *
 * BetterAuth uses this table internally for email-verification, password-reset,
 * and OTP flows. We wrap a subset of its internalAdapter operations so the
 * account-change OTP handlers (Request/Verify Email/Phone) can store and
 * lookup pending change records by structured identifier.
 *
 * Identifier convention (set by `pendingChangeIdentifier()` in @d2/auth-domain):
 *   "account-change:{type}:{userId}"   e.g. "account-change:email:abc-123"
 *
 * Value is JSON-encoded `PendingChangeValue` (codeHash, pendingValue, attempts).
 */

export interface VerificationRecord {
  readonly id: string;
  readonly identifier: string;
  readonly value: string;
  readonly expiresAt: Date;
}

export interface IVerificationStore {
  /** Insert a new verification record. Returns the created record (incl. id). */
  create(input: {
    identifier: string;
    value: string;
    expiresAt: Date;
  }): Promise<VerificationRecord>;

  /** Look up by exact identifier. Returns null if not found. */
  findByIdentifier(identifier: string): Promise<VerificationRecord | null>;

  /** Update the `value` (e.g., to bump the attempt counter). */
  updateValue(id: string, newValue: string): Promise<void>;

  /** Delete a verification record by id. No-op if not found. */
  deleteById(id: string): Promise<void>;
}
