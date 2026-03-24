import type { IHandler, RedactionSpec } from "@d2/handler";

export interface GetFileVariantUrlInput {
  readonly fileId: string;
  readonly variantName: string;
}

export interface GetFileVariantUrlOutput {
  readonly url: string;
}

export const GET_FILE_VARIANT_URL_REDACTION: RedactionSpec = {
  outputFields: ["url"],
};

/** Resolves a presigned GET URL for a file variant after verifying access. */
export type IGetFileVariantUrlHandler = IHandler<GetFileVariantUrlInput, GetFileVariantUrlOutput>;
