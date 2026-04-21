import type { IHandler, RedactionSpec } from "@d2/handler";

export interface AnonymizeUserInput {
  readonly userId: string;
}

export interface AnonymizeUserOutput {
  /**
   * False when the row didn't match the `status='pending_deletion'` guard
   * (already anonymized OR cancelled by sign-in between job find + job
   * finalize). Caller should treat this as a successful no-op.
   */
  readonly anonymized: boolean;
  /**
   * Captured BEFORE the scrub for use by the final notification (sent via
   * `alternativeContactInfo` since Geo will tear down the contact when it
   * consumes the user-anonymize event) and by the fanout payload.
   * Undefined when `anonymized: false`.
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
