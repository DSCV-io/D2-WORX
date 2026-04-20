import type { IHandler, RedactionSpec } from "@d2/handler";
import type { Session } from "@d2/auth-domain";
import type { WhoIsDTO } from "@d2/protos";

export interface GetMySessionsInput {
  readonly userId: string;
  /** Token from the current session cookie — used to flag the entry as `isCurrent`. */
  readonly currentSessionToken?: string;
}

/** A single session enriched with the resolved Geo WhoIs data (when available). */
export interface EnrichedSession {
  session: Session;
  whoIs?: WhoIsDTO;
  isCurrent: boolean;
}

export interface GetMySessionsOutput {
  sessions: EnrichedSession[];
}

/** Recommended redaction — output contains tokens, IPs, UAs, full WhoIs/location data. */
export const GET_MY_SESSIONS_REDACTION: RedactionSpec = {
  inputFields: ["currentSessionToken"],
  suppressOutput: true,
};

export interface IGetMySessionsHandler
  extends IHandler<GetMySessionsInput, GetMySessionsOutput> {
  readonly redaction: RedactionSpec;
}
