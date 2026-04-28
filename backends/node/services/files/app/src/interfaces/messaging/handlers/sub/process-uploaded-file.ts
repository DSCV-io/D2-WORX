import type { IHandler } from "@d2/handler";

export interface ProcessUploadedFileInput {
  readonly fileId: string;
}

export interface ProcessUploadedFileOutput {}

/** Subscribes to the processing queue — invokes ProcessFile for scanning + variant generation. */
export interface IProcessUploadedFileHandler extends IHandler<
  ProcessUploadedFileInput,
  ProcessUploadedFileOutput
> {}
