import type { IHandler } from "@d2/handler";

export interface PresignGetUrlInput {
  readonly key: string;
}

export interface PresignGetUrlOutput {
  readonly url: string;
}

/** Generates a presigned GET URL for direct client download from object storage. */
export type IPresignGetUrl = IHandler<PresignGetUrlInput, PresignGetUrlOutput>;
