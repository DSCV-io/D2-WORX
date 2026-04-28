import type { IHandler, RedactionSpec } from "@d2/handler";

export interface GetDeletedUsersToPurgeInput {
  /**
   * Cutoff: users with `deleted_at < graceCutoff` are eligible.
   * Compute as `new Date(Date.now() - userDeletionGracePeriodMs)` at call site.
   */
  readonly graceCutoff: Date;
}

export interface GetDeletedUsersToPurgeOutput {
  /**
   * Flat list of all eligible user ids. Implementation does cursor-based
   * batching internally so callers don't deal with paging — most days the
   * eligible set is tiny, and even with thousands of pending deletions the
   * cursor loop bounds memory at `DEFAULT_BATCH_SIZE`.
   */
  readonly userIds: string[];
}

export const GET_DELETED_USERS_TO_PURGE_REDACTION: RedactionSpec = {};

export type IGetDeletedUsersToPurgeHandler = IHandler<
  GetDeletedUsersToPurgeInput,
  GetDeletedUsersToPurgeOutput
>;
