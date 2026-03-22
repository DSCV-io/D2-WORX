import type { IHandler } from "@d2/handler";

export interface UpdateUserUsernameInput {
  readonly userId: string;
  readonly username: string;
  readonly displayUsername: string;
}

export interface UpdateUserUsernameOutput {}

export type IUpdateUserUsernameHandler = IHandler<
  UpdateUserUsernameInput,
  UpdateUserUsernameOutput
>;
