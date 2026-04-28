import type { IHandler, RedactionSpec } from "@d2/handler";

export interface RequestUserDeletionInput {
  readonly userId: string;
  readonly currentPassword: string;
  /** Optional flexible blob persisted with the deletion request. Both fields free-text. */
  readonly feedback?: { reason?: string; comment?: string };
}

export interface RequestUserDeletionOutput {
  /** ISO date string when the account will be permanently anonymized if the user doesn't sign back in. */
  readonly scheduledFor: string;
}

/**
 * Output is non-PII (just a date), but input carries the password — must be
 * suppressed in logs. RedactionSpec also covers the optional feedback (which
 * is user-supplied free text and may contain PII like names or grievances).
 */
export const REQUEST_USER_DELETION_REDACTION: RedactionSpec = {
  inputFields: ["currentPassword", "feedback"],
};

export interface IRequestUserDeletionHandler extends IHandler<
  RequestUserDeletionInput,
  RequestUserDeletionOutput
> {
  readonly redaction: RedactionSpec;
}
