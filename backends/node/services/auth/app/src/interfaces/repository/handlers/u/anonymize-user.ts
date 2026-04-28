import type { IHandler, RedactionSpec } from "@d2/handler";

export interface AnonymizeUserInput {
  readonly userId: string;
}

export interface AnonymizeUserOutput {
  /**
   * False when the anonymization didn't run. Two reasons:
   *
   *   - Status guard miss: row no longer `pending_deletion` (already
   *     anonymized OR cancelled by sign-in between job find + finalize).
   *     `autoCancelledSoleOwner` is undefined.
   *
   *   - Auto-cancelled because the user became sole owner of one or more
   *     orgs during the grace window (TOCTOU between RequestUserDeletion
   *     and grace expiry). The transaction flips the row back to ACTIVE
   *     and clears `deletedAt`; `autoCancelledSoleOwner` is populated so
   *     the caller can notify the user. Anonymization is forfeit — they
   *     must re-request after transferring ownership.
   */
  readonly anonymized: boolean;
  /**
   * When set, the anonymization was auto-cancelled because the user is now
   * the sole owner of these orgs. The user record has been flipped back
   * to ACTIVE atomically. Caller should notify the user via the
   * "deletion auto-cancelled" email template.
   */
  readonly autoCancelledSoleOwner?: {
    readonly soleOwnerOrgIds: readonly string[];
  };
  /**
   * Captured BEFORE the scrub (or before the auto-cancel) for use by the
   * caller's notification email. Set whenever the user row exists, regardless
   * of which outcome path ran.
   */
  readonly originalEmail?: string;
  readonly originalName?: string;
}

/** Output carries the user's original email + name (PII) — must be suppressed in logs. */
export const ANONYMIZE_USER_REDACTION: RedactionSpec = {
  suppressOutput: true,
};

export interface IAnonymizeUserHandler extends IHandler<AnonymizeUserInput, AnonymizeUserOutput> {
  readonly redaction: RedactionSpec;
}
