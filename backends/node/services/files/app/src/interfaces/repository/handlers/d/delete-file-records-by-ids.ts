import type { IHandler } from "@d2/handler";
import type { FileStatus } from "@d2/files-domain";

export interface DeleteFileRecordsByIdsInput {
  readonly ids: readonly string[];
  /**
   * CAS guard: when set, only deletes rows whose current status still matches.
   * Used by the cleanup job to drop a candidate snapshot if ProcessFile (or
   * any other writer) has moved the row's status in the gap between
   * GetStaleFiles and DeleteFileRecordsByIds.
   */
  readonly expectedStatus?: FileStatus;
}

export interface DeleteFileRecordsByIdsOutput {
  readonly rowsAffected: number;
}

export type IDeleteFileRecordsByIdsHandler = IHandler<
  DeleteFileRecordsByIdsInput,
  DeleteFileRecordsByIdsOutput
>;
