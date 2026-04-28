import type { IHandler, RedactionSpec } from "@d2/handler";

export interface VerifyPhoneChangeInput {
  readonly userId: string;
  readonly code: string;
}

export interface VerifyPhoneChangeOutput {
  /** The new phone (digits-only) that is now persisted on the user. */
  readonly phone: string;
}

export const VERIFY_PHONE_CHANGE_REDACTION: RedactionSpec = {
  inputFields: ["code"],
  suppressOutput: true,
};

export interface IVerifyPhoneChangeHandler extends IHandler<
  VerifyPhoneChangeInput,
  VerifyPhoneChangeOutput
> {
  readonly redaction: RedactionSpec;
}
