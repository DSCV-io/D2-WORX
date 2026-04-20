import type { IHandler, RedactionSpec } from "@d2/handler";
import type { Session } from "@d2/auth-domain";

export interface FindActiveSessionsByUserIdInput {
  readonly userId: string;
}

export interface FindActiveSessionsByUserIdOutput {
  sessions: Session[];
}

/** Suppress output — sessions contain tokens, IPs, and user agents. */
export const FIND_ACTIVE_SESSIONS_REDACTION: RedactionSpec = {
  suppressOutput: true,
};

export interface IFindActiveSessionsByUserIdHandler
  extends IHandler<FindActiveSessionsByUserIdInput, FindActiveSessionsByUserIdOutput> {
  readonly redaction: RedactionSpec;
}
