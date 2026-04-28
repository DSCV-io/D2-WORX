import type { IHandler } from "@d2/handler";

export interface DeleteFileRecordInput {
  readonly id: string;
}

export interface DeleteFileRecordOutput {}

export type IDeleteFileRecordHandler = IHandler<DeleteFileRecordInput, DeleteFileRecordOutput>;
