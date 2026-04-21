import type { IHandler, RedactionSpec } from "@d2/handler";

export interface CallCanAccessInput {
  readonly address: string;
  readonly contextKey: string;
  readonly relatedEntityId: string;
  readonly requestingUserId: string;
  readonly requestingOrgId?: string;
  readonly action: "upload" | "read";
}

export interface CallCanAccessOutput {
  readonly allowed: boolean;
}

/** `address` is an internal service endpoint — redact from logs. */
export const CALL_CAN_ACCESS_REDACTION: RedactionSpec = {
  inputFields: ["address"],
};

/** gRPC CanAccess call — queries the owning service for access authorization. */
export interface ICallCanAccess extends IHandler<CallCanAccessInput, CallCanAccessOutput> {
  readonly redaction: RedactionSpec;
}
