import type { IHandler } from "@d2/handler";
import type { File } from "@d2/files-domain";

export interface UpdateFileRecordInput {
  readonly file: File;
  /**
   * When provided, adds a WHERE condition on the file's current status.
   * If the row's status does not match, zero rows are returned and the
   * handler returns `notFound()` — signaling a concurrent transition.
   */
  readonly expectedStatus?: string;
}

export interface UpdateFileRecordOutput {
  readonly file: File;
}

export type IUpdateFileRecordHandler = IHandler<UpdateFileRecordInput, UpdateFileRecordOutput>;
