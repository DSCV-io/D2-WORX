import type { IHandler, RedactionSpec } from "@d2/handler";

export interface UpdateUserEmailInput {
  readonly userId: string;
  readonly email: string;
  /** Set true after OTP verification, false otherwise. */
  readonly emailVerified: boolean;
}

export interface UpdateUserEmailOutput {}

/** `email` is PII and must NOT appear in handler I/O logs. */
export const UPDATE_USER_EMAIL_REDACTION: RedactionSpec = {
  inputFields: ["email"],
};

export interface IUpdateUserEmailHandler extends IHandler<UpdateUserEmailInput, UpdateUserEmailOutput> {
  readonly redaction: RedactionSpec;
}
