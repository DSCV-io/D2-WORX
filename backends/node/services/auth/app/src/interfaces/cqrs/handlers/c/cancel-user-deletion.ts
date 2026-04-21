import type { IHandler, RedactionSpec } from "@d2/handler";

export interface CancelUserDeletionInput {
  readonly userId: string;
}

export interface CancelUserDeletionOutput {
  /** False when the user wasn't actually `pending_deletion` (idempotent no-op). */
  readonly cancelled: boolean;
}

/** Output flags non-PII; input is just an opaque userId. Default redaction is fine. */
export const CANCEL_USER_DELETION_REDACTION: RedactionSpec = {};

export type ICancelUserDeletionHandler = IHandler<
  CancelUserDeletionInput,
  CancelUserDeletionOutput
>;
