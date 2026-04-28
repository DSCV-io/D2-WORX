import type { IHandler, RedactionSpec } from "@d2/handler";

export interface RequestEmailChangeInput {
  readonly userId: string;
  readonly newEmail: string;
  readonly currentPassword: string;
}

export interface RequestEmailChangeOutput {
  /** ISO timestamp — when the OTP code expires (frontend uses this for countdown). */
  readonly expiresAt: Date;
}

export const REQUEST_EMAIL_CHANGE_REDACTION: RedactionSpec = {
  inputFields: ["newEmail", "currentPassword"],
  suppressOutput: false,
};

export interface IRequestEmailChangeHandler extends IHandler<
  RequestEmailChangeInput,
  RequestEmailChangeOutput
> {
  readonly redaction: RedactionSpec;
}
