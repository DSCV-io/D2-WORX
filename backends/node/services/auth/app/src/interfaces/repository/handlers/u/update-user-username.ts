import type { IHandler, RedactionSpec } from "@d2/handler";

export interface UpdateUserUsernameInput {
  readonly userId: string;
  readonly username: string;
  readonly displayUsername: string;
}

export interface UpdateUserUsernameOutput {}

/** `username` and `displayUsername` are PII and must NOT appear in handler I/O logs. */
export const UPDATE_USER_USERNAME_REDACTION: RedactionSpec = {
  inputFields: ["username", "displayUsername"],
};

export interface IUpdateUserUsernameHandler extends IHandler<
  UpdateUserUsernameInput,
  UpdateUserUsernameOutput
> {
  readonly redaction: RedactionSpec;
}
