import type { IHandler, RedactionSpec } from "@d2/handler";

export interface UpdateUsernameInput {
  readonly userId: string;
  readonly username: string;
}

export type UpdateUsernameOutput = {
  username: string;
  displayUsername: string;
};

/** Recommended redaction for UpdateUsername handlers. */
export const UPDATE_USERNAME_REDACTION: RedactionSpec = {
  inputFields: ["username"],
};

/** Handler for updating a user's username. Requires redaction (input contains PII). */
export interface IUpdateUsernameHandler extends IHandler<
  UpdateUsernameInput,
  UpdateUsernameOutput
> {
  readonly redaction: RedactionSpec;
}
