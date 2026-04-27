import type { IHandler } from "@d2/handler";
import type { File, FileStatus } from "@d2/files-domain";

export interface GetStaleFilesInput {
  readonly status: FileStatus;
  readonly cutoffDate: Date;
  readonly limit: number;
}

export interface GetStaleFilesOutput {
  readonly files: readonly File[];
}

export type IGetStaleFilesHandler = IHandler<GetStaleFilesInput, GetStaleFilesOutput>;
