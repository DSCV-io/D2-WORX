import type { IHandler } from "@d2/handler";
import type { File } from "@d2/files-domain";

export interface ProcessFileInput {
  readonly fileId: string;
}

export interface ProcessFileOutput {
  readonly file: File;
}

export interface IProcessFileHandler extends IHandler<ProcessFileInput, ProcessFileOutput> {}
