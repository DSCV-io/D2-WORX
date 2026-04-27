import type { IHandler, RedactionSpec } from "@d2/handler";

export interface CheckUsernameAvailableInput {
  /** The username to check (case-insensitive). */
  readonly username: string;
}

export interface CheckUsernameAvailableOutput {
  readonly available: boolean;
}

/** `username` is PII (user-chosen identifier) — redact from logs. */
export const CHECK_USERNAME_AVAILABLE_REDACTION: RedactionSpec = {
  inputFields: ["username"],
};

export interface ICheckUsernameAvailableHandler extends IHandler<
  CheckUsernameAvailableInput,
  CheckUsernameAvailableOutput
> {
  readonly redaction: RedactionSpec;
}
