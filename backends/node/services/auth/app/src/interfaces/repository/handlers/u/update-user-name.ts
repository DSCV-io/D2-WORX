import type { IHandler, RedactionSpec } from "@d2/handler";

export interface UpdateUserNameInput {
  readonly userId: string;
  readonly name: string;
}

export interface UpdateUserNameOutput {}

/** `name` is PII and must NOT appear in handler I/O logs. */
export const UPDATE_USER_NAME_REDACTION: RedactionSpec = {
  inputFields: ["name"],
};

export interface IUpdateUserNameHandler extends IHandler<
  UpdateUserNameInput,
  UpdateUserNameOutput
> {
  readonly redaction: RedactionSpec;
}
