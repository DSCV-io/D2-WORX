import type { IHandler } from "@d2/handler";

export interface PresignPutUrlInput {
  readonly key: string;
  readonly contentType: string;
  readonly maxSizeBytes: number;
}

export interface PresignPutUrlOutput {
  readonly url: string;
}

/** Generates a presigned PUT URL for direct client upload to object storage. */
export type IPresignPutUrl = IHandler<PresignPutUrlInput, PresignPutUrlOutput>;
