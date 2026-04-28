import type { IHandler, RedactionSpec } from "@d2/handler";
import type { SignInEvent } from "@d2/auth-domain";
import type { WhoIsDTO } from "@d2/protos";

export interface GetSignInEventsInput {
  readonly userId: string;
  readonly limit?: number;
  readonly offset?: number;
}

/**
 * Sign-in event enriched with the cross-service Geo WhoIs lookup for the row's
 * `ipAddress`. `whoIs` is undefined when the lookup fails or returns no match —
 * the FE falls back to displaying the raw IP.
 */
export interface EnrichedSignInEvent {
  event: SignInEvent;
  whoIs?: WhoIsDTO;
}

export interface GetSignInEventsOutput {
  events: EnrichedSignInEvent[];
  total: number;
}

/** Recommended redaction for GetSignInEvents handlers. */
export const GET_SIGN_IN_EVENTS_REDACTION: RedactionSpec = {
  suppressOutput: true,
};

/** Handler for retrieving paginated sign-in events. Requires redaction (output contains PII). */
export interface IGetSignInEventsHandler extends IHandler<
  GetSignInEventsInput,
  GetSignInEventsOutput
> {
  readonly redaction: RedactionSpec;
}
