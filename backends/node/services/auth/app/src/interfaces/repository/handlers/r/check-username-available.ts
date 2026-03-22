import type { IHandler } from "@d2/handler";

export interface CheckUsernameAvailableInput {
  /** The username to check (case-insensitive). */
  readonly username: string;
}

export interface CheckUsernameAvailableOutput {
  readonly available: boolean;
}

export type ICheckUsernameAvailableHandler = IHandler<
  CheckUsernameAvailableInput,
  CheckUsernameAvailableOutput
>;
