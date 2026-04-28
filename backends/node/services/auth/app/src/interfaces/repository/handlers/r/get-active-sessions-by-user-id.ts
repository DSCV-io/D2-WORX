import type { IHandler, RedactionSpec } from "@d2/handler";
import type { Session } from "@d2/auth-domain";

export interface GetActiveSessionsByUserIdInput {
  readonly userId: string;
}

export interface GetActiveSessionsByUserIdOutput {
  sessions: Session[];
}

/** Suppress output — sessions contain tokens, IPs, and user agents. */
export const GET_ACTIVE_SESSIONS_REDACTION: RedactionSpec = {
  suppressOutput: true,
};

export interface IGetActiveSessionsByUserIdHandler extends IHandler<
  GetActiveSessionsByUserIdInput,
  GetActiveSessionsByUserIdOutput
> {
  readonly redaction: RedactionSpec;
}
