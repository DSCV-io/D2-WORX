import type { IHandler, RedactionSpec } from "@d2/handler";

export interface DownloadFileVariantInput {
  readonly fileId: string;
  readonly variantName: string;
}

export interface DownloadFileVariantOutput {
  readonly buffer: Buffer;
  readonly contentType: string;
  readonly displayName: string;
}

export const DOWNLOAD_FILE_VARIANT_REDACTION: RedactionSpec = {
  suppressOutput: true,
};

/** Downloads a file variant's content after verifying access. */
export type IDownloadFileVariantHandler = IHandler<
  DownloadFileVariantInput,
  DownloadFileVariantOutput
>;
