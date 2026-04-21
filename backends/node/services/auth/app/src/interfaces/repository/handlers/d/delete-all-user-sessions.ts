import type { IHandler, RedactionSpec } from "@d2/handler";

export interface DeleteAllUserSessionsInput {
  readonly userId: string;
}

export interface DeleteAllUserSessionsOutput {
  /** Number of session rows removed. */
  readonly rowsAffected: number;
}

export const DELETE_ALL_USER_SESSIONS_REDACTION: RedactionSpec = {};

export type IDeleteAllUserSessionsHandler = IHandler<
  DeleteAllUserSessionsInput,
  DeleteAllUserSessionsOutput
>;
