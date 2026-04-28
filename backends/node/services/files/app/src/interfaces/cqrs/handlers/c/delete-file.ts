import type { IHandler } from "@d2/handler";

export interface DeleteFileInput {
  readonly fileId: string;
}

export interface DeleteFileOutput {}

export interface IDeleteFileHandler extends IHandler<DeleteFileInput, DeleteFileOutput> {}
