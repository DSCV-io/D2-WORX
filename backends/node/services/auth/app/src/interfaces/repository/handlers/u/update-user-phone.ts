import type { IHandler, RedactionSpec } from "@d2/handler";

/**
 * Sets or clears the user's phone (and `phoneVerified` flag) atomically.
 *
 * Pattern: explicit `clear: boolean` separates "set to a value" from "remove
 * the value entirely" — avoids the `null`-as-data ambiguity. When `clear` is
 * `true`, `phone` is ignored and the DB column is set to `NULL`. When `clear`
 * is `false`, `phone` MUST be a defined digits-only E.164 string (no `+`,
 * 7-15 digits) and is written verbatim. Chosen over splitting into
 * `SetUserPhone` / `ClearUserPhone` to keep one DI key + one repo handler
 * for what is fundamentally one column mutation.
 *
 * Callers should validate phone uniqueness BEFORE calling — the handler
 * returns 409 on collision (partial unique index).
 */
export interface UpdateUserPhoneInput {
  readonly userId: string;
  /**
   * Digits-only E.164 (no `+`, 7-15 digits). Required when `clear` is
   * `false`; ignored when `clear` is `true`.
   */
  readonly phone?: string;
  /** Set true after OTP verification, false on removal. */
  readonly phoneVerified: boolean;
  /** `true` to set phone to NULL (remove phone); `false` to write `phone`. */
  readonly clear: boolean;
}

export interface UpdateUserPhoneOutput {}

/** `phone` is PII and must NOT appear in handler I/O logs. */
export const UPDATE_USER_PHONE_REDACTION: RedactionSpec = {
  inputFields: ["phone"],
};

export interface IUpdateUserPhoneHandler extends IHandler<
  UpdateUserPhoneInput,
  UpdateUserPhoneOutput
> {
  readonly redaction: RedactionSpec;
}
