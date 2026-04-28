import type { IHandler, RedactionSpec } from "@d2/handler";

export interface GetUserIdByIdentifierInput {
  /** Lowercase email or username — exactly one required. */
  readonly email?: string;
  readonly username?: string;
}

export interface GetUserIdByIdentifierOutput {
  /** undefined if no user matches (failed sign-in for nonexistent identifier). */
  userId?: string;
}

/** Suppress input/output — both fields are PII used for sign-in audit. */
export const GET_USER_ID_BY_IDENTIFIER_REDACTION: RedactionSpec = {
  inputFields: ["email", "username"],
  suppressOutput: true,
};

export interface IGetUserIdByIdentifierHandler extends IHandler<
  GetUserIdByIdentifierInput,
  GetUserIdByIdentifierOutput
> {
  readonly redaction: RedactionSpec;
}
