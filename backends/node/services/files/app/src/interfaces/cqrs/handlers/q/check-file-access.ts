import type { IHandler } from "@d2/handler";

export interface CheckFileAccessInput {
  readonly address: string;
  readonly contextKey: string;
  readonly relatedEntityId: string;
  readonly requestingUserId: string;
  readonly requestingOrgId?: string;
  /**
   * - `upload` — caller wants to write a file under (contextKey, relatedEntityId)
   * - `read`   — caller wants to fetch a single file by id under that scope
   * - `list`   — caller wants to enumerate the file collection under that scope
   *
   * Owning services SHOULD apply distinct policies — e.g. allow public reads
   * by id but require thread membership for listing.
   */
  readonly action: "upload" | "read" | "list";
}

export interface CheckFileAccessOutput {
  readonly allowed: boolean;
}

/** gRPC CanAccess callback — queries the owning service for access authorization. */
export interface ICheckFileAccessHandler extends IHandler<
  CheckFileAccessInput,
  CheckFileAccessOutput
> {}
