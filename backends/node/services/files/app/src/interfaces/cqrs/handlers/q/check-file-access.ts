import type { IHandler } from "@d2/handler";

export interface CheckFileAccessInput {
  readonly address: string;
  readonly contextKey: string;
  readonly relatedEntityId: string;
  readonly requestingUserId: string;
  readonly requestingOrgId?: string;
  readonly action: "upload" | "read";
}

export interface CheckFileAccessOutput {
  readonly allowed: boolean;
}

/** gRPC CanAccess callback — queries the owning service for access authorization. */
export interface ICheckFileAccessHandler extends IHandler<
  CheckFileAccessInput,
  CheckFileAccessOutput
> {}
