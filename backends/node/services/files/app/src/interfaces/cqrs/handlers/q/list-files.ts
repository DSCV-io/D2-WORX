import type { IHandler, RedactionSpec } from "@d2/handler";
import type { File } from "@d2/files-domain";

export interface ListFilesInput {
  readonly contextKey: string;
  readonly relatedEntityId: string;
  readonly limit?: number;
  readonly offset?: number;
}

export interface ListFilesOutput {
  readonly files: readonly File[];
  readonly total: number;
}

export const LIST_FILES_REDACTION: RedactionSpec = {
  suppressOutput: true,
};

export interface IListFilesHandler extends IHandler<ListFilesInput, ListFilesOutput> {}
