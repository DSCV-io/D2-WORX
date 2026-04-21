import type { IHandler, RedactionSpec } from "@d2/handler";

export interface UpdateUserPhoneInput {
  readonly userId: string;
  /** Digits-only E.164 (no `+`, 7-15 digits) — null clears the phone. */
  readonly phone: string | null;
  /** Set true after OTP verification, false on removal. */
  readonly phoneVerified: boolean;
}

export interface UpdateUserPhoneOutput {}

/** `phone` is PII and must NOT appear in handler I/O logs. */
export const UPDATE_USER_PHONE_REDACTION: RedactionSpec = {
  inputFields: ["phone"],
};

export interface IUpdateUserPhoneHandler extends IHandler<UpdateUserPhoneInput, UpdateUserPhoneOutput> {
  readonly redaction: RedactionSpec;
}
