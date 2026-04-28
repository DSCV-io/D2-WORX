import type { IHandler } from "@d2/handler";

export interface UpdateUserTimezoneInput {
  readonly userId: string;
  readonly timezone: string;
}

export interface UpdateUserTimezoneOutput {}

export type IUpdateUserTimezoneHandler = IHandler<
  UpdateUserTimezoneInput,
  UpdateUserTimezoneOutput
>;
