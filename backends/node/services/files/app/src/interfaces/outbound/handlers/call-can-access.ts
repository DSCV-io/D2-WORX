import type { IHandler } from "@d2/handler";

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

/** gRPC CanAccess call — queries the owning service for access authorization. */
export type ICallCanAccess = IHandler<CallCanAccessInput, CallCanAccessOutput>;
