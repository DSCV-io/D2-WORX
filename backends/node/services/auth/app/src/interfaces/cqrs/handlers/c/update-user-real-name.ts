import type { IHandler, RedactionSpec } from "@d2/handler";

export interface UpdateUserRealNameInput {
  readonly userId: string;
  readonly firstName: string;
  readonly lastName: string;
}

export type UpdateUserRealNameOutput = {
  /** The combined name that was persisted (firstName + lastName). */
  name: string;
};

/** Recommended redaction for UpdateUserRealName handlers. */
export const UPDATE_USER_REAL_NAME_REDACTION: RedactionSpec = {
  inputFields: ["firstName", "lastName"],
};

/** Handler for updating user name (first + last) and syncing to Geo contact. */
export interface IUpdateUserRealNameHandler extends IHandler<
  UpdateUserRealNameInput,
  UpdateUserRealNameOutput
> {
  readonly redaction: RedactionSpec;
}
