import type { IHandler, RedactionSpec } from "@d2/handler";

export interface RequestPhoneChangeInput {
  readonly userId: string;
  /** Digits-only E.164 (no `+`, 7-15 digits). Frontend strips formatting before sending. */
  readonly newPhone: string;
  readonly currentPassword: string;
}

export interface RequestPhoneChangeOutput {
  readonly expiresAt: Date;
}

export const REQUEST_PHONE_CHANGE_REDACTION: RedactionSpec = {
  inputFields: ["newPhone", "currentPassword"],
  suppressOutput: false,
};

export interface IRequestPhoneChangeHandler extends IHandler<
  RequestPhoneChangeInput,
  RequestPhoneChangeOutput
> {
  readonly redaction: RedactionSpec;
}
