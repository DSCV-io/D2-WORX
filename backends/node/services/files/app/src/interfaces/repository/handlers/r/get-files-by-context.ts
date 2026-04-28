import type { IHandler } from "@d2/handler";
import type { File } from "@d2/files-domain";

export interface GetFilesByContextInput {
  readonly contextKey: string;
  readonly relatedEntityId: string;
  readonly limit?: number;
  readonly offset?: number;
}

export interface GetFilesByContextOutput {
  readonly files: readonly File[];
  readonly total: number;
}

export type IGetFilesByContextHandler = IHandler<GetFilesByContextInput, GetFilesByContextOutput>;
