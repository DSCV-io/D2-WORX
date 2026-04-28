import type { IHandler, RedactionSpec } from "@d2/handler";

export interface FinalizeDeletedUserInput {
  readonly userId: string;
}

export interface FinalizeDeletedUserOutput {
  /**
   * False when AnonymizeUser's status guard didn't match (already anonymized,
   * or the user signed back in between the job's find + finalize). Caller
   * treats this as a successful no-op.
   */
  readonly anonymized: boolean;
}

export const FINALIZE_DELETED_USER_REDACTION: RedactionSpec = {};

export type IFinalizeDeletedUserHandler = IHandler<
  FinalizeDeletedUserInput,
  FinalizeDeletedUserOutput
>;
