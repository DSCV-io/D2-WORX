import type { IHandler } from "@d2/handler";

export interface UpdateUserPhoneInput {
  readonly userId: string;
  /** Digits-only E.164 (no `+`, 7-15 digits) — null clears the phone. */
  readonly phone: string | null;
  /** Set true after OTP verification, false on removal. */
  readonly phoneVerified: boolean;
}

export interface UpdateUserPhoneOutput {}

export type IUpdateUserPhoneHandler = IHandler<UpdateUserPhoneInput, UpdateUserPhoneOutput>;
