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

/** Recommended redaction for RecordSignInEvent handlers. */
export const RECORD_SIGN_IN_EVENT_REDACTION: RedactionSpec = {
  inputFields: ["ipAddress", "userAgent"],
  suppressOutput: true,
};

/** Handler for recording sign-in events. Requires redaction (I/O contains PII). */
export interface IRecordSignInEventHandler extends IHandler<
  RecordSignInEventInput,
  RecordSignInEventOutput
> {
  readonly redaction: RedactionSpec;
}
