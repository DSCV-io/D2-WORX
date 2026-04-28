import type { IHandler } from "@d2/handler";

export interface ScanFileInput {
  readonly buffer: Buffer;
  readonly contentType: string;
}

export interface ScanFileOutput {
  readonly clean: boolean;
  readonly threat?: string;
}

/** ClamAV virus scanning provider. */
export type IScanFile = IHandler<ScanFileInput, ScanFileOutput>;
