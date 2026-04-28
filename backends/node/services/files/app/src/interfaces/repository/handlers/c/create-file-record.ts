import type { IHandler } from "@d2/handler";
import type { File } from "@d2/files-domain";

export interface CreateFileRecordInput {
  readonly file: File;
}

export interface CreateFileRecordOutput {
  readonly file: File;
}

export type ICreateFileRecordHandler = IHandler<CreateFileRecordInput, CreateFileRecordOutput>;
