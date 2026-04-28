import type { IHandler } from "@d2/handler";
import type { File } from "@d2/files-domain";

export interface GetFileByIdInput {
  readonly id: string;
}

export interface GetFileByIdOutput {
  readonly file: File;
}

export type IGetFileByIdHandler = IHandler<GetFileByIdInput, GetFileByIdOutput>;
