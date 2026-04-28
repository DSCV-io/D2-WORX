import type { IHandler, RedactionSpec } from "@d2/handler";
import type { SignInEvent } from "@d2/auth-domain";

export interface RecordSignInEventInput {
  readonly userId: string;
  readonly successful: boolean;
  readonly ipAddress: string;
  readonly userAgent: string;
  readonly whoIsId?: string;
  readonly deviceFingerprint?: string;
  readonly clientFingerprint?: string;
  readonly serverFingerprint?: string;
  readonly failureReason?: string;
}

export type RecordSignInEventOutput = { event: SignInEvent };

/**
 * Recommended redaction for RecordSignInEvent handlers.
 *
 * Fingerprints are SHA-256 hashes (opaque identifiers, not raw PII) but are
 * derived from PII (UA, IP). Per defense-in-depth + log audit consistency
 * with the source PII fields above, treat them the same — redact in logs.
 */
export const RECORD_SIGN_IN_EVENT_REDACTION: RedactionSpec = {
  inputFields: [
    "ipAddress",
    "userAgent",
    "deviceFingerprint",
    "clientFingerprint",
    "serverFingerprint",
  ],
  suppressOutput: true,
};

/** Handler for recording sign-in events. Requires redaction (I/O contains PII). */
export interface IRecordSignInEventHandler extends IHandler<
  RecordSignInEventInput,
  RecordSignInEventOutput
> {
  readonly redaction: RedactionSpec;
}
