import type { IHandler } from "@d2/handler";

export interface UpdateUserNameInput {
  readonly userId: string;
  readonly name: string;
}

export interface UpdateUserNameOutput {}

export type IUpdateUserNameHandler = IHandler<UpdateUserNameInput, UpdateUserNameOutput>;
