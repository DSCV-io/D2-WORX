import type { IHandler, RedactionSpec } from "@d2/handler";

export interface VerifyEmailChangeInput {
  readonly userId: string;
  readonly code: string;
}

export interface VerifyEmailChangeOutput {
  /** The new email that is now persisted on the user. */
  readonly newEmail: string;
}

export const VERIFY_EMAIL_CHANGE_REDACTION: RedactionSpec = {
  inputFields: ["code"],
  suppressOutput: true,
};

export interface IVerifyEmailChangeHandler extends IHandler<
  VerifyEmailChangeInput,
  VerifyEmailChangeOutput
> {
  readonly redaction: RedactionSpec;
}
