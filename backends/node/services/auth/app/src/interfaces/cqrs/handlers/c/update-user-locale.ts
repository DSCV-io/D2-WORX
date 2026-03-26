import type { IHandler } from "@d2/handler";

export interface UpdateUserLocaleInput {
  readonly userId: string;
  readonly locale: string;
}

export interface UpdateUserLocaleOutput {}

export type IUpdateUserLocaleHandler = IHandler<UpdateUserLocaleInput, UpdateUserLocaleOutput>;
