import type { IHandler } from "@d2/handler";

export interface UpdateUserEmailInput {
  readonly userId: string;
  readonly email: string;
  /** Set true after OTP verification, false otherwise. */
  readonly emailVerified: boolean;
}

export interface UpdateUserEmailOutput {}

export type IUpdateUserEmailHandler = IHandler<UpdateUserEmailInput, UpdateUserEmailOutput>;
