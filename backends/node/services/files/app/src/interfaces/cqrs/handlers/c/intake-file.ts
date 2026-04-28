import type { IHandler } from "@d2/handler";
import type { File } from "@d2/files-domain";

export interface IntakeFileInput {
  readonly fileId: string;
}

export interface IntakeFileOutput {
  readonly discarded: boolean;
  readonly reason?: string;
  readonly file?: File;
}

export interface IIntakeFileHandler extends IHandler<IntakeFileInput, IntakeFileOutput> {}
